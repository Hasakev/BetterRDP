// Application service — the C# port of src/better_rdp/app.py.
//
// The single seam between the GUI (WinUI) and the domain. The view never touches the
// Vault, rdp text, or mstsc directly — it goes through an AppService. That keeps the
// orchestration (decrypt password -> generate .rdp -> launch -> remember last-used)
// testable without spinning up WinUI or mstsc.

namespace BetterRdp.Core;

public sealed class AppService
{
    private readonly Vault _vault;

    private AppService(Vault vault) => _vault = vault;

    /// <summary>Default vault location: <c>%APPDATA%/BetterRDP/vault.json</c>.</summary>
    public static string DefaultVaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(appData, "BetterRDP", "vault.json");
    }

    /// <summary>Open the Vault at <paramref name="path"/> if it exists, else create a fresh
    /// one keyed by <paramref name="master"/>. Opening with the wrong master fails loudly.</summary>
    public static AppService OpenOrCreate(string path, string master)
    {
        if (File.Exists(path))
            return new AppService(Vault.Open(path, master));
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        return new AppService(Vault.Create(path, master));
    }

    public IReadOnlyList<Server> Servers() => _vault.Servers();

    /// <summary>Credentials without plaintext passwords — safe to hold in the UI layer.</summary>
    public IReadOnlyList<Credential> Credentials() => _vault.Credentials();

    public IReadOnlyList<DisplayProfile> Profiles() => _vault.Profiles();

    public void AddServer(Server server)
    {
        _vault.AddServer(server);
        _vault.Save();
    }

    public void AddCredential(Credential credential)
    {
        _vault.AddCredential(credential);
        _vault.Save();
    }

    public void AddProfile(DisplayProfile profile)
    {
        _vault.AddProfile(profile);
        _vault.Save();
    }

    public void Save() => _vault.Save();

    /// <summary>Launch a Connection: decrypt the Credential, generate + run a temp .rdp,
    /// then record this Credential/Profile as the Server's new defaults.</summary>
    public object Launch(string serverName, string credentialId, string profileName, MstscRunner? runner = null)
    {
        var server = Servers().FirstOrDefault(s => s.Name == serverName)
                     ?? throw new KeyNotFoundException($"name={serverName}");
        var profile = Profiles().FirstOrDefault(p => p.Name == profileName)
                      ?? throw new KeyNotFoundException($"name={profileName}");
        var listed = Credentials().FirstOrDefault(c => c.Id == credentialId)
                     ?? throw new KeyNotFoundException($"id={credentialId}");

        // Re-attach the decrypted password just for this launch.
        var password = _vault.GetPassword(credentialId);
        var cred = listed with { Password = password };

        var result = Launcher.Launch(server, cred, profile, password, runner);

        server.LastCredentialId = credentialId;
        server.LastProfileName = profileName;
        _vault.UpdateServer(server);
        _vault.Save();
        return result;
    }
}
