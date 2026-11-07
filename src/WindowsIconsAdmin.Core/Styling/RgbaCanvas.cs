namespace WindowsIconsAdmin.Core.Styling;

public sealed class RgbaCanvas
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public RgbaCanvas(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    public RgbaCanvas(int width, int height, byte[] pixels)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != width * height * 4)
        {
            throw new ArgumentException($"Pixel buffer length ({pixels.Length}) does not match expected size ({width * height * 4}).", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public (byte R, byte G, byte B, byte A) GetPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException($"Coordinates ({x}, {y}) out of range [0..{Width}-1, 0..{Height}-1]");
        }

        var offset = (y * Width + x) * 4;
        return (Pixels[offset], Pixels[offset + 1], Pixels[offset + 2], Pixels[offset + 3]);
    }

    public void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        var offset = (y * Width + x) * 4;
        Pixels[offset] = r;
        Pixels[offset + 1] = g;
        Pixels[offset + 2] = b;
        Pixels[offset + 3] = a;
    }

    public void Clear()
    {
        Array.Clear(Pixels, 0, Pixels.Length);
    }

    public RgbaCanvas Clone()
    {
        var copy = new byte[Pixels.Length];
        Buffer.BlockCopy(Pixels, 0, copy, 0, Pixels.Length);
        return new RgbaCanvas(Width, Height, copy);
    }
}
