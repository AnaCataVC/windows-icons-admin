# Contract: App Icon Styling Engine v1

- **Status:** FROZEN for Cleanroom Cycle.
- **Language:** C# 13 / .NET 9, `Nullable` enabled.
- **Assembly:** `WindowsIconsAdmin.Core`.
- **Namespaces:** `WindowsIconsAdmin.Core.Styling`, `WindowsIconsAdmin.Core.Apps`.
- **Test Framework:** xUnit (`tests/WindowsIconsAdmin.Core.Tests`).

---

## 1. Domain Models (`WindowsIconsAdmin.Core.Styling`)

### 1.1 Specification Types

```csharp
namespace WindowsIconsAdmin.Core.Styling;

public enum ContainerShape
{
    Squircle,
    RoundedSquare,
    Circle
}

public sealed record ContainerSpec(
    ContainerShape Shape,
    string BackgroundColor, // Hex format: "#RRGGBB" or "#AARRGGBB"
    double Padding          // Range: [0.00, 0.40], default 0.16
);

public sealed record TreatmentSpec(
    double Saturation,  // Range: [-1.0, 1.0], 0.0 = identity, -1.0 = Rec.709 grayscale
    double Brightness,  // Range: [-1.0, 1.0], 0.0 = identity
    double Contrast,    // Range: [-1.0, 1.0], 0.0 = identity
    string? TintColor = null // Optional hex color: null or "#RRGGBB"
);

public sealed record IconStyle(
    int SchemaVersion,
    string Id,
    string Name,
    ContainerSpec Container,
    TreatmentSpec Treatment
)
{
    public const int CurrentSchemaVersion = 1;
    public static IconStyle CreateDefault(string name = "Soft Lavender") => ...;
}
```

### 1.2 `RgbaCanvas` Pixel Buffer

```csharp
namespace WindowsIconsAdmin.Core.Styling;

public sealed class RgbaCanvas
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; } // 4 bytes per pixel: [R, G, B, A] in row-major order

    public RgbaCanvas(int width, int height);
    public RgbaCanvas(int width, int height, byte[] pixels);

    public (int R, int G, int B, int A) GetPixel(int x, int y);
    public void SetPixel(int x, int y, int r, int g, int b, int a);

    public RgbaCanvas Clone();
}
```

### 1.3 `IconNormalizer`

```csharp
namespace WindowsIconsAdmin.Core.Styling;

public static class IconNormalizer
{
    public const byte AlphaThreshold = 16;

    /// <summary>
    /// Computes visible bounding box of non-transparent pixels (Alpha >= 16).
    /// If all pixels have Alpha < 16, returns the entire canvas bounds.
    /// </summary>
    public static (int MinX, int MinY, int MaxX, int MaxY) GetEffectiveBounds(RgbaCanvas source);

    /// <summary>
    /// Crops outer transparent margins, scales uniformly to fit within target inner area,
    /// and centers onto a new target canvas of dimension targetSize x targetSize.
    /// </summary>
    public static RgbaCanvas Normalize(RgbaCanvas source, int targetSize, double padding);
}
```

### 1.4 `IconStyleRenderer`

```csharp
namespace WindowsIconsAdmin.Core.Styling;

public static class IconStyleRenderer
{
    public const int DefaultCanvasSize = 256;

    /// <summary>
    /// Applies treatment parameters (saturation, brightness, contrast, tint) in-place or to a cloned canvas.
    /// </summary>
    public static RgbaCanvas ApplyTreatment(RgbaCanvas source, TreatmentSpec treatment);

    /// <summary>
    /// Renders container background shape onto a 256x256 canvas with anti-aliasing.
    /// </summary>
    public static RgbaCanvas RenderContainer(int size, ContainerSpec container);

    /// <summary>
    /// Executes the full styling pipeline:
    /// 1. Normalize source to target size with container padding
    /// 2. Apply color treatment to normalized icon
    /// 3. Composite treated icon over background container
    /// </summary>
    public static RgbaCanvas Render(RgbaCanvas source, IconStyle style, int canvasSize = DefaultCanvasSize);

    /// <summary>
    /// Computes deterministic SHA-256 cache key for source icon + style.
    /// </summary>
    public static string ComputeCacheKey(byte[] sourceBytes, IconStyle style);
}
```

---

## 2. Invariants & Mathematical Rules

### S1: Container Shapes & Anti-Aliasing
- **Squircle**: Evaluated using superellipse equation:
  $$ \left|\frac{x - c_x}{r}\right|^{4.5} + \left|\frac{y - c_y}{r}\right|^{4.5} \le 1.0 $$
  where $c_x = c_y = (size - 1) / 2.0$, and $r = size / 2.0$.
  Edge pixels compute alpha coverage using a $4\times 4$ subpixel grid (16 samples) for smooth, deterministic anti-aliasing.
- **RoundedSquare**: Evaluated using rounded rectangle with corner radius equal to $22.5\%$ of $size$. Evaluated with distance-to-corner antialiasing.
- **Circle**: Evaluated using distance to center $d = \sqrt{(x - c_x)^2 + (y - c_y)^2} \le r$.

### S2: Color Transformations
- **Saturation**:
  Luminance $Y = 0.2126 \cdot R + 0.7152 \cdot G + 0.0722 \cdot B$.
  For $s \in [-1.0, 1.0]$:
  $R' = \text{clamp}(Y + (R - Y) \cdot (1.0 + s), 0, 255)$.
  $G' = \text{clamp}(Y + (G - Y) \cdot (1.0 + s), 0, 255)$.
  $B' = \text{clamp}(Y + (B - Y) \cdot (1.0 + s), 0, 255)$.
  Alpha is untouched.
- **Brightness**:
  For $b \in [-1.0, 1.0]$:
  $R' = \text{clamp}(R + b \cdot 255.0, 0, 255)$.
  $G' = \text{clamp}(G + b \cdot 255.0, 0, 255)$.
  $B' = \text{clamp}(B + b \cdot 255.0, 0, 255)$.
  Alpha is untouched.
- **Contrast**:
  For $c \in [-1.0, 1.0]$, let factor $f = (1.0 + c) / (1.0 - c \cdot 0.99)$.
  $R' = \text{clamp}(((R / 255.0 - 0.5) \cdot f + 0.5) \cdot 255.0, 0, 255)$.
  $G' = \text{clamp}(((G / 255.0 - 0.5) \cdot f + 0.5) \cdot 255.0, 0, 255)$.
  $B' = \text{clamp}(((B / 255.0 - 0.5) \cdot f + 0.5) \cdot 255.0, 0, 255)$.
  Alpha is untouched.

### S3: Compositing (Source-Over)
When overlaying foreground pixel $(R_f, G_f, B_f, A_f)$ over background pixel $(R_b, G_b, B_b, A_b)$:
Let $\alpha_f = A_f / 255.0$, $\alpha_b = A_b / 255.0$.
Out $\alpha_o = \alpha_f + \alpha_b \cdot (1 - \alpha_f)$.
If $\alpha_o > 0$:
$$ C_o = \frac{C_f \cdot \alpha_f + C_b \cdot \alpha_b \cdot (1 - \alpha_f)}{\alpha_o} $$
$A_o = \text{round}(\alpha_o \cdot 255)$.

### S4: Determinism
Applying `Render` with the same input pixel buffer and the same `IconStyle` must produce identical byte sequences in `RgbaCanvas.Pixels`.
Zero non-deterministic threading race conditions or randomized dither.
