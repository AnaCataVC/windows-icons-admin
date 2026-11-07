using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsIconsAdmin.Core.Styling;
using WindowsIconsAdmin_App.ViewModels;

namespace WindowsIconsAdmin_App.Dialogs;

public sealed partial class StyleEditorDialog : ContentDialog
{
    private readonly StyleStore _styleStore;
    private readonly ObservableCollection<AppPreviewItemViewModel> _previewItems = [];
    private bool _isInitializing = true;
    private IconStyle _currentStyle;

    public IconStyle CurrentStyle => _currentStyle;

    public StyleEditorDialog(StyleStore? styleStore = null, IconStyle? initialStyle = null)
    {
        InitializeComponent();
        _styleStore = styleStore ?? new StyleStore();
        _currentStyle = initialStyle ?? _styleStore.GetAll().FirstOrDefault() ?? IconStyle.CreateDefault();

        PreviewListView.ItemsSource = _previewItems;

        LoadStyleIntoUi(_currentStyle);
        _isInitializing = false;

        // Initialize sample applications
        var samples = AppPreviewItemViewModel.CreateSampleApps(_currentStyle);
        foreach (var sample in samples)
        {
            _previewItems.Add(sample);
        }
    }

    private void LoadStyleIntoUi(IconStyle style)
    {
        StyleNameBox.Text = style.Name;

        // Shape
        ShapeCombo.SelectedIndex = style.Container.Shape switch
        {
            ContainerShape.Squircle => 0,
            ContainerShape.RoundedSquare => 1,
            ContainerShape.Circle => 2,
            _ => 0
        };

        // Background color
        BgColorBox.Text = style.Container.BackgroundColor;

        // Sliders
        PaddingSlider.Value = style.Container.Padding * 100.0;
        PaddingValueLabel.Text = $"{(int)PaddingSlider.Value}%";

        SaturationSlider.Value = style.Treatment.Saturation * 100.0;
        SaturationValueLabel.Text = $"{(int)SaturationSlider.Value}%";

        BrightnessSlider.Value = style.Treatment.Brightness * 100.0;
        BrightnessValueLabel.Text = $"{(int)BrightnessSlider.Value}%";

        ContrastSlider.Value = style.Treatment.Contrast * 100.0;
        ContrastValueLabel.Text = $"{(int)ContrastSlider.Value}%";
    }

    private void OnParameterChanged(object sender, object e)
    {
        if (_isInitializing) return;

        UpdateLabels();
        RecomputeLivePreviews();
    }

    private void OnPaletteColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            BgColorBox.Text = hex;
        }
    }

    private void UpdateLabels()
    {
        PaddingValueLabel.Text = $"{(int)PaddingSlider.Value}%";
        SaturationValueLabel.Text = $"{(int)SaturationSlider.Value}%";
        BrightnessValueLabel.Text = $"{(int)BrightnessSlider.Value}%";
        ContrastValueLabel.Text = $"{(int)ContrastSlider.Value}%";
    }

    private void RecomputeLivePreviews()
    {
        var shape = ShapeCombo.SelectedIndex switch
        {
            0 => ContainerShape.Squircle,
            1 => ContainerShape.RoundedSquare,
            2 => ContainerShape.Circle,
            _ => ContainerShape.Squircle
        };

        var bgHex = string.IsNullOrWhiteSpace(BgColorBox.Text) ? "#E6DBFA" : BgColorBox.Text.Trim();
        var padding = Math.Clamp(PaddingSlider.Value / 100.0, 0.0, 0.40);

        var sat = Math.Clamp(SaturationSlider.Value / 100.0, -1.0, 1.0);
        var bri = Math.Clamp(BrightnessSlider.Value / 100.0, -1.0, 1.0);
        var con = Math.Clamp(ContrastSlider.Value / 100.0, -1.0, 1.0);

        _currentStyle = new IconStyle(
            SchemaVersion: 1,
            Id: _currentStyle.Id,
            Name: string.IsNullOrWhiteSpace(StyleNameBox.Text) ? "Custom Style" : StyleNameBox.Text.Trim(),
            Container: new ContainerSpec(shape: shape, backgroundColor: bgHex, padding: padding),
            Treatment: new TreatmentSpec(saturation: sat, brightness: bri, contrast: con)
        );

        foreach (var item in _previewItems)
        {
            item.UpdateStyled(_currentStyle);
        }
    }

    private void OnSaveStyleClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Don't close immediately if saving
        args.Cancel = true;

        try
        {
            _styleStore.Save(_currentStyle);
            StatusInfoBar.Message = $"Estilo '{_currentStyle.Name}' guardado correctamente en tu equipo.";
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            StatusInfoBar.Message = $"Error al guardar el estilo: {ex.Message}";
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.IsOpen = true;
        }
    }

    private void OnResetStyleClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;

        _isInitializing = true;
        _currentStyle = IconStyle.CreateDefault();
        LoadStyleIntoUi(_currentStyle);
        _isInitializing = false;

        foreach (var item in _previewItems)
        {
            item.UpdateStyled(_currentStyle);
        }

        StatusInfoBar.Message = "Valores restablecidos al estilo predeterminado 'Soft Lavender'.";
        StatusInfoBar.Severity = InfoBarSeverity.Informational;
        StatusInfoBar.IsOpen = true;
    }
}
