// The Vault — the C# port of src/better_rdp/vault.py. See ADR 0001.
//
// Stored password layering is:  Dpapi.Protect( AesGcm( password, key = Argon2id(master, salt) ) )
// AES-GCM and DPAPI are native to .NET; only Argon2id comes from a NuGet package
// (Konscious.Security.Cryptography.Argon2). Persisted as JSON.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Konscious.Security.Cryptography;

namespace BetterRdp.Core;

/// <summary>Stateless crypto primitives behind the Vault (Argon2id KDF + AES-GCM).</summary>
public static class VaultCrypto
{
    // Argon2id parameters. Fixed so a derived key is reproducible across runs for a given
    // (master, salt). Interactive-login-grade, not archival-grade.
    private const int Argon2TimeCost = 3;
    private const int Argon2MemoryKiB = 64 * 1024; // 64 MiB
    private const int Argon2Parallelism = 4;
    private const int KeyLen = 32;   // AES-256
    internal const int SaltLen = 16;
    private const int NonceLen = 12;
    private const int TagLen = 16;

    /// <summary>Derive a 32-byte key from the Master Password via Argon2id. Deterministic
    /// for a given (master, salt).</summary>
    public static byte[] DeriveKey(string master, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(master))
        {
            Salt = salt,
            DegreeOfParallelism = Argon2Parallelism,
            Iterations = Argon2TimeCost,
            MemorySize = Argon2MemoryKiB,
        };
        return argon2.GetBytes(KeyLen);
    }

    /// <summary>AES-GCM encrypt. Returns nonce || ciphertext || tag.</summary>
    public static byte[] EncryptSecret(string plaintext, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var ct = new byte[pt.Length];
        var tag = new byte[TagLen];
        using var gcm = new AesGcm(key, TagLen);
        gcm.Encrypt(nonce, pt, ct, tag);

        var blob = new byte[NonceLen + ct.Length + TagLen];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceLen);
        Buffer.BlockCopy(ct, 0, blob, NonceLen, ct.Length);
        Buffer.BlockCopy(tag, 0, blob, NonceLen + ct.Length, TagLen);
        return blob;
    }

    /// <summary>AES-GCM decrypt. Throws if the key is wrong — never returns garbage.</summary>
    public static string DecryptSecret(byte[] blob, byte[] key)
    {
        var nonce = blob[..NonceLen];
        var tag = blob[^TagLen..];
        var ct = blob[NonceLen..^TagLen];
        var pt = new byte[ct.Length];
        using var gcm = new AesGcm(key, TagLen);
        gcm.Decrypt(nonce, ct, tag, pt); // throws AuthenticationTagMismatchException on wrong key
        return Encoding.UTF8.GetString(pt);
    }
}

// --- on-disk JSON DTOs (the schema is part of the contract) -------------------------

internal sealed class VaultData
{
    [JsonPropertyName("kdf")] public KdfData Kdf { get; set; } = new();
    [JsonPropertyName("verifier")] public string Verifier { get; set; } = "";
    [JsonPropertyName("credentials")] public List<CredentialDto> Credentials { get; set; } = [];
    [JsonPropertyName("servers")] public List<ServerDto> Servers { get; set; } = [];
    [JsonPropertyName("profiles")] public List<ProfileDto> Profiles { get; set; } = [];
}

internal sealed class KdfData
{
    [JsonPropertyName("salt")] public string Salt { get; set; } = "";
}

internal sealed class CredentialDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("domain")] public string? Domain { get; set; }
    [JsonPropertyName("secret")] public string Secret { get; set; } = "";
}

internal sealed class ServerDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("address")] public string Address { get; set; } = "";
    [JsonPropertyName("notes")] public string Notes { get; set; } = "";
    [JsonPropertyName("last_credential_id")] public string? LastCredentialId { get; set; }
    [JsonPropertyName("last_profile_name")] public string? LastProfileName { get; set; }
}

