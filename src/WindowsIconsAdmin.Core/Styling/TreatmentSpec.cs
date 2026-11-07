namespace WindowsIconsAdmin.Core.Styling;

public sealed record TreatmentSpec
{
    private readonly double _saturation = 0.0;
    private readonly double _brightness = 0.0;
    private readonly double _contrast = 0.0;

    public double Saturation
    {
        get => _saturation;
        init
        {
            if (value < -1.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Saturation must be between -1.0 and 1.0");
            _saturation = value;
        }
    }

    public double Brightness
    {
        get => _brightness;
        init
        {
            if (value < -1.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Brightness must be between -1.0 and 1.0");
            _brightness = value;
        }
    }

    public double Contrast
    {
        get => _contrast;
        init
        {
            if (value < -1.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Contrast must be between -1.0 and 1.0");
            _contrast = value;
        }
    }

    public string? TintColor { get; init; } = null;

    public TreatmentSpec() { }

    public TreatmentSpec(double saturation, double brightness, double contrast, string? tintColor = null)
    {
        Saturation = saturation;
        Brightness = brightness;
        Contrast = contrast;
        TintColor = tintColor;
    }
}
