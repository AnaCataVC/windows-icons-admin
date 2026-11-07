using WindowsIconsAdmin.Core.Imaging;
using WindowsIconsAdmin.Core.Styling;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Imaging;

public class PureIcoEncoderTests
{
    [Fact]
    public void FromCanvas_GeneratesValid7LayerIcoBinary()
    {
        var canvas = new RgbaCanvas(64, 64);
        canvas.SetPixel(32, 32, 255, 128, 0, 255);

        var icoBytes = PureIcoEncoder.FromCanvas(canvas);

        Assert.NotNull(icoBytes);
        Assert.True(icoBytes.Length > 100);

        // Reserved = 0, Type = 1, Count = 7
        Assert.Equal(0, BitConverter.ToInt16(icoBytes, 0));
        Assert.Equal(1, BitConverter.ToInt16(icoBytes, 2));
        Assert.Equal(7, BitConverter.ToInt16(icoBytes, 4));

        // Check first entry size is 16
        Assert.Equal(16, icoBytes[6]); // width
        Assert.Equal(16, icoBytes[7]); // height

        // Check 7th entry size is 256 (stored as 0)
        var offsetEntry7 = 6 + 16 * 6;
        Assert.Equal(0, icoBytes[offsetEntry7]);
        Assert.Equal(0, icoBytes[offsetEntry7 + 1]);
    }
}
