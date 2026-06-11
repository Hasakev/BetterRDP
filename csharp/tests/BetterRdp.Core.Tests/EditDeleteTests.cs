// Edit/delete operations on the Vault. The subtle one is credential editing: a null
// password must preserve the existing encrypted secret (the UI never round-trips plaintext
// passwords back into an edit form), while a non-null password re-encrypts.

using BetterRdp.Core;

namespace BetterRdp.Core.Tests;

public class EditDeleteTests
{
    private static string NewVaultPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "betterrdp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "vault.json");
    }

    private static Vault Seeded()
    {
        var v = Vault.Create(NewVaultPath(), "master");
        v.AddCredential(new Credential("alice", "alice", Password: "pw-alice"));
        v.AddServer(new Server { Name = "Prod", Address = "prod01.intranet.local" });
        v.AddProfile(new DisplayProfile { Name = "Dynamic", Mode = DisplayMode.WindowedDynamic });
        return v;
    }

    [Fact]
    public void EditCredential_with_null_password_keeps_the_stored_secret()
    {
        var v = Seeded();
        // Rename the username/domain but pass no new password.
        v.EditCredential("alice", new Credential("alice", "alice", Domain: "CORP", Password: null));

        Assert.Equal("pw-alice", v.GetPassword("alice"));
        Assert.Equal("CORP", v.Credentials().Single().Domain);
    }

    [Fact]
    public void EditCredential_with_new_password_reencrypts()
    {
        var v = Seeded();
        v.EditCredential("alice", new Credential("alice", "alice", Password: "pw-new"));
        Assert.Equal("pw-new", v.GetPassword("alice"));
    }

    [Fact]
    public void EditServer_can_rename_and_persists()
    {
        var path = NewVaultPath();
        var v = Vault.Create(path, "master");
        v.AddServer(new Server { Name = "Prod", Address = "old.local" });
        v.EditServer("Prod", new Server { Name = "Production", Address = "new.local" });
        v.Save();

        var reopened = Vault.Open(path, "master");
        var s = reopened.Servers().Single();
        Assert.Equal("Production", s.Name);
        Assert.Equal("new.local", s.Address);
    }

    [Fact]
    public void EditProfile_replaces_in_place()
    {
        var v = Seeded();
        v.EditProfile("Dynamic", new DisplayProfile { Name = "Fixed1080", Mode = DisplayMode.WindowedFixed, Width = 1920, Height = 1080 });
        var p = v.Profiles().Single();
        Assert.Equal("Fixed1080", p.Name);
        Assert.Equal(DisplayMode.WindowedFixed, p.Mode);
        Assert.Equal(1920, p.Width);
    }

    [Fact]
    public void Remove_drops_each_entity()
    {
        var v = Seeded();
        v.RemoveCredential("alice");
        v.RemoveServer("Prod");
        v.RemoveProfile("Dynamic");
        Assert.Empty(v.Credentials());
        Assert.Empty(v.Servers());
        Assert.Empty(v.Profiles());
    }

    [Fact]
    public void Remove_unknown_key_throws()
    {
        var v = Seeded();
        Assert.Throws<KeyNotFoundException>(() => v.RemoveServer("nope"));
        Assert.Throws<KeyNotFoundException>(() => v.RemoveCredential("nope"));
        Assert.Throws<KeyNotFoundException>(() => v.RemoveProfile("nope"));
    }
}
