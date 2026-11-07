using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WindowsIconsAdmin.Core.Styling;

public static class IconStyleRenderer
{
    public const int DefaultCanvasSize = 256;
    public const int RendererVersion = 1;

    public static RgbaCanvas ApplyTreatment(RgbaCanvas source, TreatmentSpec treatment)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(treatment);

        var result = source.Clone();

        var sat = treatment.Saturation;
        var bri = treatment.Brightness;
        var con = treatment.Contrast;
        var factor = (1.0 + con) / (1.0 - con * 0.99);

        var (hasTint, tintR, tintG, tintB) = ParseColorRgb(treatment.TintColor);

        for (var i = 0; i < result.Pixels.Length; i += 4)
        {
            var alpha = result.Pixels[i + 3];
            if (alpha == 0) continue;

            double r = result.Pixels[i];
            double g = result.Pixels[i + 1];
            double b = result.Pixels[i + 2];

            // 1. Saturation (Rec.709 luminance)
            if (sat != 0.0)
            {
                var y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                r = y + (r - y) * (1.0 + sat);
                g = y + (g - y) * (1.0 + sat);
                b = y + (b - y) * (1.0 + sat);
            }

            // 2. Brightness
            if (bri != 0.0)
            {
                var offset = bri * 255.0;
                r += offset;
                g += offset;
                b += offset;
            }

            // 3. Contrast
            if (con != 0.0)
            {
                r = ((r / 255.0 - 0.5) * factor + 0.5) * 255.0;
                g = ((g / 255.0 - 0.5) * factor + 0.5) * 255.0;
                b = ((b / 255.0 - 0.5) * factor + 0.5) * 255.0;
            }

            // 4. Optional tint (gentle 15% blend)
            if (hasTint)
            {
                r = r * 0.85 + tintR * 0.15;
                g = g * 0.85 + tintG * 0.15;
                b = b * 0.85 + tintB * 0.15;
            }

            result.Pixels[i] = (byte)Math.Clamp((int)Math.Round(r), 0, 255);
            result.Pixels[i + 1] = (byte)Math.Clamp((int)Math.Round(g), 0, 255);
            result.Pixels[i + 2] = (byte)Math.Clamp((int)Math.Round(b), 0, 255);
        }

