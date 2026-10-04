using WindowsIconsAdmin.Core.Safety;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Safety;

public class SystemFolderGuardTests
{
    #region SC-F1 to SC-F7: Target Folder Safety

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t   ")]
    public void ValidateTargetFolder_WhenNullOrWhiteSpace_ReturnsViolationInvalidCharacters(string? path)
    {
        var result = SystemFolderGuard.ValidateTargetFolder(path);

        Assert.False(result.IsValid);
        Assert.Equal(SafetyViolationKind.InvalidCharacters, result.ViolationKind);
        Assert.NotNull(result.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureSafeTargetFolder_WhenNullOrWhiteSpace_ThrowsArgumentException(string? path)
    {
        Assert.Throws<ArgumentException>(() => SystemFolderGuard.EnsureSafeTargetFolder(path));
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:")]
    [InlineData(@"D:\")]
    [InlineData(@"d:")]
    [InlineData(@"c:/")]
    [InlineData(@"Z:\")]
    [InlineData(@"\\")]
    [InlineData("/")]
    public void ValidateTargetFolder_WhenDriveRoot_ReturnsViolationDriveRoot(string path)
    {
        var result = SystemFolderGuard.ValidateTargetFolder(path);

        Assert.False(result.IsValid);
        Assert.Equal(SafetyViolationKind.DriveRoot, result.ViolationKind);
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"D:")]
    [InlineData("/")]
    [InlineData(@"\\")]
    public void EnsureSafeTargetFolder_WhenDriveRoot_ThrowsSystemProtectionExceptionWithDriveRoot(string path)
    {
        var ex = Assert.Throws<SystemProtectionException>(() => SystemFolderGuard.EnsureSafeTargetFolder(path));
        Assert.Equal(SafetyViolationKind.DriveRoot, ex.ViolationKind);
    }

    [Fact]
    public void ValidateTargetFolder_WhenInsideSystemRoot_ReturnsViolationSystemRoot()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);

        var pathsToTest = new[]
        {
            winDir,
            winDir + @"\",
            winDir.ToLowerInvariant(),
            winDir.ToUpperInvariant(),
            sysDir,
            Path.Combine(winDir, "System32"),
            Path.Combine(winDir, "SysWOW64"),
            Path.Combine(winDir, @"System32\drivers"),
            winDir.Replace('\\', '/')
        };

        foreach (var path in pathsToTest)
        {
            var result = SystemFolderGuard.ValidateTargetFolder(path);
            Assert.False(result.IsValid, $"Path should be blocked as SystemRoot: {path}");
            Assert.Equal(SafetyViolationKind.SystemRoot, result.ViolationKind);
        }
    }

    [Fact]
    public void EnsureSafeTargetFolder_WhenInsideSystemRoot_ThrowsSystemProtectionExceptionWithSystemRoot()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        var ex = Assert.Throws<SystemProtectionException>(() => SystemFolderGuard.EnsureSafeTargetFolder(winDir));
        Assert.Equal(SafetyViolationKind.SystemRoot, ex.ViolationKind);
    }

    [Fact]
    public void IsProtectedFolder_WhenInsideSystemRoot_ReturnsTrue()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Assert.True(SystemFolderGuard.IsProtectedFolder(winDir));
        Assert.True(SystemFolderGuard.IsProtectedFolder(Path.Combine(winDir, "System32")));
    }

    [Fact]
    public void ValidateTargetFolder_WhenInsideProgramFiles_ReturnsViolationProgramFiles()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var pathsToTest = new List<string> { pf };
        if (!string.IsNullOrEmpty(pfX86))
        {
            pathsToTest.Add(pfX86);
        }

        pathsToTest.Add(Path.Combine(pf, "Common Files"));
        pathsToTest.Add(pf.ToLowerInvariant());
        pathsToTest.Add(pf + @"\");
        pathsToTest.Add(pf.Replace('\\', '/'));

        foreach (var path in pathsToTest)
        {
            var result = SystemFolderGuard.ValidateTargetFolder(path);
            Assert.False(result.IsValid, $"Path should be blocked as ProgramFiles: {path}");
            Assert.Equal(SafetyViolationKind.ProgramFiles, result.ViolationKind);
        }
    }

    [Fact]
    public void EnsureSafeTargetFolder_WhenInsideProgramFiles_ThrowsSystemProtectionExceptionWithProgramFiles()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        var ex = Assert.Throws<SystemProtectionException>(() => SystemFolderGuard.EnsureSafeTargetFolder(pf));
        Assert.Equal(SafetyViolationKind.ProgramFiles, ex.ViolationKind);
    }

    [Fact]
    public void IsProtectedFolder_WhenInsideProgramFiles_ReturnsTrue()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.True(SystemFolderGuard.IsProtectedFolder(pf));
    }

