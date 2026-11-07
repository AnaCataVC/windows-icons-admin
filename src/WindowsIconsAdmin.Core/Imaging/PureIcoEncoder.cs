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
            entries.Add(EncodeDib(resampled, size));
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

                var r = Interpolate(p00.R, p10.R, p01.R, p11.R, fx, fy);
                var g = Interpolate(p00.G, p10.G, p01.G, p11.G, fx, fy);
                var b = Interpolate(p00.B, p10.B, p01.B, p11.B, fx, fy);
                var a = Interpolate(p00.A, p10.A, p01.A, p11.A, fx, fy);

                result.SetPixel(x, y, r, g, b, a);
            }
        }
        return result;
    }

    private static byte Interpolate(byte v00, byte v10, byte v01, byte v11, double fx, double fy)
    {
        var top = v00 * (1.0 - fx) + v10 * fx;
        var bottom = v01 * (1.0 - fx) + v11 * fx;
        var result = top * (1.0 - fy) + bottom * fy;
        return (byte)Math.Clamp((int)Math.Round(result), 0, 255);
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

        var maskBytes = (size + 31) / 32 * 4 * size;
        var result = new byte[40 + pixels.Length + maskBytes];

        // BITMAPINFOHEADER
        BitConverter.TryWriteBytes(result.AsSpan(0, 4), 40);
        BitConverter.TryWriteBytes(result.AsSpan(4, 4), size);
        BitConverter.TryWriteBytes(result.AsSpan(8, 4), size * 2); // biHeight is double for icon DIB
        BitConverter.TryWriteBytes(result.AsSpan(12, 2), (short)1);
        BitConverter.TryWriteBytes(result.AsSpan(14, 2), (short)32);
        BitConverter.TryWriteBytes(result.AsSpan(20, 4), pixels.Length + maskBytes);

        pixels.CopyTo(result, 40);
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
