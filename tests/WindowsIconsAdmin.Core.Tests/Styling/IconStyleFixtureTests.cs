using WindowsIconsAdmin.Core.Styling;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Styling;

public class IconStyleFixtureTests
{
    private readonly IconStyle _style = IconStyle.CreateDefault("Fixture Style");

    [Fact]
    public void Fixture_CircularIcon_RendersAndPreservesShape()
    {
        // 64x64 inscribed circle fixture
        var source = new RgbaCanvas(64, 64);
        var cx = 31.5;
        var cy = 31.5;
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d2 <= 25 * 25)
                {
                    source.SetPixel(x, y, 0, 120, 215, 255);
                }
            }
        }

        var result = IconStyleRenderer.Render(source, _style, 128);

        Assert.Equal(128, result.Width);
        Assert.Equal(128, result.Height);

        // Center must have the styled foreground
        var center = result.GetPixel(64, 64);
        Assert.Equal(255, center.A);
        // Container background is present at (30, 30) outside inner normalized circle
        var bgZone = result.GetPixel(30, 30);
        Assert.Equal(255, bgZone.A);
    }

    [Fact]
    public void Fixture_SquareIcon_NormalizesProperly()
    {
        // 64x64 solid square centered
        var source = new RgbaCanvas(64, 64);
        for (var y = 16; y < 48; y++)
        {
            for (var x = 16; x < 48; x++)
            {
                source.SetPixel(x, y, 220, 80, 50, 255);
            }
        }

        var result = IconStyleRenderer.Render(source, _style, 128);
        var center = result.GetPixel(64, 64);

        Assert.Equal(255, center.A);
    }

    [Fact]
    public void Fixture_VerticalIcon_CentersWithoutDistortion()
    {
        // 10x50 vertical bar in 64x64 canvas
        var source = new RgbaCanvas(64, 64);
        for (var y = 7; y < 57; y++)
        {
            for (var x = 27; x < 37; x++)
            {
                source.SetPixel(x, y, 50, 200, 100, 255);
            }
        }

        var result = IconStyleRenderer.Render(source, _style, 128);

        // Center of the vertical bar
        var center = result.GetPixel(64, 64);
        Assert.Equal(255, center.A);

        // Horizontal flank outside the bar should be the container background (#E6DBFA: 230, 219, 250)
        var flank = result.GetPixel(40, 64);
        Assert.Equal(230, flank.R);
        Assert.Equal(219, flank.G);
        Assert.Equal(250, flank.B);
        Assert.Equal(255, flank.A);
    }

    [Fact]
    public void Fixture_IconWithLargeTransparentMargins_CropsPaddingAndNormalizes()
    {
        // Tiny 8x8 content inside a 128x128 canvas (vast transparent margins)
        var source = new RgbaCanvas(128, 128);
        for (var y = 60; y < 68; y++)
        {
            for (var x = 60; x < 68; x++)
            {
                source.SetPixel(x, y, 255, 200, 0, 255);
            }
        }

        var result = IconStyleRenderer.Render(source, _style, 128);

        // The tiny 8x8 content should now be scaled up to fill the normalized inner area (128 * (1 - 2*0.16) = ~87px)
        // Check pixel at (50, 64) - inside normalized area, it should contain scaled content
        var scaledPixel = result.GetPixel(50, 64);
        Assert.True(scaledPixel.R > 200);
    }

    [Fact]
    public void Fixture_HighlyColorfulIcon_PreservesVibrancyUnderGentleTreatment()
    {
        var source = new RgbaCanvas(64, 64);
        // 4 color quadrants
        for (var y = 16; y < 48; y++)
        {
            for (var x = 16; x < 48; x++)
            {
                if (x < 32 && y < 32) source.SetPixel(x, y, 255, 0, 0, 255);       // Red
                else if (x >= 32 && y < 32) source.SetPixel(x, y, 0, 255, 0, 255);  // Green
                else if (x < 32 && y >= 32) source.SetPixel(x, y, 0, 0, 255, 255);  // Blue
                else source.SetPixel(x, y, 255, 255, 0, 255);                       // Yellow
            }
        }

        var result = IconStyleRenderer.Render(source, _style, 128);

        // Check that red quadrant is still primarily red
        var redSample = result.GetPixel(54, 54);
        Assert.True(redSample.R > redSample.G && redSample.R > redSample.B);
    }

    [Fact]
    public void Fixture_DarkIcon_RemainsDistinctFromBackground()
    {
        // Dark theme icon (#1E1E1E)
        var source = new RgbaCanvas(64, 64);
        for (var y = 16; y < 48; y++)
        {
            for (var x = 16; x < 48; x++)
            {
                source.SetPixel(x, y, 30, 30, 30, 255);
            }
        }

        var result = IconStyleRenderer.Render(source, _style, 128);

        var darkPixel = result.GetPixel(64, 64);
        // It should be darker than the background (#E6DBFA ~ 230)
        Assert.True(darkPixel.R < 100);
        Assert.True(darkPixel.G < 100);
        Assert.True(darkPixel.B < 100);
    }

    [Fact]
    public void Fixture_LightIcon_BlendsHarmoniously()
    {
        // Near-white icon (#F0F0F0)
        var source = new RgbaCanvas(64, 64);
        for (var y = 16; y < 48; y++)
        {
            for (var x = 16; x < 48; x++)
            {
                source.SetPixel(x, y, 240, 240, 240, 255);
            }
        }

        var result = IconStyleRenderer.Render(source, _style, 128);

        var lightPixel = result.GetPixel(64, 64);
        Assert.True(lightPixel.R > 200);
        Assert.True(lightPixel.G > 200);
        Assert.True(lightPixel.B > 200);
    }
}
