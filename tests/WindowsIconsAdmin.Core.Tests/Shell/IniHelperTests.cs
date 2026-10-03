using WindowsIconsAdmin.Core.Shell;

namespace WindowsIconsAdmin.Core.Tests.Shell;

public class IniHelperTests
{
    [Fact]
    public void SetIconResource_EmptyExistingContent_AddsSectionAndResource()
    {
        var result = IniHelper.SetIconResource(null, "C:\\icon.ico,0");
        Assert.Contains("[.ShellClassInfo]", result);
        Assert.Contains("IconResource=C:\\icon.ico,0", result);
    }

    [Fact]
    public void SetIconResource_PreservesExistingSections()
    {
        var existing = "[ViewState]\r\nMode=\r\nVid=\r\nFolderType=Generic\r\n";
        var result = IniHelper.SetIconResource(existing, ".\\.folder_icon.ico,0");

        Assert.Contains("[ViewState]", result);
        Assert.Contains("FolderType=Generic", result);
        Assert.Contains("[.ShellClassInfo]", result);
        Assert.Contains("IconResource=.\\.folder_icon.ico,0", result);
    }

    [Fact]
    public void SetIconResource_UpdatesExistingIconResource()
    {
        var existing = "[.ShellClassInfo]\r\nIconResource=C:\\old.ico,0\r\n[ViewState]\r\nFolderType=Generic\r\n";
        var result = IniHelper.SetIconResource(existing, "C:\\new.ico,0");

        Assert.Contains("IconResource=C:\\new.ico,0", result);
        Assert.DoesNotContain("C:\\old.ico,0", result);
        Assert.Contains("FolderType=Generic", result);
    }

    [Fact]
    public void RemoveShellClassInfo_WhenOnlySection_ReturnsNull()
    {
        var existing = "[.ShellClassInfo]\r\nIconResource=C:\\icon.ico,0\r\n";
        var result = IniHelper.RemoveShellClassInfo(existing);

        Assert.Null(result);
    }

    [Fact]
    public void RemoveShellClassInfo_WhenOtherSectionsExist_PreservesThem()
    {
        var existing = "[.ShellClassInfo]\r\nIconResource=C:\\icon.ico,0\r\n[ViewState]\r\nFolderType=Generic\r\n";
        var result = IniHelper.RemoveShellClassInfo(existing);

        Assert.NotNull(result);
        Assert.DoesNotContain("[.ShellClassInfo]", result);
        Assert.DoesNotContain("IconResource", result);
        Assert.Contains("[ViewState]", result);
        Assert.Contains("FolderType=Generic", result);
    }
}