internal sealed class ProfileDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("mode")] public DisplayMode Mode { get; set; }
    [JsonPropertyName("monitors")] public List<int> Monitors { get; set; } = [];
    [JsonPropertyName("width")] public int? Width { get; set; }
    [JsonPropertyName("height")] public int? Height { get; set; }
    [JsonPropertyName("scale_factor")] public int? ScaleFactor { get; set; }
}

/// <summary>Holds Servers, Credentials and Display Profiles; persists to JSON with
/// encrypted secrets.</summary>
public sealed class Vault
{
    // A constant sentinel encrypted under the derived key at create time. Decrypting it on
    // open is how we tell a correct Master Password from a wrong one (authenticated failure).
    private const string VerifierPlaintext = "better-rdp-verifier-v1";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly byte[] _key;
    private readonly VaultData _data;

    private Vault(string path, byte[] key, VaultData data)
    {
        _path = path;
        _key = key;
        _data = data;
    }

    private static string Wrap(string plaintext, byte[] key)
        => Convert.ToBase64String(Dpapi.Protect(VaultCrypto.EncryptSecret(plaintext, key)));

    private static string Unwrap(string storedB64, byte[] key)
        => VaultCrypto.DecryptSecret(Dpapi.Unprotect(Convert.FromBase64String(storedB64)), key);

    /// <summary>Create a new, empty Vault at <paramref name="path"/> keyed by
    /// <paramref name="master"/> (set-once for v1).</summary>
    public static Vault Create(string path, string master)
    {
        var salt = RandomNumberGenerator.GetBytes(VaultCrypto.SaltLen);
        var key = VaultCrypto.DeriveKey(master, salt);
        var data = new VaultData
        {
            Kdf = new KdfData { Salt = Convert.ToBase64String(salt) },
            Verifier = Wrap(VerifierPlaintext, key),
        };
        return new Vault(path, key, data);
    }

    /// <summary>Open an existing Vault. A wrong master must fail loudly, not silently.</summary>
    public static Vault Open(string path, string master)
    {
        var data = JsonSerializer.Deserialize<VaultData>(File.ReadAllText(path), JsonOpts)
                   ?? throw new InvalidDataException("Vault file is empty or malformed");
        var salt = Convert.FromBase64String(data.Kdf.Salt);
        var key = VaultCrypto.DeriveKey(master, salt);
        // Authenticated check: a wrong key makes Unwrap throw, never succeed.
        if (Unwrap(data.Verifier, key) != VerifierPlaintext)
            throw new UnauthorizedAccessException("Master password verification failed");
        return new Vault(path, key, data);
    }

    /// <summary>Store a Credential; its plaintext password is encrypted, never written raw.</summary>
    public void AddCredential(Credential credential)
        => _data.Credentials.Add(new CredentialDto
        {
            Id = credential.Id,
            Username = credential.Username,
            Domain = credential.Domain,
            Secret = Wrap(credential.Password ?? "", _key),
        });

    /// <summary>Decrypt and return the plaintext password for a stored Credential.</summary>
    public string GetPassword(string credentialId)
    {
        foreach (var e in _data.Credentials)
            if (e.Id == credentialId)
                return Unwrap(e.Secret, _key);
        throw new KeyNotFoundException(credentialId);
    }

    /// <summary>All stored Credentials, without plaintext passwords.</summary>
    public IReadOnlyList<Credential> Credentials()
        => [.. _data.Credentials.Select(e => new Credential(e.Id, e.Username, e.Domain))];

    public void AddServer(Server server)
        => _data.Servers.Add(ToDto(server));

    public IReadOnlyList<Server> Servers()
        => [.. _data.Servers.Select(FromDto)];

    /// <summary>Replace the stored Server matching <c>Name</c> (e.g. to record last-used).</summary>
    public void UpdateServer(Server server)
    {
        for (int i = 0; i < _data.Servers.Count; i++)
            if (_data.Servers[i].Name == server.Name)
            {
                _data.Servers[i] = ToDto(server);
                return;
            }
        throw new KeyNotFoundException(server.Name);
    }

