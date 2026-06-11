# Better RDP — Python

The original implementation: a PySide6 GUI over an `AppService` that generates `.rdp` files
and launches `mstsc.exe`. See [`../CONTEXT.md`](../CONTEXT.md) for the domain glossary and
[`../docs/adr/0001-credential-vault-encryption.md`](../docs/adr/0001-credential-vault-encryption.md)
for the crypto design.

## Layout

| Path | What it is |
|---|---|
| `src/better_rdp/dpapi.py` | Thin wrappers over Windows DPAPI (via pywin32). |
| `src/better_rdp/rdp.py` | `.rdp` file text generation, incl. the `password 51:b:` field. |
| `src/better_rdp/vault.py` | The Vault: Argon2id + AES-GCM + DPAPI, persisted as JSON. |
| `src/better_rdp/launch.py` | Materialise a temp `.rdp`, run `mstsc`, delete the temp file. |
| `src/better_rdp/app.py` | `AppService` — the seam between the GUI and the domain. |
| `src/better_rdp/gui.py` | The PySide6 window (server list + connection card + dialogs). |
| `src/better_rdp/theme.py` | The dark "midnight" QSS theme. |
| `tests/` | The pytest contract suite (crypto / rdp / launch / app-service). |

## Develop

```pwsh
uv sync                 # create the venv and install deps (incl. dev)
uv run pytest           # run the contract suite
uv run better-rdp       # launch the GUI
```

> Note: the dev virtualenv (`.venv`) is not tracked. If you had one at the repo root from
> before the `python/` reorg, recreate it here with `uv sync`.

## Stack

Python 3.12+ · PySide6 · pywin32 · argon2-cffi · cryptography · pytest.
