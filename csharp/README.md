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

## Shipping a release

Installer and updates are [Velopack](https://velopack.io). One command:

```pwsh
dotnet tool install -g vpk     # once
gh auth login                  # once — release.ps1 reads the token from gh

cd csharp
./release.ps1 -Version 0.0.5
```

That publishes `BetterRdp-win-Setup.exe` to a GitHub release tagged `v0.0.5`. Users
download it once; from then on the app updates itself. Add `-LocalOnly` to build the
installer into `artifacts/releases/` without publishing.

**How updates reach users:** `App.CheckForUpdatesAsync` polls the repo's releases on
launch, downloads any newer version in the background, and stages it to install *after*
the user quits — so an update never kills a live Connection or the `mstsc` windows it
spawned. The next launch is the new version. There is no update UI and nothing to click.
Deltas keep this cheap: a typical update ships ~0.2 MB against a 102 MB full package.

**Install shape:** per-user, into `%LocalAppData%\BetterRdp\`, no admin rights. Start-menu
and desktop shortcuts, plus an Add/Remove Programs entry. The Vault in `%APPDATA%` lives
outside the install directory, so it survives updates and uninstall.

Two things the release path depends on:

- `WindowsAppSDKSelfContained` — the app folder carries the entire Windows App Runtime
  (263 MB unpacked). Without it the publish output only has the Bootstrap DLLs and the app
  refuses to start on a machine that lacks the WinAppSDK redistributable.
- `Program.cs` — a hand-written entry point (`DISABLE_XAML_GENERATED_MAIN`) so
  `VelopackApp.Build().Run()` executes before the WinUI runtime. Velopack drives
  install/update/uninstall by re-running the exe with reserved arguments; handling them
  from the `App` constructor means booting XAML first, and `vpk pack` warns about it.

Releases are unsigned, so first-run shows SmartScreen's "Windows protected your PC"
(More info → Run anyway). Pass `-SignThumbprint <sha1>` to sign with an Authenticode
certificate from the current user's Personal store. This is separate from
`BETTER_RDP_SIGN_THUMBPRINT`, which signs generated `.rdp` files at runtime.

## Stack notes

- **DPAPI** is native (`System.Security.Cryptography.ProtectedData`, CurrentUser scope) — the
  same Win32 API the Python build used via pywin32, so blobs are byte-compatible.
- **AES-GCM** is native (`System.Security.Cryptography.AesGcm`); only **Argon2id** is a NuGet
  dep (`Konscious.Security.Cryptography.Argon2`).
- Vaults are a **fresh start** — the C# build does not read Python `vault.json` files (a
  deliberate decision; re-entering ~2 credentials is trivial vs. cross-language format parity).
- Packaging: unpackaged, self-contained (.NET *and* Windows App SDK), wrapped by Velopack.
  WinUI 3 supports neither NativeAOT nor trimming, so the ~260 MB folder is the floor.
