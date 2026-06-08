"""Thin wrappers over Windows DPAPI (CryptProtectData / CryptUnprotectData).

Windows-only. Heavy imports (win32crypt) are deferred into the function bodies so this
module imports cleanly on any platform for test collection.
"""

from __future__ import annotations


def protect(data: bytes, entropy: bytes = b"") -> bytes:
    """DPAPI-encrypt ``data`` bound to the current Windows user (CurrentUser scope)."""
    raise NotImplementedError


def unprotect(blob: bytes, entropy: bytes = b"") -> bytes:
    """DPAPI-decrypt a blob produced by :func:`protect`."""
    raise NotImplementedError
