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

/// <summary>Takes the rdpsign argv list. Injectable so tests can assert signing happens
/// before mstsc sees the file without requiring a local code-signing certificate.</summary>
public delegate object RdpSigner(IReadOnlyList<string> argv);

public static class Launcher
{
    public const string SignThumbprintEnv = "BETTER_RDP_SIGN_THUMBPRINT";
    public const string LegacySignCertEnv = "BETTER_RDP_SIGN_SHA256";

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

    private static object DefaultSigner(IReadOnlyList<string> argv)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(argv[0])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        for (int i = 1; i < argv.Count; i++)
            psi.ArgumentList.Add(argv[i]);
        var proc = System.Diagnostics.Process.Start(psi)
                   ?? throw new InvalidOperationException($"Failed to start {argv[0]}");
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"{argv[0]} failed with exit code {proc.ExitCode}. {stdout}{stderr}".Trim());
        return proc.ExitCode;
    }

    private static string NormaliseThumbprint(string thumbprint) =>
        string.Concat(thumbprint.Where(c => !char.IsWhiteSpace(c)));

    private static string? ConfiguredSigningThumbprint() =>
        Environment.GetEnvironmentVariable(SignThumbprintEnv)
        ?? Environment.GetEnvironmentVariable(LegacySignCertEnv);

    /// <summary>
    /// Write a temp .rdp for this Connection, optionally sign it with rdpsign.exe, invoke
    /// <c>runner([mstsc, rdpPath])</c>, then delete the temp file (even if the runner
    /// throws). Returns the runner's result. <paramref name="runner"/> defaults to real
    /// <see cref="System.Diagnostics.Process"/> execution; tests inject a fake.
    ///
    /// If <paramref name="rdpsignSha256"/> (or environment variable
    /// <c>BETTER_RDP_SIGN_THUMBPRINT</c>) is set, the temp .rdp is signed with
    /// <c>rdpsign.exe /sha256 &lt;thumbprint&gt;</c> before mstsc sees it. Despite the rdpsign
    /// switch name, Windows expects the certificate's normal SHA-1 thumbprint here. A
    /// signed .rdp lets mstsc verify and display the Publisher instead of showing the
    /// unknown-publisher prompt, provided the signing certificate is trusted by Windows.
    /// </summary>
    public static object Launch(
        Server server,
        Credential credential,
        DisplayProfile profile,
        string plaintextPassword,
        MstscRunner? runner = null,
        string mstsc = "mstsc.exe",
        RdpSigner? signer = null,
        string rdpsign = "rdpsign.exe",
        string? rdpsignSha256 = null)
    {
        runner ??= DefaultRunner;
        signer ??= DefaultSigner;
        rdpsignSha256 ??= ConfiguredSigningThumbprint();

        var text = Rdp.Generate(server, credential, profile, plaintextPassword);
        var path = Path.Combine(Path.GetTempPath(), $"better_rdp_{Guid.NewGuid():N}.rdp");
        try
        {
            File.WriteAllText(path, text);
            if (!string.IsNullOrWhiteSpace(rdpsignSha256))
            {
                signer([rdpsign, "/sha256", NormaliseThumbprint(rdpsignSha256), path]);
                if (!File.ReadLines(path).Any(line => line.StartsWith("signature:s:", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("rdpsign completed but the .rdp file does not contain a signature.");
            }
            return runner([mstsc, path]);
        }
        finally
        {
            // The temp .rdp carries a usable DPAPI password blob — delete it promptly.
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
