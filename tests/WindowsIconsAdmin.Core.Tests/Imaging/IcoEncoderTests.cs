using WindowsIconsAdmin.Core.Imaging;
using WindowsIconsAdmin.Core.Tests.Helpers;

namespace WindowsIconsAdmin.Core.Tests.Imaging;

public class IcoEncoderTests
{
    private static readonly int[] ExpectedSizes = [16, 24, 32, 48, 64, 128, 256];

    private static IcoFile Convert(byte[] png) => IcoParser.Parse(IcoEncoder.FromPng(png));

    private static byte[] RedPng(int w = 64, int h = 64) => PngBuilder.SolidRgba(w, h, 255, 0, 0);

    // Sizes
    [Fact]
    public void Sizes_AreExactlyTheSevenStandardSizes()
    {
        Assert.Equal(ExpectedSizes, IcoEncoder.Sizes.ToArray());
    }

    // I2
    [Fact]
    public void I2_HeaderHasReservedZeroTypeOneCountSeven()
    {
        var ico = Convert(RedPng());

        Assert.Equal(0, ico.Reserved);
        Assert.Equal(1, ico.Type);
        Assert.Equal(7, ico.Count);
        Assert.Equal(7, ico.Entries.Count);
    }

    [Fact]
    public void I2_EntriesAreInAscendingSizeOrder()
    {
        var ico = Convert(RedPng());

        var sizes = ico.Entries.Select(e => e.WidthByte == 0 ? 256 : e.WidthByte).ToArray();
        Assert.Equal(ExpectedSizes, sizes);
    }

    // I3
    [Fact]
    public void I3_WidthAndHeightBytesMatchSizeWith256StoredAsZero()
    {
        var ico = Convert(RedPng());

        for (var i = 0; i < ExpectedSizes.Length; i++)
        {
            var expected = ExpectedSizes[i] == 256 ? (byte)0 : (byte)ExpectedSizes[i];
            Assert.Equal(expected, ico.Entries[i].WidthByte);
            Assert.Equal(expected, ico.Entries[i].HeightByte);
        }
    }

    [Fact]
    public void I3_PlanesIsOneAndBitCountIs32()
    {
        var ico = Convert(RedPng());

        Assert.All(ico.Entries, e =>
        {
            Assert.Equal(1, e.Planes);
            Assert.Equal(32, e.BitCount);
        });
    }

    [Fact]
    public void I3_EntriesLieInsideFileWithoutOverlapAndWithIncreasingOffsets()
    {
        var bytes = IcoEncoder.FromPng(RedPng());
        var ico = IcoParser.Parse(bytes);

        long previousEnd = 0;
        uint previousOffset = 0;
        var first = true;
        foreach (var e in ico.Entries)
        {
            Assert.True(e.BytesInRes > 0);
            Assert.True((long)e.ImageOffset + e.BytesInRes <= bytes.Length);
            if (!first)
            {
                Assert.True(e.ImageOffset > previousOffset);
                Assert.True(e.ImageOffset >= previousEnd);
            }
            previousOffset = e.ImageOffset;
            previousEnd = (long)e.ImageOffset + e.BytesInRes;
            first = false;
        }
    }

    [Fact]
    public void I3_DataDoesNotOverlapDirectory()
    {
        var ico = Convert(RedPng());

        Assert.All(ico.Entries, e => Assert.True(e.ImageOffset >= 6 + 7 * 16));
    }

    // I4 / I4b
    [Fact]
    public void I4_SmallEntriesHaveBitmapInfoHeader()
    {
        var ico = Convert(RedPng());

        foreach (var e in ico.Entries.Where(e => e.WidthByte != 0))
        {
            var s = e.WidthByte;
            Assert.Equal(40, e.DibHeaderSize);
            Assert.Equal(s, e.DibWidth);
            Assert.Equal(2 * s, e.DibHeight);
            Assert.Equal(1, e.DibPlanes);
            Assert.Equal(32, e.DibBitCount);
            Assert.Equal(0u, e.DibCompression);
        }
    }

