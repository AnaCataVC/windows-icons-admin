using System.Collections.ObjectModel;
using System.ComponentModel;
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
using WindowsIconsAdmin_App.Services;
using WindowsIconsAdmin_App.ViewModels;

namespace WindowsIconsAdmin_App;

public sealed partial class MainPage : Page
{
    private readonly ObservableCollection<FolderItemViewModel> _folders = new();
    private readonly ShellIconService _shellService = new();
    private readonly IconStorageService _storageService = new();
    private readonly UndoStore _undoStore;
    private readonly RuleStore _ruleStore;
    private readonly List<FolderRule> _rules = new();

    private byte[]? _selectedIconBytes;
    private string? _selectedIconPath;
    private CancellationTokenSource? _currentCts;

    public MainPage()
    {
        InitializeComponent();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataDir = Path.Combine(localAppData, "WindowsIconsAdmin");
        var historyFile = Path.Combine(appDataDir, "history.json");
        var rulesFile = Path.Combine(appDataDir, "rules.json");

        _undoStore = new UndoStore(historyFile);
        _ruleStore = new RuleStore(rulesFile);
        _rules.AddRange(_ruleStore.GetAll());

        FoldersListView.ItemsSource = _folders;
        _folders.CollectionChanged += (_, _) => UpdateEmptyAndSelectionState();
        UpdateEmptyAndSelectionState();
    }

    private void UpdateEmptyAndSelectionState()
    {
        var total = _folders.Count;
        var checkedCount = _folders.Count(f => f.IsSelected);

        EmptyListPlaceholder.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (FoldersHeaderTitle != null)
        {
            FoldersHeaderTitle.Text = total == 0
                ? "Carpetas seleccionadas"
                : $"Carpetas en la lista ({checkedCount} de {total} marcadas)";
        }

        if (ApplyIconPrimaryButton != null)
        {
            ApplyIconPrimaryButton.Content = checkedCount > 0
                ? $"Aplicar icono a {checkedCount} {(checkedCount == 1 ? "carpeta" : "carpetas")}"
                : "Aplicar icono a seleccionadas";
        }
    }

    private IconStorageMode CurrentStorageMode =>
        (StorageModeRadioButtons.SelectedItem as RadioButton)?.Tag?.ToString() == "Central"
            ? IconStorageMode.CentralCache
            : IconStorageMode.PortableEmbedded;

    #region Drag and Drop & Multi-Folder Selection

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
        var added = 0;
        foreach (var item in items)
        {
            if (item is StorageFolder folder)
            {
                if (AddFolderIfNotPresent(folder.Path)) added++;
            }
            else if (Directory.Exists(item.Path))
            {
                if (AddFolderIfNotPresent(item.Path)) added++;
            }
        }

