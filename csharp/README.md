# Better RDP — C# / WinUI 3 port

A native-`.exe` port of the Python launcher (`../python/src/better_rdp`). Same architecture: a thin
launcher that generates `.rdp` files and shells out to `mstsc.exe`. See `../CONTEXT.md` for the
domain glossary and `../docs/adr/0001-credential-vault-encryption.md` for the crypto design.

## Layout

| Project | What it is |
|---|---|
| `src/BetterRdp.Core` | Framework-agnostic domain core: models, DPAPI, `.rdp` generation, Vault (Argon2id + AES-GCM + DPAPI), launcher, `AppService`. No UI dependency. |
| `tests/BetterRdp.Core.Tests` | xUnit port of the Python contract suite (24 tests). |
| `src/BetterRdp.App` | WinUI 3 (Windows App SDK) shell. Talks only to `AppService`. |

## Status: core green, WinUI shell built

The `Core` is **fully implemented and all 24 contract tests pass** — built red-first, the
same approach used for the Python build (Dpapi → VaultCrypto/Vault → Rdp → Launcher →
AppService).

The **WinUI 3 shell is built** over `AppService`, mirroring the PySide6 GUI:

- **Unlock** (`Dialogs/MasterPasswordDialog`) — prompts for the Master Password on launch,
  creates the vault on first run, re-prompts on a wrong password, closes the app on cancel.
- **MainPage** — header, a server `ListView` (the launch surface), and a connection card
  with credential + display-profile pickers and an accent **Launch** button. Selecting a
  server defaults the pickers to its last-used credential/profile; launching remembers them.
- **Add dialogs** (`Dialogs/ServerDialog`, `CredentialDialog`, `ProfileDialog`) — the
  profile dialog enumerates physical displays via `DisplayArea.FindAll()` and shows
  mode-dependent monitor / resolution / scale fields.
- Dark theme + Mica backdrop. Launch runs on a background thread so the UI stays responsive
  while mstsc is open; failures surface in a dialog.

The view talks only to `MainViewModel` → `AppService`; dialogs and error surfacing live in
the code-behind (they need a `XamlRoot`). The `[ObservableProperty]` fields emit a harmless
`MVVMTK0045` AOT advisory — the partial-property form this toolkit version ships won't
generate its implementation part (CS9248), and this app isn't AOT.

Next: the manual mstsc smoke against a real intranet host (the one thing no test can prove),
and monitor-id calibration (smoke S2). Then `dotnet publish` packaging.

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
