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

## Quick start

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
