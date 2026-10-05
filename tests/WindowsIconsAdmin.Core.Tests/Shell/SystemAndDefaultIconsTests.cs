using WindowsIconsAdmin.Core.Shell;

namespace WindowsIconsAdmin.Core.Tests.Shell;

public class SystemAndDefaultIconsTests
{
    private readonly ShellIconService _shellService = new();

    [Theory]
    [InlineData(@"C:\Icons\custom.ico")]
    [InlineData(@"D:\Folder\test.png")]
    [InlineData(@"C:\Windows\System32\shell32.dll,3")]
    public void ValidateLocalIconPath_ValidPaths_DoNotThrow(string path)
    {
        var ex = Record.Exception(() => ShellIconService.ValidateLocalIconPath(path));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(@"\\remote-server\share\icon.ico")]
    [InlineData(@"//192.168.1.1/icon.ico")]
    [InlineData(@"relative\path\icon.ico")]
    [InlineData(@"C:\Icons\script.bat")]
    [InlineData(@"C:\Icons\malicious.ps1")]
    public void ValidateLocalIconPath_InvalidOrUnsafePaths_ThrowsArgumentException(string path)
    {
        Assert.Throws<ArgumentException>(() => ShellIconService.ValidateLocalIconPath(path));
    }

    [Fact]
    public void FormatIconResourcePath_AppendsIndexZeroWhenMissing()
    {
        var formatted = ShellIconService.FormatIconResourcePath(@"C:\Icons\test.ico");
        Assert.Equal(@"C:\Icons\test.ico,0", formatted);
    }

    [Fact]
    public void FormatIconResourcePath_PreservesExistingIndex()
    {
        var formatted = ShellIconService.FormatIconResourcePath(@"C:\Windows\System32\shell32.dll,-154");
        Assert.Equal(@"C:\Windows\System32\shell32.dll,-154", formatted);
    }

    [Fact]
    public void SpecialFoldersService_ResolvesAllKnownFolders()
    {
        var folders = SpecialFoldersService.GetAllSpecialFolders();
        Assert.Equal(6, folders.Count);

        foreach (var folder in folders)
        {
            Assert.False(string.IsNullOrWhiteSpace(folder.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(folder.FolderPath));
            Assert.True(Path.IsPathRooted(folder.FolderPath));
        }
    }

    [Fact]
    public void FileTypeIconService_SetAndRestoreExtensionIcon_WorksInRegistry()
    {
        const string testExt = ".wiaconfigtest";
        const string testIcon = @"C:\Test\test_icon.ico";

        try
        {
            FileTypeIconService.SetExtensionIcon(testExt, testIcon);
            var current = FileTypeIconService.GetExtensionIcon(testExt);
            Assert.NotNull(current);
            Assert.Contains("test_icon.ico", current);

            FileTypeIconService.RestoreExtensionIcon(testExt);
            var restored = FileTypeIconService.GetExtensionIcon(testExt);
            Assert.Null(restored);
        }
        finally
        {
            FileTypeIconService.RestoreExtensionIcon(testExt);
        }
    }

    [Fact]
    public void ShellIconService_SetAndRestoreDefaultFolderIcon_WorksInRegistry()
    {
        const string testIcon = @"C:\Test\default_folder.ico";
        var previousIcon = _shellService.GetCurrentDefaultFolderIcon(machineWide: false);

        try
        {
            _shellService.SetDefaultFolderIcon(testIcon, machineWide: false);
            var current = _shellService.GetCurrentDefaultFolderIcon(machineWide: false);
            Assert.NotNull(current);
            Assert.Contains("default_folder.ico", current);

            _shellService.RestoreDefaultFolderIcon(machineWide: false);
            var restored = _shellService.GetCurrentDefaultFolderIcon(machineWide: false);
            Assert.Null(restored);
        }
        finally
        {
            if (!string.IsNullOrEmpty(previousIcon))
            {
                _shellService.SetDefaultFolderIcon(previousIcon, machineWide: false);
            }
            else
            {
                _shellService.RestoreDefaultFolderIcon(machineWide: false);
            }
        }
    }

    [Fact]
    public void ShellIconService_SetAndRestoreDefaultFileIcon_WorksInRegistry()
    {
        const string testIcon = @"C:\Test\default_file.ico";
        var previousIcon = _shellService.GetCurrentDefaultFileIcon(machineWide: false);

        try
        {
            _shellService.SetDefaultFileIcon(testIcon, machineWide: false);
            var current = _shellService.GetCurrentDefaultFileIcon(machineWide: false);
            Assert.NotNull(current);
            Assert.Contains("default_file.ico", current);

            _shellService.RestoreDefaultFileIcon(machineWide: false);
            var restored = _shellService.GetCurrentDefaultFileIcon(machineWide: false);
            Assert.Null(restored);
        }
        finally
        {
            if (!string.IsNullOrEmpty(previousIcon))
            {
                _shellService.SetDefaultFileIcon(previousIcon, machineWide: false);
            }
            else
            {
                _shellService.RestoreDefaultFileIcon(machineWide: false);
            }
        }
    }

    [Theory]
    [InlineData(SpecialFolderKind.Desktop)]
    [InlineData(SpecialFolderKind.Downloads)]
    [InlineData(SpecialFolderKind.Documents)]
    [InlineData(SpecialFolderKind.Pictures)]
    [InlineData(SpecialFolderKind.Music)]
    [InlineData(SpecialFolderKind.Videos)]
    public void SpecialFoldersService_GetSpecialFolderClsids_ReturnsNonEmptyClsidList(SpecialFolderKind kind)
    {
        var clsids = SpecialFoldersService.GetSpecialFolderClsids(kind);
        Assert.NotEmpty(clsids);
        Assert.All(clsids, c =>
        {
            Assert.StartsWith("{", c);
            Assert.EndsWith("}", c);
            Assert.True(Guid.TryParse(c, out _));
        });
    }

    [Fact]
    public void SpecialFoldersService_TryGetSpecialFolderKind_MatchesResolvedKnownFolders()
    {
        foreach (var kind in Enum.GetValues<SpecialFolderKind>())
        {
            var resolved = SpecialFoldersService.ResolveFolderPath(kind);
            var matched = SpecialFoldersService.TryGetSpecialFolderKind(resolved, out var detectedKind);
            Assert.True(matched);
            Assert.Equal(kind, detectedKind);
        }
    }

    [Fact]
    public void ShellIconService_SetAndRestoreSpecialFolderRegistryIcons_WorksAndPreservesPreviousState()
    {
        const SpecialFolderKind targetKind = SpecialFolderKind.Desktop;
        const string testIcon = @"C:\Test\special_desktop.ico";
        var previousIcon = _shellService.GetCurrentSpecialFolderRegistryIcon(targetKind);

        try
        {
            _shellService.SetSpecialFolderRegistryIcons(targetKind, testIcon);
            var current = _shellService.GetCurrentSpecialFolderRegistryIcon(targetKind);
            Assert.NotNull(current);
            Assert.Contains("special_desktop.ico", current);

            _shellService.RestoreSpecialFolderRegistryIcons(targetKind);
            var restored = _shellService.GetCurrentSpecialFolderRegistryIcon(targetKind);
            Assert.Null(restored);
        }
        finally
        {
            if (!string.IsNullOrEmpty(previousIcon))
            {
                _shellService.SetSpecialFolderRegistryIcons(targetKind, previousIcon);
            }
            else
            {
                _shellService.RestoreSpecialFolderRegistryIcons(targetKind);
            }
        }
    }
}
