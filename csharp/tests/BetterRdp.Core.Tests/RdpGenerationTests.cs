// Port of tests/test_rdp_generation.py. Field-level assertions on generated .rdp text,
// one test per display mode. The password field is checked structurally (it's a
// non-deterministic DPAPI blob, so an exact value can't be asserted).

using System.Text;
using System.Text.RegularExpressions;
using BetterRdp.Core;

namespace BetterRdp.Core.Tests;

public class RdpGenerationTests
{
    // Parse .rdp text into {key: (type, value)}. Lines are `key:type:value`.
    private static Dictionary<string, (string Type, string Value)> Parse(string text)
    {
        var fields = new Dictionary<string, (string, string)>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(':', 3);
            fields[parts[0]] = (parts[1], parts[2]);
        }
        return fields;
    }

    private static readonly Server TestServer = new() { Name = "Prod box", Address = "prod01.intranet.local" };
    private static readonly Credential TestCred = new("c1", "alice", Password: "s3cret-pw");

    [Fact]
    public void FullscreenMultimon_emits_selected_monitors()
    {
        var profile = new DisplayProfile { Name = "All three", Mode = DisplayMode.FullscreenMultimon, Monitors = [0, 1, 2] };
        var f = Parse(Rdp.Generate(TestServer, TestCred, profile, "s3cret-pw"));

        Assert.Equal(("s", "prod01.intranet.local"), f["full address"]);
        Assert.Equal(("s", "alice"), f["username"]);
        Assert.Equal(("i", "2"), f["screen mode id"]);
        Assert.Equal(("i", "1"), f["use multimon"]);
        Assert.Equal(("s", "0,1,2"), f["selectedmonitors"]);
    }

    [Fact]
    public void WindowedFixed_emits_resolution_and_no_monitors()
    {
        var profile = new DisplayProfile { Name = "Window 1080p", Mode = DisplayMode.WindowedFixed, Width = 1920, Height = 1080 };
        var f = Parse(Rdp.Generate(TestServer, TestCred, profile, "s3cret-pw"));

        Assert.Equal(("i", "1"), f["screen mode id"]);
        Assert.Equal(("i", "0"), f["use multimon"]);
        Assert.Equal(("i", "1920"), f["desktopwidth"]);
        Assert.Equal(("i", "1080"), f["desktopheight"]);
        Assert.False(f.ContainsKey("selectedmonitors")); // a windowed profile must NOT span monitors
    }

    [Fact]
    public void WindowedDynamic_follows_window_and_no_monitors()
    {
        var profile = new DisplayProfile { Name = "Dynamic", Mode = DisplayMode.WindowedDynamic };
        var f = Parse(Rdp.Generate(TestServer, TestCred, profile, "s3cret-pw"));

        Assert.Equal(("i", "1"), f["screen mode id"]);
        Assert.Equal(("i", "1"), f["dynamic resolution"]);
        Assert.False(f.ContainsKey("selectedmonitors"));
    }

    [Fact]
    public void Domain_emitted_only_when_present()
    {
        var profile = new DisplayProfile { Name = "Dynamic", Mode = DisplayMode.WindowedDynamic };

        var withDomain = new Credential("c2", "bob", Domain: "CORP", Password: "x");
        var f1 = Parse(Rdp.Generate(TestServer, withDomain, profile, "x"));
        Assert.Equal(("s", "CORP"), f1["domain"]);

        var withoutDomain = new Credential("c3", "bob", Password: "x");
        var f2 = Parse(Rdp.Generate(TestServer, withoutDomain, profile, "x"));
        Assert.False(f2.ContainsKey("domain"));
    }

    [Fact]
    public void ScaleFactor_emitted_when_set()
    {
        var profile = new DisplayProfile { Name = "HiDPI", Mode = DisplayMode.FullscreenMultimon, Monitors = [0], ScaleFactor = 150 };
        var f = Parse(Rdp.Generate(TestServer, TestCred, profile, "s3cret-pw"));
        Assert.Equal(("i", "150"), f["desktopscalefactor"]);
    }

    [Fact]
    public void Suppresses_remote_identity_prompt()
    {
        var profile = new DisplayProfile { Name = "Dynamic", Mode = DisplayMode.WindowedDynamic };
        var f = Parse(Rdp.Generate(TestServer, TestCred, profile, "s3cret-pw"));
        // 0 = "connect and don't warn me" on a failed server-cert check (intranet scope).
        Assert.Equal(("i", "0"), f["authentication level"]);
    }

    [Fact]
    public void PasswordField_is_structurally_a_dpapi_blob()
    {
        var profile = new DisplayProfile { Name = "Dynamic", Mode = DisplayMode.WindowedDynamic };
        var text = Rdp.Generate(TestServer, TestCred, profile, "s3cret-pw");
        // mstsc expects: password 51:b:<uppercase hex of a DPAPI blob>
        Assert.Matches(new Regex("^password 51:b:[0-9A-F]+$", RegexOptions.Multiline), text);
    }

    [Fact]
    public void PasswordBlob_roundtrips_via_dpapi()
    {
        // The durable byproduct of the manual mstsc smoke: the blob we put in the .rdp must
        // DPAPI-decrypt back to the UTF-16LE password mstsc expects.
        var line = Rdp.PasswordField("s3cret-pw");
        Assert.StartsWith("password 51:b:", line);
        var hex = line.Split(':', 3)[2];
        var recovered = Dpapi.Unprotect(Convert.FromHexString(hex));
        Assert.Equal("s3cret-pw", Encoding.Unicode.GetString(recovered));
    }
}
