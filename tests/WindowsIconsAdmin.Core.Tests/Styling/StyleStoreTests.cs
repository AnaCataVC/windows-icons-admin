using WindowsIconsAdmin.Core.Styling;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Styling;

public class StyleStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StyleStore _store;

    public StyleStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "StyleStoreTests_" + Guid.NewGuid().ToString("N"));
        _store = new StyleStore(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public void GetAll_EmptyDirectory_ReturnsDefaultStyle()
    {
        var styles = _store.GetAll();
        Assert.Single(styles);
        Assert.Equal("Soft Lavender", styles[0].Name);
    }

    [Fact]
    public void SaveAndGet_Roundtrip_Works()
    {
        var custom = new IconStyle(
            SchemaVersion: 1,
            Id: "style-mint-green",
            Name: "Mint Green",
            Container: new ContainerSpec(shape: ContainerShape.RoundedSquare, backgroundColor: "#C2F0C2", padding: 0.12),
            Treatment: new TreatmentSpec(saturation: -0.2, brightness: 0.1, contrast: -0.05)
        );

        _store.Save(custom);

        var retrieved = _store.Get("style-mint-green");
        Assert.NotNull(retrieved);
        Assert.Equal("Mint Green", retrieved.Name);
        Assert.Equal(ContainerShape.RoundedSquare, retrieved.Container.Shape);
        Assert.Equal("#C2F0C2", retrieved.Container.BackgroundColor);

        var all = _store.GetAll();
        Assert.Contains(all, s => s.Id == "style-mint-green");
    }

    [Fact]
    public void Delete_RemovesStyle()
    {
        var style = IconStyle.CreateDefault("To Delete") with { Id = "style-delete-me" };
        _store.Save(style);

        Assert.NotNull(_store.Get("style-delete-me"));
        Assert.True(_store.Delete("style-delete-me"));
        Assert.Null(_store.Get("style-delete-me"));
    }
}
