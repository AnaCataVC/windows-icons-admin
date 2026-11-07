using System.Runtime.InteropServices;
using System.Security.Cryptography;
using WindowsIconsAdmin.Core.History;
using WindowsIconsAdmin.Core.Shell;
using WindowsIconsAdmin.Core.Styling;

namespace WindowsIconsAdmin.Core.Apps;

public class AppIconService : IAppIconService
{
    private readonly IShortcutService _shortcutService;
    private readonly AppIconUndoStore _undoStore;
    private readonly string _generatedIconsDirectory;

    public AppIconService(
        IShortcutService? shortcutService = null,
        AppIconUndoStore? undoStore = null,
        string? generatedIconsDirectory = null)
    {
        _shortcutService = shortcutService ?? new WindowsShortcutService();
        _undoStore = undoStore ?? new AppIconUndoStore(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsIconsAdmin", "State", "app-undo.json"));

        _generatedIconsDirectory = generatedIconsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsIconsAdmin", "GeneratedIcons");
    }

    public string GeneratedIconsDirectory => _generatedIconsDirectory;
    public AppIconUndoStore UndoStore => _undoStore;

    public AppBatchResult ApplyBatch(
        IReadOnlyList<(InstalledApp App, IconStyle Style, byte[] IcoBytes)> batchItems,
        string? batchId = null)
    {
        ArgumentNullException.ThrowIfNull(batchItems);

        Directory.CreateDirectory(_generatedIconsDirectory);

        var appliedChanges = new List<AppIconChange>();
        var errors = new List<string>();
        var skippedCount = 0;
        var appliedCount = 0;

        foreach (var (app, style, icoBytes) in batchItems)
        {
            var supportedTargets = app.Targets
                .Where(t => t.SupportStatus == AppSupportStatus.Supported)
                .ToList();

            if (supportedTargets.Count == 0)
            {
                skippedCount++;
                continue;
            }

            var hash = Convert.ToHexString(SHA256.HashData(icoBytes)).ToLowerInvariant();
            var iconFilePath = Path.Combine(_generatedIconsDirectory, $"{hash}.ico");

            try
            {
                if (!File.Exists(iconFilePath))
                {
                    File.WriteAllBytes(iconFilePath, icoBytes);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error al guardar el icono para {app.DisplayName}: {ex.Message}");
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

                    _shortcutService.SetShortcutIcon(target.TargetPath, iconFilePath, 0);

                    appliedChanges.Add(new AppIconChange(
                        AppId: app.Id,
                        AppDisplayName: app.DisplayName,
                        TargetPath: target.TargetPath,
                        TargetType: target.TargetType,
                        PreviousIconLocation: oldIconLoc,
                        PreviousIconIndex: oldIconIdx,
                        OriginalSnapshotPath: null,
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
                // Best effort restoration per shortcut
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
