namespace WindowsIconsAdmin.Core.Styling;

public static class IconNormalizer
{
    public const byte AlphaThreshold = 16;

    public static (int MinX, int MinY, int MaxX, int MaxY) GetEffectiveBounds(RgbaCanvas source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var minX = source.Width;
        var minY = source.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < source.Height; y++)
        {
            var rowOffset = y * source.Width * 4;
            for (var x = 0; x < source.Width; x++)
            {
                var alpha = source.Pixels[rowOffset + x * 4 + 3];
                if (alpha >= AlphaThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < 0 || maxY < 0)
        {
            return (0, 0, source.Width - 1, source.Height - 1);
        }

        return (minX, minY, maxX, maxY);
    }

    public static RgbaCanvas Normalize(RgbaCanvas source, int targetSize = 256, double padding = 0.16)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (targetSize <= 0) throw new ArgumentOutOfRangeException(nameof(targetSize), "Target size must be positive.");
        if (padding < 0.0 || padding > 0.40) throw new ArgumentOutOfRangeException(nameof(padding), "Padding must be between 0.0 and 0.40.");

        var (minX, minY, maxX, maxY) = GetEffectiveBounds(source);
        var cropWidth = maxX - minX + 1;
        var cropHeight = maxY - minY + 1;

        var innerSize = targetSize * (1.0 - 2.0 * padding);
        var scale = Math.Min(innerSize / cropWidth, innerSize / cropHeight);

        var dstWidth = Math.Clamp((int)Math.Round(cropWidth * scale), 1, targetSize);
        var dstHeight = Math.Clamp((int)Math.Round(cropHeight * scale), 1, targetSize);

        var dstX = (targetSize - dstWidth) / 2;
        var dstY = (targetSize - dstHeight) / 2;

        var target = new RgbaCanvas(targetSize, targetSize);

        // Bilinear resampling from crop rect into (dstX, dstY, dstWidth, dstHeight)
        for (var dy = 0; dy < dstHeight; dy++)
        {
            var srcY = cropHeight == 1
                ? minY
                : minY + (dy / (double)(dstHeight > 1 ? dstHeight - 1 : 1)) * (cropHeight - 1);

            var y0 = (int)Math.Floor(srcY);
            var y1 = Math.Min(y0 + 1, maxY);
            var fy = srcY - y0;

            for (var dx = 0; dx < dstWidth; dx++)
            {
                var srcX = cropWidth == 1
                    ? minX
                    : minX + (dx / (double)(dstWidth > 1 ? dstWidth - 1 : 1)) * (cropWidth - 1);

                var x0 = (int)Math.Floor(srcX);
                var x1 = Math.Min(x0 + 1, maxX);
                var fx = srcX - x0;

                // Sample 4 neighboring pixels
                var p00 = source.GetPixel(x0, y0);
                var p10 = source.GetPixel(x1, y0);
                var p01 = source.GetPixel(x0, y1);
                var p11 = source.GetPixel(x1, y1);

                // Alpha-weighted bilinear interpolation prevents dark halos around transparent borders
                var a00 = p00.A / 255.0;
                var a10 = p10.A / 255.0;
                var a01 = p01.A / 255.0;
                var a11 = p11.A / 255.0;

                var outA = Interpolate(a00, a10, a01, a11, fx, fy);
                if (outA <= 0.001)
                {
                    target.SetPixel(dstX + dx, dstY + dy, 0, 0, 0, 0);
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

                    target.SetPixel(
                        dstX + dx,
                        dstY + dy,
                        (byte)Math.Clamp((int)Math.Round(outR), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(outG), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(outB), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(outA * 255.0), 0, 255));
                }
            }
        }

        return target;
    }

    private static double Interpolate(double v00, double v10, double v01, double v11, double fx, double fy)
    {
        var top = v00 * (1.0 - fx) + v10 * fx;
        var bottom = v01 * (1.0 - fx) + v11 * fx;
        return top * (1.0 - fy) + bottom * fy;
    }
}
