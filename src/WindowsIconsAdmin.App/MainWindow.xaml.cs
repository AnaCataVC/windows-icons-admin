using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WindowsIconsAdmin.Core.Layout;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WindowsIconsAdmin_App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    public static new MainWindow Current { get; private set; } = null!;
    public IntPtr Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(this);

    private const int DesiredWidthDip = 1160;
    private const int DesiredHeightDip = 720;
    private const int MinWidthDip = 800;
    private const int MinHeightDip = 520;

    public MainWindow()
    {
        Current = this;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        ApplyInitialSize();

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    private void ApplyInitialSize()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var dpiScale = GetDpiForWindow(Hwnd) / 96.0;

        var size = WindowSizing.ComputeInitialSize(
            new SizePx(DesiredWidthDip, DesiredHeightDip),
            new SizePx(MinWidthDip, MinHeightDip),
            new SizePx(workArea.Width, workArea.Height),
            dpiScale);

        AppWindow.Resize(new Windows.Graphics.SizeInt32(size.Width, size.Height));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
