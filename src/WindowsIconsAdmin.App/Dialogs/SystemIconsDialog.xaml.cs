using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        RefreshStatuses();
    }

    private void RefreshStatuses()
    {
        UpdateStatusLabel(SystemIconKind.RecycleBinEmpty, RecycleEmptyStatus);
        UpdateStatusLabel(SystemIconKind.RecycleBinFull, RecycleFullStatus);
        UpdateStatusLabel(SystemIconKind.ThisPC, ThisPcStatus);
        UpdateStatusLabel(SystemIconKind.Network, NetworkStatus);
        UpdateStatusLabel(SystemIconKind.UserFiles, UserFilesStatus);
    }

    private void UpdateStatusLabel(SystemIconKind kind, TextBlock label)
    {
        var current = _shellService.GetCurrentSystemIcon(kind);
        label.Text = string.IsNullOrEmpty(current) ? "Predeterminado" : current;
    }

    private async Task ChangeIconForKindAsync(SystemIconKind kind, TextBlock label)
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
            byte[] icoBytes;
            if (file.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                var pngBytes = await File.ReadAllBytesAsync(file.Path);
                icoBytes = IcoEncoder.FromPng(pngBytes);
            }
            else
            {
                icoBytes = await File.ReadAllBytesAsync(file.Path);
            }

            // Central cache is required for system registry icons (registry cannot use relative paths)
            var stored = _storageService.PrepareIconForFolder(Path.GetTempPath(), icoBytes, IconStorageMode.CentralCache);
            var cleanPath = stored.IconResourceString.Split(',')[0];

            _shellService.SetSystemIcon(kind, cleanPath);
            RefreshStatuses();

            SystemInfoBar.IsOpen = true;
            SystemInfoBar.Severity = InfoBarSeverity.Success;
            SystemInfoBar.Message = "Icono del sistema actualizado correctamente.";
        }
        catch (Exception ex)
        {
            SystemInfoBar.IsOpen = true;
            SystemInfoBar.Severity = InfoBarSeverity.Error;
            SystemInfoBar.Message = $"Error al asignar icono: {ex.Message}";
        }
    }

    private void RestoreIconForKind(SystemIconKind kind)
    {
        try
        {
            _shellService.RestoreSystemIcon(kind);
            RefreshStatuses();

            SystemInfoBar.IsOpen = true;
            SystemInfoBar.Severity = InfoBarSeverity.Success;
            SystemInfoBar.Message = "Icono restaurado a su valor predeterminado.";
        }
        catch (Exception ex)
        {
            SystemInfoBar.IsOpen = true;
            SystemInfoBar.Severity = InfoBarSeverity.Error;
            SystemInfoBar.Message = $"Error al restaurar: {ex.Message}";
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
}
