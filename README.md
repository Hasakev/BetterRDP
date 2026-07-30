# Better RDP

A launcher for Windows Remote Desktop (`mstsc.exe`) built for **painless account rotation**.
Pick a server, a credential, and a display layout, then Launch — no re-typing passwords, no
hunting through `mstsc` dialogs. Passwords are stored encrypted and injected into a throwaway
`.rdp` file so the connection logs in silently.

There are two implementations of the same design:

| Folder | Stack | Status |
|---|---|---|
| [`python/`](python/) | Python 3.12+ · PySide6 · pywin32 | The original, fully working. |
| [`csharp/`](csharp/) | .NET 9 · WinUI 3 · Windows App SDK | A native-`.exe` port. Core fully tested; WinUI shell built. |

Both share the same architecture — a thin GUI over an `AppService` that generates `.rdp`
text and shells out to `mstsc` — and the same security model.

## Shared design docs

These live at the repo root because they describe the design, not one implementation:

- [`CONTEXT.md`](CONTEXT.md) — domain glossary (Server, Credential, Display Profile, Connection).
- [`docs/adr/0001-credential-vault-encryption.md`](docs/adr/0001-credential-vault-encryption.md) —
  the credential-vault crypto design: `DPAPI(AES-GCM(password, key = Argon2id(master, salt)))`.
- [`docs/SMOKE.md`](docs/SMOKE.md) — the manual smoke checklist (real `mstsc`, real monitors).

## The password trick

The load-bearing idea: `mstsc` accepts a saved password as the `.rdp` field
`password 51:b:<HEX>`, where `<HEX>` is a Windows **DPAPI** blob of the UTF-16LE password
bound to the current user. Generate that field, write a temp `.rdp`, launch `mstsc`, delete
the file. No password prompt. See the ADR for how the at-rest vault adds a master-password
layer on top.

## Verified `.rdp` publisher

Windows shows **"Unknown remote connection / Unknown publisher"** for unsigned `.rdp`
files, especially when local resources such as clipboard or printers are redirected. To
prevent that prompt, configure a trusted certificate thumbprint and Better RDP signs each
temp `.rdp` with `rdpsign.exe` before launching `mstsc`:

```pwsh
$env:BETTER_RDP_SIGN_THUMBPRINT = "<certificate SHA-1 thumbprint>"
```

Despite the `rdpsign.exe /sha256` switch name, Windows expects the certificate's normal
SHA-1 thumbprint here. The certificate must be in the current user's Personal store with a
private key and must chain to a trusted root (for a self-signed test cert, install it into
Trusted Root Certification Authorities / Trusted Publishers). Then `mstsc` can verify the
file and display the certificate subject as the Publisher.

## Install (Windows)

Download `BetterRdp-win-Setup.exe` from the
[latest release](https://github.com/Hasakev/BetterRDP/releases/latest) and run it. It
installs per-user (no admin), adds Start-menu and desktop shortcuts, and from then on the
app updates itself in the background whenever a new release is posted — updates apply
after you quit, never mid-session.

### The unsigned-installer warnings

Releases are unsigned, so the first install costs two clicks:

1. The browser says the file "isn't commonly downloaded" → **Keep**.
2. Windows says "Windows protected your PC" → **More info → Run anyway**.

Both come from Mark-of-the-Web, the tag a browser attaches to downloaded files. Neither
recurs: updates are fetched by the app itself rather than a browser, so they carry no
Mark-of-the-Web and never trip SmartScreen. Installing per-user also means no UAC prompt.
It is one warning, once per person.

To skip both entirely, hand out the first install over the intranet instead of a GitHub
download — copy `BetterRdp-win-Setup.exe` to a file share. Files opened from a UNC path in
the Local Intranet zone get no Mark-of-the-Web, so SmartScreen never runs. Updates still
come from GitHub as normal. Failing that, clear the tag by hand:

```pwsh
Unblock-File .\BetterRdp-win-Setup.exe
```

A self-signed certificate does **not** help here — SmartScreen keys its reputation on
certificates Microsoft has seen before, so a homemade one is no better than unsigned. That
is the opposite of the `.rdp` signing above, where self-signed works fine because you are
choosing to trust it on your own machines. See
[`csharp/README.md`](csharp/README.md#shipping-a-release) for the signing hook if you ever
do buy a certificate.

## Quick start (from source)

**Python:**
```pwsh
cd python
uv sync
uv run better-rdp
```

**C#:**
```pwsh
cd csharp
dotnet run --project src/BetterRdp.App
```

See each folder's `README.md` for details.

## Changing the logo

The header logo is [`csharp/src/BetterRdp.App/Assets/AppLogo.png`](csharp/src/BetterRdp.App/Assets/AppLogo.png).
Replace that file with your PNG, keep the filename, then rebuild. It appears at 42 × 42
pixels in the header. Use a square image of at least 256 × 256 with a transparent
background — the taskbar icon is generated from it and cannot be sharper than the source.

The taskbar and Explorer icon is `Assets/AppIcon.ico`, embedded in the `.exe` via
`<ApplicationIcon>`. After changing the logo, regenerate it:

```pwsh
python -c "from PIL import Image; s=Image.open('csharp/src/BetterRdp.App/Assets/AppLogo.png').convert('RGBA'); n=max(s.size); c=Image.new('RGBA',(n,n),(0,0,0,0)); c.paste(s,((n-s.width)//2,(n-s.height)//2)); c.resize((256,256),Image.LANCZOS).save('csharp/src/BetterRdp.App/Assets/AppIcon.ico',sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)])"
```

Both files are read from disk at runtime, so they carry `CopyToPublishDirectory` in the
`.csproj`. Without it they reach `bin/` but are dropped by `dotnet publish`, and the
shipped build silently falls back to no logo and the stock icon.
