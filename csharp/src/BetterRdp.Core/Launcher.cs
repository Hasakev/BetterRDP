// Connection launch — the C# port of src/better_rdp/launch.py.
//
// Materialise a temp .rdp, invoke mstsc, then delete the temp file (even if the runner
// throws). The temp .rdp briefly contains a usable DPAPI password blob, so it must be
// deleted immediately after mstsc has read it (see ADR 0001 > Consequences).

namespace BetterRdp.Core;

/// <summary>Takes the argv list and returns whatever the caller cares about. Injectable so
/// tests can assert argument construction + temp-file cleanup without spawning a window.
/// Replaces Python's <c>Runner</c> callable.</summary>
public delegate object MstscRunner(IReadOnlyList<string> argv);

public static class Launcher
{
    /// <summary>Spawn mstsc and wait for it. mstsc reads the .rdp at startup, so once it
    /// returns we are safe to delete the temp file.</summary>
    private static object DefaultRunner(IReadOnlyList<string> argv)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(argv[0])
        {
            UseShellExecute = false,
        };
        for (int i = 1; i < argv.Count; i++)
            psi.ArgumentList.Add(argv[i]);
        var proc = System.Diagnostics.Process.Start(psi)
                   ?? throw new InvalidOperationException($"Failed to start {argv[0]}");
        proc.WaitForExit();
        return proc.ExitCode;
    }

    /// <summary>
    /// Write a temp .rdp for this Connection, invoke <c>runner([mstsc, rdpPath])</c>, then
    /// delete the temp file (even if the runner throws). Returns the runner's result.
    /// <paramref name="runner"/> defaults to real <see cref="System.Diagnostics.Process"/>
    /// execution; tests inject a fake.
    /// </summary>
    public static object Launch(
        Server server,
        Credential credential,
        DisplayProfile profile,
        string plaintextPassword,
        MstscRunner? runner = null,
        string mstsc = "mstsc.exe")
    {
        runner ??= DefaultRunner;

        var text = Rdp.Generate(server, credential, profile, plaintextPassword);
        var path = Path.Combine(Path.GetTempPath(), $"better_rdp_{Guid.NewGuid():N}.rdp");
        try
        {
            File.WriteAllText(path, text);
            return runner([mstsc, path]);
        }
        finally
        {
            // The temp .rdp carries a usable DPAPI password blob — delete it promptly.
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
