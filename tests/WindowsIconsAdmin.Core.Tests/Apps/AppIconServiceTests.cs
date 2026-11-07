using WindowsIconsAdmin.Core.Apps;
using WindowsIconsAdmin.Core.History;
using WindowsIconsAdmin.Core.Styling;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Apps;

public class AppIconServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _undoFile;
    private readonly string _iconsDir;
    private readonly FakeShortcutService _fakeShortcutService;
    private readonly AppIconUndoStore _undoStore;
    private readonly AppIconService _service;

    public AppIconServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AppIconServiceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _undoFile = Path.Combine(_tempDir, "app-undo.json");
        _iconsDir = Path.Combine(_tempDir, "GeneratedIcons");

        _fakeShortcutService = new FakeShortcutService();
        _undoStore = new AppIconUndoStore(_undoFile);
        _service = new AppIconService(_fakeShortcutService, _undoStore, _iconsDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public void ApplyBatch_ModifiesSupportedTarget_AndRecordsInUndoStore()
    {
        var lnkPath = Path.Combine(_tempDir, "Discord.lnk");
        File.WriteAllText(lnkPath, "fake shortcut");
        _fakeShortcutService.SetData(lnkPath, @"C:\AppData\Discord\Discord.exe", @"C:\AppData\Discord\Discord.exe", 0);

        var target = new IconTarget(
            TargetType: IconTargetType.UserStartMenuShortcut,
            TargetPath: lnkPath,
            CurrentIconLocation: @"C:\AppData\Discord\Discord.exe",
            CurrentIconIndex: 0,
            SupportStatus: AppSupportStatus.Supported);

        var app = new InstalledApp(
            Id: "app-discord",
            DisplayName: "Discord",
            ExecutablePath: @"C:\AppData\Discord\Discord.exe",
            OriginalIconSource: @"C:\AppData\Discord\Discord.exe",
            Targets: [target]);

        var style = IconStyle.CreateDefault("Soft Lavender");
        var fakeIcoBytes = new byte[] { 0, 0, 1, 0, 1, 0, 16, 16 };

        var result = _service.ApplyBatch([(app, style, fakeIcoBytes)]);

        Assert.Equal(1, result.AppliedCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.NotNull(result.BatchRecord);

        // Check that shortcut's icon was updated to a file in GeneratedIcons
        var (_, newIcon, newIdx) = _fakeShortcutService.ReadShortcut(lnkPath);
        Assert.NotNull(newIcon);
        Assert.StartsWith(_iconsDir, newIcon);
        Assert.Equal(0, newIdx);

        // Check UndoStore has 1 pending batch
        var pending = _undoStore.GetLastPending();
        Assert.NotNull(pending);
        Assert.Equal(result.BatchRecord.BatchId, pending.BatchId);
        Assert.Single(pending.Changes);
        Assert.Equal(@"C:\AppData\Discord\Discord.exe", pending.Changes[0].PreviousIconLocation);
    }

    [Fact]
    public void RevertBatch_RestoresOriginalShortcutIconLocation()
    {
        var lnkPath = Path.Combine(_tempDir, "Chrome.lnk");
        File.WriteAllText(lnkPath, "fake shortcut");
        _fakeShortcutService.SetData(lnkPath, @"C:\Program Files\Chrome\chrome.exe", @"C:\Program Files\Chrome\chrome.exe", 1);

        var target = new IconTarget(
            TargetType: IconTargetType.UserStartMenuShortcut,
            TargetPath: lnkPath,
            CurrentIconLocation: @"C:\Program Files\Chrome\chrome.exe",
            CurrentIconIndex: 1,
            SupportStatus: AppSupportStatus.Supported);

        var app = new InstalledApp(
            Id: "app-chrome",
            DisplayName: "Chrome",
            ExecutablePath: @"C:\Program Files\Chrome\chrome.exe",
            OriginalIconSource: @"C:\Program Files\Chrome\chrome.exe",
            Targets: [target]);

        var style = IconStyle.CreateDefault();
        var fakeIcoBytes = new byte[] { 0, 0, 1, 0, 1, 0, 32, 32 };

        var result = _service.ApplyBatch([(app, style, fakeIcoBytes)]);
        Assert.Equal(1, result.AppliedCount);

        // Revert
        var reverted = _service.RevertLastBatch();
        Assert.True(reverted);

        // Shortcut icon location must be restored back to original
        var (_, restoredIcon, restoredIdx) = _fakeShortcutService.ReadShortcut(lnkPath);
        Assert.Equal(@"C:\Program Files\Chrome\chrome.exe", restoredIcon);
        Assert.Equal(1, restoredIdx);

        // No more pending batches in UndoStore
        Assert.Null(_undoStore.GetLastPending());
    }

    private class FakeShortcutService : IShortcutService
    {
        private readonly Dictionary<string, (string Target, string? Icon, int Index)> _data = new(StringComparer.OrdinalIgnoreCase);

        public void SetData(string path, string target, string? icon, int index)
        {
            _data[path] = (target, icon, index);
        }

        public bool ShortcutExists(string shortcutPath) => File.Exists(shortcutPath);

        public (string TargetPath, string? IconLocation, int IconIndex) ReadShortcut(string shortcutPath)
        {
            if (_data.TryGetValue(shortcutPath, out var val))
            {
                return val;
            }
            return (shortcutPath, null, 0);
        }

        public void SetShortcutIcon(string shortcutPath, string iconLocation, int iconIndex)
        {
            if (_data.TryGetValue(shortcutPath, out var val))
            {
                _data[shortcutPath] = (val.Target, iconLocation, iconIndex);
            }
        }
    }
}
