using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media.Imaging;
using WindowsIconsAdmin.Core.Styling;
using WindowsIconsAdmin_App.Services;

namespace WindowsIconsAdmin_App.ViewModels;

public sealed class AppPreviewItemViewModel : INotifyPropertyChanged
{
    private WriteableBitmap? _styledImageSource;

    public string DisplayName { get; }
    public RgbaCanvas OriginalCanvas { get; }
    public WriteableBitmap OriginalImageSource { get; }

    public WriteableBitmap? StyledImageSource
    {
        get => _styledImageSource;
        private set
        {
            if (!ReferenceEquals(_styledImageSource, value))
            {
                _styledImageSource = value;
                OnPropertyChanged();
            }
        }
    }

    public AppPreviewItemViewModel(string displayName, RgbaCanvas originalCanvas, IconStyle initialStyle)
    {
        DisplayName = displayName;
        OriginalCanvas = originalCanvas;
        OriginalImageSource = RgbaImageHelper.ToWriteableBitmap(originalCanvas);
        UpdateStyled(initialStyle);
    }

    public void UpdateStyled(IconStyle style)
    {
        var styledCanvas = IconStyleRenderer.Render(OriginalCanvas, style, canvasSize: 128);
        StyledImageSource = RgbaImageHelper.ToWriteableBitmap(styledCanvas);
    }

    public static List<AppPreviewItemViewModel> CreateSampleApps(IconStyle style)
    {
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
                    // Green disk #1ED760
                    canvas.SetPixel(x, y, 30, 215, 96, 255);
                }
            }
        }
        // Black wave lines
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
                    // Azure Blue #007ACC
                    canvas.SetPixel(x, y, 0, 122, 204, 255);
                }
            }
        }
        return canvas;
    }

    private static RgbaCanvas CreateDiscordCanvas()
    {
        var canvas = new RgbaCanvas(64, 64);
        var cx = 31.5;
        var cy = 31.5;
        for (var y = 14; y < 50; y++)
        {
            for (var x = 12; x < 52; x++)
            {
                // Blurple #5865F2
                canvas.SetPixel(x, y, 88, 101, 242, 255);
            }
        }
        // White eyes
        for (var y = 28; y < 36; y++)
        {
            for (var x = 20; x < 26; x++) canvas.SetPixel(x, y, 255, 255, 255, 255);
            for (var x = 38; x < 44; x++) canvas.SetPixel(x, y, 255, 255, 255, 255);
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
                if (d2 <= 26 * 26 && d2 > 10 * 10)
                {
                    if (y < 32 && x > 24) canvas.SetPixel(x, y, 234, 67, 53, 255);      // Red
                    else if (x <= 32) canvas.SetPixel(x, y, 52, 168, 83, 255);           // Green
                    else canvas.SetPixel(x, y, 251, 188, 4, 255);                        // Yellow
                }
                else if (d2 <= 10 * 10)
                {
                    canvas.SetPixel(x, y, 66, 133, 244, 255);                            // Blue center
                }
            }
        }
        return canvas;
    }

    private static RgbaCanvas CreateTerminalCanvas()
    {
        var canvas = new RgbaCanvas(64, 64);
        for (var y = 14; y < 50; y++)
        {
            for (var x = 12; x < 52; x++)
            {
                // Dark slate background #1E1E1E
                canvas.SetPixel(x, y, 30, 30, 30, 255);
            }
        }
        // Green prompt >_
        for (var i = 0; i < 6; i++)
        {
            canvas.SetPixel(20 + i, 28 + i, 0, 255, 120, 255);
            canvas.SetPixel(20 + i, 40 - i, 0, 255, 120, 255);
        }
        for (var x = 30; x < 38; x++)
        {
            canvas.SetPixel(x, 40, 0, 255, 120, 255);
        }
        return canvas;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
