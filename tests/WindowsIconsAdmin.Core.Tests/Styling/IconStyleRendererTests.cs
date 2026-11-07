using WindowsIconsAdmin.Core.Styling;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Styling;

public class IconStyleRendererTests
{
    [Fact]
    public void RenderContainer_Squircle_CenterIsOpaque_CornersAreTransparent()
    {
        var container = new ContainerSpec(
            shape: ContainerShape.Squircle,
            backgroundColor: "#E6DBFA",
            padding: 0.16);

        var canvas = IconStyleRenderer.RenderContainer(64, container);

        Assert.Equal(64, canvas.Width);
        Assert.Equal(64, canvas.Height);

        // Center pixel (32, 32) must be fully opaque background color (#E6DBFA = 230, 219, 250)
        var center = canvas.GetPixel(32, 32);
        Assert.Equal(230, center.R);
        Assert.Equal(219, center.G);
        Assert.Equal(250, center.B);
        Assert.Equal(255, center.A);

        // Extreme corner (0, 0) must be completely transparent
        var corner = canvas.GetPixel(0, 0);
        Assert.Equal(0, corner.A);
    }

    [Fact]
    public void RenderContainer_Circle_RadiusMatchesHalfSize()
    {
        var container = new ContainerSpec(
            shape: ContainerShape.Circle,
            backgroundColor: "#FF0000",
            padding: 0.10);

        var canvas = IconStyleRenderer.RenderContainer(100, container);

        // Center (50, 50) is opaque red
        var center = canvas.GetPixel(50, 50);
        Assert.Equal(255, center.R);
        Assert.Equal(0, center.G);
        Assert.Equal(0, center.B);
        Assert.Equal(255, center.A);

        // Corner (5, 5) outside circle radius (50) is transparent
        var corner = canvas.GetPixel(5, 5);
        Assert.Equal(0, corner.A);
    }

    [Fact]
    public void ApplyTreatment_SaturationMinusOne_ProducesGrayscaleLuminance()
    {
        var canvas = new RgbaCanvas(1, 1);
        // Pure vibrant red (255, 0, 0)
        canvas.SetPixel(0, 0, 255, 0, 0, 255);

        var treatment = new TreatmentSpec(saturation: -1.0, brightness: 0.0, contrast: 0.0);
        var treated = IconStyleRenderer.ApplyTreatment(canvas, treatment);

        var p = treated.GetPixel(0, 0);
        // Rec.709 red weight: 0.2126 * 255 = ~54
        Assert.Equal(p.R, p.G);
        Assert.Equal(p.G, p.B);
        Assert.InRange(p.R, 53, 55);
        Assert.Equal(255, p.A);
    }

    [Fact]
    public void ApplyTreatment_BrightnessAndContrast_PreserveAlpha()
    {
        var canvas = new RgbaCanvas(1, 1);
        canvas.SetPixel(0, 0, 100, 100, 100, 180);

        var treatment = new TreatmentSpec(saturation: 0.0, brightness: 0.2, contrast: 0.3);
        var treated = IconStyleRenderer.ApplyTreatment(canvas, treatment);

        var p = treated.GetPixel(0, 0);
        Assert.Equal(180, p.A);
        Assert.True(p.R > 100);
    }

    [Fact]
    public void Composite_OpaqueForegroundOverBackground_YieldsForeground()
    {
        var bg = new RgbaCanvas(2, 2);
        bg.SetPixel(0, 0, 255, 255, 255, 255);

        var fg = new RgbaCanvas(2, 2);
        fg.SetPixel(0, 0, 255, 0, 0, 255); // Opaque red

        var comp = IconStyleRenderer.Composite(bg, fg);
        var p = comp.GetPixel(0, 0);

        Assert.Equal(255, p.R);
        Assert.Equal(0, p.G);
        Assert.Equal(0, p.B);
        Assert.Equal(255, p.A);
    }

    [Fact]
    public void Composite_TransparentForegroundOverBackground_YieldsBackground()
    {
        var bg = new RgbaCanvas(2, 2);
        bg.SetPixel(0, 0, 10, 20, 30, 255);

        var fg = new RgbaCanvas(2, 2);
        // fg is all zero (alpha 0)

        var comp = IconStyleRenderer.Composite(bg, fg);
        var p = comp.GetPixel(0, 0);

        Assert.Equal(10, p.R);
        Assert.Equal(20, p.G);
        Assert.Equal(30, p.B);
        Assert.Equal(255, p.A);
    }

    [Fact]
    public void Render_EndToEnd_IsBitwiseDeterministic()
    {
        // 64x64 colorful test fixture
        var source = new RgbaCanvas(64, 64);
        for (var y = 16; y < 48; y++)
        {
            for (var x = 16; x < 48; x++)
            {
                source.SetPixel(x, y, (byte)(x * 4), (byte)(y * 4), 180, 255);
            }
        }

        var style = IconStyle.CreateDefault("Test Style");

        var render1 = IconStyleRenderer.Render(source, style, canvasSize: 64);
        var render2 = IconStyleRenderer.Render(source, style, canvasSize: 64);

        Assert.True(render1.Pixels.AsSpan().SequenceEqual(render2.Pixels));
    }

    [Fact]
    public void ComputeCacheKey_DifferentiatesChanges()
    {
        var bytesA = new byte[] { 1, 2, 3, 4, 5 };
        var bytesB = new byte[] { 1, 2, 3, 4, 6 };

        var style1 = IconStyle.CreateDefault("Style 1");
        var style2 = style1 with { Name = "Style 2" };

        var keyA1 = IconStyleRenderer.ComputeCacheKey(bytesA, style1);
        var keyA1Repeat = IconStyleRenderer.ComputeCacheKey(bytesA, style1);
        var keyB1 = IconStyleRenderer.ComputeCacheKey(bytesB, style1);
        var keyA2 = IconStyleRenderer.ComputeCacheKey(bytesA, style2);

        Assert.Equal(keyA1, keyA1Repeat);
        Assert.NotEqual(keyA1, keyB1);
        Assert.NotEqual(keyA1, keyA2);
    }
}
