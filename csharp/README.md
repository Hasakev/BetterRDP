# Better RDP — C# / WinUI 3 port

A native-`.exe` port of the Python launcher (`../src/better_rdp`). Same architecture: a thin
launcher that generates `.rdp` files and shells out to `mstsc.exe`. See `../CONTEXT.md` for the
domain glossary and `../docs/adr/0001-credential-vault-encryption.md` for the crypto design.

## Layout

| Project | What it is |
|---|---|
| `src/BetterRdp.Core` | Framework-agnostic domain core: models, DPAPI, `.rdp` generation, Vault (Argon2id + AES-GCM + DPAPI), launcher, `AppService`. No UI dependency. |
| `tests/BetterRdp.Core.Tests` | xUnit port of the Python contract suite (24 tests). |
| `src/BetterRdp.App` | WinUI 3 (Windows App SDK) shell. Talks only to `AppService`. |

## Status: core green, WinUI shell pending

The `Core` is **fully implemented and all 24 contract tests pass** — built red-first, the
same approach used for the Python build. Implementation order was Dpapi → VaultCrypto/Vault
→ Rdp → Launcher → AppService. The WinUI app is still the MVVM template placeholder, wired
to reference the core.

Next: build the real WinUI shell (unlock → server list → connection launch) over `AppService`.

## Build & test

```pwsh
dotnet test  csharp/tests/BetterRdp.Core.Tests   # core contracts
dotnet build csharp/src/BetterRdp.App            # WinUI shell
```

## Stack notes

- **DPAPI** is native (`System.Security.Cryptography.ProtectedData`, CurrentUser scope) — the
  same Win32 API the Python build used via pywin32, so blobs are byte-compatible.
- **AES-GCM** is native (`System.Security.Cryptography.AesGcm`); only **Argon2id** is a NuGet
  dep (`Konscious.Security.Cryptography.Argon2`).
- Vaults are a **fresh start** — the C# build does not read Python `vault.json` files (a
  deliberate decision; re-entering ~2 credentials is trivial vs. cross-language format parity).
- Packaging target: self-contained / unpackaged `dotnet publish` (WinUI 3 does not support
  NativeAOT; single-file is more involved than WPF — handled at publish time).