    [Fact]
    public void ValidateTargetFolder_WhenUserProfileRoot_ReturnsViolationUserProfileRoot()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var pathsToTest = new[]
        {
            userProfile,
            userProfile + @"\",
            userProfile.ToLowerInvariant(),
            userProfile.ToUpperInvariant(),
            userProfile.Replace('\\', '/')
        };

        foreach (var path in pathsToTest)
        {
            var result = SystemFolderGuard.ValidateTargetFolder(path);
            Assert.False(result.IsValid, $"UserProfile root should be blocked: {path}");
            Assert.Equal(SafetyViolationKind.UserProfileRoot, result.ViolationKind);
        }
    }

    [Fact]
    public void EnsureSafeTargetFolder_WhenUserProfileRoot_ThrowsSystemProtectionExceptionWithUserProfileRoot()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var ex = Assert.Throws<SystemProtectionException>(() => SystemFolderGuard.EnsureSafeTargetFolder(userProfile));
        Assert.Equal(SafetyViolationKind.UserProfileRoot, ex.ViolationKind);
    }

    [Fact]
    public void ValidateTargetFolder_WhenUserProfileChild_ReturnsSuccess()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var childDir = Path.Combine(userProfile, "MyProjects", "SafeFolder");

        var result = SystemFolderGuard.ValidateTargetFolder(childDir);
        Assert.True(result.IsValid);
        Assert.Equal(SafetyViolationKind.None, result.ViolationKind);
    }

    [Fact]
    public void ValidateTargetFolder_WhenInsideAppData_ReturnsViolationAppDataSensitive()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var pathsToTest = new[]
        {
            appData,
            localAppData,
            Path.Combine(appData, "Microsoft"),
            Path.Combine(localAppData, "Temp"),
            Path.Combine(localAppData, "Google", "Chrome"),
            appData.ToLowerInvariant(),
            localAppData + @"\"
        };

        foreach (var path in pathsToTest)
        {
            var result = SystemFolderGuard.ValidateTargetFolder(path);
            Assert.False(result.IsValid, $"AppData path should be blocked: {path}");
            Assert.Equal(SafetyViolationKind.AppDataSensitive, result.ViolationKind);
        }
    }

    [Fact]
    public void EnsureSafeTargetFolder_WhenInsideAppData_ThrowsSystemProtectionExceptionWithAppDataSensitive()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var ex = Assert.Throws<SystemProtectionException>(() => SystemFolderGuard.EnsureSafeTargetFolder(localAppData));
        Assert.Equal(SafetyViolationKind.AppDataSensitive, ex.ViolationKind);
    }

    [Fact]
    public void ValidateTargetFolder_WhenInsideAppDataOwnAppCacheFolder_IsPermitted()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var ownCacheFolder = Path.Combine(localAppData, "WindowsIconsAdmin", "Cache");

        var result = SystemFolderGuard.ValidateTargetFolder(ownCacheFolder);
        Assert.True(result.IsValid, "Own application cache folder under AppData must be permitted.");
        Assert.Equal(SafetyViolationKind.None, result.ViolationKind);
    }

    [Theory]
    [InlineData(@"D:\Projects\Code")]
    [InlineData(@"C:\SafeWork\Icons")]
    [InlineData(@"D:\Projet Français 2026\Música")]
    public void ValidateTargetFolder_WhenSafeFolder_ReturnsSuccess(string path)
    {
        var result = SystemFolderGuard.ValidateTargetFolder(path);

        Assert.True(result.IsValid);
        Assert.Equal(SafetyViolationKind.None, result.ViolationKind);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void EnsureSafeTargetFolder_WhenSafeFolder_DoesNotThrow()
    {
        var customPath = @"D:\MyPersonalIcons\FolderA";
        var exception = Record.Exception(() => SystemFolderGuard.EnsureSafeTargetFolder(customPath));
        Assert.Null(exception);
    }

    [Fact]
    public void IsProtectedFolder_WhenSafeFolder_ReturnsFalse()
    {
        Assert.False(SystemFolderGuard.IsProtectedFolder(@"D:\Projects\CustomFolder"));
    }

    #endregion

    #region SC-E1 to SC-E4: Embedded File Name Anti-Traversal

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ValidateEmbeddedFileName_WhenNullOrWhiteSpace_ReturnsViolationInvalidCharacters(string? fileName)
    {
        var result = SystemFolderGuard.ValidateEmbeddedFileName(fileName);

        Assert.False(result.IsValid);
        Assert.Equal(SafetyViolationKind.InvalidCharacters, result.ViolationKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureSafeEmbeddedFileName_WhenNullOrWhiteSpace_ThrowsArgumentException(string? fileName)
    {
        Assert.Throws<ArgumentException>(() => SystemFolderGuard.EnsureSafeEmbeddedFileName(fileName));
    }

    [Theory]
    [InlineData("../icon.ico")]
    [InlineData(@"..\icon.ico")]
    [InlineData("folder/icon.ico")]
    [InlineData(@"folder\icon.ico")]
    [InlineData(@"C:\icon.ico")]
    [InlineData("/icon.ico")]
    [InlineData(@"..\..\evil.ico")]
    [InlineData(@"sub/dir/test.ico")]
    public void ValidateEmbeddedFileName_WhenContainsPathTraversal_ReturnsViolationPathTraversal(string fileName)
    {
        var result = SystemFolderGuard.ValidateEmbeddedFileName(fileName);

        Assert.False(result.IsValid);
        Assert.Equal(SafetyViolationKind.PathTraversal, result.ViolationKind);
    }

    [Theory]
    [InlineData("../icon.ico")]
    [InlineData(@"..\icon.ico")]
    [InlineData(@"C:\icon.ico")]
    [InlineData("sub/folder/icon.ico")]
    public void EnsureSafeEmbeddedFileName_WhenContainsPathTraversal_ThrowsPathTraversalException(string fileName)
    {
        Assert.Throws<PathTraversalException>(() => SystemFolderGuard.EnsureSafeEmbeddedFileName(fileName));
    }

    [Theory]
    [InlineData("icon*.ico")]
    [InlineData("icon?.ico")]
    [InlineData("icon<>.ico")]
    [InlineData("icon|.ico")]
    [InlineData("icon:test.ico")]
    [InlineData("icon\"name.ico")]
    [InlineData("icon\0name.ico")]
    public void ValidateEmbeddedFileName_WhenContainsInvalidChars_ReturnsViolationInvalidCharacters(string fileName)
    {
        var result = SystemFolderGuard.ValidateEmbeddedFileName(fileName);

        Assert.False(result.IsValid);
        Assert.Equal(SafetyViolationKind.InvalidCharacters, result.ViolationKind);
    }

    [Theory]
    [InlineData(".folder_icon.ico")]
    [InlineData("icon.ico")]
    [InlineData("MY_CUSTOM_ICON.ICO")]
    [InlineData("folder.custom.ico")]
    [InlineData("ícono_música.ico")]
    public void ValidateEmbeddedFileName_WhenValidIco_ReturnsSuccess(string fileName)
    {
        var result = SystemFolderGuard.ValidateEmbeddedFileName(fileName);

        Assert.True(result.IsValid);
        Assert.Equal(SafetyViolationKind.None, result.ViolationKind);
    }

    [Theory]
    [InlineData("icon.exe")]
    [InlineData("script.bat")]
    [InlineData("image.png")]
    [InlineData("document.pdf")]
    [InlineData("icon.ico.txt")]
    [InlineData("noextension")]
    public void ValidateEmbeddedFileName_WhenNonIcoExtension_ReturnsViolation(string fileName)
    {
        var result = SystemFolderGuard.ValidateEmbeddedFileName(fileName);

        Assert.False(result.IsValid);
        Assert.True(
            result.ViolationKind == SafetyViolationKind.RestrictedFileExtension ||
            result.ViolationKind == SafetyViolationKind.InvalidCharacters,
            $"Expected RestrictedFileExtension or InvalidCharacters but got {result.ViolationKind}");
    }

    [Theory]
    [InlineData("icon.exe")]
    [InlineData("script.bat")]
    [InlineData("photo.png")]
    public void EnsureSafeEmbeddedFileName_WhenNonIcoExtension_ThrowsException(string fileName)
    {
        Assert.ThrowsAny<Exception>(() => SystemFolderGuard.EnsureSafeEmbeddedFileName(fileName));
    }

    #endregion

    #region SC-X1 to SC-X3: File Extension Safety

    [Fact]
    public void RestrictedExtensions_ContainsMandatedSecurityExtensions()
    {
        var mandated = new[]
        {
            ".exe", ".dll", ".sys", ".bat", ".cmd", ".com", ".scr",
            ".msi", ".vbs", ".ps1", ".reg", ".lnk", ".cpl", ".drv"
        };

        Assert.NotNull(SystemFolderGuard.RestrictedExtensions);

        foreach (var ext in mandated)
        {
            var containsWithDot = SystemFolderGuard.RestrictedExtensions.Contains(ext);
            var containsWithoutDot = SystemFolderGuard.RestrictedExtensions.Contains(ext.TrimStart('.'));
            Assert.True(containsWithDot || containsWithoutDot,
                $"RestrictedExtensions collection must contain mandated extension: {ext}");
        }
    }

    [Theory]
    [InlineData("exe")]
    [InlineData(".exe")]
    [InlineData(".EXE")]
    [InlineData("EXE")]
    [InlineData(".dll")]
    [InlineData("DLL")]
    [InlineData(".sys")]
    [InlineData(".bat")]
    [InlineData("BAT")]
    [InlineData(".cmd")]
    [InlineData(".com")]
    [InlineData(".scr")]
    [InlineData(".msi")]
    [InlineData(".vbs")]
    [InlineData(".ps1")]
    [InlineData(".reg")]
    [InlineData(".lnk")]
    [InlineData(".cpl")]
    [InlineData(".drv")]
    public void ValidateExtension_WhenRestricted_ReturnsViolationRestrictedExtension(string extension)
    {
        var result = SystemFolderGuard.ValidateExtension(extension);

        Assert.False(result.IsValid);
        Assert.Equal(SafetyViolationKind.RestrictedFileExtension, result.ViolationKind);
    }

    [Theory]
    [InlineData("exe")]
    [InlineData(".bat")]
    [InlineData(".ps1")]
    [InlineData("DLL")]
    public void EnsureSafeExtension_WhenRestricted_ThrowsRestrictedExtensionException(string extension)
    {
        var ex = Assert.Throws<RestrictedExtensionException>(() => SystemFolderGuard.EnsureSafeExtension(extension));
        Assert.NotNull(ex.Extension);
        Assert.True(
            string.Equals(ex.Extension.TrimStart('.'), extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase),
            $"Exception Extension property '{ex.Extension}' should match queried '{extension}'");
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData("exe")]
    [InlineData(".bat")]
    [InlineData(".PS1")]
    [InlineData(".dll")]
    public void IsRestrictedExtension_WhenRestricted_ReturnsTrue(string extension)
    {
        Assert.True(SystemFolderGuard.IsRestrictedExtension(extension));
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData("txt")]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".json")]
    [InlineData(".py")]
    [InlineData(".custom")]
    [InlineData(".ico")]
    [InlineData("ico")]
    [InlineData(".png")]
    public void ValidateExtension_WhenAllowed_ReturnsSuccess(string extension)
    {
        var result = SystemFolderGuard.ValidateExtension(extension);

        Assert.True(result.IsValid);
        Assert.Equal(SafetyViolationKind.None, result.ViolationKind);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".ico")]
    [InlineData(".pdf")]
    public void EnsureSafeExtension_WhenAllowed_DoesNotThrow(string extension)
    {
        var exception = Record.Exception(() => SystemFolderGuard.EnsureSafeExtension(extension));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".ico")]
    [InlineData(".png")]
    public void IsRestrictedExtension_WhenAllowed_ReturnsFalse(string extension)
    {
        Assert.False(SystemFolderGuard.IsRestrictedExtension(extension));
    }

    #endregion

    #region SC-C1 to SC-C4: Path Containment (IsContainedWithin)

    [Theory]
    [InlineData(@"C:\Folder", @"C:\Folder\sub\file.ico")]
    [InlineData(@"C:\Folder\", @"C:\Folder\file.ico")]
    [InlineData(@"C:\Folder", @"C:\Folder\deep\nested\structure\icon.ico")]
    [InlineData(@"c:\folder", @"C:\FOLDER\sub\file.ico")] // Case-insensitivity
    [InlineData("C:/Folder", @"C:\Folder\sub\file.ico")] // Forward slashes
    [InlineData(@"C:\Folder", "C:/Folder/sub/file.ico")]
    public void IsContainedWithin_WhenDirectOrNestedChild_ReturnsTrue(string parent, string child)
    {
        Assert.True(SystemFolderGuard.IsContainedWithin(parent, child));
    }

    [Theory]
    [InlineData(@"C:\Folder", @"C:\Folder\..\Other\file.ico")]
    [InlineData(@"C:\Folder", @"C:\Folder\..\file.ico")]
    [InlineData(@"C:\Folder\Sub", @"C:\Folder\Sub\..\..\file.ico")]
    [InlineData(@"C:\Folder", @"C:\OtherFolder\file.ico")]
    public void IsContainedWithin_WhenEscapingTraversal_ReturnsFalse(string parent, string child)
    {
        Assert.False(SystemFolderGuard.IsContainedWithin(parent, child));
    }

    [Theory]
    [InlineData(@"C:\Folder", @"C:\Folder")]
    [InlineData(@"C:\Folder\", @"C:\Folder")]
    [InlineData(@"C:\Folder", @"C:\Folder\")]
    [InlineData(@"c:\folder", @"C:\FOLDER")]
    public void IsContainedWithin_WhenExactSamePath_ReturnsFalse(string parent, string child)
    {
        Assert.False(SystemFolderGuard.IsContainedWithin(parent, child));
    }

    [Theory]
    [InlineData(@"C:\Folder", @"C:\FolderNotChild\file.ico")]
    [InlineData(@"C:\Foo", @"C:\FooBar\baz.ico")]
    [InlineData(@"C:\Base", @"C:\BaseDir\file.txt")]
    public void IsContainedWithin_WhenPrefixMatchesWithoutSeparator_ReturnsFalse(string parent, string child)
    {
        Assert.False(SystemFolderGuard.IsContainedWithin(parent, child));
    }

    #endregion

    #region Known Folders (IsKnownFolder)

    [Fact]
    public void IsKnownFolder_WhenSpecialFolders_ReturnsTrue()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        if (!string.IsNullOrEmpty(desktop))
        {
            Assert.True(SystemFolderGuard.IsKnownFolder(desktop));
        }

        if (!string.IsNullOrEmpty(documents))
        {
            Assert.True(SystemFolderGuard.IsKnownFolder(documents));
        }
    }

    [Fact]
    public void IsKnownFolder_WhenArbitraryUserFolder_ReturnsFalse()
    {
        Assert.False(SystemFolderGuard.IsKnownFolder(@"C:\Users\username\Desktop\MyCustomProject"));
        Assert.False(SystemFolderGuard.IsKnownFolder(@"D:\Projects\Code"));
    }

    #endregion
}
