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
        _service = new AppIconService(_fakeShortcutService, _undoStore, new RenderedIconCache(_iconsDir));
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

    [Fact]
    public void ApplyBatch_WhenCancelled_CommitsCompletedItems()
    {
        var lnk1 = Path.Combine(_tempDir, "App1.lnk");
        var lnk2 = Path.Combine(_tempDir, "App2.lnk");
        File.WriteAllText(lnk1, "app1");
        File.WriteAllText(lnk2, "app2");
        _fakeShortcutService.SetData(lnk1, @"C:\App1\app1.exe", null, 0);
        _fakeShortcutService.SetData(lnk2, @"C:\App2\app2.exe", null, 0);

        var app1 = new InstalledApp("app-1", "App 1", @"C:\App1\app1.exe", null, [new IconTarget(IconTargetType.UserStartMenuShortcut, lnk1, null, 0, AppSupportStatus.Supported)]);
        var app2 = new InstalledApp("app-2", "App 2", @"C:\App2\app2.exe", null, [new IconTarget(IconTargetType.UserStartMenuShortcut, lnk2, null, 0, AppSupportStatus.Supported)]);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        var style = IconStyle.CreateDefault();
        var result = _service.ApplyBatch([(app1, style, new byte[] { 1 }), (app2, style, new byte[] { 2 })], cancellationToken: cts.Token);

        Assert.True(result.WasCancelled);
        Assert.Equal(0, result.AppliedCount);
    }

    [Fact]
    public void RevertAll_RestoresMultipleBatches()
    {
        var lnk1 = Path.Combine(_tempDir, "Bat1.lnk");
        var lnk2 = Path.Combine(_tempDir, "Bat2.lnk");
        File.WriteAllText(lnk1, "bat1");
        File.WriteAllText(lnk2, "bat2");
        _fakeShortcutService.SetData(lnk1, @"C:\Bat1\b1.exe", @"C:\Bat1\orig1.ico", 0);
        _fakeShortcutService.SetData(lnk2, @"C:\Bat2\b2.exe", @"C:\Bat2\orig2.ico", 0);

        var app1 = new InstalledApp("app-b1", "Bat 1", @"C:\Bat1\b1.exe", null, [new IconTarget(IconTargetType.UserStartMenuShortcut, lnk1, @"C:\Bat1\orig1.ico", 0, AppSupportStatus.Supported)]);
        var app2 = new InstalledApp("app-b2", "Bat 2", @"C:\Bat2\b2.exe", null, [new IconTarget(IconTargetType.UserStartMenuShortcut, lnk2, @"C:\Bat2\orig2.ico", 0, AppSupportStatus.Supported)]);

        var style = IconStyle.CreateDefault();
        _service.ApplyBatch([(app1, style, new byte[] { 1 })]);
        _service.ApplyBatch([(app2, style, new byte[] { 2 })]);

        var restoredCount = _service.RevertAll();
        Assert.Equal(2, restoredCount);

        var (_, icon1, _) = _fakeShortcutService.ReadShortcut(lnk1);
        var (_, icon2, _) = _fakeShortcutService.ReadShortcut(lnk2);
        Assert.Equal(@"C:\Bat1\orig1.ico", icon1);
        Assert.Equal(@"C:\Bat2\orig2.ico", icon2);
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
