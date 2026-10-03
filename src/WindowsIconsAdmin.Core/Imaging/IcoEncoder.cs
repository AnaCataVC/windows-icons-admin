using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WindowsIconsAdmin.Core.Imaging;

public static class IcoEncoder
{
    private static readonly int[] SizeValues = [16, 24, 32, 48, 64, 128, 256];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static IReadOnlyList<int> Sizes { get; } = Array.AsReadOnly(SizeValues);

    public static byte[] FromPng(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        if (pngBytes.Length < PngSignature.Length || !pngBytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
        {
            throw new InvalidImageException("The data is not a PNG image.");
        }

        ValidatePngChunks(pngBytes);

        using var source = Decode(pngBytes);
        var entries = new List<byte[]>(SizeValues.Length);
        foreach (var size in SizeValues)
        {
            using var canvas = Render(source, size);
            entries.Add(size == 256 ? EncodePng(canvas) : EncodeDib(canvas, size));
        }

        return Assemble(entries);
    }

    private static Bitmap Decode(byte[] pngBytes)
    {
        try
        {
            using var stream = new MemoryStream(pngBytes, writable: false);
            using var decoded = new Bitmap(stream);
            if (decoded.Width < 1 || decoded.Height < 1)
            {
                throw new InvalidImageException("The image has no pixels.");
            }

            var argb = new Bitmap(decoded.Width, decoded.Height, PixelFormat.Format32bppArgb);
            argb.SetResolution(96, 96);
            using var g = Graphics.FromImage(argb);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(decoded, new Rectangle(0, 0, argb.Width, argb.Height), 0, 0, decoded.Width, decoded.Height, GraphicsUnit.Pixel);
            return argb;
        }
        catch (InvalidImageException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException or InvalidOperationException or IOException)
        {
            throw new InvalidImageException("The PNG data could not be decoded.", ex);
        }
    }

    private static Bitmap Render(Bitmap source, int size)
    {
        var canvas = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        canvas.SetResolution(96, 96);
        var scale = Math.Min((double)size / source.Width, (double)size / source.Height);
        var width = Math.Clamp((int)Math.Round(source.Width * scale), 1, size);
        var height = Math.Clamp((int)Math.Round(source.Height * scale), 1, size);
        var x = (size - width) / 2;
        var y = (size - height) / 2;

        using var g = Graphics.FromImage(canvas);
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.HighQuality;
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(source, new Rectangle(x, y, width, height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        return canvas;
    }

    private static byte[] EncodePng(Bitmap canvas)
    {
        using var stream = new MemoryStream();
        canvas.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static byte[] EncodeDib(Bitmap canvas, int size)
    {
        var pixels = new byte[size * size * 4];
        var data = canvas.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var row = 0; row < size; row++)
            {
                var destRow = size - 1 - row;
                Marshal.Copy(data.Scan0 + row * data.Stride, pixels, destRow * size * 4, size * 4);
            }
        }
        finally
        {
            canvas.UnlockBits(data);
        }

        var maskBytes = (size + 31) / 32 * 4 * size;
        var result = new byte[40 + pixels.Length + maskBytes];
        BitConverter.TryWriteBytes(result.AsSpan(0, 4), 40);
        BitConverter.TryWriteBytes(result.AsSpan(4, 4), size);
        BitConverter.TryWriteBytes(result.AsSpan(8, 4), size * 2);
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
        BitConverter.TryWriteBytes(output.AsSpan(2, 2), (short)1);
        BitConverter.TryWriteBytes(output.AsSpan(4, 2), (short)entries.Count);

        var offset = headerSize;
        for (var i = 0; i < entries.Count; i++)
        {
            var size = SizeValues[i];
            var dir = output.AsSpan(6 + 16 * i, 16);
            dir[0] = (byte)(size == 256 ? 0 : size);
            dir[1] = (byte)(size == 256 ? 0 : size);
            BitConverter.TryWriteBytes(dir.Slice(4, 2), (short)1);
            BitConverter.TryWriteBytes(dir.Slice(6, 2), (short)32);
            BitConverter.TryWriteBytes(dir.Slice(8, 4), entries[i].Length);
            BitConverter.TryWriteBytes(dir.Slice(12, 4), offset);
            entries[i].CopyTo(output, offset);
            offset += entries[i].Length;
        }

        return output;
    }

    private static void ValidatePngChunks(byte[] pngBytes)
    {
        var offset = 8;
        var foundIhdr = false;
        var foundIend = false;

        while (offset + 8 <= pngBytes.Length)
        {
            var length = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(offset, 4));
            if (length < 0)
            {
                throw new InvalidImageException("Invalid PNG chunk length.");
            }

            var type = System.Text.Encoding.ASCII.GetString(pngBytes, offset + 4, 4);
            if (type == "IHDR")
            {
                foundIhdr = true;
            }

            offset += 8 + length + 4;
            if (offset > pngBytes.Length)
            {
                throw new InvalidImageException("Truncated PNG chunk data.");
            }

            if (type == "IEND")
            {
                foundIend = true;
                break;
            }
        }

        if (!foundIhdr || !foundIend || offset != pngBytes.Length)
        {
            throw new InvalidImageException("Truncated or incomplete PNG file.");
        }
    }
}
