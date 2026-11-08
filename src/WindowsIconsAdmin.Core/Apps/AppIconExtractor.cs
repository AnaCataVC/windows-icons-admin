using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WindowsIconsAdmin.Core.Styling;

namespace WindowsIconsAdmin.Core.Apps;

public static class AppIconExtractor
{
    [DllImport("user32.dll", EntryPoint = "PrivateExtractIconsW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PrivateExtractIconsW(
        string szFileName,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        IntPtr[]? phicon,
        uint[]? piconid,
        uint nIcons,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetDIBits(
        IntPtr hdc,
        IntPtr hbm,
        uint uStartScan,
        uint cScanLines,
        byte[]? lpvBits,
        ref BITMAPINFO lpbi,
        uint uUsage);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbSizeFileInfo,
        uint uFlags);

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public uint xHotspot;
        public uint yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    /// <summary>
    /// Extracts the highest resolution 32-bit ARGB icon canvas available for the given application.
    /// Supports .ico, .exe, .dll, negative resource IDs, environment variables, and unboxes white plates if requested.
    /// </summary>
    public static RgbaCanvas ExtractAppIcon(InstalledApp app, bool unboxWhitePlate = true)
    {
        ArgumentNullException.ThrowIfNull(app);

        // 1. Gather all potential sources
        var candidates = new List<(string FilePath, int Index)>();

        void TryAdd(string? path, int index)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim('"', ' '));
            // Skip paths pointing to our own rendered icons cache to avoid "inception"
            if (expanded.Contains("WindowsIconsAdmin\\RenderedIcons", StringComparison.OrdinalIgnoreCase)) return;

            if (File.Exists(expanded))
            {
                candidates.Add((expanded, index));
            }
        }

        TryAdd(app.OriginalIconSource, 0);

        foreach (var t in app.Targets)
        {
            TryAdd(t.CurrentIconLocation, t.CurrentIconIndex);
        }

        TryAdd(app.ExecutablePath, 0);

        // 2. Extract best icon via PrivateExtractIconsW
        RgbaCanvas? bestCanvas = null;

        foreach (var (file, idx) in candidates)
        {
            // Try extracting larger sizes first: 256, 128, 64, 48, 32
            foreach (var reqSize in new[] { 256, 128, 64, 48, 32 })
            {
                var hIcons = new IntPtr[1];
                var ids = new uint[1];
                uint count = PrivateExtractIconsW(file, idx, reqSize, reqSize, hIcons, ids, 1, 0);
                if (count > 0 && hIcons[0] != IntPtr.Zero)
                {
                    try
                    {
                        var canvas = ExtractHIconToCanvas(hIcons[0]);
                        if (canvas != null && (bestCanvas == null || canvas.Width > bestCanvas.Width))
                        {
                            bestCanvas = canvas;
                            if (canvas.Width >= 256) break; // Reached max resolution
                        }
                    }
                    finally
                    {
                        DestroyIcon(hIcons[0]);
                    }
                }
            }

            if (bestCanvas != null && bestCanvas.Width >= 128)
            {
                break;
            }
        }