    [Fact]
    public void I4b_BytesInResMatchesFormulaForSmallEntries()
    {
        var ico = Convert(RedPng());

        foreach (var e in ico.Entries.Where(e => e.WidthByte != 0))
        {
            int s = e.WidthByte;
            var maskBytes = ((s + 31) / 32) * 4 * s;
            Assert.Equal((uint)(40 + s * s * 4 + maskBytes), e.BytesInRes);
        }
    }

    [Fact]
    public void I4b_AndMaskBitsAreAllZero()
    {
        var ico = Convert(RedPng());

        foreach (var e in ico.Entries.Where(e => e.WidthByte != 0))
        {
            var mask = e.AndMask;
            Assert.Equal(e.ExpectedMaskBytes, mask.Length);
            Assert.All(mask, b => Assert.Equal(0, b));
        }
    }

    [Fact]
    public void I4_Entry256IsPngWithSignature()
    {
        var ico = Convert(RedPng());

        Assert.True(ico.Entries[6].HasPngSignature);
    }

    [Fact]
    public void I4_SmallEntriesAreNotPng()
    {
        var ico = Convert(RedPng());

        Assert.All(ico.Entries.Where(e => e.WidthByte != 0), e => Assert.False(e.HasPngSignature));
    }

    [Fact]
    public void I6b_Entry256PngIhdrIs256By256()
    {
        var ico = Convert(RedPng());

        Assert.Equal((256u, 256u), ico.Entries[6].PngDimensions);
    }

    [Fact]
    public void I4_Entry256PngIs256By256ForNonSquareSource()
    {
        var ico = Convert(RedPng(200, 100));

        Assert.True(ico.Entries[6].HasPngSignature);
        Assert.Equal((256u, 256u), ico.Entries[6].PngDimensions);
    }

    [Fact]
    public void I4_PixelsAreBottomUp()
    {
        // Top half blue, bottom half green; BGRA bottom-up means stored first row is the visual bottom.
        var png = PngBuilder.Rgba(64, 64, (x, y) => y < 32 ? ((byte)0, (byte)0, (byte)255, (byte)255) : ((byte)0, (byte)255, (byte)0, (byte)255));
        var e = Convert(png).Entries.Single(en => en.WidthByte == 32);

        var topVisual = e.Pixel(16, 4);
        var bottomVisual = e.Pixel(16, 27);
        Assert.Equal((byte)255, topVisual.B);
        Assert.Equal((byte)0, topVisual.G);
        Assert.Equal((byte)255, bottomVisual.G);
        Assert.Equal((byte)0, bottomVisual.B);

        // First stored row (offset 40) is the visual bottom => green.
        Assert.Equal((byte)255, e.Data[40 + 1]);
        Assert.Equal((byte)0, e.Data[40 + 2]);
    }

    // I5
    [Fact]
    public void I5_WideSourceIsLetterboxedVertically()
    {
        var e = Convert(RedPng(200, 100)).Entries.Single(en => en.WidthByte == 128);

        for (var x = 0; x < 128; x++)
        {
            Assert.Equal(0, e.Pixel(x, 0).A);
            Assert.Equal(0, e.Pixel(x, 127).A);
        }

        var hasImagePixel = false;
        for (var x = 0; x < 128; x++)
        {
            if (e.Pixel(x, 64).A > 0)
            {
                hasImagePixel = true;
            }
        }
        Assert.True(hasImagePixel);
        Assert.Equal(255, e.Pixel(64, 64).A);
    }

    [Fact]
    public void I5_TallSourceIsLetterboxedHorizontally()
    {
        var e = Convert(RedPng(100, 200)).Entries.Single(en => en.WidthByte == 128);

        for (var y = 0; y < 128; y++)
        {
            Assert.Equal(0, e.Pixel(0, y).A);
            Assert.Equal(0, e.Pixel(127, y).A);
        }
        Assert.Equal(255, e.Pixel(64, 64).A);
    }

