// .rdp file text generation — the C# port of src/better_rdp/rdp.py.
//
// Turns a Server + Credential + Display Profile into the text mstsc.exe consumes. The
// password is emitted as `password 51:b:<HEX>`, where HEX is DPAPI-encrypted UTF-16LE
// password bytes — the only form mstsc understands. Lines are LF-joined (mstsc parses LF
// fine and it keeps the password field free of a trailing CR).

namespace BetterRdp.Core;

public static class Rdp
{
    /// <summary>
    /// Return the single line <c>password 51:b:&lt;HEX&gt;</c>, where HEX is the
    /// uppercase hex of <c>Dpapi.Protect(UTF-16LE(plaintext))</c>.
    /// </summary>
    public static string PasswordField(string plaintext)
        => throw new NotImplementedException();

    /// <summary>Return the full text of a <c>.rdp</c> file for this Connection.</summary>
    public static string Generate(Server server, Credential credential, DisplayProfile profile, string plaintextPassword)
        => throw new NotImplementedException();
}
