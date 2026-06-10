// Port of tests/test_app_service.py — the orchestration layer between the GUI and Vault.
// AppService is the only thing the WinUI window talks to: it lists Servers / Credentials /
// Display Profiles, launches a Connection, and remembers the last Credential + Profile used
// per Server. A fake mstsc runner keeps anything from spawning.

using BetterRdp.Core;

namespace BetterRdp.Core.Tests;

public class AppServiceTests
{
    private static string NewVaultPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "betterrdp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "vault.json");
    }

    private static void Seed(AppService svc)
    {
        svc.AddCredential(new Credential("alice", "alice", Password: "pw-alice"));
        svc.AddCredential(new Credential("bob", "bob", Password: "pw-bob"));
        svc.AddProfile(new DisplayProfile { Name = "Dynamic", Mode = DisplayMode.WindowedDynamic });
        svc.AddServer(new Server { Name = "Prod", Address = "prod01.intranet.local" });
    }

    [Fact]
    public void OpenOrCreate_creates_then_opens()
    {
        var path = NewVaultPath();
        var created = AppService.OpenOrCreate(path, "master");
        created.AddCredential(new Credential("c1", "alice", Password: "pw"));
        created.Save();
        Assert.True(File.Exists(path));

        var reopened = AppService.OpenOrCreate(path, "master");
        Assert.Equal(["c1"], reopened.Credentials().Select(c => c.Id));
    }

    [Fact]
    public void Lists_expose_what_the_gui_renders()
    {
        var svc = AppService.OpenOrCreate(NewVaultPath(), "master");
        Seed(svc);
        Assert.Equal(["Prod"], svc.Servers().Select(s => s.Name));
        Assert.Equal(["alice", "bob"], svc.Credentials().Select(c => c.Id));
        Assert.Equal(["Dynamic"], svc.Profiles().Select(p => p.Name));
        // Credentials handed to the GUI must not carry plaintext passwords around.
        Assert.All(svc.Credentials(), c => Assert.Null(c.Password));
    }

    [Fact]
    public void Launch_passes_decrypted_password_into_the_rdp()
    {
        var svc = AppService.OpenOrCreate(NewVaultPath(), "master");
        Seed(svc);
        string text = "";

        object Runner(IReadOnlyList<string> argv)
        {
            text = File.ReadAllText(argv[1]);
            return 0;
        }

        svc.Launch("Prod", "bob", "Dynamic", Runner);

        Assert.Contains("username:s:bob", text);
        Assert.Contains("password 51:b:", text);
    }

    [Fact]
    public void Launch_remembers_last_credential_and_profile_per_server()
    {
        var path = NewVaultPath();
        var svc = AppService.OpenOrCreate(path, "master");
        Seed(svc);

        svc.Launch("Prod", "bob", "Dynamic", _ => 0);

        var reopened = AppService.OpenOrCreate(path, "master");
        var prod = reopened.Servers().First(s => s.Name == "Prod");
        Assert.Equal("bob", prod.LastCredentialId);
        Assert.Equal("Dynamic", prod.LastProfileName);
    }
}
