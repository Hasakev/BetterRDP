// The Vault — the C# port of src/better_rdp/vault.py. See ADR 0001.
//
// Stored password layering is:  Dpapi.Protect( AesGcm( password, key = Argon2id(master, salt) ) )
// AES-GCM and DPAPI are native to .NET; only Argon2id comes from a NuGet package
// (Konscious.Security.Cryptography.Argon2). Persisted as JSON.

namespace BetterRdp.Core;

/// <summary>Stateless crypto primitives behind the Vault (Argon2id KDF + AES-GCM).</summary>
public static class VaultCrypto
{
    /// <summary>Derive a 32-byte key from the Master Password via Argon2id. Deterministic
    /// for a given (master, salt).</summary>
    public static byte[] DeriveKey(string master, byte[] salt)
        => throw new NotImplementedException();

    /// <summary>AES-GCM encrypt. Returns nonce || ciphertext || tag.</summary>
    public static byte[] EncryptSecret(string plaintext, byte[] key)
        => throw new NotImplementedException();

    /// <summary>AES-GCM decrypt. Throws if the key is wrong — never returns garbage.</summary>
    public static string DecryptSecret(byte[] blob, byte[] key)
        => throw new NotImplementedException();
}

/// <summary>Holds Servers, Credentials and Display Profiles; persists to JSON with
/// encrypted secrets.</summary>
public sealed class Vault
{
    /// <summary>Create a new, empty Vault at <paramref name="path"/> keyed by
    /// <paramref name="master"/> (set-once for v1).</summary>
    public static Vault Create(string path, string master)
        => throw new NotImplementedException();

    /// <summary>Open an existing Vault. A wrong master must fail loudly, not silently.</summary>
    public static Vault Open(string path, string master)
        => throw new NotImplementedException();

    /// <summary>Store a Credential; its plaintext password is encrypted, never written raw.</summary>
    public void AddCredential(Credential credential)
        => throw new NotImplementedException();

    /// <summary>Decrypt and return the plaintext password for a stored Credential.</summary>
    public string GetPassword(string credentialId)
        => throw new NotImplementedException();

    /// <summary>All stored Credentials, without plaintext passwords.</summary>
    public IReadOnlyList<Credential> Credentials()
        => throw new NotImplementedException();

    public void AddServer(Server server)
        => throw new NotImplementedException();

    public IReadOnlyList<Server> Servers()
        => throw new NotImplementedException();

    /// <summary>Replace the stored Server matching <c>Name</c> (e.g. to record last-used).</summary>
    public void UpdateServer(Server server)
        => throw new NotImplementedException();

    public void AddProfile(DisplayProfile profile)
        => throw new NotImplementedException();

    public IReadOnlyList<DisplayProfile> Profiles()
        => throw new NotImplementedException();

    /// <summary>Persist to the JSON file on disk.</summary>
    public void Save()
        => throw new NotImplementedException();
}
