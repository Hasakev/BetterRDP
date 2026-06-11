using BetterRdp.Core;
using BetterRdp_App.Dialogs;
using BetterRdp_App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BetterRdp_App;

/// <summary>
/// The launch surface. Unlocks the Vault on load, then shows the server list + connection
/// card. All domain work goes through <see cref="MainViewModel"/> / AppService; this
/// code-behind only owns the dialogs (which need a XamlRoot) and error surfacing.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        var service = await UnlockAsync();
        if (service is null)
        {
            App.Window.Close(); // user cancelled the unlock
            return;
        }
        ViewModel.Load(service);
    }

    /// <summary>Prompt for the Master Password and return an unlocked service, or null if the
    /// user cancelled. Creates the Vault on first run; re-prompts on a wrong password.</summary>
    private async Task<AppService?> UnlockAsync()
    {
        var path = AppService.DefaultVaultPath();
        var firstRun = !File.Exists(path);

        var dialog = new MasterPasswordDialog { XamlRoot = XamlRoot };
        if (firstRun)
            dialog.SetFirstRun();

        while (true)
        {
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return null;
            if (dialog.Password.Length == 0)
                continue;
            try
            {
                return AppService.OpenOrCreate(path, dialog.Password);
            }
            catch (Exception)
            {
                dialog.ShowError("That Master Password did not unlock the vault.");
            }
        }
    }

    private async void OnLaunch(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Launching…";
        var error = await ViewModel.LaunchAsync();
        StatusText.Text = "";
        if (error is not null)
            await ShowMessageAsync("Launch failed", error);
    }

    private async void OnAddServer(object sender, RoutedEventArgs e)
    {
        var dialog = new ServerDialog { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is { } server)
            ViewModel.AddServer(server);
    }

    private async void OnAddCredential(object sender, RoutedEventArgs e)
    {
        var dialog = new CredentialDialog { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is { } cred)
            ViewModel.AddCredential(cred);
    }

    private async void OnAddProfile(object sender, RoutedEventArgs e)
    {
        var dialog = new ProfileDialog { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is { } profile)
            ViewModel.AddProfile(profile);
    }

    private Task ShowMessageAsync(string title, string message)
        => new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK",
        }.ShowAsync().AsTask();
}
