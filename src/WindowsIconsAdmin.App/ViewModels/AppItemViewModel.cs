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
    private WriteableBitmap? _originalPreview;

    public InstalledApp App { get; }
    public RgbaCanvas? BaselineCanvas { get; private set; }

    public string DisplayName => App.DisplayName;
    public string PrimaryTargetPath => App.Targets.FirstOrDefault()?.TargetPath ?? App.ExecutablePath ?? string.Empty;
    public string TargetPath => PrimaryTargetPath;
    public AppSupportStatus SupportStatus => App.SupportStatus;
    public bool IsSupported => App.SupportStatus == AppSupportStatus.Supported;

    public string SupportBadgeText => App.SupportStatus switch
    {
        AppSupportStatus.Supported => "✓ Soportada",
        AppSupportStatus.PartiallySupported => "⚠ Requiere Admin",
        AppSupportStatus.Unsupported => "✕ No soportada",
        _ => "Desconocido"
    };

    public string StatusBadgeText => SupportBadgeText;

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
            _styledPreview = value;
            OnPropertyChanged();
        }
    }

    public WriteableBitmap? OriginalPreview
    {
        get => _originalPreview;
        private set
        {
            _originalPreview = value;
            OnPropertyChanged();
        }
    }

    public AppItemViewModel(InstalledApp app, IconStyle? initialStyle = null)
    {
        App = app;
        _isSelected = IsSupported;

        try
        {
            BaselineCanvas = AppIconExtractor.ExtractAppIcon(app, unboxWhitePlate: true);
        }
        catch
        {
            // ExtractAppIcon already has internal fallbacks, but keep robust
        }

        if (BaselineCanvas != null)
        {
            OriginalPreview = RgbaImageHelper.ToWriteableBitmap(BaselineCanvas);
        }

        if (initialStyle != null && BaselineCanvas != null)
        {
            UpdatePreview(initialStyle);
        }
    }

    public void UpdatePreview(IconStyle style)
    {
        if (BaselineCanvas == null) return;
        var styled = IconStyleRenderer.Render(BaselineCanvas, style, canvasSize: 64, enableEffects: true);
        StyledPreview = RgbaImageHelper.ToWriteableBitmap(styled);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
