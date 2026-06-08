"""The Vault: Argon2id key derivation + AES-GCM + DPAPI, persisted as JSON.

See ADR 0001. Stored password layering is:
    dpapi.protect( aes_gcm_encrypt( password, key=Argon2id(master, salt) ) )

Heavy imports (argon2, cryptography) are deferred into function bodies so this module
imports cleanly without those packages installed (for test collection).
"""

from __future__ import annotations

from pathlib import Path

from .models import Credential, Server


def derive_key(master: str, salt: bytes) -> bytes:
    """Derive a 32-byte key from the Master Password via Argon2id. Deterministic for a
    given (master, salt)."""
    raise NotImplementedError


def encrypt_secret(plaintext: str, key: bytes) -> bytes:
    """AES-GCM encrypt. Returns nonce || ciphertext || tag."""
    raise NotImplementedError


def decrypt_secret(blob: bytes, key: bytes) -> str:
    """AES-GCM decrypt. Raises (InvalidTag) if the key is wrong — never returns garbage."""
    raise NotImplementedError


class Vault:
    """Holds Servers and Credentials; persists to a JSON file with encrypted secrets."""

    @classmethod
    def create(cls, path: Path, master: str) -> "Vault":
        """Create a new, empty Vault at ``path`` keyed by ``master`` (set-once for v1)."""
        raise NotImplementedError

    @classmethod
    def open(cls, path: Path, master: str) -> "Vault":
        """Open an existing Vault. Wrong master must fail loudly, not silently."""
        raise NotImplementedError

    def add_credential(self, credential: Credential) -> None:
        """Store a Credential; its plaintext password is encrypted, never written raw."""
        raise NotImplementedError

    def get_password(self, credential_id: str) -> str:
        """Decrypt and return the plaintext password for a stored Credential."""
        raise NotImplementedError

    def add_server(self, server: Server) -> None:
        raise NotImplementedError

    def save(self) -> None:
        """Persist to the JSON file on disk."""
        raise NotImplementedError
