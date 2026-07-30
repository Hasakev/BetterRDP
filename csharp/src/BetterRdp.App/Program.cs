using Velopack;

namespace BetterRdp_App;

/// <summary>
/// Replaces the XAML-generated entry point (see DISABLE_XAML_GENERATED_MAIN in the
/// .csproj) so Velopack's install / update / uninstall hooks run before the WinUI
/// runtime starts. Those hooks re-run this exe with reserved arguments and exit the
/// process; handling them from the <see cref="App"/> constructor instead would mean
/// spinning up XAML first, which Velopack warns against.
/// </summary>
public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(p =>
        {
            var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(
                new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(queue));
            _ = new App();
        });
    }
}
