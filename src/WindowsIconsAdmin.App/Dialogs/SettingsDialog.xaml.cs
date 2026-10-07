using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsIconsAdmin.Core.Settings;
using WindowsIconsAdmin.Core.Storage;

namespace WindowsIconsAdmin_App.Dialogs;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly AppSettingsService _settingsService;
    private readonly IconStorageService _storageService;

    public SettingsDialog(AppSettingsService settingsService, IconStorageService storageService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _storageService = storageService;

        EnablePortableModeToggle.IsOn = _settingsService.Current.EnablePortableMode;
        PortableWarningInfoBar.IsOpen = _settingsService.Current.EnablePortableMode;
        CachePathTextBlock.Text = _storageService.CentralCacheDirectory;
    }

    private void OnPortableModeToggled(object sender, RoutedEventArgs e)
    {
        var enabled = EnablePortableModeToggle.IsOn;
        PortableWarningInfoBar.IsOpen = enabled;
        _settingsService.Update(s => s with { EnablePortableMode = enabled });
    }

    private void OnOpenCacheFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_storageService.CentralCacheDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _storageService.CentralCacheDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            GitIgnoreStatusTextBlock.Text = $"Error al abrir carpeta: {ex.Message}";
            GitIgnoreStatusTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void OnConfigureGitIgnoreClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var gitIgnoreGlobalPath = Path.Combine(userProfile, ".gitignore_global");

            var entriesToAdd = new List<string>
            {
                "desktop.ini",
                "[Dd]esktop.ini",
                ".folder_icon.ico",
                "Thumbs.db"
            };

            var existingLines = File.Exists(gitIgnoreGlobalPath)
                ? File.ReadAllLines(gitIgnoreGlobalPath).ToList()
                : new List<string>();

            var addedCount = 0;
            foreach (var entry in entriesToAdd)
            {
                if (!existingLines.Any(line => line.Trim().Equals(entry, StringComparison.OrdinalIgnoreCase)))
                {
                    existingLines.Add(entry);
                    addedCount++;
                }
            }

            if (addedCount > 0 || !File.Exists(gitIgnoreGlobalPath))
            {
                File.WriteAllLines(gitIgnoreGlobalPath, existingLines);
            }

            // Set git config --global core.excludesfile "~/.gitignore_global"
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"config --global core.excludesfile \"{gitIgnoreGlobalPath.Replace('\\', '/')}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
            }
            catch
            {
                // Best-effort git CLI invocation
            }

            GitIgnoreStatusTextBlock.Text = $"✓ 'desktop.ini' y '.folder_icon.ico' configurados en {gitIgnoreGlobalPath}";
            GitIgnoreStatusTextBlock.Visibility = Visibility.Visible;
            ConfigureGitIgnoreButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            GitIgnoreStatusTextBlock.Text = $"Error: {ex.Message}";
            GitIgnoreStatusTextBlock.Visibility = Visibility.Visible;
        }
    }
}
