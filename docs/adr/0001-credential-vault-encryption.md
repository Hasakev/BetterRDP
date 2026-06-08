# Credential vault: Argon2id-derived key wrapped in DPAPI

Passwords in the Vault are encrypted with AES using a key derived from a user-supplied
Master Password via Argon2id, and that ciphertext is then wrapped with Windows DPAPI
(CurrentUser scope). We chose two layers deliberately: DPAPI alone binds secrets to the
Windows user but leaves them readable by anyone with that user's session (e.g. a copied
vault file at an unlocked machine); the Master Password adds a layer that is not derivable
from the Windows session, so an attacker needs *both* the Windows session *and* the Master
Password. We rejected a UI-gate-only master password (protects the window, not the
secrets) and rejected using Windows Credential Manager as the vault (no real gain on a
single-user machine, extra API surface).

## Consequences

- The Master Password is required once per app start; the derived key lives in memory for
  the process lifetime. Losing it means the Vault is unrecoverable by design.
- `mstsc` only understands a plain DPAPI blob in the `.rdp` `password 51:b:` field, so at
  launch the password is briefly materialised as a DPAPI blob in a temp `.rdp` that is
  deleted immediately after launch. That temp file is decryptable by the Windows user for
  its short lifetime — an accepted residual risk.
