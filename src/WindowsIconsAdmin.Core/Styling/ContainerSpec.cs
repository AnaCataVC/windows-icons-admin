namespace WindowsIconsAdmin.Core.Styling;

public sealed record ContainerSpec
{
    private readonly double _padding = 0.16;

    public ContainerShape Shape { get; init; } = ContainerShape.Squircle;
    public string BackgroundColor { get; init; } = "#E6DBFA";

    public double Padding
    {
        get => _padding;
        init
        {
            if (value < 0.0 || value > 0.40)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Padding must be between 0.0 and 0.40");
            }
            _padding = value;
        }
    }

    public ContainerSpec() { }

    public ContainerSpec(ContainerShape shape, string backgroundColor, double padding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundColor);
        Shape = shape;
        BackgroundColor = backgroundColor;
        Padding = padding;
    }
}
