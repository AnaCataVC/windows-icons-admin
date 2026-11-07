using WindowsIconsAdmin.Core.Apps;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Apps;

public class AppDiscoveryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeShortcutService _fakeShortcutService;

    public AppDiscoveryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AppDiscoveryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _fakeShortcutService = new FakeShortcutService();
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
    public void DiscoverInstalledApps_DiscoversAndClassifiesCorrectly()
    {
        var appPath = Path.Combine(_tempDir, "Visual Studio Code.lnk");
        File.WriteAllText(appPath, "fake lnk content");
        _fakeShortcutService.SetData(appPath, @"C:\Program Files\VSCode\Code.exe", null, 0);

        var uninstallerPath = Path.Combine(_tempDir, "Uninstall VS Code.lnk");
        File.WriteAllText(uninstallerPath, "fake lnk content");
        _fakeShortcutService.SetData(uninstallerPath, @"C:\Program Files\VSCode\unins000.exe", null, 0);

        var discoveryService = new AppDiscoveryService(_fakeShortcutService, [_tempDir]);
        var apps = discoveryService.DiscoverInstalledApps();

        // Must find Visual Studio Code and ignore Uninstall
        Assert.Single(apps);
        var app = apps[0];
        Assert.Equal("Visual Studio Code", app.DisplayName);
        Assert.Equal(@"C:\Program Files\VSCode\Code.exe", app.ExecutablePath);
        Assert.Equal(AppSupportStatus.Supported, app.SupportStatus);
        Assert.Single(app.Targets);
    }

    [Fact]
    public void DiscoverInstalledApps_CleansDisplayName()
    {
        var appPath = Path.Combine(_tempDir, "Spotify - Shortcut.lnk");
        File.WriteAllText(appPath, "fake lnk content");
        _fakeShortcutService.SetData(appPath, @"C:\Users\User\AppData\Local\Spotify\Spotify.exe", null, 0);

        var discoveryService = new AppDiscoveryService(_fakeShortcutService, [_tempDir]);
        var apps = discoveryService.DiscoverInstalledApps();

        Assert.Single(apps);
        Assert.Equal("Spotify", apps[0].DisplayName);
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
