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
    private readonly List<AppItemViewModel> _allAppItems = [];
    private readonly ObservableCollection<AppItemViewModel> _displayedAppItems = [];
    private readonly List<IconStyle> _availableStyles = [];
    private IconStyle _currentStyle;
    private CancellationTokenSource? _currentCts;
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

        _availableStyles = _styleStore.GetAllWithPresets().ToList();
        _currentStyle = _availableStyles.FirstOrDefault() ?? IconStyle.CreateDefault();

        LoadStylesIntoUi();
        AppsListView.ItemsSource = _displayedAppItems;

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

        _allAppItems.Clear();
        _displayedAppItems.Clear();

        await Task.Run(() =>
        {
            var apps = _discoveryService.DiscoverInstalledApps();

            App.AppDispatcherQueue?.TryEnqueue(() =>
            {
                foreach (var app in apps)
                {
                    _allAppItems.Add(new AppItemViewModel(app, _currentStyle));
                }
                ApplySearchFilter();
            });
        });

        SetProgress(false);
    }

    private void ApplySearchFilter()
    {
        var filter = SearchBox.Text?.Trim() ?? string.Empty;
        _displayedAppItems.Clear();

        var matches = string.IsNullOrEmpty(filter)
            ? _allAppItems
            : _allAppItems.Where(a => a.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                      a.PrimaryTargetPath.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var item in matches)
        {
            _displayedAppItems.Add(item);
        }

        UpdateAppCountLabels();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySearchFilter();
    }

    private void UpdateAppCountLabels()
    {
        var total = _allAppItems.Count;
        var supported = _allAppItems.Count(a => a.IsSupported);
        var displayed = _displayedAppItems.Count;

        AppCountLabel.Text = displayed == total
            ? $"{total} aplicaciones detectadas ({supported} soportadas)"
            : $"{displayed} de {total} mostradas ({supported} soportadas)";
    }

    private void OnStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StyleComboBox.SelectedItem is ComboBoxItem { Tag: IconStyle selectedStyle })
        {
            _currentStyle = selectedStyle;
            foreach (var item in _allAppItems)
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

        // WinUI 3 invariant: Only one ContentDialog can be open at a time.
        // Hide self, show editor, then restore self.
        this.Hide();
        try
        {
            await editor.ShowAsync();
            _currentStyle = editor.CurrentStyle;
        }
        finally
        {
            _availableStyles.Clear();
            _availableStyles.AddRange(_styleStore.GetAllWithPresets());
            LoadStylesIntoUi();

            foreach (var item in _allAppItems)
            {
                item.UpdatePreview(_currentStyle);
            }

            _ = this.ShowAsync();
        }
    }

    private async void OnScanAppsClick(object sender, RoutedEventArgs e)
    {
        await ScanAppsAsync();
    }

    private void OnSelectSupportedClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in _displayedAppItems)
        {
            item.IsSelected = item.IsSupported;
        }
    }

    private void OnDeselectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in _displayedAppItems)
        {
            item.IsSelected = false;
        }
    }

    private async void OnApplySelectedClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selected = _allAppItems.Where(a => a.IsSelected && a.IsSupported).ToList();
        if (selected.Count == 0)
        {
            ShowInfo("Selecciona al menos una aplicación soportada para aplicar.", InfoBarSeverity.Warning);
            return;
        }

        _currentCts = new CancellationTokenSource();
        SetProgress(true, $"Iniciando aplicación de estilos ({selected.Count} apps)...", canCancel: true);

        var progress = new Progress<(int Current, int Total, string Status)>(report =>
        {
            BatchProgressBar.IsIndeterminate = false;
            BatchProgressBar.Maximum = report.Total;
            BatchProgressBar.Value = report.Current;
            ProgressStatusText.Text = $"Procesando ({report.Current}/{report.Total}): {report.Status}";
        });

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

            var result = _appIconService.ApplyBatch(batchItems, progress: progress, cancellationToken: _currentCts.Token);

            App.AppDispatcherQueue?.TryEnqueue(() =>
            {
                foreach (var item in selected)
                {
                    item.Status = "Aplicado";
                }

                if (result.WasCancelled)
                {
                    ShowInfo($"Lote cancelado por el usuario. Se completaron {result.AppliedCount} aplicaciones antes de detenerse.", InfoBarSeverity.Warning);
                }
                else if (result.ErrorCount > 0)
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

    private void OnCancelBatchClick(object sender, RoutedEventArgs e)
    {
        _currentCts?.Cancel();
        ProgressStatusText.Text = "Cancelando...";
        CancelButton.IsEnabled = false;
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
                    foreach (var item in _allAppItems)
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

    private async void OnRestoreAllClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        SetProgress(true, "Restaurando todos los iconos de aplicaciones...");

        await Task.Run(() =>
        {
            var restoredCount = _appIconService.RevertAll();

            App.AppDispatcherQueue?.TryEnqueue(() =>
            {
                if (restoredCount > 0)
                {
                    foreach (var item in _allAppItems)
                    {
                        item.Status = "Original";
                    }
                    ShowInfo($"Se restauraron {restoredCount} accesos directos a su icono original.", InfoBarSeverity.Success);
                }
                else
                {
                    ShowInfo("No había accesos directos modificados pendientes de restaurar.", InfoBarSeverity.Informational);
                }
            });
        });

        SetProgress(false);
    }

    private void SetProgress(bool visible, string status = "", bool canCancel = false)
    {
        _isBusy = visible;
        ProgressArea.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        BatchProgressBar.IsIndeterminate = visible;
        ProgressStatusText.Text = status;
        CancelButton.Visibility = canCancel ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = canCancel;
        ApplyButton.IsEnabled = !visible;
        UndoButton.IsEnabled = !visible;
        RestoreAllButton.IsEnabled = !visible;
    }

    private void ShowInfo(string message, InfoBarSeverity severity)
    {
        MainInfoBar.Message = message;
        MainInfoBar.Severity = severity;
        MainInfoBar.IsOpen = true;
    }
}
