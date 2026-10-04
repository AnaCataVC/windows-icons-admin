using WindowsIconsAdmin.Core.Storage;

namespace WindowsIconsAdmin.Core.Tests.Storage;

public class IconStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IconStorageService _storageService;

    public IconStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wia_storage_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _storageService = new IconStorageService(Path.Combine(_tempDir, "CentralCache"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public void PortableEmbedded_WritesIconToTargetFolderAndReturnsRelativePath()
    {
        var targetFolder = Path.Combine(_tempDir, "MyTarget");
        Directory.CreateDirectory(targetFolder);
        var iconBytes = new byte[] { 0, 1, 2, 3 };

        var result = _storageService.PrepareIconForFolder(targetFolder, iconBytes, IconStorageMode.PortableEmbedded);

        Assert.Equal(".\\.folder_icon.ico,0", result.IconResourceString);
        Assert.Equal(".folder_icon.ico", result.CopiedFileName);
        Assert.False(result.WarnGitRepository);
        Assert.True(File.Exists(Path.Combine(targetFolder, ".folder_icon.ico")));
    }

    [Fact]
    public void PortableEmbedded_WhenGitPresent_WarnsGit()
    {
        var targetFolder = Path.Combine(_tempDir, "GitRepo");
        Directory.CreateDirectory(Path.Combine(targetFolder, ".git"));
        var iconBytes = new byte[] { 0, 1, 2, 3 };

        var result = _storageService.PrepareIconForFolder(targetFolder, iconBytes, IconStorageMode.PortableEmbedded);

        Assert.True(result.WarnGitRepository);
    }

    [Fact]
    public void CentralCache_WritesIconToCentralDirectoryAndReturnsAbsolutePath()
    {
        var targetFolder = Path.Combine(_tempDir, "MyTarget2");
        Directory.CreateDirectory(targetFolder);
        var iconBytes = new byte[] { 10, 20, 30, 40 };

        var result = _storageService.PrepareIconForFolder(targetFolder, iconBytes, IconStorageMode.CentralCache);

        Assert.Null(result.CopiedFileName);
        Assert.EndsWith(".ico,0", result.IconResourceString);
        Assert.StartsWith(_storageService.CentralCacheDirectory, result.IconResourceString);
        Assert.False(File.Exists(Path.Combine(targetFolder, ".folder_icon.ico")));
    }

    [Fact]
    public void IsRemoteOrNetworkPath_IdentifiesUncPaths()
    {
        Assert.True(IconStorageService.IsRemoteOrNetworkPath(@"\\server\share\folder"));
        Assert.False(IconStorageService.IsRemoteOrNetworkPath(@"C:\Local\Folder"));
    }

    [Fact]
    public void PortableEmbedded_SecondApplicationOverwritesHiddenIconSuccessfully()
    {
        var targetFolder = Path.Combine(_tempDir, "ReApplyTarget");
        Directory.CreateDirectory(targetFolder);

        var firstBytes = new byte[] { 1, 2, 3, 4 };
        var secondBytes = new byte[] { 9, 8, 7, 6, 5 };

        _storageService.PrepareIconForFolder(targetFolder, firstBytes, IconStorageMode.PortableEmbedded);
        var secondResult = _storageService.PrepareIconForFolder(targetFolder, secondBytes, IconStorageMode.PortableEmbedded);

        var iconPath = Path.Combine(targetFolder, ".folder_icon.ico");
        Assert.Equal(".\\.folder_icon.ico,0", secondResult.IconResourceString);
        Assert.Equal(secondBytes, File.ReadAllBytes(iconPath));
        Assert.True((File.GetAttributes(iconPath) & FileAttributes.Hidden) != 0);
    }
}
