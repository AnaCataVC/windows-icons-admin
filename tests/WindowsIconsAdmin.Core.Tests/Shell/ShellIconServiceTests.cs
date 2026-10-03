using WindowsIconsAdmin.Core.Shell;

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
}
