using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO.Compression;
using WindowsIconsAdmin.Core.Styling;

namespace WindowsIconsAdmin.Core.Imaging;

public static class PureIcoEncoder
{
    private static readonly int[] SizeValues = [16, 24, 32, 48, 64, 128, 256];

    public static byte[] FromCanvas(RgbaCanvas masterCanvas)
    {
        ArgumentNullException.ThrowIfNull(masterCanvas);

        var entries = new List<byte[]>(SizeValues.Length);
        foreach (var size in SizeValues)
        {
            var resampled = Resample(masterCanvas, size);
            entries.Add(size == 256 ? EncodePng(resampled) : EncodeDib(resampled, size));
        }

        return Assemble(entries);
    }

    private static RgbaCanvas Resample(RgbaCanvas source, int size)
    {
        if (source.Width == size && source.Height == size)
        {
            return source;
        }

        var result = new RgbaCanvas(size, size);
        for (var y = 0; y < size; y++)
        {
            var srcY = (y / (double)(size > 1 ? size - 1 : 1)) * (source.Height - 1);
            var y0 = (int)Math.Floor(srcY);
            var y1 = Math.Min(y0 + 1, source.Height - 1);
            var fy = srcY - y0;

            for (var x = 0; x < size; x++)
            {
                var srcX = (x / (double)(size > 1 ? size - 1 : 1)) * (source.Width - 1);
                var x0 = (int)Math.Floor(srcX);
                var x1 = Math.Min(x0 + 1, source.Width - 1);
                var fx = srcX - x0;

                var p00 = source.GetPixel(x0, y0);
                var p10 = source.GetPixel(x1, y0);
                var p01 = source.GetPixel(x0, y1);
                var p11 = source.GetPixel(x1, y1);

                var a00 = p00.A / 255.0;
                var a10 = p10.A / 255.0;
                var a01 = p01.A / 255.0;
                var a11 = p11.A / 255.0;

                var outA = Interpolate(a00, a10, a01, a11, fx, fy);
                if (outA <= 0.001)
                {
                    result.SetPixel(x, y, 0, 0, 0, 0);
                }
                else
                {
                    var pr00 = p00.R * a00;
                    var pr10 = p10.R * a10;
                    var pr01 = p01.R * a01;
                    var pr11 = p11.R * a11;

                    var pg00 = p00.G * a00;
                    var pg10 = p10.G * a10;
                    var pg01 = p01.G * a01;
                    var pg11 = p11.G * a11;

                    var pb00 = p00.B * a00;
                    var pb10 = p10.B * a10;
                    var pb01 = p01.B * a01;
                    var pb11 = p11.B * a11;

                    var outR = Interpolate(pr00, pr10, pr01, pr11, fx, fy) / outA;
                    var outG = Interpolate(pg00, pg10, pg01, pg11, fx, fy) / outA;
                    var outB = Interpolate(pb00, pb10, pb01, pb11, fx, fy) / outA;

                    result.SetPixel(
                        x,
                        y,
                        (byte)Math.Clamp((int)Math.Round(outR), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(outG), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(outB), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(outA * 255.0), 0, 255));
                }
            }
        }
        return result;
    }

    private static double Interpolate(double v00, double v10, double v01, double v11, double fx, double fy)
    {
        var top = v00 * (1.0 - fx) + v10 * fx;
        var bottom = v01 * (1.0 - fx) + v11 * fx;
        return top * (1.0 - fy) + bottom * fy;
    }

    private static byte[] EncodePng(RgbaCanvas canvas)
    {
        using var bmp = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                var p = canvas.GetPixel(x, y);
                bmp.SetPixel(x, y, Color.FromArgb(p.A, p.R, p.G, p.B));
            }
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static byte[] EncodeDib(RgbaCanvas canvas, int size)
    {
        var pixels = new byte[size * size * 4];

        // Bottom-up row order, convert RGBA to BGRA
        for (var row = 0; row < size; row++)
        {
            var destRow = size - 1 - row;
            for (var col = 0; col < size; col++)
            {
                var p = canvas.GetPixel(col, row);
                var offset = (destRow * size + col) * 4;
                pixels[offset] = p.B;
                pixels[offset + 1] = p.G;
                pixels[offset + 2] = p.R;
                pixels[offset + 3] = p.A;
            }
        }

        var maskRowBytes = (size + 31) / 32 * 4;
        var maskBytes = maskRowBytes * size;
        var mask = new byte[maskBytes];

        // Fill 1-bit AND mask: bit 1 where pixel is transparent (A < 128)
        for (var row = 0; row < size; row++)
        {
            var destRow = size - 1 - row;
            for (var col = 0; col < size; col++)
            {
                var p = canvas.GetPixel(col, row);
                if (p.A < 128)
                {
                    var byteIndex = destRow * maskRowBytes + (col / 8);
                    var bitIndex = 7 - (col % 8);
                    mask[byteIndex] |= (byte)(1 << bitIndex);
                }
            }
        }

        var result = new byte[40 + pixels.Length + maskBytes];

        // BITMAPINFOHEADER
        BitConverter.TryWriteBytes(result.AsSpan(0, 4), 40);
        BitConverter.TryWriteBytes(result.AsSpan(4, 4), size);
        BitConverter.TryWriteBytes(result.AsSpan(8, 4), size * 2); // biHeight is double for icon DIB
        BitConverter.TryWriteBytes(result.AsSpan(12, 2), (short)1);
        BitConverter.TryWriteBytes(result.AsSpan(14, 2), (short)32);
        BitConverter.TryWriteBytes(result.AsSpan(20, 4), pixels.Length + maskBytes);

        pixels.CopyTo(result, 40);
        mask.CopyTo(result, 40 + pixels.Length);
        return result;
    }

    private static byte[] Assemble(List<byte[]> entries)
    {
        var headerSize = 6 + 16 * entries.Count;
        var total = headerSize + entries.Sum(e => e.Length);
        var output = new byte[total];

        // ICONDIR
        BitConverter.TryWriteBytes(output.AsSpan(2, 2), (short)1); // Type = 1 (Icon)
        BitConverter.TryWriteBytes(output.AsSpan(4, 2), (short)entries.Count);

        var offset = headerSize;
        for (var i = 0; i < entries.Count; i++)
        {
            var size = SizeValues[i];
            var dir = output.AsSpan(6 + 16 * i, 16);
            dir[0] = (byte)(size == 256 ? 0 : size);
            dir[1] = (byte)(size == 256 ? 0 : size);
            BitConverter.TryWriteBytes(dir.Slice(4, 2), (short)1);     // Color planes
            BitConverter.TryWriteBytes(dir.Slice(6, 2), (short)32);    // Bits per pixel
            BitConverter.TryWriteBytes(dir.Slice(8, 4), entries[i].Length);
            BitConverter.TryWriteBytes(dir.Slice(12, 4), offset);

            entries[i].CopyTo(output, offset);
            offset += entries[i].Length;
        }

        return output;
    }
}
