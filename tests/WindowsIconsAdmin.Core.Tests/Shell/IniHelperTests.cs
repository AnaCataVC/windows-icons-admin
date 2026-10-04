using WindowsIconsAdmin.Core.Shell;
using Xunit;

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

    #region SC-I1 to SC-I3: Anti-Injection & Hardened SetIconResource

    [Theory]
    [InlineData("C:\\test.ico,0\r\n[EvilSection]")]
    [InlineData("C:\\test.ico,0\nInjectedKey=1")]
    [InlineData("C:\\test.ico,0\rInjectedKey=1")]
    [InlineData("\r\nC:\\test.ico,0")]
    public void SetIconResource_WhenContainsCarriageReturnOrLineFeed_ThrowsArgumentException(string maliciousResource)
    {
        Assert.Throws<ArgumentException>(() => IniHelper.SetIconResource(null, maliciousResource));
    }

    [Theory]
    [InlineData("C:\\Icons\\[Section]icon.ico,0")]
    [InlineData("C:\\test.ico,0[ViewState]")]
    [InlineData("C:\\icon[1].ico,0")]
    [InlineData("[.ShellClassInfo]IconResource=C:\\evil.ico")]
    public void SetIconResource_WhenContainsSquareBrackets_ThrowsArgumentException(string maliciousResource)
    {
        Assert.Throws<ArgumentException>(() => IniHelper.SetIconResource(null, maliciousResource));
    }

    [Fact]
    public void SetIconResource_ValidResource_PreservesCommentsAndOtherSections()
    {
        var existing = "; Leading comment about folder config\r\n" +
                       "[ViewState]\r\n" +
                       "FolderType=Generic\r\n\r\n" +
                       "; Another comment\r\n" +
                       "[.ShellClassInfo]\r\n" +
                       "IconResource=C:\\Old\\icon.ico,0\r\n";

        var result = IniHelper.SetIconResource(existing, @"C:\Icons\myicon.ico,0");

        Assert.Contains("; Leading comment about folder config", result);
        Assert.Contains("; Another comment", result);
        Assert.Contains("[ViewState]", result);
        Assert.Contains("FolderType=Generic", result);
        Assert.Contains("[.ShellClassInfo]", result);
        Assert.Contains(@"IconResource=C:\Icons\myicon.ico,0", result);
        Assert.DoesNotContain(@"C:\Old\icon.ico,0", result);
    }

    #endregion

    #region SC-K1 to SC-K3: Hardened RemoveShellClassInfo

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

    [Fact]
    public void RemoveShellClassInfo_PreserveFalse_RemovesEntireShellClassInfoSection()
    {
        var existing = "[.ShellClassInfo]\r\n" +
                       "LocalizedResourceName=@%SystemRoot%\\system32\\shell32.dll,-21770\r\n" +
                       "IconResource=C:\\Custom\\icon.ico,0\r\n" +
                       "CLSID={FDD39AD0-238F-46AF-ADB4-6C85480369C7}\r\n";

        var result = IniHelper.RemoveShellClassInfo(existing, preserveShellDirectives: false);

        Assert.Null(result);
    }

    [Fact]
    public void RemoveShellClassInfo_PreserveTrue_RetainsSystemDirectivesAndRemovesIconKeys()
    {
        var existing = "[.ShellClassInfo]\r\n" +
                       "LocalizedResourceName=@%SystemRoot%\\system32\\shell32.dll,-21770\r\n" +
                       "IconResource=C:\\Custom\\icon.ico,0\r\n" +
                       "IconFile=C:\\Custom\\icon.ico\r\n" +
                       "IconIndex=0\r\n" +
                       "CLSID={FDD39AD0-238F-46AF-ADB4-6C85480369C7}\r\n";

        var result = IniHelper.RemoveShellClassInfo(existing, preserveShellDirectives: true);

        Assert.NotNull(result);
        Assert.Contains("[.ShellClassInfo]", result);
        Assert.Contains("LocalizedResourceName=@%SystemRoot%\\system32\\shell32.dll,-21770", result);
        Assert.Contains("CLSID={FDD39AD0-238F-46AF-ADB4-6C85480369C7}", result);
        Assert.DoesNotContain("IconResource", result);
        Assert.DoesNotContain("IconFile", result);
        Assert.DoesNotContain("IconIndex", result);
    }

    [Fact]
    public void RemoveShellClassInfo_PreserveTrue_WhenOnlyIconKeysInShellClassInfo_RemovesShellClassInfo()
    {
        var existing = "[.ShellClassInfo]\r\n" +
                       "IconResource=C:\\Custom\\icon.ico,0\r\n";

        var result = IniHelper.RemoveShellClassInfo(existing, preserveShellDirectives: true);

        Assert.Null(result);
    }

    [Fact]
    public void RemoveShellClassInfo_PreserveTrue_WhenOnlyIconKeysAndOtherSectionExists_RemovesShellClassInfoPreservesOther()
    {
        var existing = "[.ShellClassInfo]\r\n" +
                       "IconResource=C:\\Custom\\icon.ico,0\r\n" +
                       "[ViewState]\r\n" +
                       "FolderType=Generic\r\n";

        var result = IniHelper.RemoveShellClassInfo(existing, preserveShellDirectives: true);

        Assert.NotNull(result);
        Assert.DoesNotContain("[.ShellClassInfo]", result);
        Assert.DoesNotContain("IconResource", result);
        Assert.Contains("[ViewState]", result);
        Assert.Contains("FolderType=Generic", result);
    }

    #endregion
}
