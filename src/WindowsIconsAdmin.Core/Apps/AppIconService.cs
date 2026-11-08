using System.Runtime.InteropServices;
using System.Security.Cryptography;
using WindowsIconsAdmin.Core.History;
using WindowsIconsAdmin.Core.Styling;

namespace WindowsIconsAdmin.Core.Apps;

public class AppIconService : IAppIconService
{
    private readonly IShortcutService _shortcutService;
    private readonly AppIconUndoStore _undoStore;
    private readonly RenderedIconCache _iconCache;
    private readonly OriginalSnapshotStore _snapshotStore;

    public AppIconService(
        IShortcutService? shortcutService = null,
        AppIconUndoStore? undoStore = null,
        RenderedIconCache? iconCache = null,
        OriginalSnapshotStore? snapshotStore = null)
    {
        _shortcutService = shortcutService ?? new WindowsShortcutService();
        _undoStore = undoStore ?? new AppIconUndoStore(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsIconsAdmin", "State", "app-undo.json"));
        _iconCache = iconCache ?? new RenderedIconCache();
        _snapshotStore = snapshotStore ?? new OriginalSnapshotStore();
    }

    public AppIconUndoStore UndoStore => _undoStore;
    public RenderedIconCache IconCache => _iconCache;
    public OriginalSnapshotStore SnapshotStore => _snapshotStore;

    public AppBatchResult ApplyBatch(
        IReadOnlyList<(InstalledApp App, IconStyle Style, byte[] IcoBytes)> batchItems,
        string? batchId = null,
        IProgress<(int Current, int Total, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchItems);

        var appliedChanges = new List<AppIconChange>();
        var errors = new List<string>();
        var skippedCount = 0;
        var appliedCount = 0;
        var wasCancelled = false;

        for (var i = 0; i < batchItems.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                wasCancelled = true;
                break;
            }

            var (app, style, icoBytes) = batchItems[i];
            progress?.Report((i + 1, batchItems.Count, app.DisplayName));

            var supportedTargets = app.Targets
                .Where(t => t.SupportStatus == AppSupportStatus.Supported)
                .ToList();

            if (supportedTargets.Count == 0)
            {
                skippedCount++;
                continue;
            }

            var cacheKey = IconStyleRenderer.ComputeCacheKey(icoBytes, style);
            string iconFilePath;
            try
            {
                iconFilePath = _iconCache.Store(cacheKey, icoBytes);
            }
            catch (Exception ex)
            {
                errors.Add($"Error al guardar icono para {app.DisplayName}: {ex.Message}");
                continue;
            }

            foreach (var target in supportedTargets)
            {
                if (!_shortcutService.ShortcutExists(target.TargetPath))
                {
                    errors.Add($"Acceso directo no encontrado: {target.TargetPath}");
                    continue;
                }

                try
                {
                    var (oldTarget, oldIconLoc, oldIconIdx) = _shortcutService.ReadShortcut(target.TargetPath);

                    // Backup original icon if not already snapshotted
                    string? snapshotPath = null;
                    if (!_snapshotStore.SnapshotExists(app.Id) && !string.IsNullOrWhiteSpace(oldIconLoc) && File.Exists(oldIconLoc))
                    {
                        try
                        {
                            var oldBytes = File.ReadAllBytes(oldIconLoc);
                            snapshotPath = _snapshotStore.SaveSnapshot(app.Id, oldBytes);
                        }
                        catch
                        {
                            // Best-effort snapshot
                        }
                    }

                    _shortcutService.SetShortcutIcon(target.TargetPath, iconFilePath, 0);

                    appliedChanges.Add(new AppIconChange(
                        AppId: app.Id,
                        AppDisplayName: app.DisplayName,
                        TargetPath: target.TargetPath,
                        TargetType: target.TargetType,
                        PreviousIconLocation: oldIconLoc,
                        PreviousIconIndex: oldIconIdx,
                        OriginalSnapshotPath: snapshotPath,
                        AppliedIconPath: iconFilePath,
                        TimestampUtc: DateTimeOffset.UtcNow));

                    appliedCount++;
                    NotifyShortcutUpdated(target.TargetPath);
                }
                catch (Exception ex)
                {
                    errors.Add($"Error al aplicar icono en {target.TargetPath}: {ex.Message}");
                }
            }
        }

        AppBatchRecord? batchRecord = null;
        if (appliedChanges.Count > 0)
        {
            var firstItem = batchItems.FirstOrDefault();
            batchRecord = new AppBatchRecord(
                BatchId: batchId ?? Guid.NewGuid().ToString("N"),
                TimestampUtc: DateTimeOffset.UtcNow,
                StyleId: firstItem.Style?.Id ?? "custom-style",
                StyleName: firstItem.Style?.Name ?? "Custom Style",
                Changes: appliedChanges,
                Reverted: false);

            _undoStore.Add(batchRecord);
            NotifyShellAssociationChanged();
        }

        return new AppBatchResult(
            AppliedCount: appliedCount,
            SkippedCount: skippedCount,
            ErrorCount: errors.Count,
            WasCancelled: wasCancelled,
            BatchRecord: batchRecord,
            ErrorMessages: errors.AsReadOnly());
    }

