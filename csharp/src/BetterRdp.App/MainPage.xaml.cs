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

    private async void OnEditServer(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedServer is not { } server)
            return;
        var dialog = new ServerDialog { XamlRoot = XamlRoot };
        dialog.LoadForEdit(server);
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is { } updated)
            ViewModel.EditServer(server.Name, updated);
    }

    private async void OnDeleteServer(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedServer is not { } server)
            return;
        if (await ConfirmDeleteAsync("server", server.Name))
            ViewModel.RemoveServer(server.Name);
    }

    private async void OnEditCredential(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedCredential is not { } cred)
            return;
        var dialog = new CredentialDialog { XamlRoot = XamlRoot };
        dialog.LoadForEdit(cred);
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is { } updated)
            ViewModel.EditCredential(cred.Id, updated);
    }

    private async void OnDeleteCredential(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedCredential is not { } cred)
            return;
        if (await ConfirmDeleteAsync("credential", cred.Id))
            ViewModel.RemoveCredential(cred.Id);
    }

    private async void OnEditProfile(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedProfile is not { } profile)
            return;
        var dialog = new ProfileDialog { XamlRoot = XamlRoot };
        dialog.LoadForEdit(profile);
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is { } updated)
            ViewModel.EditProfile(profile.Name, updated);
    }

    private async void OnDeleteProfile(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedProfile is not { } profile)
            return;
        if (await ConfirmDeleteAsync("display profile", profile.Name))
            ViewModel.RemoveProfile(profile.Name);
    }

    private async Task<bool> ConfirmDeleteAsync(string kind, string name)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Delete {kind}?",
            Content = $"“{name}” will be removed. This can't be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
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
