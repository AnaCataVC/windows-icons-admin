using System.IO.Compression;

namespace WindowsIconsAdmin.Core.Tests.Helpers;

/// <summary>Minimal dependency-free PNG encoder (8-bit, non-interlaced) for test inputs.</summary>
public static class PngBuilder
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Rgba(int width, int height, Func<int, int, (byte R, byte G, byte B, byte A)> pixel)
    {
        var raw = new byte[height * (1 + width * 4)];
        var pos = 0;
        for (var y = 0; y < height; y++)
        {
            raw[pos++] = 0;
            for (var x = 0; x < width; x++)
            {
                var (r, g, b, a) = pixel(x, y);
                raw[pos++] = r;
                raw[pos++] = g;
                raw[pos++] = b;
                raw[pos++] = a;
            }
        }
        return Build(width, height, 8, 6, raw);
    }

    public static byte[] Rgb(int width, int height, Func<int, int, (byte R, byte G, byte B)> pixel)
    {
        var raw = new byte[height * (1 + width * 3)];
        var pos = 0;
        for (var y = 0; y < height; y++)
        {
            raw[pos++] = 0;
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = pixel(x, y);
                raw[pos++] = r;
                raw[pos++] = g;
                raw[pos++] = b;
            }
        }
        return Build(width, height, 8, 2, raw);
    }

    public static byte[] Gray(int width, int height, Func<int, int, byte> pixel)
    {
        var raw = new byte[height * (1 + width)];
        var pos = 0;
        for (var y = 0; y < height; y++)
        {
            raw[pos++] = 0;
            for (var x = 0; x < width; x++)
            {
                raw[pos++] = pixel(x, y);
            }
        }
        return Build(width, height, 8, 0, raw);
    }

    public static byte[] SolidRgba(int width, int height, byte r, byte g, byte b, byte a = 255)
        => Rgba(width, height, (_, _) => (r, g, b, a));

    private static byte[] Build(int width, int height, byte bitDepth, byte colorType, byte[] rawScanlines)
    {
        using var output = new MemoryStream();
        output.Write(Signature);

        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, (uint)width);
        WriteBigEndian(ihdr, 4, (uint)height);
        ihdr[8] = bitDepth;
        ihdr[9] = colorType;
        WriteChunk(output, "IHDR", ihdr);

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(rawScanlines);
        }
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBigEndian(len, 0, (uint)data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        var crc = new byte[4];
        WriteBigEndian(crc, 0, Crc32(crcInput));
        s.Write(crc);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return table;
    }
}
