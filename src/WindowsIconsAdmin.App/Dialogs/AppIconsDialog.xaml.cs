using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsIconsAdmin.Core.Apps;
using WindowsIconsAdmin.Core.Imaging;
using WindowsIconsAdmin.Core.Styling;
using WindowsIconsAdmin_App.ViewModels;

namespace WindowsIconsAdmin_App.Dialogs;

public sealed partial class AppIconsDialog : ContentDialog
{
    private readonly IAppDiscoveryService _discoveryService;
    private readonly IAppIconService _appIconService;
    private readonly StyleStore _styleStore;
    private readonly ObservableCollection<AppItemViewModel> _appItems = [];
    private readonly List<IconStyle> _availableStyles = [];
    private IconStyle _currentStyle;
    private bool _isBusy;

    public AppIconsDialog(
        IAppDiscoveryService? discoveryService = null,
        IAppIconService? appIconService = null,
        StyleStore? styleStore = null)
    {
        InitializeComponent();
        _discoveryService = discoveryService ?? new AppDiscoveryService();
        _appIconService = appIconService ?? new AppIconService();
        _styleStore = styleStore ?? new StyleStore();

        _availableStyles = _styleStore.GetAll().ToList();
        _currentStyle = _availableStyles.FirstOrDefault() ?? IconStyle.CreateDefault();

        LoadStylesIntoUi();
        AppsListView.ItemsSource = _appItems;

        Loaded += async (s, e) => await ScanAppsAsync();
    }

    private void LoadStylesIntoUi()
    {
        StyleComboBox.Items.Clear();
        foreach (var style in _availableStyles)
        {
            StyleComboBox.Items.Add(new ComboBoxItem
            {
                Content = $"{style.Name} ({style.Container.Shape})",
                Tag = style
            });
        }

        if (StyleComboBox.Items.Count > 0)
        {
            StyleComboBox.SelectedIndex = 0;
        }
    }

    private async Task ScanAppsAsync()
    {
        if (_isBusy) return;
        SetProgress(true, "Buscando aplicaciones instaladas...");

        _appItems.Clear();

        await Task.Run(() =>
        {
            var apps = _discoveryService.DiscoverInstalledApps();

            App.AppDispatcherQueue?.TryEnqueue(() =>
            {
                foreach (var app in apps)
                {
                    _appItems.Add(new AppItemViewModel(app, _currentStyle));
                }
                UpdateAppCountLabels();
            });
        });

        SetProgress(false);
    }

    private void UpdateAppCountLabels()
    {
        var total = _appItems.Count;
        var supported = _appItems.Count(a => a.IsSupported);
        AppCountLabel.Text = $"{total} aplicaciones detectadas ({supported} soportadas para personalización)";
    }

    private void OnStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StyleComboBox.SelectedItem is ComboBoxItem { Tag: IconStyle selectedStyle })
        {
            _currentStyle = selectedStyle;
            foreach (var item in _appItems)
            {
                item.UpdatePreview(_currentStyle);
            }
        }
    }

    private async void OnOpenStyleEditorClick(object sender, RoutedEventArgs e)
    {
        var editor = new StyleEditorDialog(_styleStore, _currentStyle)
        {
            XamlRoot = this.XamlRoot
        };
        await editor.ShowAsync();

        // Refresh styles list
        _availableStyles.Clear();
        _availableStyles.AddRange(_styleStore.GetAll());
        LoadStylesIntoUi();
    }

    private async void OnScanAppsClick(object sender, RoutedEventArgs e)
    {
        await ScanAppsAsync();
    }

    private void OnSelectSupportedClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in _appItems)
        {
            item.IsSelected = item.IsSupported;
        }
    }

    private void OnDeselectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in _appItems)
        {
            item.IsSelected = false;
        }
    }

    private async void OnApplySelectedClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selected = _appItems.Where(a => a.IsSelected && a.IsSupported).ToList();
        if (selected.Count == 0)
        {
            ShowInfo("Selecciona al menos una aplicación soportada para aplicar.", InfoBarSeverity.Warning);
            return;
        }

        SetProgress(true, $"Generando y aplicando iconos ({selected.Count} apps)...");

        await Task.Run(() =>
        {
            var batchItems = new List<(InstalledApp App, IconStyle Style, byte[] IcoBytes)>();

            foreach (var item in selected)
            {
                if (item.BaselineCanvas != null)
                {
                    var styledCanvas = IconStyleRenderer.Render(item.BaselineCanvas, _currentStyle, 256);
                    var icoBytes = PureIcoEncoder.FromCanvas(styledCanvas);
                    batchItems.Add((item.App, _currentStyle, icoBytes));
                }
            }

            var result = _appIconService.ApplyBatch(batchItems);

            App.AppDispatcherQueue?.TryEnqueue(() =>
            {
                foreach (var item in selected)
                {
                    item.Status = "Aplicado";
                }

                if (result.ErrorCount > 0)
                {
                    ShowInfo($"Lote aplicado con advertencias: {result.AppliedCount} iconos actualizados, {result.ErrorCount} errores.", InfoBarSeverity.Warning);
                }
                else
                {
                    ShowInfo($"¡Éxito! Se actualizaron {result.AppliedCount} accesos directos con el estilo '{_currentStyle.Name}'.", InfoBarSeverity.Success);
                }
            });
        });

        SetProgress(false);
    }

    private async void OnUndoLastBatchClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        SetProgress(true, "Revirtiendo último lote de aplicaciones...");

        await Task.Run(() =>
        {
            var reverted = _appIconService.RevertLastBatch();

            App.AppDispatcherQueue?.TryEnqueue(() =>
            {
                if (reverted)
                {
                    foreach (var item in _appItems)
                    {
                        item.Status = "Revertido";
                    }
                    ShowInfo("Último lote de aplicaciones revertido al icono original.", InfoBarSeverity.Success);
                }
                else
                {
                    ShowInfo("No hay lotes previos pendientes para revertir.", InfoBarSeverity.Informational);
                }
            });
        });

        SetProgress(false);
    }

    private void SetProgress(bool visible, string status = "")
    {
        _isBusy = visible;
        ProgressArea.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        BatchProgressBar.IsIndeterminate = visible;
        ProgressStatusText.Text = status;
        ApplyButton.IsEnabled = !visible;
        UndoButton.IsEnabled = !visible;
    }

    private void ShowInfo(string message, InfoBarSeverity severity)
    {
        MainInfoBar.Message = message;
        MainInfoBar.Severity = severity;
        MainInfoBar.IsOpen = true;
    }
}
