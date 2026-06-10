// Port of tests/test_launch.py — the launch path with a mocked mstsc runner.
// We never spawn a real mstsc window here. The injected runner lets us assert (1) the argv
// handed to mstsc, and (2) that the temp .rdp exists during the call and is deleted after,
// including when the runner throws.

using BetterRdp.Core;

namespace BetterRdp.Core.Tests;

public class LaunchTests
{
    private static (Server, Credential, DisplayProfile) Conn() => (
        new Server { Name = "Prod", Address = "prod01.intranet.local" },
        new Credential("c1", "alice", Password: "pw"),
        new DisplayProfile { Name = "Dynamic", Mode = DisplayMode.WindowedDynamic });

    [Fact]
    public void Launch_invokes_mstsc_with_a_rdp_file()
    {
        var (server, cred, profile) = Conn();
        string[] argv = [];
        bool existedDuringCall = false;

        object Runner(IReadOnlyList<string> a)
        {
            argv = [.. a];
            existedDuringCall = File.Exists(a[1]);
            return 0;
        }

        Launcher.Launch(server, cred, profile, "pw", Runner, "mstsc.exe");

        Assert.Equal("mstsc.exe", argv[0]);
        Assert.EndsWith(".rdp", argv[1]);
        Assert.True(existedDuringCall);
    }

    [Fact]
    public void Temp_rdp_is_deleted_after_launch()
    {
        var (server, cred, profile) = Conn();
        string? path = null;

        Launcher.Launch(server, cred, profile, "pw", a => { path = a[1]; return 0; });

        Assert.NotNull(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Temp_rdp_is_deleted_even_if_runner_raises()
    {
        var (server, cred, profile) = Conn();
        string? path = null;

        object Runner(IReadOnlyList<string> a)
        {
            path = a[1];
            throw new InvalidOperationException("mstsc failed to start");
        }

        Assert.Throws<InvalidOperationException>(() => Launcher.Launch(server, cred, profile, "pw", Runner));

        Assert.NotNull(path); // the temp file was created before the runner ran
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Launch_returns_runner_result()
    {
        var (server, cred, profile) = Conn();
        var sentinel = new object();
        var result = Launcher.Launch(server, cred, profile, "pw", _ => sentinel);
        Assert.Same(sentinel, result);
    }
}