    public void AddProfile(DisplayProfile profile)
        => _data.Profiles.Add(ToDto(profile));

    public IReadOnlyList<DisplayProfile> Profiles()
        => [.. _data.Profiles.Select(FromDto)];

    // --- edit / delete -------------------------------------------------------------

    /// <summary>Replace the Server matched by <paramref name="originalName"/> (rename-capable).</summary>
    public void EditServer(string originalName, Server server)
    {
        for (int i = 0; i < _data.Servers.Count; i++)
            if (_data.Servers[i].Name == originalName)
            {
                _data.Servers[i] = ToDto(server);
                return;
            }
        throw new KeyNotFoundException(originalName);
    }

    public void RemoveServer(string name)
    {
        if (_data.Servers.RemoveAll(e => e.Name == name) == 0)
            throw new KeyNotFoundException(name);
    }

    /// <summary>Replace the Credential matched by <paramref name="originalId"/>. A null
    /// <see cref="Credential.Password"/> keeps the existing encrypted secret; a non-null one
    /// re-encrypts.</summary>
    public void EditCredential(string originalId, Credential credential)
    {
        for (int i = 0; i < _data.Credentials.Count; i++)
            if (_data.Credentials[i].Id == originalId)
            {
                var existing = _data.Credentials[i];
                _data.Credentials[i] = new CredentialDto
                {
                    Id = credential.Id,
                    Username = credential.Username,
                    Domain = credential.Domain,
                    Secret = credential.Password is null ? existing.Secret : Wrap(credential.Password, _key),
                };
                return;
            }
        throw new KeyNotFoundException(originalId);
    }

    public void RemoveCredential(string id)
    {
        if (_data.Credentials.RemoveAll(e => e.Id == id) == 0)
            throw new KeyNotFoundException(id);
    }

    /// <summary>Replace the Display Profile matched by <paramref name="originalName"/> (rename-capable).</summary>
    public void EditProfile(string originalName, DisplayProfile profile)
    {
        for (int i = 0; i < _data.Profiles.Count; i++)
            if (_data.Profiles[i].Name == originalName)
            {
                _data.Profiles[i] = ToDto(profile);
                return;
            }
        throw new KeyNotFoundException(originalName);
    }

    public void RemoveProfile(string name)
    {
        if (_data.Profiles.RemoveAll(e => e.Name == name) == 0)
            throw new KeyNotFoundException(name);
    }

    /// <summary>Persist to the JSON file on disk.</summary>
    public void Save()
        => File.WriteAllText(_path, JsonSerializer.Serialize(_data, JsonOpts));

    // --- (de)serialisation of the non-secret records -------------------------------

    private static ServerDto ToDto(Server s) => new()
    {
        Name = s.Name,
        Address = s.Address,
        Notes = s.Notes,
        LastCredentialId = s.LastCredentialId,
        LastProfileName = s.LastProfileName,
    };

    private static Server FromDto(ServerDto e) => new()
    {
        Name = e.Name,
        Address = e.Address,
        Notes = e.Notes,
        LastCredentialId = e.LastCredentialId,
        LastProfileName = e.LastProfileName,
    };

    private static ProfileDto ToDto(DisplayProfile p) => new()
    {
        Name = p.Name,
        Mode = p.Mode,
        Monitors = [.. p.Monitors],
        Width = p.Width,
        Height = p.Height,
        ScaleFactor = p.ScaleFactor,
    };

    private static DisplayProfile FromDto(ProfileDto e) => new()
    {
        Name = e.Name,
        Mode = e.Mode,
        Monitors = [.. e.Monitors],
        Width = e.Width,
        Height = e.Height,
        ScaleFactor = e.ScaleFactor,
    };
}
