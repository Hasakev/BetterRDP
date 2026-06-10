// Port of tests/test_vault_crypto.py — the crypto/vault invariants from ADR 0001.
// These are security guarantees, so a regression is a silent failure, not a crash.
//
// On-disk Vault JSON schema these tests rely on (part of the contract):
//   {
//     "kdf":  { "salt": "<base64>" },
//     "credentials": [ { "id","username","domain","secret": "<base64 of dpapi(aes_gcm)>" } ],
//     "servers": [ ... ], "profiles": [ ... ]
//   }

using System.Text;
using System.Text.Json;
using BetterRdp.Core;

namespace BetterRdp.Core.Tests;

public class VaultCryptoTests
{
    private const string Secret = "hunter2-unique-marker-9f3a";

    private static string NewVaultPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "betterrdp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "vault.json");
    }

    private static bool BytesContain(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length) return false;
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j]) j++;
            if (j == needle.Length) return true;
        }
        return false;
    }

    // --- 1. key derivation ---------------------------------------------------------

    [Fact]
    public void KeyDerivation_is_deterministic_and_salt_sensitive()
    {
        var saltA = Encoding.ASCII.GetBytes("0123456789abcdef");
        var saltB = Encoding.ASCII.GetBytes("fedcba9876543210");
        Assert.Equal(VaultCrypto.DeriveKey("master", saltA), VaultCrypto.DeriveKey("master", saltA));
        Assert.NotEqual(VaultCrypto.DeriveKey("master", saltA), VaultCrypto.DeriveKey("master", saltB));
        Assert.NotEqual(VaultCrypto.DeriveKey("master", saltA), VaultCrypto.DeriveKey("other", saltA));
    }

    // --- 2 & 3. AES-GCM round-trip and authenticated failure -----------------------

    [Fact]
    public void AesRoundtrip()
    {
        var key = VaultCrypto.DeriveKey("master", Encoding.ASCII.GetBytes("0123456789abcdef"));
        var blob = VaultCrypto.EncryptSecret(Secret, key);
        Assert.Equal(Secret, VaultCrypto.DecryptSecret(blob, key));
    }

    [Fact]
    public void WrongKey_fails_loudly_never_returns_garbage()
    {
        var key = VaultCrypto.DeriveKey("master", Encoding.ASCII.GetBytes("0123456789abcdef"));
        var wrong = VaultCrypto.DeriveKey("WRONG", Encoding.ASCII.GetBytes("0123456789abcdef"));
        var blob = VaultCrypto.EncryptSecret(Secret, key);
        // Authenticated encryption (GCM) => wrong key must throw, not return corrupt plaintext.
        Assert.ThrowsAny<Exception>(() => VaultCrypto.DecryptSecret(blob, wrong));
    }

    // --- 4. no plaintext on disk ---------------------------------------------------

    [Fact]
    public void NoPlaintextPassword_on_disk()
    {
        var path = NewVaultPath();
        var v = Vault.Create(path, "masterA");
        v.AddCredential(new Credential("c1", "alice", Password: Secret));
        v.Save();

        var raw = File.ReadAllBytes(path);
        Assert.False(BytesContain(raw, Encoding.UTF8.GetBytes(Secret)));
        Assert.False(BytesContain(raw, Encoding.Unicode.GetBytes(Secret)));
    }

    // --- 5. the ADR 0001 two-layer guarantee, made executable ----------------------

    [Fact]
    public void MasterPasswordLayer_holds_even_with_dpapi_stripped()
    {
        // Prove the inner layer is real: an attacker who is the same Windows user (so DPAPI
        // decrypts for them) still cannot recover the password without the Master Password.
        var path = NewVaultPath();
        var v = Vault.Create(path, "masterA");
        v.AddCredential(new Credential("c1", "alice", Password: Secret));
        v.Save();

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var storedB64 = doc.RootElement.GetProperty("credentials")[0].GetProperty("secret").GetString()!;
        var inner = Dpapi.Unprotect(Convert.FromBase64String(storedB64)); // strip the DPAPI layer

        // The DPAPI-stripped bytes are still AES-GCM ciphertext, not the plaintext.
        Assert.False(BytesContain(inner, Encoding.UTF8.GetBytes(Secret)));
        Assert.False(BytesContain(inner, Encoding.Unicode.GetBytes(Secret)));
        // And without the master-derived key, decryption fails.
        var saltB64 = doc.RootElement.GetProperty("kdf").GetProperty("salt").GetString()!;
        var wrong = VaultCrypto.DeriveKey("guessed-master", Convert.FromBase64String(saltB64));
        Assert.ThrowsAny<Exception>(() => VaultCrypto.DecryptSecret(inner, wrong));
    }

    [Fact]
    public void OpenWithWrongMaster_fails_loudly()
    {
        var path = NewVaultPath();
        var v = Vault.Create(path, "masterA");
        v.AddCredential(new Credential("c1", "alice", Password: Secret));
        v.Save();

        Assert.ThrowsAny<Exception>(() => Vault.Open(path, "wrong-master"));
    }

    [Fact]
    public void CorrectMaster_round_trips_credential()
    {
        var path = NewVaultPath();
        var v = Vault.Create(path, "masterA");
        v.AddCredential(new Credential("c1", "alice", Password: Secret));
        v.Save();

        var reopened = Vault.Open(path, "masterA");
        Assert.Equal(Secret, reopened.GetPassword("c1"));
    }

    // --- 6. DPAPI round-trip -------------------------------------------------------

    [Fact]
    public void DpapiRoundtrip()
    {
        Assert.Equal(
            Encoding.ASCII.GetBytes("abc-123"),
            Dpapi.Unprotect(Dpapi.Protect(Encoding.ASCII.GetBytes("abc-123"))));
    }
}