    [Fact]
    public void I5_SquareSourceFillsCanvas()
    {
        var e = Convert(RedPng()).Entries.Single(en => en.WidthByte == 32);

        Assert.Equal(255, e.Pixel(16, 16).A);
        Assert.Equal(255, e.Pixel(16, 1).A);
        Assert.Equal(255, e.Pixel(16, 30).A);
    }

    [Fact]
    public void I5_WideSourceLetterboxApplies_InEverySmallSize()
    {
        var ico = Convert(RedPng(400, 100));

        foreach (var e in ico.Entries.Where(en => en.WidthByte is 32 or 48 or 64 or 128))
        {
            var s = e.DibSize;
            Assert.Equal(0, e.Pixel(s / 2, 0).A);
            Assert.Equal(0, e.Pixel(s / 2, s - 1).A);
            Assert.Equal(255, e.Pixel(s / 2, s / 2).A);
        }
    }

    // I6
    [Fact]
    public void I6_FullyTransparentSourceStaysTransparentInAllSizes()
    {
        var png = PngBuilder.Rgba(40, 40, (_, _) => (10, 20, 30, 0));
        var ico = Convert(png);

        foreach (var e in ico.Entries.Where(en => en.WidthByte != 0))
        {
            for (var y = 0; y < e.DibSize; y++)
            {
                for (var x = 0; x < e.DibSize; x++)
                {
                    Assert.Equal(0, e.Pixel(x, y).A);
                }
            }
        }
    }

    [Fact]
    public void I6_TransparentRegionRemainsTransparent()
    {
        var png = PngBuilder.Rgba(64, 64, (x, _) => x < 32 ? ((byte)0, (byte)0, (byte)0, (byte)0) : ((byte)255, (byte)0, (byte)0, (byte)255));
        var ico = Convert(png);

        foreach (var e in ico.Entries.Where(en => en.WidthByte != 0))
        {
            var s = e.DibSize;
            for (var y = 0; y < s; y++)
            {
                Assert.Equal(0, e.Pixel(0, y).A);
            }
        }
    }

    [Fact]
    public void I6_OpaqueSourceHasOpaqueCenter()
    {
        var ico = Convert(RedPng());

        foreach (var e in ico.Entries.Where(en => en.WidthByte != 0))
        {
            Assert.Equal(255, e.Pixel(e.DibSize / 2, e.DibSize / 2).A);
        }
    }

    [Fact]
    public void I6_RgbSourceWithoutAlphaIsOpaque()
    {
        var png = PngBuilder.Rgb(50, 50, (_, _) => (10, 200, 30));
        var ico = Convert(png);

        foreach (var e in ico.Entries.Where(en => en.WidthByte != 0))
        {
            Assert.Equal(255, e.Pixel(e.DibSize / 2, e.DibSize / 2).A);
        }
        Assert.True(ico.Entries[6].HasPngSignature);
    }

    [Fact]
    public void I6_GrayscaleSourceIsAcceptedAndOpaque()
    {
        var png = PngBuilder.Gray(30, 30, (_, _) => 128);
        var ico = Convert(png);

        Assert.Equal(7, ico.Count);
        foreach (var e in ico.Entries.Where(en => en.WidthByte != 0))
        {
            Assert.Equal(255, e.Pixel(e.DibSize / 2, e.DibSize / 2).A);
        }
    }

    [Fact]
    public void I6_PartialAlphaIsPreservedApproximately()
    {
        var png = PngBuilder.Rgba(64, 64, (_, _) => (255, 0, 0, 128));
        var e = Convert(png).Entries.Single(en => en.WidthByte == 32);

        var a = e.Pixel(16, 16).A;
        Assert.InRange(a, 120, 136);
    }

