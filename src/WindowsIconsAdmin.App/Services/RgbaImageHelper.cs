using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using WindowsIconsAdmin.Core.Styling;

namespace WindowsIconsAdmin_App.Services;

public static class RgbaImageHelper
{
    public static WriteableBitmap ToWriteableBitmap(RgbaCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var bitmap = new WriteableBitmap(canvas.Width, canvas.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            // WinUI 3 WriteableBitmap expects BGRA (B, G, R, A)
            var bgra = new byte[canvas.Pixels.Length];
            for (var i = 0; i < canvas.Pixels.Length; i += 4)
            {
                bgra[i] = canvas.Pixels[i + 2];     // B
                bgra[i + 1] = canvas.Pixels[i + 1]; // G
                bgra[i + 2] = canvas.Pixels[i];     // R
                bgra[i + 3] = canvas.Pixels[i + 3]; // A
            }
            stream.Write(bgra, 0, bgra.Length);
        }
        bitmap.Invalidate();
        return bitmap;
    }
}
