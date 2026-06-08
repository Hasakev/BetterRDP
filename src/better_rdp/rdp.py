""".rdp file text generation.

Turns a Server + Credential + Display Profile into the text of a `.rdp` file that
`mstsc.exe` consumes. The password is emitted as the `password 51:b:<HEX>` field, where
HEX is DPAPI-encrypted bytes of the UTF-16LE password — the only form mstsc understands.
"""

from __future__ import annotations

from .models import Credential, DisplayProfile, Server


def rdp_password_field(plaintext: str) -> str:
    """Return the single line ``password 51:b:<HEX>``.

    HEX is the uppercase hex of ``dpapi.protect(plaintext.encode("utf-16-le"))`` — the
    encoding mstsc expects in a saved .rdp file.
    """
    raise NotImplementedError


def generate(
    server: Server,
    credential: Credential,
    profile: DisplayProfile,
    plaintext_password: str,
) -> str:
    """Return the full text of a `.rdp` file for this Connection."""
    raise NotImplementedError
