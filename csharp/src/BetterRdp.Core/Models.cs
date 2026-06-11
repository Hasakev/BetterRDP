// Domain models — the C# port of src/better_rdp/models.py. Glossary lives in CONTEXT.md.
// These are intentionally thin and dependency-free so the test project can use them
// without pulling in WinUI or any crypto package.

namespace BetterRdp.Core;

/// <summary>How a <see cref="DisplayProfile"/> renders. See CONTEXT.md &gt; Display Profile.</summary>
public enum DisplayMode
{
    FullscreenMultimon,
    WindowedFixed,
    WindowedDynamic,
}

/// <summary>
/// A username + password (+ optional domain) you authenticate <em>with</em>.
/// <see cref="Password"/> is the transient plaintext, held in memory only; it is never
/// serialized to the Vault as plaintext (see ADR 0001).
/// </summary>
public sealed record Credential(string Id, string Username, string? Domain = null, string? Password = null);

/// <summary>A named, reusable display layout selected at launch. See CONTEXT.md.</summary>
// Note: properties are init-only with defaults rather than `required` — the WinUI XAML
// type-info generator emits a parameterless activator for every data-bound type, which
// `required` members forbid. Construction sites still set Name/Mode via initializers.
public sealed record DisplayProfile
{
    public string Name { get; init; } = "";
    public DisplayMode Mode { get; init; }
    public IReadOnlyList<int> Monitors { get; init; } = [];   // selected mstsc monitor ids
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? ScaleFactor { get; init; }                    // 100/125/150/175/200
}

/// <summary>A remote host you connect to. See CONTEXT.md.</summary>
public sealed record Server
{
    public string Name { get; init; } = "";
    public string Address { get; init; } = "";
    public string Notes { get; init; } = "";
    // Mutated when a Connection is launched, to remember per-Server defaults.
    public string? LastCredentialId { get; set; }
    public string? LastProfileName { get; set; }
}
