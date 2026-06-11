// .rdp file text generation — the C# port of src/better_rdp/rdp.py.
//
// Turns a Server + Credential + Display Profile into the text mstsc.exe consumes. The
// password is emitted as `password 51:b:<HEX>`, where HEX is DPAPI-encrypted UTF-16LE
// password bytes — the only form mstsc understands. Lines are LF-joined (mstsc parses LF
// fine and it keeps the password field free of a trailing CR).

using System.Text;

namespace BetterRdp.Core;

public static class Rdp
{
    /// <summary>
    /// Return the single line <c>password 51:b:&lt;HEX&gt;</c>, where HEX is the
    /// uppercase hex of <c>Dpapi.Protect(UTF-16LE(plaintext))</c> — the encoding mstsc
    /// expects in a saved .rdp file.
    /// </summary>
    public static string PasswordField(string plaintext)
    {
        var blob = Dpapi.Protect(Encoding.Unicode.GetBytes(plaintext));
        return $"password 51:b:{Convert.ToHexString(blob)}"; // ToHexString is uppercase
    }

    /// <summary>Return the full text of a <c>.rdp</c> file for this Connection.</summary>
    public static string Generate(Server server, Credential credential, DisplayProfile profile, string plaintextPassword)
    {
        var lines = new List<string>
        {
            $"full address:s:{server.Address}",
            $"username:s:{credential.Username}",
        };
        if (!string.IsNullOrEmpty(credential.Domain))
            lines.Add($"domain:s:{credential.Domain}");
        lines.Add(PasswordField(plaintextPassword));

        // Suppress the "identity of the remote computer cannot be verified" prompt mstsc
        // raises on a self-signed/untrusted server cert. 0 = connect and don't warn. The
        // right default for a trusted intranet (the project's stated scope); it trades the
        // cert-mismatch warning away, acceptable there but not over an untrusted link.
        lines.Add("authentication level:i:0");

        switch (profile.Mode)
        {
            case DisplayMode.FullscreenMultimon:
                // screen mode id 2 = full screen; span the selected monitors.
                lines.Add("screen mode id:i:2");
                lines.Add("use multimon:i:1");
                if (profile.Monitors.Count > 0)
                    lines.Add("selectedmonitors:s:" + string.Join(",", profile.Monitors));
                break;
            case DisplayMode.WindowedFixed:
                // screen mode id 1 = windowed; fixed resolution, single screen.
                lines.Add("screen mode id:i:1");
                lines.Add("use multimon:i:0");
                if (profile.Width is int w)
                    lines.Add($"desktopwidth:i:{w}");
                if (profile.Height is int h)
                    lines.Add($"desktopheight:i:{h}");
                break;
            case DisplayMode.WindowedDynamic:
                // Windowed, resolution follows the window as it's resized.
                lines.Add("screen mode id:i:1");
                lines.Add("use multimon:i:0");
                lines.Add("dynamic resolution:i:1");
                break;
        }

        if (profile.ScaleFactor is int sf)
            lines.Add($"desktopscalefactor:i:{sf}");

        return string.Join("\n", lines) + "\n";
    }
}
