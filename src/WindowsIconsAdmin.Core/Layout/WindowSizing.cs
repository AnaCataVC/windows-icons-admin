namespace WindowsIconsAdmin.Core.Layout;

public static class WindowSizing
{
    private const long WorkAreaNumerator = 9;
    private const long WorkAreaDenominator = 10;

    /// <param name="desired">Desired size in device-independent pixels (DIP).</param>
    /// <param name="minimum">Minimum size in DIP.</param>
    /// <param name="workAreaPx">Usable monitor work area in physical pixels.</param>
    /// <param name="dpiScale">Display scale factor (1.0 = 100%).</param>
    /// <returns>Initial window size in physical pixels.</returns>
    public static SizePx ComputeInitialSize(SizePx desired, SizePx minimum, SizePx workAreaPx, double dpiScale)
    {
        ValidatePositive(desired, nameof(desired));
        ValidatePositive(minimum, nameof(minimum));
        ValidatePositive(workAreaPx, nameof(workAreaPx));

        var scale = double.IsFinite(dpiScale) && dpiScale > 0 ? dpiScale : 1.0;

        return new SizePx(
            ComputeAxis(desired.Width, minimum.Width, workAreaPx.Width, scale),
            ComputeAxis(desired.Height, minimum.Height, workAreaPx.Height, scale));
    }

    private static int ComputeAxis(int desired, int minimum, int workArea, double scale)
    {
        var scaledDesired = ScaleToPixels(desired, scale);
        var scaledMinimum = ScaleToPixels(minimum, scale);
        var upperBound = Math.Max(1L, workArea * WorkAreaNumerator / WorkAreaDenominator);

        var result = Math.Min(scaledDesired, upperBound);
        result = Math.Max(result, scaledMinimum);
        result = Math.Min(result, upperBound);
        return (int)Math.Max(1L, result);
    }

    private static long ScaleToPixels(int value, double scale)
    {
        var scaled = Math.Round(value * scale, MidpointRounding.AwayFromZero);
        return scaled >= int.MaxValue ? int.MaxValue : (long)scaled;
    }

    private static void ValidatePositive(SizePx size, string paramName)
    {
        if (size.Width <= 0)
            throw new ArgumentOutOfRangeException(paramName, size.Width, "Width must be greater than zero.");
        if (size.Height <= 0)
            throw new ArgumentOutOfRangeException(paramName, size.Height, "Height must be greater than zero.");
    }
}
