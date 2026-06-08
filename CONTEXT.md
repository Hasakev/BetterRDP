# Better RDP

A custom Windows GUI that launches Remote Desktop sessions. It is a *launcher*: it
generates `.rdp` files and shells out to the built-in `mstsc.exe`, rather than embedding
a remote-desktop renderer. Its reason to exist is friction the built-in client and tools
like mRemoteNG handle poorly — chiefly fast switching between multiple accounts on the
same host without re-typing passwords.

**Scope boundary — connectivity:** the Launcher assumes the Server is already reachable on
the intranet. Getting there (VPN when off-site) is handled externally and is out of scope.
No RD Gateway / jump-host support. Scale is small (~7 Servers), so there is deliberately no
search, favourites, grouping, or separate connection-history surface — the Server list *is*
the launch surface.

## Language

**Launcher**:
The custom GUI app itself. It does not render remote desktops; it composes a connection
and hands off to `mstsc.exe`.
_Avoid_: client, viewer (those imply we render the session ourselves, which we don't)

**Server**:
A remote host you connect to (address + display name + connection defaults).
_Avoid_: host, machine, target (pick one — Server)

**mstsc**:
The built-in Windows Remote Desktop client (`mstsc.exe`) that the Launcher invokes with
a generated `.rdp` file.

**Credential**:
A username + password (+ optional domain) you authenticate *with*. Independent of Server:
any Credential can be used against any Server (many-to-many). What the user calls an
"account."
_Avoid_: account, login, user (use Credential)

**Connection**:
A single act of launching = one chosen Server + one chosen Credential + a monitor/display
choice, materialised as one generated `.rdp` file and one `mstsc` window. Launching twice
yields two independent Connections (e.g. same Server under two Credentials, side by side).
_Avoid_: session (the live remote session is owned by mstsc/the server, not the Launcher)

**Display Profile**:
A named, reusable display layout selected at launch — which monitors a Connection opens
on, plus a display *mode* (fullscreen-on-selected-monitors / windowed-fixed-resolution /
windowed-dynamic) and an optional desktop scale factor. E.g. "All three", "Laptop only",
"Left two". Each Server defaults to the Display Profile last used with it.
_Avoid_: layout, screen config, monitor set (use Display Profile)

**Vault**:
The Launcher's own store (in `%APPDATA%`) holding Servers and Credentials. Passwords are
encrypted with a key derived from the Master Password (Argon2id) and then wrapped with
Windows DPAPI. See ADR 0001.
_Avoid_: keystore, database (use Vault)

**Master Password**:
A secret the user types **once per app start** to unlock the Vault. It is a real
encryption key (via KDF), not just a UI gate — without it the secrets are unreadable even
by the logged-in Windows user.

## Relationships

- **Server ↔ Credential**: many-to-many. The Launcher remembers the last Credential used
  per Server as a default, but switching is a one-click override at launch time.
- **Vault → .rdp**: at launch the Launcher decrypts a Credential in memory, re-encrypts the
  password as a plain DPAPI blob (all `mstsc` understands), writes it into a temp `.rdp`,
  launches `mstsc`, then deletes the temp file.
