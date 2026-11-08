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
            // WinUI 3 WriteableBitmap expects premultiplied BGRA (B, G, R, A)
            var bgra = new byte[canvas.Pixels.Length];
            for (var i = 0; i < canvas.Pixels.Length; i += 4)
            {
                var r = canvas.Pixels[i];
                var g = canvas.Pixels[i + 1];
                var b = canvas.Pixels[i + 2];
                var a = canvas.Pixels[i + 3];

                if (a == 255)
                {
                    bgra[i]     = b;
                    bgra[i + 1] = g;
                    bgra[i + 2] = r;
                    bgra[i + 3] = 255;
                }
                else if (a == 0)
                {
                    bgra[i]     = 0;
                    bgra[i + 1] = 0;
                    bgra[i + 2] = 0;
                    bgra[i + 3] = 0;
                }
                else
                {
                    var aNorm = a / 255.0;
                    bgra[i]     = (byte)Math.Clamp((int)Math.Round(b * aNorm), 0, 255);
                    bgra[i + 1] = (byte)Math.Clamp((int)Math.Round(g * aNorm), 0, 255);
                    bgra[i + 2] = (byte)Math.Clamp((int)Math.Round(r * aNorm), 0, 255);
                    bgra[i + 3] = a;
                }
            }
            stream.Write(bgra, 0, bgra.Length);
        }
        bitmap.Invalidate();
        return bitmap;
    }
}
