using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media.Imaging;
using WindowsIconsAdmin.Core.Apps;
using WindowsIconsAdmin.Core.Styling;
using WindowsIconsAdmin_App.Services;

namespace WindowsIconsAdmin_App.ViewModels;

public sealed class AppPreviewItemViewModel : INotifyPropertyChanged
{
    private WriteableBitmap? _styledImageSource;
    private WriteableBitmap? _originalImageSource;

    public string AppName { get; }
    public string DisplayName => AppName;
    public RgbaCanvas OriginalCanvas { get; }

    public WriteableBitmap? StyledImageSource
    {
        get => _styledImageSource;
        private set
        {
            _styledImageSource = value;
            OnPropertyChanged();
        }
    }

    public WriteableBitmap? OriginalImageSource
    {
        get => _originalImageSource;
        private set
        {
            _originalImageSource = value;
            OnPropertyChanged();
        }
    }

    public AppPreviewItemViewModel(string appName, RgbaCanvas originalCanvas, IconStyle currentStyle)
    {
        AppName = appName;
        OriginalCanvas = originalCanvas;
        OriginalImageSource = RgbaImageHelper.ToWriteableBitmap(originalCanvas);
        UpdateStyle(currentStyle);
    }

    public void UpdateStyle(IconStyle style)
    {
        var styledCanvas = IconStyleRenderer.Render(OriginalCanvas, style, canvasSize: 128, enableEffects: true);
        StyledImageSource = RgbaImageHelper.ToWriteableBitmap(styledCanvas);
    }

    public void UpdateStyled(IconStyle style) => UpdateStyle(style);

    public static List<AppPreviewItemViewModel> CreateSampleApps(IconStyle style)
    {
        // Try discovering real apps from Start Menu to showcase actual icons
        try
        {
            var discovery = new AppDiscoveryService();
            var realApps = discovery.DiscoverInstalledApps();

            var preferredNames = new[] { "Visual Studio Code", "Code", "Discord", "Spotify", "Google Chrome", "Chrome", "Firefox", "Terminal", "Windows Terminal", "Android Studio", "Antigravity", "Access" };
            var selectedSamples = new List<AppPreviewItemViewModel>();

            foreach (var pref in preferredNames)
            {
                var match = realApps.FirstOrDefault(a => a.DisplayName.Contains(pref, StringComparison.OrdinalIgnoreCase));
                if (match != null && !selectedSamples.Any(s => s.AppName.Equals(match.DisplayName, StringComparison.OrdinalIgnoreCase)))
                {
                    var canvas = AppIconExtractor.ExtractAppIcon(match, unboxWhitePlate: true);
                    selectedSamples.Add(new AppPreviewItemViewModel(match.DisplayName, canvas, style));
                    if (selectedSamples.Count >= 5) break;
                }
            }

            if (selectedSamples.Count >= 3)
            {
                return selectedSamples;
            }

            // Fill with first available real apps
            foreach (var app in realApps)
            {
                if (!selectedSamples.Any(s => s.AppName.Equals(app.DisplayName, StringComparison.OrdinalIgnoreCase)))
                {
                    var canvas = AppIconExtractor.ExtractAppIcon(app, unboxWhitePlate: true);
                    selectedSamples.Add(new AppPreviewItemViewModel(app.DisplayName, canvas, style));
                    if (selectedSamples.Count >= 5) break;
                }
            }

            if (selectedSamples.Count > 0)
            {
                return selectedSamples;
            }
        }
        catch
        {
            // Fallback below
        }

        return
        [
            new AppPreviewItemViewModel("Spotify", CreateSpotifyCanvas(), style),
            new AppPreviewItemViewModel("VS Code", CreateVsCodeCanvas(), style),
            new AppPreviewItemViewModel("Discord", CreateDiscordCanvas(), style),
            new AppPreviewItemViewModel("Chrome", CreateChromeCanvas(), style),
            new AppPreviewItemViewModel("Terminal", CreateTerminalCanvas(), style)
        ];
    }

    private static RgbaCanvas CreateSpotifyCanvas()
    {
        var canvas = new RgbaCanvas(64, 64);
        var cx = 31.5;
        var cy = 31.5;
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d2 <= 26 * 26)
                {
                    canvas.SetPixel(x, y, 30, 215, 96, 255);
                }
            }
        }
        for (var x = 18; x < 46; x++)
        {
            canvas.SetPixel(x, 24, 0, 0, 0, 255);
            canvas.SetPixel(x, 32, 0, 0, 0, 255);
            canvas.SetPixel(x, 40, 0, 0, 0, 255);
        }
        return canvas;
    }

    private static RgbaCanvas CreateVsCodeCanvas()
    {
        var canvas = new RgbaCanvas(64, 64);
        for (var y = 12; y < 52; y++)
        {
            for (var x = 14; x < 50; x++)
            {
                if ((x + y > 35) && (x - y < 25))
                {
                    canvas.SetPixel(x, y, 0, 122, 204, 255);
                }
            }
        }
        return canvas;
    }

    private static RgbaCanvas CreateDiscordCanvas()
    {
        var canvas = new RgbaCanvas(64, 64);
        for (var y = 14; y < 50; y++)
        {
            for (var x = 12; x < 52; x++)
            {
                canvas.SetPixel(x, y, 88, 101, 242, 255);
            }
        }
        return canvas;
    }

    private static RgbaCanvas CreateChromeCanvas()
    {
        var canvas = new RgbaCanvas(64, 64);
        var cx = 31.5;
        var cy = 31.5;
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d2 <= 24 * 24)
                {
                    if (y < 25) canvas.SetPixel(x, y, 234, 67, 53, 255);
                    else if (x > 32) canvas.SetPixel(x, y, 251, 188, 5, 255);
                    else canvas.SetPixel(x, y, 52, 168, 83, 255);
                }
            }
        }
        return canvas;
    }

    private static RgbaCanvas CreateTerminalCanvas()
    {
        var canvas = new RgbaCanvas(64, 64);
        for (var y = 10; y < 54; y++)
        {
            for (var x = 8; x < 56; x++)
            {
                canvas.SetPixel(x, y, 12, 12, 12, 255);
            }
        }
        for (var i = 0; i < 10; i++)
        {
            canvas.SetPixel(16 + i, 24 + i, 255, 255, 255, 255);
            canvas.SetPixel(16 + i, 42 - i, 255, 255, 255, 255);
        }
        return canvas;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
