using System.Buffers.Binary;

namespace WindowsIconsAdmin.Core.Tests.Helpers;

public sealed record IcoEntry(
    byte WidthByte,
    byte HeightByte,
    byte ColorCount,
    byte Reserved,
    ushort Planes,
    ushort BitCount,
    uint BytesInRes,
    uint ImageOffset,
    byte[] Data)
{
    public bool HasPngSignature =>
        Data.Length >= 8 && Data.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

    public (uint Width, uint Height) PngDimensions =>
        (BinaryPrimitives.ReadUInt32BigEndian(Data.AsSpan(16, 4)), BinaryPrimitives.ReadUInt32BigEndian(Data.AsSpan(20, 4)));

    public int DibHeaderSize => BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(0, 4));
    public int DibWidth => BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(4, 4));
    public int DibHeight => BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(8, 4));
    public ushort DibPlanes => BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(12, 2));
    public ushort DibBitCount => BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(14, 2));
    public uint DibCompression => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(16, 4));

    /// <summary>Size S of the square image (valid for DIB entries).</summary>
    public int DibSize => DibWidth;

    public int ExpectedMaskBytes => ((DibSize + 31) / 32) * 4 * DibSize;

    /// <summary>Returns the BGRA pixel at (x, y) where y = 0 is the visual top row.</summary>
    public (byte B, byte G, byte R, byte A) Pixel(int x, int y)
    {
        var s = DibSize;
        var storedRow = s - 1 - y;
        var offset = 40 + (storedRow * s + x) * 4;
        return (Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]);
    }

    public byte[] AndMask
    {
        get
        {
            var start = 40 + DibSize * DibSize * 4;
            return Data.AsSpan(start).ToArray();
        }
    }
}

public sealed record IcoFile(ushort Reserved, ushort Type, ushort Count, IReadOnlyList<IcoEntry> Entries, int FileLength);

public static class IcoParser
{
    public static IcoFile Parse(byte[] bytes)
    {
        var reserved = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2));
        var type = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        var entries = new List<IcoEntry>();
        for (var i = 0; i < count; i++)
        {
            var o = 6 + i * 16;
            var size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o + 8, 4));
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o + 12, 4));
            var data = bytes.AsSpan((int)offset, (int)size).ToArray();
            entries.Add(new IcoEntry(
                bytes[o],
                bytes[o + 1],
                bytes[o + 2],
                bytes[o + 3],
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(o + 4, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(o + 6, 2)),
                size,
                offset,
                data));
        }
        return new IcoFile(reserved, type, count, entries, bytes.Length);
    }
}
