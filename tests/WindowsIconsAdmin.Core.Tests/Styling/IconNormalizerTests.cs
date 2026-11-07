using WindowsIconsAdmin.Core.Styling;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Styling;

public class IconNormalizerTests
{
    [Fact]
    public void GetEffectiveBounds_FullyTransparentCanvas_ReturnsFullBoundsFallback()
    {
        var canvas = new RgbaCanvas(64, 64);
        var (minX, minY, maxX, maxY) = IconNormalizer.GetEffectiveBounds(canvas);

        Assert.Equal(0, minX);
        Assert.Equal(0, minY);
        Assert.Equal(63, maxX);
        Assert.Equal(63, maxY);
    }

    [Fact]
    public void GetEffectiveBounds_IgnoresAlphaBelowThreshold()
    {
        var canvas = new RgbaCanvas(100, 100);
        // Set faint ghost pixel (alpha 10 < 16) at (5, 5)
        canvas.SetPixel(5, 5, 255, 0, 0, 10);
        // Set visible pixel (alpha 255 >= 16) at (20, 30) to (50, 60)
        canvas.SetPixel(20, 30, 255, 0, 0, 255);
        canvas.SetPixel(50, 60, 255, 0, 0, 255);

        var (minX, minY, maxX, maxY) = IconNormalizer.GetEffectiveBounds(canvas);

        Assert.Equal(20, minX);
        Assert.Equal(30, minY);
        Assert.Equal(50, maxX);
        Assert.Equal(60, maxY);
    }

    [Fact]
    public void Normalize_SquareIcon_ScalesAndCentersInsidePadding()
    {
        // 50x50 solid red square inside a 200x200 canvas
        var canvas = new RgbaCanvas(200, 200);
        for (var y = 75; y < 125; y++)
        {
            for (var x = 75; x < 125; x++)
            {
                canvas.SetPixel(x, y, 255, 0, 0, 255);
            }
        }

        // Target size 100 with padding 0.10 => innerSize = 80
        var normalized = IconNormalizer.Normalize(canvas, targetSize: 100, padding: 0.10);

        Assert.Equal(100, normalized.Width);
        Assert.Equal(100, normalized.Height);

        // Center pixel (50, 50) must be red and opaque
        var center = normalized.GetPixel(50, 50);
        Assert.Equal(255, center.R);
        Assert.Equal(0, center.G);
        Assert.Equal(0, center.B);
        Assert.Equal(255, center.A);

        // Border pixel (5, 5) must be fully transparent
        var corner = normalized.GetPixel(5, 5);
        Assert.Equal(0, corner.A);
    }

    [Fact]
    public void Normalize_VerticalIcon_PreservesAspectRatio()
    {
        // 20x80 narrow vertical icon
        var canvas = new RgbaCanvas(100, 100);
        for (var y = 10; y < 90; y++)
        {
            for (var x = 40; x < 60; x++)
            {
                canvas.SetPixel(x, y, 0, 255, 0, 255);
            }
        }

        var normalized = IconNormalizer.Normalize(canvas, targetSize: 100, padding: 0.10);

        // Center is green
        var center = normalized.GetPixel(50, 50);
        Assert.Equal(0, center.R);
        Assert.Equal(255, center.G);
        Assert.Equal(0, center.B);
        Assert.Equal(255, center.A);

        // Horizontal edges at (15, 50) must be transparent because aspect ratio is preserved
        var leftEdge = normalized.GetPixel(15, 50);
        Assert.Equal(0, leftEdge.A);
    }

    [Fact]
    public void Normalize_IsDeterministic()
    {
        var canvas = new RgbaCanvas(64, 64);
        canvas.SetPixel(20, 20, 100, 150, 200, 255);
        canvas.SetPixel(40, 40, 200, 150, 100, 255);

        var norm1 = IconNormalizer.Normalize(canvas, 128, 0.16);
        var norm2 = IconNormalizer.Normalize(canvas, 128, 0.16);

        Assert.True(norm1.Pixels.AsSpan().SequenceEqual(norm2.Pixels));
    }
}
