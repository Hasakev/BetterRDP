using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Velopack;
using Velopack.Sources;
// VelopackApp.Build().Run() lives in Program.cs — it has to precede the WinUI runtime.

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BetterRdp_App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>The GitHub repo that releases are published to and updated from.</summary>
    private const string RepoUrl = "https://github.com/Hasakev/BetterRDP";

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();

        _ = CheckForUpdatesAsync();
    }

    /// <summary>
    /// Downloads any newer GitHub release in the background and stages it to be
    /// applied once the user quits, so an update never interrupts a live session
    /// or the mstsc windows it launched. No-op outside a Velopack install
    /// (dev builds, the plain unpacked zip), and best-effort: a machine that is
    /// offline or rate-limited just keeps running the version it has.
    /// </summary>
    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
            if (!manager.IsInstalled)
            {
                return;
            }

            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
            {
                return;
            }

            await manager.DownloadUpdatesAsync(update);
            manager.WaitExitThenApplyUpdates(update);
        }
        catch
        {
            // Updating is optional; never let it take the app down.
        }
    }
}