        // 3. Fallback to Shell Large Icon if nothing extracted
        if (bestCanvas == null)
        {
            foreach (var (file, _) in candidates)
            {
                var shinfo = new SHFILEINFO();
                var res = SHGetFileInfo(file, 0, ref shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
                if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        bestCanvas = ExtractHIconToCanvas(shinfo.hIcon);
                        if (bestCanvas != null) break;
                    }
                    finally
                    {
                        DestroyIcon(shinfo.hIcon);
                    }
                }
            }
        }

        // 4. Ultimate fallback: clean geometric emblem
        if (bestCanvas == null)
        {
            bestCanvas = GenerateFallbackEmblem(app.DisplayName);
        }

        // 5. Unbox white plate if applicable (for icons like Antigravity)
        if (unboxWhitePlate)
        {
            bestCanvas = UnboxWhitePlateIfApplicable(bestCanvas);
        }

        return bestCanvas;
    }

    public static RgbaCanvas? ExtractHIconToCanvas(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero) return null;
        if (!GetIconInfo(hIcon, out var info)) return null;

        try
        {
            if (info.hbmColor == IntPtr.Zero) return null;

            var hdc = CreateCompatibleDC(IntPtr.Zero);
            try
            {
                var bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
                if (GetDIBits(hdc, info.hbmColor, 0, 0, null, ref bmi, 0) == 0)
                    return null;

                int width = bmi.bmiHeader.biWidth;
                int height = Math.Abs(bmi.bmiHeader.biHeight);
                if (width <= 0 || height <= 0) return null;

                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height; // top-down
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0;

                byte[] bgra = new byte[width * height * 4];
                if (GetDIBits(hdc, info.hbmColor, 0, (uint)height, bgra, ref bmi, 0) == 0)
                    return null;

                bool hasAlpha = false;
                for (int i = 3; i < bgra.Length; i += 4)
                {
                    if (bgra[i] != 0)
                    {
                        hasAlpha = true;
                        break;
                    }
                }

                if (!hasAlpha && info.hbmMask != IntPtr.Zero)
                {
                    var maskBmi = new BITMAPINFO();
                    maskBmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
                    maskBmi.bmiHeader.biWidth = width;
                    maskBmi.bmiHeader.biHeight = -height;
                    maskBmi.bmiHeader.biPlanes = 1;
                    maskBmi.bmiHeader.biBitCount = 32;
                    maskBmi.bmiHeader.biCompression = 0;

                    byte[] maskBits = new byte[width * height * 4];
                    if (GetDIBits(hdc, info.hbmMask, 0, (uint)height, maskBits, ref maskBmi, 0) != 0)
                    {
                        for (int i = 0; i < bgra.Length; i += 4)
                        {
                            bgra[i + 3] = maskBits[i] == 0 ? (byte)255 : (byte)0;
                        }
                    }
                    else
                    {
                        for (int i = 3; i < bgra.Length; i += 4) bgra[i] = 255;
                    }
                }

                // Convert BGRA to RGBA
                byte[] rgba = new byte[bgra.Length];
                for (int i = 0; i < bgra.Length; i += 4)
                {
                    rgba[i]     = bgra[i + 2];
                    rgba[i + 1] = bgra[i + 1];
                    rgba[i + 2] = bgra[i];
                    rgba[i + 3] = bgra[i + 3];
                }

                return new RgbaCanvas(width, height, rgba);
            }
            finally
            {
                DeleteDC(hdc);
            }
        }
        finally
        {
            if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
            if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
        }
    }

    /// <summary>
    /// Removes outer white squircle/square plate if the icon is a colorful logo trapped inside a pure white background (e.g. Antigravity).
    /// </summary>
    public static RgbaCanvas UnboxWhitePlateIfApplicable(RgbaCanvas source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var (minX, minY, maxX, maxY) = IconNormalizer.GetEffectiveBounds(source);
        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        if (w < 32 || h < 32) return source;

        double aspect = (double)w / h;
        if (aspect < 0.88 || aspect > 1.12) return source;

        int inset = Math.Max(4, w / 10);
        var topMid = source.GetPixel(minX + w / 2, minY + inset);
        var botMid = source.GetPixel(minX + w / 2, maxY - inset);
        var leftMid = source.GetPixel(minX + inset, minY + h / 2);
        var rightMid = source.GetPixel(maxX - inset, minY + h / 2);

        static bool IsNearWhite((byte R, byte G, byte B, byte A) p) =>
            p.A > 220 && p.R >= 240 && p.G >= 240 && p.B >= 240;

        if (!IsNearWhite(topMid) || !IsNearWhite(botMid) || !IsNearWhite(leftMid) || !IsNearWhite(rightMid))
            return source;

        int opaqueCount = 0;
        int colorfulCount = 0;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var p = source.GetPixel(x, y);
                if (p.A >= 128)
                {
                    opaqueCount++;
                    if (p.R < 225 || p.G < 225 || p.B < 225)
                        colorfulCount++;
                }
            }
        }

        if (opaqueCount == 0) return source;
        double fillRatio = (double)opaqueCount / (w * h);
        double colorRatio = (double)colorfulCount / opaqueCount;

        if (fillRatio < 0.84 || colorRatio < 0.12 || colorRatio > 0.75)
            return source;

        var result = source.Clone();
        var visited = new bool[source.Width * source.Height];
        var queue = new Queue<(int X, int Y)>();

        void TrySeed(int sx, int sy)
        {
            if (sx >= 0 && sx < source.Width && sy >= 0 && sy < source.Height)
            {
                int idx = sy * source.Width + sx;
                if (!visited[idx])
                {
                    visited[idx] = true;
                    queue.Enqueue((sx, sy));
                }
            }
        }

        for (int x = minX; x <= maxX; x++)
        {
            TrySeed(x, minY);
            TrySeed(x, maxY);
            TrySeed(x, minY + inset);
            TrySeed(x, maxY - inset);
        }
        for (int y = minY; y <= maxY; y++)
        {
            TrySeed(minX, y);
            TrySeed(maxX, y);
            TrySeed(minX + inset, y);
            TrySeed(maxX - inset, y);
        }

        int[] dx = [1, -1, 0, 0];
        int[] dy = [0, 0, 1, -1];

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            var p = result.GetPixel(cx, cy);

            int dist = Math.Max(255 - p.R, Math.Max(255 - p.G, 255 - p.B));
            if (p.A < 32 || dist <= 45)
            {
                if (dist <= 12)
                {
                    result.SetPixel(cx, cy, 0, 0, 0, 0);
                }
                else
                {
                    byte newAlpha = (byte)Math.Clamp((dist - 12) * 255 / 33, 0, p.A);
                    double aRatio = newAlpha / 255.0;
                    byte nr = (byte)Math.Clamp((p.R - 255 * (1.0 - aRatio)) / Math.Max(0.15, aRatio), 0, 255);
                    byte ng = (byte)Math.Clamp((p.G - 255 * (1.0 - aRatio)) / Math.Max(0.15, aRatio), 0, 255);
                    byte nb = (byte)Math.Clamp((p.B - 255 * (1.0 - aRatio)) / Math.Max(0.15, aRatio), 0, 255);
                    result.SetPixel(cx, cy, nr, ng, nb, newAlpha);
                }

                if (dist <= 28)
                {
                    for (int d = 0; d < 4; d++)
                    {
                        TrySeed(cx + dx[d], cy + dy[d]);
                    }
                }
            }
        }

        return result;
    }

    private static RgbaCanvas GenerateFallbackEmblem(string displayName)
    {
        const int size = 128;
        var canvas = new RgbaCanvas(size, size);
        var seed = (uint)displayName.GetHashCode();
        var r = (byte)(70 + (seed & 0x7F));
        var g = (byte)(70 + ((seed >> 7) & 0x7F));
        var b = (byte)(70 + ((seed >> 14) & 0x7F));

        var cx = (size - 1) / 2.0;
        var cy = (size - 1) / 2.0;
        var rad = size * 0.40;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - cx) / rad;
                var dy = (y - cy) / rad;
                if (dx * dx + dy * dy <= 1.0)
                {
                    canvas.SetPixel(x, y, r, g, b, 255);
                }
            }
        }

        // Inner white dot
        var innerRad = rad * 0.40;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - cx) / innerRad;
                var dy = (y - cy) / innerRad;
                if (dx * dx + dy * dy <= 1.0)
                {
                    canvas.SetPixel(x, y, 255, 255, 255, 240);
                }
            }
        }

        return canvas;
    }
}
