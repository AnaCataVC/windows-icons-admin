using WindowsIconsAdmin.Core.Settings;

namespace WindowsIconsAdmin.Core.Tests.Settings;

public class AppSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsFilePath;

    public AppSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wia_settings_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsFilePath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup failure in temp
        }
    }

    [Fact]
    public void Constructor_WhenFileDoesNotExist_ReturnsDefaultSettingsWithPortableModeFalse()
    {
        var service = new AppSettingsService(_settingsFilePath);

        Assert.NotNull(service.Current);
        Assert.False(service.Current.EnablePortableMode);
    }

    [Fact]
    public void Save_PersistsSettingsToDiskAndReloadsAccurately()
    {
        var service = new AppSettingsService(_settingsFilePath);
        var updated = new AppSettings { EnablePortableMode = true };

        service.Save(updated);

        Assert.True(File.Exists(_settingsFilePath));

        // Create new service instance reading the same file
        var reloadedService = new AppSettingsService(_settingsFilePath);
        Assert.True(reloadedService.Current.EnablePortableMode);
    }

    [Fact]
    public void Update_ModifiesSettingsAtomically()
    {
        var service = new AppSettingsService(_settingsFilePath);
        Assert.False(service.Current.EnablePortableMode);

        service.Update(s => s with { EnablePortableMode = true });

        Assert.True(service.Current.EnablePortableMode);

        var reloadedService = new AppSettingsService(_settingsFilePath);
        Assert.True(reloadedService.Current.EnablePortableMode);
    }

    [Fact]
    public void Load_WhenFileIsCorrupted_CreatesBakAndReturnsDefaults()
    {
        File.WriteAllText(_settingsFilePath, "{ invalid json content @@@");

        var service = new AppSettingsService(_settingsFilePath);

        Assert.NotNull(service.Current);
        Assert.False(service.Current.EnablePortableMode);
        Assert.True(File.Exists(_settingsFilePath + ".bak"));
    }

    [Fact]
    public void Save_ThrowsWhenNull()
    {
        var service = new AppSettingsService(_settingsFilePath);

        Assert.Throws<ArgumentNullException>(() => service.Save(null!));
    }

    [Fact]
    public void Update_ThrowsWhenNull()
    {
        var service = new AppSettingsService(_settingsFilePath);

        Assert.Throws<ArgumentNullException>(() => service.Update(null!));
    }
}
