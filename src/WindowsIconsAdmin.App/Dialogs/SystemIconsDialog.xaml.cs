using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WindowsIconsAdmin.Core.Imaging;
using WindowsIconsAdmin.Core.Shell;
using WindowsIconsAdmin.Core.Storage;

namespace WindowsIconsAdmin_App.Dialogs;

public sealed partial class SystemIconsDialog : ContentDialog
{
    private readonly ShellIconService _shellService;
    private readonly IconStorageService _storageService;

    public SystemIconsDialog(ShellIconService shellService, IconStorageService storageService)
    {
        InitializeComponent();
        _shellService = shellService;
        _storageService = storageService;

        RefreshAllStatuses();
        PopulateSpecialFolders();
    }

    private void OnSectionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SectionSelector == null) return;

        var selectedIndex = SectionSelector.SelectedIndex;
        if (DesktopSection != null) DesktopSection.Visibility = selectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (SpecialFoldersSection != null) SpecialFoldersSection.Visibility = selectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        if (GlobalDefaultsSection != null) GlobalDefaultsSection.Visibility = selectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        if (FileExtensionsSection != null) FileExtensionsSection.Visibility = selectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshAllStatuses()
    {
        // 0: Desktop
        UpdateStatusLabel(SystemIconKind.RecycleBinEmpty, RecycleEmptyStatus);
        UpdateStatusLabel(SystemIconKind.RecycleBinFull, RecycleFullStatus);
        UpdateStatusLabel(SystemIconKind.ThisPC, ThisPcStatus);
        UpdateStatusLabel(SystemIconKind.Network, NetworkStatus);
        UpdateStatusLabel(SystemIconKind.UserFiles, UserFilesStatus);

        // 2: Global Defaults
        var folderDef = _shellService.GetCurrentDefaultFolderIcon(MachineWideFolderCheckbox?.IsChecked == true);
        if (DefaultFolderStatus != null)
        {
            DefaultFolderStatus.Text = string.IsNullOrEmpty(folderDef) ? "Predeterminado de Windows" : folderDef;
        }

        var fileDef = _shellService.GetCurrentDefaultFileIcon(MachineWideFileCheckbox?.IsChecked == true);
        if (DefaultFileStatus != null)
        {
            DefaultFileStatus.Text = string.IsNullOrEmpty(fileDef) ? "Predeterminado de Windows" : fileDef;
        }
    }

    private void UpdateStatusLabel(SystemIconKind kind, TextBlock label)
    {
        var current = _shellService.GetCurrentSystemIcon(kind);
        label.Text = string.IsNullOrEmpty(current) ? "Predeterminado" : current;
    }

    private async Task<string?> PickAndPrepareIconAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = MainWindow.Current.Hwnd;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
        picker.FileTypeFilter.Add(".ico");
        picker.FileTypeFilter.Add(".png");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return null;

        byte[] icoBytes;
        if (file.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            var pngBytes = await File.ReadAllBytesAsync(file.Path);
            icoBytes = await Task.Run(() => IcoEncoder.FromPng(pngBytes));
        }
        else
        {
            icoBytes = await File.ReadAllBytesAsync(file.Path);
        }

        var stored = _storageService.PrepareIconForFolder(Path.GetTempPath(), icoBytes, IconStorageMode.CentralCache);
        return stored.IconResourceString;
    }

    #region SECCIÓN 0: Iconos de Escritorio

    private async Task ChangeIconForKindAsync(SystemIconKind kind, TextBlock label)
    {
        try
        {
            var iconResource = await PickAndPrepareIconAsync();
            if (iconResource == null) return;

            _shellService.SetSystemIcon(kind, iconResource);
            RefreshAllStatuses();

            ShowInfo("Icono del sistema actualizado y notificado a Explorer con éxito.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al asignar icono del sistema: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void RestoreIconForKind(SystemIconKind kind)
    {
        try
        {
            _shellService.RestoreSystemIcon(kind);
            RefreshAllStatuses();

            ShowInfo("Icono restaurado a su valor predeterminado de Windows.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al restaurar: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void OnChangeRecycleEmptyClick(object sender, RoutedEventArgs e) => await ChangeIconForKindAsync(SystemIconKind.RecycleBinEmpty, RecycleEmptyStatus);
    private void OnRestoreRecycleEmptyClick(object sender, RoutedEventArgs e) => RestoreIconForKind(SystemIconKind.RecycleBinEmpty);

    private async void OnChangeRecycleFullClick(object sender, RoutedEventArgs e) => await ChangeIconForKindAsync(SystemIconKind.RecycleBinFull, RecycleFullStatus);
    private void OnRestoreRecycleFullClick(object sender, RoutedEventArgs e) => RestoreIconForKind(SystemIconKind.RecycleBinFull);

    private async void OnChangeThisPcClick(object sender, RoutedEventArgs e) => await ChangeIconForKindAsync(SystemIconKind.ThisPC, ThisPcStatus);
    private void OnRestoreThisPcClick(object sender, RoutedEventArgs e) => RestoreIconForKind(SystemIconKind.ThisPC);

    private async void OnChangeNetworkClick(object sender, RoutedEventArgs e) => await ChangeIconForKindAsync(SystemIconKind.Network, NetworkStatus);
    private void OnRestoreNetworkClick(object sender, RoutedEventArgs e) => RestoreIconForKind(SystemIconKind.Network);

    private async void OnChangeUserFilesClick(object sender, RoutedEventArgs e) => await ChangeIconForKindAsync(SystemIconKind.UserFiles, UserFilesStatus);
    private void OnRestoreUserFilesClick(object sender, RoutedEventArgs e) => RestoreIconForKind(SystemIconKind.UserFiles);

    #endregion

    #region SECCIÓN 1: Carpetas Especiales (Known Folders)

    private void PopulateSpecialFolders()
    {
        SpecialFoldersContainer.Children.Clear();
        var folders = SpecialFoldersService.GetAllSpecialFolders();

        foreach (var folder in folders)
        {
            var card = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };

            var grid = new Grid
            {
                ColumnSpacing = 12
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 2
            };

            var titleBlock = new TextBlock
            {
                Text = folder.DisplayName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            infoStack.Children.Add(titleBlock);

            var pathBlock = new TextBlock
            {
                Text = folder.FolderPath,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            infoStack.Children.Add(pathBlock);

            var currentCustomIcon = _shellService.GetCurrentSpecialFolderRegistryIcon(folder.Kind);
            var statusBlock = new TextBlock
            {
                Text = string.IsNullOrEmpty(currentCustomIcon) ? "Icono: Predeterminado de Windows" : $"Icono: {currentCustomIcon}",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            infoStack.Children.Add(statusBlock);

            if (folder.IsCloudSynced)
            {
                var cloudBadge = new TextBlock
                {
                    Text = "☁ Sincronizada con Nube (OneDrive/Dropbox)",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 217, 119, 6))
                };
                infoStack.Children.Add(cloudBadge);
            }

            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            var changeBtn = new Button { Content = "Cambiar..." };
            changeBtn.Click += async (s, e) =>
            {
                try
                {
                    var iconRes = await PickAndPrepareIconAsync();
                    if (iconRes == null) return;

                    if (Directory.Exists(folder.FolderPath))
                    {
                        _shellService.ApplyFolderIcon(folder.FolderPath, iconRes);
                    }
                    _shellService.SetSpecialFolderRegistryIcons(folder.Kind, iconRes);
                    _shellService.RefreshExplorerIconCache(restartExplorer: false);
                    PopulateSpecialFolders();
                    ShowInfo($"Icono de '{folder.DisplayName}' actualizado en desktop.ini y CLSIDs del sistema.", InfoBarSeverity.Success);
                }
                catch (Exception ex)
                {
                    ShowInfo($"Error al cambiar carpeta especial: {ex.Message}", InfoBarSeverity.Error);
                }
            };
            Grid.SetColumn(changeBtn, 1);
            grid.Children.Add(changeBtn);

            var restoreBtn = new Button { Content = "Restaurar" };
            restoreBtn.Click += (s, e) =>
            {
                try
                {
                    if (Directory.Exists(folder.FolderPath))
                    {
                        _shellService.RestoreFolderDefault(folder.FolderPath, isKnownFolder: true);
                    }
                    _shellService.RestoreSpecialFolderRegistryIcons(folder.Kind);
                    _shellService.RefreshExplorerIconCache(restartExplorer: false);
                    PopulateSpecialFolders();
                    ShowInfo($"Icono de '{folder.DisplayName}' restaurado a su valor nativo.", InfoBarSeverity.Success);
                }
                catch (Exception ex)
                {
                    ShowInfo($"Error al restaurar carpeta especial: {ex.Message}", InfoBarSeverity.Error);
                }
            };
            Grid.SetColumn(restoreBtn, 2);
            grid.Children.Add(restoreBtn);

            card.Child = grid;
            SpecialFoldersContainer.Children.Add(card);
        }
    }

    #endregion

    #region SECCIÓN 2: Predeterminados Globales

    private async void OnChangeDefaultFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var iconRes = await PickAndPrepareIconAsync();
            if (iconRes == null) return;

            var machineWide = MachineWideFolderCheckbox.IsChecked == true;
            _shellService.SetDefaultFolderIcon(iconRes, machineWide);
            RefreshAllStatuses();

            ShowInfo("Icono predeterminado de carpetas actualizado con éxito.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al asignar icono predeterminado de carpetas: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnRestoreDefaultFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var machineWide = MachineWideFolderCheckbox.IsChecked == true;
            _shellService.RestoreDefaultFolderIcon(machineWide);
            RefreshAllStatuses();

            ShowInfo("Icono predeterminado de carpetas restaurado al original de Windows.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al restaurar icono de carpetas: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void OnChangeDefaultFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var iconRes = await PickAndPrepareIconAsync();
            if (iconRes == null) return;

            var machineWide = MachineWideFileCheckbox.IsChecked == true;
            _shellService.SetDefaultFileIcon(iconRes, machineWide);
            RefreshAllStatuses();

            ShowInfo("Icono predeterminado de archivos desconocidos actualizado con éxito.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al asignar icono de archivos desconocidos: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnRestoreDefaultFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var machineWide = MachineWideFileCheckbox.IsChecked == true;
            _shellService.RestoreDefaultFileIcon(machineWide);
            RefreshAllStatuses();

            ShowInfo("Icono predeterminado de archivos restaurado al original de Windows.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al restaurar icono de archivos: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    #endregion

    #region SECCIÓN 3: Por Extensión de Archivo

    private void OnExtensionInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            InspectExtension(ExtensionInputTextBox.Text);
        }
    }

    private void OnCheckExtensionClick(object sender, RoutedEventArgs e)
    {
        InspectExtension(ExtensionInputTextBox.Text);
    }

    private void InspectExtension(string? rawExt)
    {
        if (string.IsNullOrWhiteSpace(rawExt))
        {
            ShowInfo("Por favor ingresa una extensión (ej. .txt o .pdf)", InfoBarSeverity.Warning);
            return;
        }

        var ext = rawExt.Trim();
        if (!ext.StartsWith('.')) ext = "." + ext;
        ext = ext.ToLowerInvariant();

        ExtensionDetailsTitle.Text = $"Detalles de la extensión: {ext}";

        var progId = FileTypeIconService.GetExtensionProgId(ext);
        ExtensionProgIdLabel.Text = string.IsNullOrEmpty(progId) ? "ProgID del sistema: (Ninguno detectado)" : $"ProgID del sistema: {progId}";

        var currentIcon = FileTypeIconService.GetExtensionIcon(ext);
        ExtensionIconStatusLabel.Text = string.IsNullOrEmpty(currentIcon) ? "Icono actual: Predeterminado" : $"Icono actual: {currentIcon}";
    }

    private async void OnChangeExtensionIconClick(object sender, RoutedEventArgs e)
    {
        var rawExt = ExtensionInputTextBox.Text;
        if (string.IsNullOrWhiteSpace(rawExt))
        {
            ShowInfo("Ingresa primero la extensión que deseas modificar.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var iconRes = await PickAndPrepareIconAsync();
            if (iconRes == null) return;

            FileTypeIconService.SetExtensionIcon(rawExt, iconRes);
            InspectExtension(rawExt);
            ShowInfo($"Icono asignado correctamente para la extensión '{rawExt}'.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al asignar icono a la extensión: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnRestoreExtensionIconClick(object sender, RoutedEventArgs e)
    {
        var rawExt = ExtensionInputTextBox.Text;
        if (string.IsNullOrWhiteSpace(rawExt))
        {
            ShowInfo("Ingresa la extensión a restaurar.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            FileTypeIconService.RestoreExtensionIcon(rawExt);
            InspectExtension(rawExt);
            ShowInfo($"Icono de la extensión '{rawExt}' restaurado a su valor nativo.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al restaurar extensión: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    #endregion

    #region ACCIÓN GLOBAL DE EMERGENCIA Y REFRESCO

    private void OnRestartExplorerClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _shellService.RefreshExplorerIconCache(restartExplorer: true);
            ShowInfo("Explorador de Windows reiniciado y caché de iconos refrescada.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error al reiniciar el Explorador: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnRestoreAllSystemIconsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _shellService.RestoreAllSystemAndDefaultIcons();
            RefreshAllStatuses();
            PopulateSpecialFolders();
            ShowInfo("Todos los iconos de sistema, carpetas especiales y predeterminados fueron restaurados al estado original de Windows.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"Error durante la restauración global: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    #endregion

    private void ShowInfo(string message, InfoBarSeverity severity)
    {
        SystemInfoBar.Message = message;
        SystemInfoBar.Severity = severity;
        SystemInfoBar.IsOpen = true;
    }
}
