using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WindowsIconsAdmin.Core.History;
using WindowsIconsAdmin.Core.Imaging;
using WindowsIconsAdmin.Core.Rules;
using WindowsIconsAdmin.Core.Shell;
using WindowsIconsAdmin.Core.Storage;
using WindowsIconsAdmin.Core.Safety;
using WindowsIconsAdmin_App.Dialogs;
using WindowsIconsAdmin_App.ViewModels;

namespace WindowsIconsAdmin_App;

public sealed partial class MainPage : Page
{
    private readonly ObservableCollection<FolderItemViewModel> _folders = new();
    private readonly ShellIconService _shellService = new();
    private readonly IconStorageService _storageService = new();
    private readonly UndoStore _undoStore;
    private readonly List<FolderRule> _rules = new();

    private byte[]? _selectedIconBytes;
    private string? _selectedIconPath;
    private CancellationTokenSource? _currentCts;

    public MainPage()
    {
        InitializeComponent();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var historyFile = Path.Combine(localAppData, "WindowsIconsAdmin", "history.json");
        _undoStore = new UndoStore(historyFile);

        FoldersListView.ItemsSource = _folders;
        _folders.CollectionChanged += (s, e) => UpdateEmptyState();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyListPlaceholder.Visibility = _folders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private IconStorageMode CurrentStorageMode =>
        (StorageModeRadioButtons.SelectedItem as RadioButton)?.Tag?.ToString() == "Central"
            ? IconStorageMode.CentralCache
            : IconStorageMode.PortableEmbedded;

    #region Drag and Drop & Add Folders

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Agregar carpetas";
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    private async void OnListDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        var items = await e.DataView.GetStorageItemsAsync();
        foreach (var item in items)
        {
            if (item is StorageFolder folder)
            {
                AddFolderIfNotPresent(folder.Path);
            }
            else if (Directory.Exists(item.Path))
            {
                AddFolderIfNotPresent(item.Path);
            }
        }
    }

