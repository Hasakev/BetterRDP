// The single view model behind MainPage. Thin over BetterRdp.Core.AppService: it holds the
// observable collections the UI renders and the current selection, and forwards every
// domain action (add, launch) to the service. Dialogs and error surfacing live in the View
// (MainPage.xaml.cs) because they need a XamlRoot; this stays UI-framework-agnostic.

using System.Collections.ObjectModel;
using BetterRdp.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BetterRdp_App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private AppService _service = null!;

    public ObservableCollection<Server> Servers { get; } = [];
    public ObservableCollection<Credential> Credentials { get; } = [];
    public ObservableCollection<DisplayProfile> Profiles { get; } = [];

    // Field-based [ObservableProperty]. The partial-property form is the AOT-clean WinUI
    // pattern, but this toolkit version's generator won't emit its implementation part
    // (CS9248). MVVMTK0045 here is an AOT-only advisory and harmless for this (non-AOT) app.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(HasSelectedServer))]
    private Server? selectedServer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(HasSelectedCredential))]
    private Credential? selectedCredential;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(HasSelectedProfile))]
    private DisplayProfile? selectedProfile;

    /// <summary>Launch is only possible once a server, a credential and a profile are all chosen.</summary>
    public bool CanLaunch => SelectedServer is not null && SelectedCredential is not null && SelectedProfile is not null;

    // Drive the enabled state of the per-section Edit/Delete buttons.
    public bool HasSelectedServer => SelectedServer is not null;
    public bool HasSelectedCredential => SelectedCredential is not null;
    public bool HasSelectedProfile => SelectedProfile is not null;

    /// <summary>Hand the unlocked service to the view model and do the first populate.</summary>
    public void Load(AppService service)
    {
        _service = service;
        ReloadPickers();
        ReloadServers();
    }

    public void ReloadServers()
    {
        var keep = SelectedServer?.Name;
        Servers.Clear();
        foreach (var s in _service.Servers())
            Servers.Add(s);
        SelectedServer = Servers.FirstOrDefault(s => s.Name == keep) ?? Servers.FirstOrDefault();
    }

    public void ReloadPickers()
    {
        var keepCred = SelectedCredential?.Id;
        var keepProfile = SelectedProfile?.Name;

        Credentials.Clear();
        foreach (var c in _service.Credentials())
            Credentials.Add(c);
        Profiles.Clear();
        foreach (var p in _service.Profiles())
            Profiles.Add(p);

        SelectedCredential = Credentials.FirstOrDefault(c => c.Id == keepCred) ?? Credentials.FirstOrDefault();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Name == keepProfile) ?? Profiles.FirstOrDefault();
        ApplyServerDefaults();
    }

    // When the selected server changes, default the pickers to whatever it last launched with.
    partial void OnSelectedServerChanged(Server? value) => ApplyServerDefaults();

    private void ApplyServerDefaults()
    {
        if (SelectedServer is not { } server)
            return;
        if (server.LastCredentialId is { } cid &&
            Credentials.FirstOrDefault(c => c.Id == cid) is { } cred)
            SelectedCredential = cred;
        if (server.LastProfileName is { } pname &&
            Profiles.FirstOrDefault(p => p.Name == pname) is { } profile)
            SelectedProfile = profile;
    }

    /// <summary>Launch the selected connection on a background thread (mstsc blocks until the
    /// session closes). Returns an error message on failure, or null on success.</summary>
    public async Task<string?> LaunchAsync()
    {
        if (!CanLaunch)
            return null;
        var serverName = SelectedServer!.Name;
        var credentialId = SelectedCredential!.Id;
        var profileName = SelectedProfile!.Name;
        try
        {
            await Task.Run(() => _service.Launch(serverName, credentialId, profileName));
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        ReloadServers(); // last-used may have changed
        return null;
    }

    public void AddServer(Server server)
    {
        _service.AddServer(server);
        ReloadServers();
    }

    public void AddCredential(Credential credential)
    {
        _service.AddCredential(credential);
        ReloadPickers();
    }

    public void AddProfile(DisplayProfile profile)
    {
        _service.AddProfile(profile);
        ReloadPickers();
    }

    public void EditServer(string originalName, Server server)
    {
        _service.EditServer(originalName, server);
        ReloadServers();
    }

    public void RemoveServer(string name)
    {
        _service.RemoveServer(name);
        ReloadServers();
    }

    public void EditCredential(string originalId, Credential credential)
    {
        _service.EditCredential(originalId, credential);
        ReloadPickers();
    }

    public void RemoveCredential(string id)
    {
        _service.RemoveCredential(id);
        ReloadPickers();
    }

    public void EditProfile(string originalName, DisplayProfile profile)
    {
        _service.EditProfile(originalName, profile);
        ReloadPickers();
    }

    public void RemoveProfile(string name)
    {
        _service.RemoveProfile(name);
        ReloadPickers();
    }
}
