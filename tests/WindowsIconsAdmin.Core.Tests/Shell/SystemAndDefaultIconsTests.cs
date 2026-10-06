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

    [Theory]
    [InlineData(SystemIconKind.Home, "{f874310e-b6b7-47dc-bc84-b9e6b38f5903}")]
    [InlineData(SystemIconKind.Gallery, "{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}")]
    [InlineData(SystemIconKind.LinuxWsl, "{B2B4A4D1-2754-4140-A2EB-9A76D9D7CDC6}")]
    [InlineData(SystemIconKind.OneDrivePersonal, "{018D5C66-4533-4307-9B53-224DE2ED1FE6}")]
    public void ShellIconService_GetSystemIconClsid_ReturnsExpectedNavigationPaneGuids(SystemIconKind kind, string expectedClsid)
    {
        var actual = ShellIconService.GetSystemIconClsid(kind);
        Assert.Equal(expectedClsid, actual, ignoreCase: true);
    }

    [Theory]
    [InlineData(@"C:\LocalAppData\Microsoft\OneDrive\OneDrive.exe,5", true)]
    [InlineData(@"""C:\Program Files\Microsoft OneDrive\OneDrive.exe"",0", true)]
    [InlineData(@"%SystemRoot%\System32\imageres.dll,-1040", true)]
    [InlineData(@"C:\Icons\custom_onedrive.ico,0", false)]
    [InlineData(@"", false)]
    [InlineData(null, false)]
    public void ShellIconService_IsNativeOneDriveIcon_DetectsNativeVsCustomIcons(string? iconValue, bool expected)
    {
        Assert.Equal(expected, ShellIconService.IsNativeOneDriveIcon(iconValue));
    }

    [Theory]
    [InlineData(SystemIconKind.Home, @"C:\Windows\system32\shell32.dll,-51380", true)]
    [InlineData(SystemIconKind.Gallery, @"C:\Windows\system32\shell32.dll,-51586", true)]
    [InlineData(SystemIconKind.LinuxWsl, @"C:\Windows\system32\wsl.exe,-1", true)]
    [InlineData(SystemIconKind.OneDrivePersonal, @"C:\LocalAppData\Microsoft\OneDrive\OneDrive.exe,5", true)]
    [InlineData(SystemIconKind.Home, @"C:\Custom\home.ico,0", false)]
    [InlineData(SystemIconKind.Gallery, @"C:\Custom\gallery.ico,0", false)]
    [InlineData(SystemIconKind.LinuxWsl, @"C:\Custom\linux.ico,0", false)]
    [InlineData(SystemIconKind.OneDrivePersonal, @"C:\Custom\onedrive.ico,0", false)]
    public void ShellIconService_IsDefaultSystemIconValue_DistinguishesDefaultsFromCustomIcons(
        SystemIconKind kind,
        string? rawValue,
        bool expected)
    {
        Assert.Equal(expected, ShellIconService.IsDefaultSystemIconValue(kind, rawValue));
    }

    [Theory]
    [InlineData(SystemIconKind.Home)]
    [InlineData(SystemIconKind.Gallery)]
    [InlineData(SystemIconKind.LinuxWsl)]
    public void ShellIconService_SetAndRestoreNavigationPaneClsid_WorksAndPreservesPreviousState(SystemIconKind kind)
    {
        const string testIcon = @"C:\Test\navpane_custom.ico";
        var previousIcon = _shellService.GetCurrentSystemIcon(kind);

        try
        {
            _shellService.SetSystemIcon(kind, testIcon);
            var current = _shellService.GetCurrentSystemIcon(kind);
            Assert.NotNull(current);
            Assert.Contains("navpane_custom.ico", current);

            _shellService.RestoreSystemIcon(kind);
            var restored = _shellService.GetCurrentSystemIcon(kind);
            Assert.Null(restored);
        }
        finally
        {
            if (!string.IsNullOrEmpty(previousIcon))
            {
                _shellService.SetSystemIcon(kind, previousIcon);
            }
            else
            {
                _shellService.RestoreSystemIcon(kind);
            }
        }
    }
}
