""".rdp file text generation.

Turns a Server + Credential + Display Profile into the text of a `.rdp` file that
`mstsc.exe` consumes. The password is emitted as the `password 51:b:<HEX>` field, where
HEX is DPAPI-encrypted bytes of the UTF-16LE password — the only form mstsc understands.
"""

from __future__ import annotations

import binascii

from . import dpapi
from .models import Credential, DisplayMode, DisplayProfile, Server


def rdp_password_field(plaintext: str) -> str:
    """Return the single line ``password 51:b:<HEX>``.

    HEX is the uppercase hex of ``dpapi.protect(plaintext.encode("utf-16-le"))`` — the
    encoding mstsc expects in a saved .rdp file.
    """
    blob = dpapi.protect(plaintext.encode("utf-16-le"))
    hex_blob = binascii.hexlify(blob).decode("ascii").upper()
    return f"password 51:b:{hex_blob}"


def generate(
    server: Server,
    credential: Credential,
    profile: DisplayProfile,
    plaintext_password: str,
) -> str:
    """Return the full text of a `.rdp` file for this Connection."""
    lines: list[str] = [
        f"full address:s:{server.address}",
        f"username:s:{credential.username}",
    ]
    if credential.domain:
        lines.append(f"domain:s:{credential.domain}")
    lines.append(rdp_password_field(plaintext_password))

    # Suppress the "identity of the remote computer cannot be verified" prompt that mstsc
    # raises on a self-signed/untrusted server cert. 0 = connect and don't warn. This is
    # the right default for a trusted intranet (the project's stated scope); it trades the
    # cert-mismatch warning away, which is acceptable there but not over an untrusted link.
    lines.append("authentication level:i:0")

    if profile.mode is DisplayMode.FULLSCREEN_MULTIMON:
        # screen mode id 2 = full screen; span the selected monitors.
        lines.append("screen mode id:i:2")
        lines.append("use multimon:i:1")
        if profile.monitors:
            lines.append("selectedmonitors:s:" + ",".join(str(m) for m in profile.monitors))
    elif profile.mode is DisplayMode.WINDOWED_FIXED:
        # screen mode id 1 = windowed; fixed resolution, single screen.
        lines.append("screen mode id:i:1")
        lines.append("use multimon:i:0")
        if profile.width is not None:
            lines.append(f"desktopwidth:i:{profile.width}")
        if profile.height is not None:
            lines.append(f"desktopheight:i:{profile.height}")
    elif profile.mode is DisplayMode.WINDOWED_DYNAMIC:
        # Windowed, resolution follows the window as it's resized.
        lines.append("screen mode id:i:1")
        lines.append("use multimon:i:0")
        lines.append("dynamic resolution:i:1")

    if profile.scale_factor is not None:
        lines.append(f"desktopscalefactor:i:{profile.scale_factor}")

    return "\n".join(lines) + "\n"
