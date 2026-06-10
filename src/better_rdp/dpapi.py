"""Thin wrappers over Windows DPAPI (CryptProtectData / CryptUnprotectData).

Windows-only. Heavy imports (win32crypt) are deferred into the function bodies so this
module imports cleanly on any platform for test collection.
"""

from __future__ import annotations


def protect(data: bytes, entropy: bytes = b"") -> bytes:
    """DPAPI-encrypt ``data`` bound to the current Windows user (CurrentUser scope)."""
    import win32crypt

    # CryptProtectData(data, description, entropy, reserved, prompt_struct, flags).
    # CRYPTPROTECT_UI_FORBIDDEN (0x1) keeps it non-interactive.
    return win32crypt.CryptProtectData(data, None, entropy, None, None, 0x1)


def unprotect(blob: bytes, entropy: bytes = b"") -> bytes:
    """DPAPI-decrypt a blob produced by :func:`protect`."""
    import win32crypt

    # CryptUnprotectData returns (description, data); we want the data.
    _, data = win32crypt.CryptUnprotectData(blob, entropy, None, None, 0x1)
    return data
