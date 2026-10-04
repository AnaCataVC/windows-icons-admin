using WindowsIconsAdmin.Core.Shell;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Shell;

public class ShellIconServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ShellIconService _shellService;

    public ShellIconServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wia_shell_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _shellService = new ShellIconService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                // Reset attributes recursively before deleting so Dispose doesn't fail
                foreach (var file in Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void ApplyFolderIcon_CreatesDesktopIniAndSetsAttributes()
    {
        var folder = Path.Combine(_tempDir, "FolderA");
        Directory.CreateDirectory(folder);

        var snapshot = _shellService.ApplyFolderIcon(folder, "C:\\test.ico,0");

        Assert.False(snapshot.HadDesktopIni);
        var iniPath = Path.Combine(folder, "desktop.ini");
        Assert.True(File.Exists(iniPath));

        var content = File.ReadAllText(iniPath);
        Assert.Contains("IconResource=C:\\test.ico,0", content);

        var iniAttrs = File.GetAttributes(iniPath);
        Assert.True((iniAttrs & FileAttributes.Hidden) != 0);
        Assert.True((iniAttrs & FileAttributes.System) != 0);

        var folderAttrs = File.GetAttributes(folder);
        Assert.True((folderAttrs & FileAttributes.ReadOnly) != 0 || (folderAttrs & FileAttributes.System) != 0);
    }

    [Fact]
    public void RestoreFolderDefault_RemovesDesktopIniWhenSingleSection()
    {
        var folder = Path.Combine(_tempDir, "FolderB");
        Directory.CreateDirectory(folder);

        _shellService.ApplyFolderIcon(folder, "C:\\test.ico,0");
        var restoreSnapshot = _shellService.RestoreFolderDefault(folder);

        var iniPath = Path.Combine(folder, "desktop.ini");
        Assert.False(File.Exists(iniPath));
    }

    [Fact]
    public void RevertFolder_RestoresPreviousStateAccurately()
    {
        var folder = Path.Combine(_tempDir, "FolderC");
        Directory.CreateDirectory(folder);

        var initialSnapshot = _shellService.ApplyFolderIcon(folder, "C:\\initial.ico,0");
        var secondSnapshot = _shellService.ApplyFolderIcon(folder, "C:\\second.ico,0");

        // Revert to state before second apply
        _shellService.RevertFolder(secondSnapshot);

        var iniPath = Path.Combine(folder, "desktop.ini");
        Assert.True(File.Exists(iniPath));
        var content = File.ReadAllText(iniPath);
        Assert.Contains("IconResource=C:\\initial.ico,0", content);
        Assert.DoesNotContain("C:\\second.ico,0", content);
    }

    #region SC-R1 & SC-R2: Safe Rollback (RevertFolder)

    [Fact]
    public void RevertFolder_WhenCopiedIconFileNameIsValidAndContained_DeletesIconFile()
    {
        var folder = Path.Combine(_tempDir, "FolderR1");
        Directory.CreateDirectory(folder);

        var copiedIconName = ".folder_icon.ico";
        var localIconPath = Path.Combine(folder, copiedIconName);
        File.WriteAllText(localIconPath, "dummy-ico-content");

        var snapshot = _shellService.ApplyFolderIcon(folder, $"{localIconPath},0") with
        {
            CopiedIconFileName = copiedIconName
        };

        Assert.True(File.Exists(localIconPath));

        _shellService.RevertFolder(snapshot);

        Assert.False(File.Exists(localIconPath), "Safe embedded icon file must be deleted upon rollback.");
    }

    [Fact]
    public void RevertFolder_WhenCopiedIconFileNameContainsTraversal_DoesNotDeleteAndDoesNotThrow()
    {
        var folder = Path.Combine(_tempDir, "FolderR2");
        Directory.CreateDirectory(folder);

        var canaryOutside = Path.Combine(_tempDir, "canary_dont_delete.txt");
        File.WriteAllText(canaryOutside, "DO_NOT_DELETE");

        var snapshot = _shellService.ApplyFolderIcon(folder, "C:\\test.ico,0") with
        {
            CopiedIconFileName = "../canary_dont_delete.txt"
        };

        var exception = Record.Exception(() => _shellService.RevertFolder(snapshot));

        Assert.Null(exception); // Must not throw or crash batch undo
        Assert.True(File.Exists(canaryOutside), "Outside file must NOT be deleted when traversal is attempted.");
    }

    [Fact]
    public void RevertFolder_WhenCopiedIconFileNameHasRestrictedExtension_DoesNotDeleteAndDoesNotThrow()
    {
        var folder = Path.Combine(_tempDir, "FolderR2Ext");
        Directory.CreateDirectory(folder);

        var scriptFile = Path.Combine(folder, "safe_script.bat");
        File.WriteAllText(scriptFile, "@echo off");

        var snapshot = _shellService.ApplyFolderIcon(folder, "C:\\test.ico,0") with
        {
            CopiedIconFileName = "safe_script.bat"
        };

        var exception = Record.Exception(() => _shellService.RevertFolder(snapshot));

        Assert.Null(exception);
        Assert.True(File.Exists(scriptFile), "File with restricted extension must NOT be deleted by RevertFolder.");
    }

    #endregion

    #region KnownFolder Safe Restoration Contract (RestoreFolderDefault)

    [Fact]
    public void RestoreFolderDefault_WhenKnownFolder_PreservesSystemDirectivesAndAttributes()
    {
        var folder = Path.Combine(_tempDir, "KnownFolderTest");
        Directory.CreateDirectory(folder);

        var iniPath = Path.Combine(folder, "desktop.ini");
        var initialContent = "[.ShellClassInfo]\r\n" +
                             "LocalizedResourceName=@%SystemRoot%\\system32\\shell32.dll,-21770\r\n" +
                             "IconResource=C:\\Custom\\icon.ico,0\r\n" +
                             "CLSID={FDD39AD0-238F-46AF-ADB4-6C85480369C7}\r\n";
        File.WriteAllText(iniPath, initialContent);
        File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);
        File.SetAttributes(folder, FileAttributes.ReadOnly);

        _shellService.RestoreFolderDefault(folder, isKnownFolder: true);

        Assert.True(File.Exists(iniPath), "desktop.ini must be preserved for KnownFolder with system directives.");
        var restoredContent = File.ReadAllText(iniPath);
        Assert.Contains("[.ShellClassInfo]", restoredContent);
        Assert.Contains("LocalizedResourceName=@%SystemRoot%\\system32\\shell32.dll,-21770", restoredContent);
        Assert.Contains("CLSID={FDD39AD0-238F-46AF-ADB4-6C85480369C7}", restoredContent);
        Assert.DoesNotContain("IconResource", restoredContent);

        var folderAttrs = File.GetAttributes(folder);
        Assert.True(
            (folderAttrs & FileAttributes.ReadOnly) != 0 || (folderAttrs & FileAttributes.System) != 0,
            "Folder ReadOnly or System attribute must be preserved on KnownFolders.");
    }

    #endregion
}
