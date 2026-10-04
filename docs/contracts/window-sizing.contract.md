# Contract: Window Sizing

> **Status:** Frozen (pending user approval)
> **Scope:** `WindowsIconsAdmin.Core` (namespace `WindowsIconsAdmin.Core.Layout`)

## 1. Public Interface

```csharp
namespace WindowsIconsAdmin.Core.Layout;

public readonly record struct SizePx(int Width, int Height);

public static class WindowSizing
{
    /// <param name="desired">Desired size in device-independent pixels (DIP).</param>
    /// <param name="minimum">Minimum size in DIP.</param>
    /// <param name="workAreaPx">Usable monitor work area in physical pixels.</param>
    /// <param name="dpiScale">Display scale factor (1.0 = 100%).</param>
    /// <returns>Initial window size in physical pixels.</returns>
    public static SizePx ComputeInitialSize(
        SizePx desired,
        SizePx minimum,
        SizePx workAreaPx,
        double dpiScale);
}
```

The function is pure, stateless and thread-safe.

## 2. Behavioral Rules

1. Effective scale: if `dpiScale` is NaN, infinite, or `<= 0`, it is treated as `1.0`.
2. Scaled desired size = `desired * scale`; scaled minimum = `minimum * scale` (rounded to nearest integer, away from zero on .5).
3. Upper bound per axis = 90% of `workAreaPx` on that axis (rounded down).
4. Result per axis = `min(scaledDesired, upperBound)`, then raised to `scaledMinimum` if lower.
5. If the scaled minimum exceeds the upper bound, the upper bound wins (the window must never exceed 90% of the work area).
6. Axes are computed independently.

## 3. Acceptance Criteria (Given-When-Then)

| # | Given | Then |
|---|-------|------|
| 1 | desired 1160x720, min 800x520, work area 1920x1040, scale 1.0 | 1160x720 |
| 2 | desired 1160x720, min 800x520, work area 1920x1080, scale 1.5 (physical 1740x1080) | 1728x972 (width capped at 90% of 1920 = 1728; height 1080 capped at 972) |
| 3 | desired 1160x720, min 800x520, work area 1366x728, scale 1.0 | 1160x655 (width fits under the 1229 cap; height capped at 655) |
| 4 | work area smaller than minimum (e.g. 600x400, min 800x520) | 540x360 (90% wins) |
| 5 | scale is 0, -1, NaN, +inf | Same result as scale 1.0 |

## 4. Boundary Values & Errors

- Any `desired`, `minimum` or `workAreaPx` dimension `<= 0` throws `ArgumentOutOfRangeException`.
- Result dimensions are always `>= 1`. They never exceed 90% of the work area, except when a work-area dimension is 1 (the cap would be 0), in which case the result is 1.
- Integer overflow must not occur for dimensions up to 16384 and scale up to 8.0.

## 5. Out of Scope

Internal rounding helpers, file layout and algorithm structure are not dictated.
