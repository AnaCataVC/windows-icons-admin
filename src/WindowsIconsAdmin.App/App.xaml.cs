using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.IO;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WindowsIconsAdmin_App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    public static Microsoft.UI.Dispatching.DispatcherQueue? AppDispatcherQueue { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        SetupDiagnostics();
        this.UnhandledException += OnAppUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        AppDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _window = new MainWindow();
        _window.Activate();
    }

    private static void SetupDiagnostics()
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsIconsAdmin", "Logs");
            Directory.CreateDirectory(logDir);
            var startupLog = Path.Combine(logDir, "startup_diagnostic.log");
            File.AppendAllText(startupLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Application starting up.{Environment.NewLine}");
        }
        catch { }
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsIconsAdmin", "Logs");
            Directory.CreateDirectory(logDir);
            var crashLog = Path.Combine(logDir, "crash.log");
            var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex?.GetType().FullName}: {ex?.Message}{Environment.NewLine}{ex?.StackTrace}{Environment.NewLine}";
            if (ex?.InnerException != null)
            {
                message += $"Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}{Environment.NewLine}{ex.InnerException.StackTrace}{Environment.NewLine}";
            }
            File.AppendAllText(crashLog, message);
        }
        catch { }
    }

    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogCrash("App.UnhandledException", e.Exception);
    }

    private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
    }

    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
    }
}
