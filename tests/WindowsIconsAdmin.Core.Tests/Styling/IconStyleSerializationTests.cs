using System.Text.Json;
using WindowsIconsAdmin.Core.Styling;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Styling;

public class IconStyleSerializationTests
{
    [Fact]
    public void CreateDefault_ProducesValidStyle()
    {
        var style = IconStyle.CreateDefault();

        Assert.Equal(IconStyle.CurrentSchemaVersion, style.SchemaVersion);
        Assert.Equal("style-soft-lavender", style.Id);
        Assert.Equal("Soft Lavender", style.Name);
        Assert.Equal(ContainerShape.Squircle, style.Container.Shape);
        Assert.Equal("#E6DBFA", style.Container.BackgroundColor);
        Assert.Equal(0.16, style.Container.Padding);
        Assert.Equal(-0.15, style.Treatment.Saturation);
        Assert.Equal(0.05, style.Treatment.Brightness);
        Assert.Equal(0.0, style.Treatment.Contrast);
        Assert.Null(style.Treatment.TintColor);
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesAllProperties()
    {
        var original = new IconStyle(
            SchemaVersion: 1,
            Id: "style-test-1",
            Name: "Neon Blue",
            Container: new ContainerSpec(ContainerShape.Circle, "#00A2FF", 0.20),
            Treatment: new TreatmentSpec(0.5, -0.1, 0.2, "#00FFFF")
        );

        var json = original.ToJson();
        var restored = IconStyle.FromJson(json);

        Assert.Equal(original.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Container.Shape, restored.Container.Shape);
        Assert.Equal(original.Container.BackgroundColor, restored.Container.BackgroundColor);
        Assert.Equal(original.Container.Padding, restored.Container.Padding);
        Assert.Equal(original.Treatment.Saturation, restored.Treatment.Saturation);
        Assert.Equal(original.Treatment.Brightness, restored.Treatment.Brightness);
        Assert.Equal(original.Treatment.Contrast, restored.Treatment.Contrast);
        Assert.Equal(original.Treatment.TintColor, restored.Treatment.TintColor);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.41)]
    public void ContainerSpec_InvalidPadding_ThrowsArgumentOutOfRangeException(double padding)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ContainerSpec(ContainerShape.Squircle, "#FFFFFF", padding));
    }

    [Theory]
    [InlineData(-1.1, 0.0, 0.0)]
    [InlineData(1.1, 0.0, 0.0)]
    [InlineData(0.0, -1.1, 0.0)]
    [InlineData(0.0, 1.1, 0.0)]
    [InlineData(0.0, 0.0, -1.1)]
    [InlineData(0.0, 0.0, 1.1)]
    public void TreatmentSpec_InvalidRanges_ThrowsArgumentOutOfRangeException(double s, double b, double c)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TreatmentSpec(s, b, c));
    }
}