    private async void OnAddFoldersClick(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = MainWindow.Current.Hwnd;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            AddFolderIfNotPresent(folder.Path);
        }
    }

    private void AddFolderIfNotPresent(string path)
    {
        if (_folders.Any(f => f.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var validation = SystemFolderGuard.ValidateTargetFolder(path);
        if (!validation.IsValid)
        {
            ShowInfo($"Carpeta protegida o no permitida: {validation.Reason}", InfoBarSeverity.Warning);
            return;
        }

        _folders.Add(new FolderItemViewModel(path));
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var f in _folders) f.IsSelected = true;
    }

    private void OnDeselectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var f in _folders) f.IsSelected = false;
    }

    private void OnClearListClick(object sender, RoutedEventArgs e)
    {
        _folders.Clear();
    }

    #endregion

    #region Icon Selection and Preview

    private async void OnSelectIconClick(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = MainWindow.Current.Hwnd;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
        picker.FileTypeFilter.Add(".ico");
        picker.FileTypeFilter.Add(".png");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            var rawBytes = await File.ReadAllBytesAsync(file.Path);
            if (file.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                _selectedIconBytes = await Task.Run(() => IcoEncoder.FromPng(rawBytes));
                IconInfoLabel.Text = $"{file.Name}\n(PNG convertido automáticamente a .ICO)";
            }
            else
            {
                _selectedIconBytes = rawBytes;
                IconInfoLabel.Text = $"{file.Name}\n(Archivo .ICO nativo)";
            }

            _selectedIconPath = file.Path;

            // Load preview bitmap
            using var ms = new MemoryStream(rawBytes);
            var ras = ms.AsRandomAccessStream();
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(ras);
            IconPreviewImage.Source = bmp;

            ShowInfo("Icono cargado correctamente.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al cargar el icono: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    #endregion

    #region Batch Operations (Apply, Restore, Undo)

    private async void OnApplyIconClick(object sender, RoutedEventArgs e)
    {
        var selectedFolders = _folders.Where(f => f.IsSelected).ToList();
        if (selectedFolders.Count == 0)
        {
            ShowInfo("Selecciona al menos una carpeta de la lista.", InfoBarSeverity.Warning);
            return;
        }

        if (_selectedIconBytes == null)
        {
            ShowInfo("Selecciona un archivo .ico o .png antes de aplicar.", InfoBarSeverity.Warning);
            return;
        }

        _currentCts = new CancellationTokenSource();
        var token = _currentCts.Token;

        SetProgress(true, 0, selectedFolders.Count, "Iniciando aplicación de icono...");

        var snapshots = new List<FolderSnapshot>();
        var mode = CurrentStorageMode;
        var iconBytes = _selectedIconBytes;
        var iconPath = _selectedIconPath;
        var successCount = 0;
        var errorCount = 0;

        await Task.Run(() =>
        {
            for (var i = 0; i < selectedFolders.Count; i++)
            {
                if (token.IsCancellationRequested) break;

                var item = selectedFolders[i];
                UpdateProgress(i + 1, selectedFolders.Count, $"Procesando: {item.FolderName}");

                try
                {
                    var prep = _storageService.PrepareIconForFolder(item.FullPath, iconBytes, mode);
                    var snapshot = _shellService.ApplyFolderIcon(item.FullPath, prep.IconResourceString, prep.CopiedFileName);
                    snapshots.Add(snapshot);

                    App.AppDispatcherQueue?.TryEnqueue(() =>
                    {
                        item.Status = "Aplicado";
                        if (prep.WarnGitRepository) item.HasGitWarning = true;
                        if (prep.WarnRemotePath) item.IsRemoteWarning = true;
                    });
                    successCount++;
                }
                catch (Exception ex)
                {
                    App.AppDispatcherQueue?.TryEnqueue(() => item.Status = $"Error: {ex.Message}");
                    errorCount++;
                }
            }

            if (snapshots.Count > 0)
            {
                var batch = new BatchRecord(
                    Guid.NewGuid().ToString("N"),
                    DateTimeOffset.UtcNow,
                    OperationKind.ApplyIcon,
                    iconPath,
                    snapshots,
                    false);

                _undoStore.Add(batch);
                _shellService.NotifyBatchCompleted();
            }
        });

        SetProgress(false);

        if (token.IsCancellationRequested)
        {
            ShowInfo($"Operación cancelada. Se procesaron {successCount} carpetas.", InfoBarSeverity.Warning);
        }
        else if (errorCount > 0)
        {
            ShowInfo($"Lote completado con advertencias: {successCount} exitosas, {errorCount} con error.", InfoBarSeverity.Warning);
        }
        else
        {
            ShowInfo($"Icono aplicado con éxito a {successCount} carpetas.", InfoBarSeverity.Success);
        }
    }

    private async void OnRestoreDefaultClick(object sender, RoutedEventArgs e)
    {
        var selectedFolders = _folders.Where(f => f.IsSelected).ToList();
        if (selectedFolders.Count == 0)
        {
            ShowInfo("Selecciona al menos una carpeta de la lista.", InfoBarSeverity.Warning);
            return;
        }

        _currentCts = new CancellationTokenSource();
        var token = _currentCts.Token;

        SetProgress(true, 0, selectedFolders.Count, "Restaurando iconos predeterminados...");

        var snapshots = new List<FolderSnapshot>();
        var count = 0;

        await Task.Run(() =>
        {
            for (var i = 0; i < selectedFolders.Count; i++)
            {
                if (token.IsCancellationRequested) break;

                var item = selectedFolders[i];
                UpdateProgress(i + 1, selectedFolders.Count, $"Restaurando: {item.FolderName}");

                try
                {
                    var snapshot = _shellService.RestoreFolderDefault(item.FullPath);
                    snapshots.Add(snapshot);
                    App.AppDispatcherQueue?.TryEnqueue(() => item.Status = "Restaurado");
                    count++;
                }
                catch (Exception ex)
                {
                    App.AppDispatcherQueue?.TryEnqueue(() => item.Status = $"Error: {ex.Message}");
                }
            }

            if (snapshots.Count > 0)
            {
                var batch = new BatchRecord(
                    Guid.NewGuid().ToString("N"),
                    DateTimeOffset.UtcNow,
                    OperationKind.RestoreDefault,
                    null,
                    snapshots,
                    false);

                _undoStore.Add(batch);
                _shellService.NotifyBatchCompleted();
            }
        });

        SetProgress(false);
        ShowInfo($"Se restauró el icono predeterminado de {count} carpetas.", InfoBarSeverity.Success);
    }

    private async void OnUndoLastBatchClick(object sender, RoutedEventArgs e)
    {
        var lastBatch = _undoStore.GetLastPending();
        if (lastBatch == null)
        {
            ShowInfo("No hay cambios pendientes para revertir.", InfoBarSeverity.Informational);
            return;
        }

        SetProgress(true, 0, lastBatch.Folders.Count, "Revirtiendo último lote...");

        await Task.Run(() =>
        {
            for (var i = 0; i < lastBatch.Folders.Count; i++)
            {
                var snap = lastBatch.Folders[i];
                UpdateProgress(i + 1, lastBatch.Folders.Count, $"Revirtiendo: {Path.GetFileName(snap.FolderPath)}");

                try
                {
                    _shellService.RevertFolder(snap);

                    App.AppDispatcherQueue?.TryEnqueue(() =>
                    {
                        var match = _folders.FirstOrDefault(f => f.FullPath.Equals(snap.FolderPath, StringComparison.OrdinalIgnoreCase));
                        if (match != null) match.Status = "Revertido";
                    });
                }
                catch { }
            }

            _undoStore.MarkReverted(lastBatch.BatchId);
            _shellService.NotifyBatchCompleted();
        });

        SetProgress(false);
        ShowInfo($"Último lote ({lastBatch.Folders.Count} carpetas) revertido con éxito.", InfoBarSeverity.Success);
    }

    private void OnCancelBatchClick(object sender, RoutedEventArgs e)
    {
        _currentCts?.Cancel();
    }

    private void SetProgress(bool visible, int current = 0, int total = 0, string status = "")
    {
        ProgressArea.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        BatchProgressBar.Maximum = total > 0 ? total : 1;
        BatchProgressBar.Value = current;
        ProgressStatusText.Text = status;
    }

    private void UpdateProgress(int current, int total, string status)
    {
        App.AppDispatcherQueue?.TryEnqueue(() =>
        {
            BatchProgressBar.Value = current;
            ProgressStatusText.Text = $"{status} ({current}/{total})";
        });
    }

    private void ShowInfo(string message, InfoBarSeverity severity)
    {
        MainInfoBar.Message = message;
        MainInfoBar.Severity = severity;
        MainInfoBar.IsOpen = true;
    }

    #endregion

    #region Dialogs (Rules & System Icons)

    private async void OnRulesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new RulesDialog(_rules, ApplyRulesToFoldersAsync)
        {
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async Task ApplyRulesToFoldersAsync(List<FolderRule> rules)
    {
        if (_folders.Count == 0)
        {
            ShowInfo("Agrega carpetas a la lista antes de aplicar reglas.", InfoBarSeverity.Warning);
            return;
        }

        var folderPaths = _folders.Select(f => f.FullPath).ToList();
        var matches = RuleEngine.Evaluate(folderPaths, rules);

        var mode = CurrentStorageMode;
        var appliedCount = 0;

        SetProgress(true, 0, matches.Count, "Aplicando reglas...");

        await Task.Run(() =>
        {
            var snapshots = new List<FolderSnapshot>();

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (match.Rule == null || string.IsNullOrEmpty(match.Rule.IconPath) || !File.Exists(match.Rule.IconPath))
                {
                    continue;
                }

                try
                {
                    byte[] bytes;
                    if (match.Rule.IconPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        bytes = IcoEncoder.FromPng(File.ReadAllBytes(match.Rule.IconPath));
                    }
                    else
                    {
                        bytes = File.ReadAllBytes(match.Rule.IconPath);
                    }

                    var prep = _storageService.PrepareIconForFolder(match.FolderPath, bytes, mode);
                    var snapshot = _shellService.ApplyFolderIcon(match.FolderPath, prep.IconResourceString, prep.CopiedFileName);
                    snapshots.Add(snapshot);

                    App.AppDispatcherQueue?.TryEnqueue(() =>
                    {
                        var item = _folders.FirstOrDefault(f => f.FullPath.Equals(match.FolderPath, StringComparison.OrdinalIgnoreCase));
                        if (item != null) item.Status = $"Regla: {match.Rule.Name}";
                    });
                    appliedCount++;
                }
                catch { }
            }

            if (snapshots.Count > 0)
            {
                var batch = new BatchRecord(
                    Guid.NewGuid().ToString("N"),
                    DateTimeOffset.UtcNow,
                    OperationKind.ApplyIcon,
                    "Rules Engine",
                    snapshots,
                    false);

                _undoStore.Add(batch);
                _shellService.NotifyBatchCompleted();
            }
        });

        SetProgress(false);
        ShowInfo($"Reglas aplicadas con éxito a {appliedCount} carpetas coincidentes.", InfoBarSeverity.Success);
    }

    private async void OnSystemIconsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SystemIconsDialog(_shellService, _storageService)
        {
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    #endregion
}
