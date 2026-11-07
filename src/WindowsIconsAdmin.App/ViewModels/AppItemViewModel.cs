using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media.Imaging;
using WindowsIconsAdmin.Core.Apps;
using WindowsIconsAdmin.Core.Styling;
using WindowsIconsAdmin_App.Services;

namespace WindowsIconsAdmin_App.ViewModels;

public sealed class AppItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _status = "Listo";
    private WriteableBitmap? _styledPreview;

    public InstalledApp App { get; }
    public string DisplayName => App.DisplayName;
    public string PrimaryTargetPath => App.Targets.FirstOrDefault()?.TargetPath ?? App.ExecutablePath ?? string.Empty;
    public AppSupportStatus SupportStatus => App.SupportStatus;

    public bool IsSupported => SupportStatus == AppSupportStatus.Supported;

    public string SupportBadgeText => SupportStatus switch
    {
        AppSupportStatus.Supported => "✓ Soportada",
        AppSupportStatus.PartiallySupported => "△ Requiere Admin",
        _ => "✕ No soportada"
    };

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public WriteableBitmap? StyledPreview
    {
        get => _styledPreview;
        private set
        {
            if (!ReferenceEquals(_styledPreview, value))
            {
                _styledPreview = value;
                OnPropertyChanged();
            }
        }
    }

    public RgbaCanvas? BaselineCanvas { get; private set; }

    public AppItemViewModel(InstalledApp app, IconStyle currentStyle)
    {
        App = app;
        _isSelected = IsSupported;

        // Create or extract baseline canvas
        BaselineCanvas = GenerateBaselineCanvas(app);
        UpdatePreview(currentStyle);
    }

    public void UpdatePreview(IconStyle style)
    {
        if (BaselineCanvas == null) return;
        var styled = IconStyleRenderer.Render(BaselineCanvas, style, canvasSize: 64);
        StyledPreview = RgbaImageHelper.ToWriteableBitmap(styled);
    }

    private static RgbaCanvas GenerateBaselineCanvas(InstalledApp app)
    {
        // Synthesize a clean geometric badge for the application baseline
        var canvas = new RgbaCanvas(64, 64);
        var seed = (uint)app.DisplayName.GetHashCode();
        var r = (byte)(60 + (seed & 0x7F));
        var g = (byte)(60 + ((seed >> 7) & 0x7F));
        var b = (byte)(60 + ((seed >> 14) & 0x7F));

        for (var y = 14; y < 50; y++)
        {
            for (var x = 14; x < 50; x++)
            {
                canvas.SetPixel(x, y, r, g, b, 255);
            }
        }

        // Inner mark
        for (var y = 24; y < 40; y++)
        {
            for (var x = 24; x < 40; x++)
            {
                canvas.SetPixel(x, y, 255, 255, 255, 230);
            }
        }

        return canvas;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