        if (added > 0)
        {
            ShowInfo($"Se agregaron {added} {(added == 1 ? "carpeta" : "carpetas")} a la lista.", InfoBarSeverity.Informational);
        }
    }

    private async void OnAddFoldersClick(object sender, RoutedEventArgs e)
    {
        var hwnd = MainWindow.Current.Hwnd;
        var selectedPaths = await MultiFolderPickerService.PickMultipleFoldersAsync(hwnd);
        if (selectedPaths.Count == 0) return;

        var added = 0;
        var skippedProtected = 0;
        foreach (var path in selectedPaths)
        {
            if (AddFolderIfNotPresent(path, out var wasProtected))
            {
                added++;
            }
            else if (wasProtected)
            {
                skippedProtected++;
            }
        }

        if (added > 0 && skippedProtected == 0)
        {
            ShowInfo($"Se agregaron {added} {(added == 1 ? "carpeta" : "carpetas")} a la lista.", InfoBarSeverity.Informational);
        }
        else if (added > 0 && skippedProtected > 0)
        {
            ShowInfo($"Se agregaron {added} carpetas ({skippedProtected} omitidas por ser carpetas protegidas del sistema).", InfoBarSeverity.Warning);
        }
    }

    private async void OnAddSubfoldersClick(object sender, RoutedEventArgs e)
    {
        var hwnd = MainWindow.Current.Hwnd;
        var parentPath = await MultiFolderPickerService.PickSingleParentFolderAsync(hwnd);
        if (string.IsNullOrWhiteSpace(parentPath) || !Directory.Exists(parentPath)) return;

        string[] subdirs;
        try
        {
            subdirs = Directory.GetDirectories(parentPath);
        }
        catch (Exception ex)
        {
            ShowInfo($"No se pudieron leer las subcarpetas de '{parentPath}': {ex.Message}", InfoBarSeverity.Error);
            return;
        }

        if (subdirs.Length == 0)
        {
            ShowInfo($"La carpeta '{Path.GetFileName(parentPath)}' no contiene subcarpetas directas.", InfoBarSeverity.Warning);
            return;
        }

        var added = 0;
        foreach (var dir in subdirs)
        {
            // Skip hidden dot-folders like .git or .vs by default when bulk-loading subfolders
            var name = Path.GetFileName(dir);
            if (name.StartsWith('.')) continue;

            try
            {
                var attrs = File.GetAttributes(dir);
                if ((attrs & System.IO.FileAttributes.System) != 0 && (attrs & System.IO.FileAttributes.Hidden) != 0)
                {
                    continue;
                }
            }
            catch
            {
                continue;
            }

            if (AddFolderIfNotPresent(dir, out _, suppressWarningToast: true))
            {
                added++;
            }
        }

        if (added > 0)
        {
            ShowInfo($"Se agregaron {added} subcarpetas desde '{Path.GetFileName(parentPath)}'.", InfoBarSeverity.Success);
        }
        else
        {
            ShowInfo("No se encontraron subcarpetas nuevas o válidas para agregar.", InfoBarSeverity.Informational);
        }
    }

    private bool AddFolderIfNotPresent(string path) =>
        AddFolderIfNotPresent(path, out _, suppressWarningToast: false);

    private bool AddFolderIfNotPresent(string path, out bool wasProtected, bool suppressWarningToast = false)
    {
        wasProtected = false;
        if (_folders.Any(f => f.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var validation = SystemFolderGuard.ValidateTargetFolder(path);
        if (!validation.IsValid)
        {
            wasProtected = true;
            if (!suppressWarningToast)
            {
                ShowInfo($"Carpeta protegida o no permitida: {validation.Reason}", InfoBarSeverity.Warning);
            }
            return false;
        }

        var vm = new FolderItemViewModel(path);
        vm.PropertyChanged += OnFolderItemPropertyChanged;
        _folders.Add(vm);
        return true;
    }

    private void OnFolderItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FolderItemViewModel.IsSelected))
        {
            UpdateEmptyAndSelectionState();
        }
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var f in _folders) f.IsSelected = true;
        UpdateEmptyAndSelectionState();
    }

    private void OnDeselectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var f in _folders) f.IsSelected = false;
        UpdateEmptyAndSelectionState();
    }

    private void OnCheckHighlightedOnlyClick(object sender, RoutedEventArgs e)
    {
        var highlighted = FoldersListView.SelectedItems
            .OfType<FolderItemViewModel>()
            .ToHashSet();

        if (highlighted.Count == 0)
        {
            ShowInfo("Selecciona primero varias filas con Ctrl + clic o Shift + clic en la lista.", InfoBarSeverity.Informational);
            return;
        }

        foreach (var f in _folders)
        {
            f.IsSelected = highlighted.Contains(f);
        }
        UpdateEmptyAndSelectionState();
    }

    private void OnRemoveSingleFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FolderItemViewModel item })
        {
            item.PropertyChanged -= OnFolderItemPropertyChanged;
            _folders.Remove(item);
        }
    }

    private void OnClearListClick(object sender, RoutedEventArgs e)
    {
        foreach (var f in _folders)
        {
            f.PropertyChanged -= OnFolderItemPropertyChanged;
        }
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
            ShowInfo("Marca al menos una carpeta de la lista para aplicar el icono.", InfoBarSeverity.Warning);
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
                        item.Status = "✓ Icono aplicado";
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
            ShowInfo($"Icono aplicado con éxito a {successCount} {(successCount == 1 ? "carpeta" : "carpetas")}.", InfoBarSeverity.Success);
        }
    }

    private async void OnRestoreDefaultClick(object sender, RoutedEventArgs e)
    {
        var selectedFolders = _folders.Where(f => f.IsSelected).ToList();
        if (selectedFolders.Count == 0)
        {
            ShowInfo("Marca al menos una carpeta de la lista.", InfoBarSeverity.Warning);
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
        ShowInfo($"Se restauró el icono predeterminado de {count} {(count == 1 ? "carpeta" : "carpetas")}.", InfoBarSeverity.Success);
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
        var allPaths = _folders.Select(f => f.FullPath).ToList();
        var selectedPaths = _folders.Where(f => f.IsSelected).Select(f => f.FullPath).ToList();

        var dialog = new RulesDialog(
            _rules,
            allPaths,
            selectedPaths,
            PersistUpdatedRules,
            ApplyRulesToFoldersAsync)
        {
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void PersistUpdatedRules(IReadOnlyList<FolderRule> updatedRules)
    {
        _rules.Clear();
        _rules.AddRange(updatedRules);
        _ruleStore.SaveAll(_rules);
    }

    private async Task ApplyRulesToFoldersAsync(IReadOnlyList<FolderRule> rules, bool selectedOnly)
    {
        PersistUpdatedRules(rules);

        var targetItems = selectedOnly
            ? _folders.Where(f => f.IsSelected).ToList()
            : _folders.ToList();

        if (targetItems.Count == 0)
        {
            ShowInfo("Agrega o marca carpetas en la lista antes de aplicar las reglas.", InfoBarSeverity.Warning);
            return;
        }

        var folderPaths = targetItems.Select(f => f.FullPath).ToList();
        IReadOnlyList<RuleMatch> matches;
        try
        {
            matches = RuleEngine.Evaluate(folderPaths, rules);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al evaluar las reglas: {ex.Message}", InfoBarSeverity.Error);
            return;
        }

        var matchedList = matches.Where(m => m.Rule != null).ToList();
        if (matchedList.Count == 0)
        {
            ShowInfo($"Ninguna de las {targetItems.Count} carpetas evaluadas coincidió con las reglas activas.", InfoBarSeverity.Informational);
            return;
        }

        var mode = CurrentStorageMode;
        var appliedCount = 0;
        var errorCount = 0;

        SetProgress(true, 0, matchedList.Count, "Aplicando reglas...");

        await Task.Run(() =>
        {
            var snapshots = new List<FolderSnapshot>();
            var encodedIconCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < matchedList.Count; i++)
            {
                var match = matchedList[i];
                var rule = match.Rule!;
                var folderVm = targetItems.FirstOrDefault(f => f.FullPath.Equals(match.FolderPath, StringComparison.OrdinalIgnoreCase));

                UpdateProgress(i + 1, matchedList.Count, $"Regla '{rule.Name}' → {Path.GetFileName(match.FolderPath)}");

                if (string.IsNullOrWhiteSpace(rule.IconPath) || !File.Exists(rule.IconPath))
                {
                    errorCount++;
                    App.AppDispatcherQueue?.TryEnqueue(() =>
                    {
                        if (folderVm != null) folderVm.Status = $"Error ({rule.Name}): icono no encontrado";
                    });
                    continue;
                }

                try
                {
                    if (!encodedIconCache.TryGetValue(rule.IconPath, out var bytes))
                    {
                        var rawBytes = File.ReadAllBytes(rule.IconPath);
                        bytes = rule.IconPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                            ? IcoEncoder.FromPng(rawBytes)
                            : rawBytes;
                        encodedIconCache[rule.IconPath] = bytes;
                    }

                    var prep = _storageService.PrepareIconForFolder(match.FolderPath, bytes, mode);
                    var snapshot = _shellService.ApplyFolderIcon(match.FolderPath, prep.IconResourceString, prep.CopiedFileName);
                    snapshots.Add(snapshot);

                    App.AppDispatcherQueue?.TryEnqueue(() =>
                    {
                        if (folderVm != null)
                        {
                            folderVm.Status = $"✓ Regla: {rule.Name}";
                            if (prep.WarnGitRepository) folderVm.HasGitWarning = true;
                            if (prep.WarnRemotePath) folderVm.IsRemoteWarning = true;
                        }
                    });
                    appliedCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    App.AppDispatcherQueue?.TryEnqueue(() =>
                    {
                        if (folderVm != null) folderVm.Status = $"Error ({rule.Name}): {ex.Message}";
                    });
                }
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
        if (errorCount > 0)
        {
            ShowInfo($"Reglas aplicadas a {appliedCount} carpetas ({errorCount} con error).", InfoBarSeverity.Warning);
        }
        else
        {
            ShowInfo($"Reglas aplicadas con éxito a {appliedCount} de {targetItems.Count} carpetas.", InfoBarSeverity.Success);
        }
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