    // Acceptance
    [Fact]
    public void Acceptance_Opaque64RedPng()
    {
        var ico = Convert(PngBuilder.SolidRgba(64, 64, 255, 0, 0));

        Assert.Equal(7, ico.Entries.Count);
        Assert.True(ico.Entries[6].HasPngSignature);
        var center = ico.Entries.Single(e => e.WidthByte == 32).Pixel(16, 16);
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), center);
    }

    [Fact]
    public void Acceptance_200x100TopAndBottomRowsTransparent()
    {
        var e = Convert(RedPng(200, 100)).Entries.Single(en => en.WidthByte == 128);

        for (var x = 0; x < 128; x++)
        {
            Assert.Equal(0, e.Pixel(x, 0).A);
            Assert.Equal(0, e.Pixel(x, 127).A);
        }
        Assert.Equal(255, e.Pixel(64, 64).A);
    }

    [Fact]
    public void Acceptance_InvalidBytesThrow()
    {
        Assert.Throws<InvalidImageException>(() => IcoEncoder.FromPng([1, 2, 3]));
    }

    // I1 / I7
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(10, 10)]
    [InlineData(300, 300)]
    [InlineData(512, 128)]
    [InlineData(17, 301)]
    public void I1_I7_AnyDimensionsProduceAllSevenEntries(int w, int h)
    {
        var ico = Convert(RedPng(w, h));

        Assert.Equal(7, ico.Count);
        Assert.Equal(ExpectedSizes, ico.Entries.Select(e => e.WidthByte == 0 ? 256 : e.WidthByte).ToArray());
        Assert.Equal((256u, 256u), ico.Entries[6].PngDimensions);
    }

    [Fact]
    public void I7_OnePixelSourceIsUpscaledToOpaqueCenter()
    {
        var ico = Convert(PngBuilder.SolidRgba(1, 1, 255, 0, 0));

        foreach (var e in ico.Entries.Where(en => en.WidthByte != 0))
        {
            Assert.Equal(255, e.Pixel(e.DibSize / 2, e.DibSize / 2).A);
        }
    }

    // I8
    [Fact]
    public void I8_SameInputProducesSameOutput()
    {
        var png = PngBuilder.Rgba(50, 30, (x, y) => ((byte)(x * 5), (byte)(y * 8), (byte)(x + y), (byte)255));

        Assert.Equal(IcoEncoder.FromPng(png), IcoEncoder.FromPng(png));
    }

    // I9
    [Fact]
    public void I9_NullThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => IcoEncoder.FromPng(null!));
    }

    [Fact]
    public void I9_EmptyArrayThrowsInvalidImage()
    {
        Assert.Throws<InvalidImageException>(() => IcoEncoder.FromPng([]));
    }

    [Fact]
    public void I9_TruncatedPngThrowsInvalidImage()
    {
        var png = RedPng();
        var truncated = png.Take(png.Length / 2).ToArray();

        Assert.Throws<InvalidImageException>(() => IcoEncoder.FromPng(truncated));
    }

    [Fact]
    public void I9_OnlySignatureThrowsInvalidImage()
    {
        var png = RedPng();

        Assert.Throws<InvalidImageException>(() => IcoEncoder.FromPng(png.Take(8).ToArray()));
    }

    [Fact]
    public void I9_NonPngDataThrowsInvalidImage()
    {
        var garbage = Enumerable.Range(0, 500).Select(i => (byte)(i * 7)).ToArray();

        Assert.Throws<InvalidImageException>(() => IcoEncoder.FromPng(garbage));
    }

    [Fact]
    public void I9_TextDataThrowsInvalidImage()
    {
        Assert.Throws<InvalidImageException>(() => IcoEncoder.FromPng(System.Text.Encoding.ASCII.GetBytes("this is not a png")));
    }

    // I10
    [Fact]
    public void I10_InputArrayIsNotMutated()
    {
        var png = PngBuilder.Rgba(40, 20, (x, y) => ((byte)x, (byte)y, (byte)7, (byte)200));
        var copy = (byte[])png.Clone();

        IcoEncoder.FromPng(png);

        Assert.Equal(copy, png);
    }

    [Fact]
    public void I10_OutputIsNotTheInputArray()
    {
        var png = RedPng();

        Assert.NotSame(png, IcoEncoder.FromPng(png));
    }
}
