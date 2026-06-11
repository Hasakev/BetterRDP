// Thin wrappers over Windows DPAPI — the C# port of src/better_rdp/dpapi.py.
//
// In .NET this is first-class: System.Security.Cryptography.ProtectedData wraps the same
// Win32 CryptProtectData / CryptUnprotectData the PowerShell smoke (SMOKE.md S1) proved,
// so the blobs are byte-compatible with the Python build.

using System.Security.Cryptography;

namespace BetterRdp.Core;

public static class Dpapi
{
    /// <summary>DPAPI-encrypt <paramref name="data"/> bound to the current Windows user.</summary>
    public static byte[] Protect(byte[] data, byte[]? entropy = null)
        => ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);

    /// <summary>DPAPI-decrypt a blob produced by <see cref="Protect"/>.</summary>
    public static byte[] Unprotect(byte[] blob, byte[]? entropy = null)
        => ProtectedData.Unprotect(blob, entropy, DataProtectionScope.CurrentUser);
}
