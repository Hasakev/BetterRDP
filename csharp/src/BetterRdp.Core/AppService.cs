// Application service — the C# port of src/better_rdp/app.py.
//
// The single seam between the GUI (WinUI) and the domain. The view never touches the
// Vault, rdp text, or mstsc directly — it goes through an AppService. That keeps the
// orchestration (decrypt password -> generate .rdp -> launch -> remember last-used)
// testable without spinning up WinUI or mstsc.

namespace BetterRdp.Core;

public sealed class AppService
{
    /// <summary>Default vault location: <c>%APPDATA%/BetterRDP/vault.json</c>.</summary>
    public static string DefaultVaultPath()
        => throw new NotImplementedException();

    /// <summary>Open the Vault at <paramref name="path"/> if it exists, else create a fresh
    /// one keyed by <paramref name="master"/>. Opening with the wrong master fails loudly.</summary>
    public static AppService OpenOrCreate(string path, string master)
        => throw new NotImplementedException();

    public IReadOnlyList<Server> Servers()
        => throw new NotImplementedException();

    /// <summary>Credentials without plaintext passwords — safe to hold in the UI layer.</summary>
    public IReadOnlyList<Credential> Credentials()
        => throw new NotImplementedException();

    public IReadOnlyList<DisplayProfile> Profiles()
        => throw new NotImplementedException();

    public void AddServer(Server server)
        => throw new NotImplementedException();

    public void AddCredential(Credential credential)
        => throw new NotImplementedException();

    public void AddProfile(DisplayProfile profile)
        => throw new NotImplementedException();

    public void Save()
        => throw new NotImplementedException();

    /// <summary>Launch a Connection: decrypt the Credential, generate + run a temp .rdp,
    /// then record this Credential/Profile as the Server's new defaults.</summary>
    public object Launch(string serverName, string credentialId, string profileName, MstscRunner? runner = null)
        => throw new NotImplementedException();
}