    public bool RevertLastBatch()
    {
        var last = _undoStore.GetLastPending();
        if (last == null) return false;
        return RevertBatch(last.BatchId);
    }

    public bool RevertBatch(string batchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);

        var batch = _undoStore.GetAll().FirstOrDefault(b => b.BatchId == batchId);
        if (batch == null || batch.Reverted) return false;

        foreach (var change in batch.Changes)
        {
            RevertSingleChange(change);
        }

        _undoStore.MarkReverted(batchId);
        NotifyShellAssociationChanged();
        return true;
    }

    public int RevertAll()
    {
        var all = _undoStore.GetAll().Where(b => !b.Reverted).ToList();
        var revertedCount = 0;

        foreach (var batch in all)
        {
            foreach (var change in batch.Changes)
            {
                RevertSingleChange(change);
                revertedCount++;
            }
            _undoStore.MarkReverted(batch.BatchId);
        }

        if (revertedCount > 0)
        {
            NotifyShellAssociationChanged();
        }

        return revertedCount;
    }

    public bool RevertApp(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        var anyReverted = false;
        var all = _undoStore.GetAll().Where(b => !b.Reverted).ToList();

        foreach (var batch in all)
        {
            var appChanges = batch.Changes.Where(c => c.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var change in appChanges)
            {
                RevertSingleChange(change);
                anyReverted = true;
            }
        }

        if (anyReverted)
        {
            NotifyShellAssociationChanged();
        }

        return anyReverted;
    }

    public void RevertSingleChange(AppIconChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (_shortcutService.ShortcutExists(change.TargetPath))
        {
            try
            {
                _shortcutService.SetShortcutIcon(
                    change.TargetPath,
                    change.PreviousIconLocation ?? string.Empty,
                    change.PreviousIconIndex);

                NotifyShortcutUpdated(change.TargetPath);
            }
            catch
            {
                // Best effort
            }
        }
    }

    private static void NotifyShortcutUpdated(string shortcutPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                NativeMethods.SHChangeNotify(
                    NativeMethods.SHCNE_UPDATEITEM,
                    NativeMethods.SHCNF_PATHW,
                    shortcutPath,
                    IntPtr.Zero);
            }
            catch
            {
                // Suppress on non-shell context
            }
        }
    }

    private static void NotifyShellAssociationChanged()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                NativeMethods.SHChangeNotify(
                    NativeMethods.SHCNE_ASSOCCHANGED,
                    NativeMethods.SHCNF_IDLIST | NativeMethods.SHCNF_FLUSHNOWAIT,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }
            catch
            {
                // Suppress
            }
        }
    }

    private static class NativeMethods
    {
        public const uint SHCNE_UPDATEITEM = 0x00002000;
        public const uint SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_IDLIST = 0x0000;
        public const uint SHCNF_PATHW = 0x0005;
        public const uint SHCNF_FLUSHNOWAIT = 0x2000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, [MarshalAs(UnmanagedType.LPWStr)] string? dwItem1, IntPtr dwItem2);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
