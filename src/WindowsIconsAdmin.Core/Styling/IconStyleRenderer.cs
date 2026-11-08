using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WindowsIconsAdmin.Core.Styling;

public static class IconStyleRenderer
{
    public const int DefaultCanvasSize = 256;
    public const int RendererVersion = 2;

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
                r = (r - 128.0) * factor + 128.0;
                g = (g - 128.0) * factor + 128.0;
                b = (b - 128.0) * factor + 128.0;
            }

            // 4. Tint
            if (hasTint)
            {
                var y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                var lum = y / 255.0;
                r = tintR * lum;
                g = tintG * lum;
                b = tintB * lum;
            }

            result.Pixels[i] = (byte)Math.Clamp((int)Math.Round(r), 0, 255);
            result.Pixels[i + 1] = (byte)Math.Clamp((int)Math.Round(g), 0, 255);
            result.Pixels[i + 2] = (byte)Math.Clamp((int)Math.Round(b), 0, 255);
        }

        return result;
    }

    public static RgbaCanvas RenderContainer(int size, ContainerSpec container, bool enableEffects = false)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive.");

        var (bgR, bgG, bgB, bgA) = ParseColorRgba(container.BackgroundColor);
        var canvas = new RgbaCanvas(size, size);

        var cx = (size - 1) / 2.0;
        var cy = (size - 1) / 2.0;
        // In effects mode, shrink by 4% so the icon breathes nicely in Explorer
        var rad = enableEffects ? size * 0.46 : size / 2.0;
        var cornerRadius = enableEffects ? (size * 0.92) * 0.225 : size * 0.225;

        // Subpixel sampling 4x4 grid (16 samples per pixel) for deterministic anti-aliasing
        const int samples = 4;
        const double step = 1.0 / samples;
        const double sampleWeight = 1.0 / (samples * samples);

        var innerRad = rad - Math.Max(1.2, size * 0.012);
        bool isLightBg = (0.2126 * bgR + 0.7152 * bgG + 0.0722 * bgB) > 180;
        byte rimTarget = isLightBg ? (byte)160 : (byte)255;

        for (var y = 0; y < size; y++)
        {
            // Subtle top-to-bottom lighting gradient (+6% at top, -6% at bottom)
            double vFactor = enableEffects ? 1.0 + 0.06 * (1.0 - 2.0 * (y / (double)(size - 1))) : 1.0;
            byte gradR = (byte)Math.Clamp((int)Math.Round(bgR * vFactor), 0, 255);
            byte gradG = (byte)Math.Clamp((int)Math.Round(bgG * vFactor), 0, 255);
            byte gradB = (byte)Math.Clamp((int)Math.Round(bgB * vFactor), 0, 255);

            for (var x = 0; x < size; x++)
            {
                var insideCount = 0;
                var insideInnerCount = 0;

                for (var sy = 0; sy < samples; sy++)
                {
                    var py = y + (sy + 0.5) * step;

                    for (var sx = 0; sx < samples; sx++)
                    {
                        var px = x + (sx + 0.5) * step;

                        var inside = container.Shape switch
                        {
                            ContainerShape.Squircle => IsInsideSquircle(px, py, cx, cy, rad),
                            ContainerShape.RoundedSquare => IsInsideRoundedSquare(px, py, size, cornerRadius, cx, cy, rad, enableEffects),
                            ContainerShape.Circle => IsInsideCircle(px, py, cx, cy, rad),
                            _ => true
                        };

                        if (inside)
                        {
                            insideCount++;
                            if (enableEffects)
                            {
                                var insideInner = container.Shape switch
                                {
                                    ContainerShape.Squircle => IsInsideSquircle(px, py, cx, cy, innerRad),
                                    ContainerShape.RoundedSquare => IsInsideRoundedSquare(px, py, size, cornerRadius - 1.2, cx, cy, innerRad, enableEffects),
                                    ContainerShape.Circle => IsInsideCircle(px, py, cx, cy, innerRad),
                                    _ => true
                                };
                                if (insideInner) insideInnerCount++;
                            }
                        }
                    }
                }

                if (insideCount > 0)
                {
                    var coverage = insideCount * sampleWeight;
                    var finalAlpha = (byte)Math.Clamp((int)Math.Round(bgA * coverage), 0, 255);

                    if (enableEffects)
                    {
                        double outerCov = insideCount * sampleWeight;
                        double innerCov = insideInnerCount * sampleWeight;
                        double rimCov = Math.Clamp(outerCov - innerCov, 0.0, 1.0);

                        double rimLight = y < size / 2 ? 0.22 : 0.08;
                        double rimStrength = isLightBg ? 0.25 : rimLight;

                        byte outR = (byte)Math.Clamp((int)Math.Round(gradR * (1.0 - rimCov * rimStrength) + rimTarget * (rimCov * rimStrength)), 0, 255);
                        byte outG = (byte)Math.Clamp((int)Math.Round(gradG * (1.0 - rimCov * rimStrength) + rimTarget * (rimCov * rimStrength)), 0, 255);
                        byte outB = (byte)Math.Clamp((int)Math.Round(gradB * (1.0 - rimCov * rimStrength) + rimTarget * (rimCov * rimStrength)), 0, 255);

                        canvas.SetPixel(x, y, outR, outG, outB, finalAlpha);
                    }
                    else
                    {
                        canvas.SetPixel(x, y, bgR, bgG, bgB, finalAlpha);
                    }
                }
            }
        }

        return canvas;
    }

    public static RgbaCanvas AddGlyphDropShadow(RgbaCanvas foreground)
    {
        ArgumentNullException.ThrowIfNull(foreground);
        int w = foreground.Width;
        int h = foreground.Height;
        int offsetY = Math.Max(1, h / 42);
        int radius = Math.Max(2, h / 32);

        var shadowAlpha = new double[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int sy = y - offsetY;
                if (sy >= 0 && sy < h)
                {
                    shadowAlpha[y * w + x] = foreground.GetPixel(x, sy).A / 255.0;
                }
            }
        }

        var temp = new double[w * h];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double sum = 0;
                    int count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int nx = Math.Clamp(x + k, 0, w - 1);
                        sum += shadowAlpha[y * w + nx];
                        count++;
                    }
                    temp[y * w + x] = sum / count;
                }
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double sum = 0;
                    int count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int ny = Math.Clamp(y + k, 0, h - 1);
                        sum += temp[ny * w + x];
                        count++;
                    }
                    shadowAlpha[y * w + x] = sum / count;
                }
            }
        }

        var shadowCanvas = new RgbaCanvas(w, h);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                byte sa = (byte)Math.Clamp((int)Math.Round(shadowAlpha[y * w + x] * 70.0), 0, 255);
                if (sa > 0)
                {
                    shadowCanvas.SetPixel(x, y, 0, 0, 0, sa);
                }
            }
        }

        return Composite(shadowCanvas, foreground);
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

    public static RgbaCanvas Render(RgbaCanvas source, IconStyle style, int canvasSize = DefaultCanvasSize, bool enableEffects = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);

        // 1. Normalize
        var normalized = IconNormalizer.Normalize(source, canvasSize, style.Container.Padding);

        // 2. Treatment
        var treated = ApplyTreatment(normalized, style.Treatment);

        // 3. Drop shadow
        var foreground = enableEffects ? AddGlyphDropShadow(treated) : treated;

        // 4. Container
        var container = RenderContainer(canvasSize, style.Container, enableEffects: enableEffects);

        // 5. Composite
        return Composite(container, foreground);
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

    private static bool IsInsideRoundedSquare(double px, double py, int size, double cornerRadius, double cx, double cy, double r, bool enableEffects)
    {
        if (enableEffects)
        {
            var left = cx - r;
            var right = cx + r;
            var top = cy - r;
            var bottom = cy + r;

            if (px < left || px > right || py < top || py > bottom) return false;

            var minX = left + cornerRadius;
            var maxX = right - cornerRadius;
            var minY = top + cornerRadius;
            var maxY = bottom - cornerRadius;

            if (px >= minX && px <= maxX) return true;
            if (py >= minY && py <= maxY) return true;

            var cornerX = px < minX ? minX : maxX;
            var cornerY = py < minY ? minY : maxY;

            var dx = px - cornerX;
            var dy = py - cornerY;
            return (dx * dx + dy * dy) <= (cornerRadius * cornerRadius);
        }
        else
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