        return result;
    }

    public static RgbaCanvas RenderContainer(int size, ContainerSpec container)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive.");

        var (bgR, bgG, bgB, bgA) = ParseColorRgba(container.BackgroundColor);
        var canvas = new RgbaCanvas(size, size);

        var cx = (size - 1) / 2.0;
        var cy = (size - 1) / 2.0;
        var r = size / 2.0;
        var cornerRadius = size * 0.225;

        // Subpixel sampling 4x4 grid (16 samples per pixel) for deterministic anti-aliasing
        const int samples = 4;
        const double step = 1.0 / samples;
        const double sampleWeight = 1.0 / (samples * samples);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var insideCount = 0;

                for (var sy = 0; sy < samples; sy++)
                {
                    var py = y + (sy + 0.5) * step;

                    for (var sx = 0; sx < samples; sx++)
                    {
                        var px = x + (sx + 0.5) * step;

                        var inside = container.Shape switch
                        {
                            ContainerShape.Squircle => IsInsideSquircle(px, py, cx, cy, r),
                            ContainerShape.RoundedSquare => IsInsideRoundedSquare(px, py, size, cornerRadius),
                            ContainerShape.Circle => IsInsideCircle(px, py, cx, cy, r),
                            _ => true
                        };

                        if (inside) insideCount++;
                    }
                }

                if (insideCount > 0)
                {
                    var coverage = insideCount * sampleWeight;
                    var finalAlpha = (byte)Math.Clamp((int)Math.Round(bgA * coverage), 0, 255);
                    canvas.SetPixel(x, y, bgR, bgG, bgB, finalAlpha);
                }
            }
        }

        return canvas;
    }

    public static RgbaCanvas Composite(RgbaCanvas background, RgbaCanvas foreground)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(foreground);
        if (background.Width != foreground.Width || background.Height != foreground.Height)
        {
            throw new ArgumentException("Canvas dimensions must match for composite.");
        }

        var result = new RgbaCanvas(background.Width, background.Height);

        for (var i = 0; i < result.Pixels.Length; i += 4)
        {
            double fr = foreground.Pixels[i];
            double fg = foreground.Pixels[i + 1];
            double fb = foreground.Pixels[i + 2];
            double fa = foreground.Pixels[i + 3] / 255.0;

            double br = background.Pixels[i];
            double bg = background.Pixels[i + 1];
            double bb = background.Pixels[i + 2];
            double ba = background.Pixels[i + 3] / 255.0;

            var outAlpha = fa + ba * (1.0 - fa);

            if (outAlpha > 0.0)
            {
                var outR = (fr * fa + br * ba * (1.0 - fa)) / outAlpha;
                var outG = (fg * fa + bg * ba * (1.0 - fa)) / outAlpha;
                var outB = (fb * fa + bb * ba * (1.0 - fa)) / outAlpha;

                result.Pixels[i] = (byte)Math.Clamp((int)Math.Round(outR), 0, 255);
                result.Pixels[i + 1] = (byte)Math.Clamp((int)Math.Round(outG), 0, 255);
                result.Pixels[i + 2] = (byte)Math.Clamp((int)Math.Round(outB), 0, 255);
                result.Pixels[i + 3] = (byte)Math.Clamp((int)Math.Round(outAlpha * 255.0), 0, 255);
            }
        }

        return result;
    }

    public static RgbaCanvas Render(RgbaCanvas source, IconStyle style, int canvasSize = DefaultCanvasSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);

        // 1. Normalize
        var normalized = IconNormalizer.Normalize(source, canvasSize, style.Container.Padding);

        // 2. Treatment
        var treated = ApplyTreatment(normalized, style.Treatment);

        // 3. Container
        var container = RenderContainer(canvasSize, style.Container);

        // 4. Composite
        return Composite(container, treated);
    }

    public static string ComputeCacheKey(byte[] sourceBytes, IconStyle style)
    {
        ArgumentNullException.ThrowIfNull(sourceBytes);
        ArgumentNullException.ThrowIfNull(style);

        var sourceHash = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
        var styleJson = style.ToJson();
        var combined = $"{sourceHash}|{styleJson}|v{RendererVersion}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(combined))).ToLowerInvariant();
    }

    private static bool IsInsideSquircle(double px, double py, double cx, double cy, double r)
    {
        var dx = Math.Abs((px - cx) / r);
        var dy = Math.Abs((py - cy) / r);
        return Math.Pow(dx, 4.5) + Math.Pow(dy, 4.5) <= 1.0;
    }

    private static bool IsInsideCircle(double px, double py, double cx, double cy, double r)
    {
        var dx = px - cx;
        var dy = py - cy;
        return (dx * dx + dy * dy) <= (r * r);
    }

    private static bool IsInsideRoundedSquare(double px, double py, int size, double cornerRadius)
    {
        if (px < 0 || px >= size || py < 0 || py >= size) return false;

        var min = cornerRadius;
        var max = size - cornerRadius;

        if (px >= min && px <= max) return true;
        if (py >= min && py <= max) return true;

        var cornerX = px < min ? min : max;
        var cornerY = py < min ? min : max;

        var dx = px - cornerX;
        var dy = py - cornerY;
        return (dx * dx + dy * dy) <= (cornerRadius * cornerRadius);
    }

    private static (byte R, byte G, byte B, byte A) ParseColorRgba(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return (230, 219, 250, 255);
        var clean = hex.Trim().TrimStart('#');

        if (clean.Length == 6)
        {
            var r = byte.Parse(clean.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var g = byte.Parse(clean.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var b = byte.Parse(clean.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (r, g, b, 255);
        }
        if (clean.Length == 8)
        {
            var a = byte.Parse(clean.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var r = byte.Parse(clean.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var g = byte.Parse(clean.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var b = byte.Parse(clean.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (r, g, b, a);
        }

        return (230, 219, 250, 255);
    }

    private static (bool Valid, byte R, byte G, byte B) ParseColorRgb(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return (false, 0, 0, 0);
        var clean = hex.Trim().TrimStart('#');
        if (clean.Length == 6)
        {
            var r = byte.Parse(clean.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var g = byte.Parse(clean.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var b = byte.Parse(clean.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (true, r, g, b);
        }
        return (false, 0, 0, 0);
    }
}
