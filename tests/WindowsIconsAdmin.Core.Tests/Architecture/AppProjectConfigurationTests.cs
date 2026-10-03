using System.IO;
using System.Xml.Linq;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Architecture;

public class AppProjectConfigurationTests
{
    private static string FindAppCsprojPath()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, "src", "WindowsIconsAdmin.App", "WindowsIconsAdmin.App.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        throw new FileNotFoundException("Could not find WindowsIconsAdmin.App.csproj from test execution directory.");
    }

    [Fact]
    public void AppCsproj_MustHavePublishTrimmedDisabled()
    {
        var csprojPath = FindAppCsprojPath();
        var doc = XDocument.Load(csprojPath);

        var publishTrimmedElements = doc.Descendants("PublishTrimmed").ToList();
        Assert.NotEmpty(publishTrimmedElements);

        foreach (var element in publishTrimmedElements)
        {
            Assert.Equal("False", element.Value, ignoreCase: true);
        }
    }

    [Fact]
    public void AppCsproj_MustBeUnpackagedAndSelfContained()
    {
        var csprojPath = FindAppCsprojPath();
        var doc = XDocument.Load(csprojPath);

        var packageType = doc.Descendants("WindowsPackageType").FirstOrDefault();
        Assert.NotNull(packageType);
        Assert.Equal("None", packageType.Value);

        var appSdkSelfContained = doc.Descendants("WindowsAppSDKSelfContained").FirstOrDefault();
        Assert.NotNull(appSdkSelfContained);
        Assert.Equal("true", appSdkSelfContained.Value, ignoreCase: true);

        var selfContained = doc.Descendants("SelfContained").FirstOrDefault();
        Assert.NotNull(selfContained);
        Assert.Equal("true", selfContained.Value, ignoreCase: true);
    }
}
