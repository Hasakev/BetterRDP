"""The Vault: Argon2id key derivation + AES-GCM + DPAPI, persisted as JSON.

See ADR 0001. Stored password layering is:
    dpapi.protect( aes_gcm_encrypt( password, key=Argon2id(master, salt) ) )

Heavy imports (argon2, cryptography) are deferred into function bodies so this module
imports cleanly without those packages installed (for test collection).
"""

from __future__ import annotations

import base64
import json
import os
from pathlib import Path

from . import dpapi
from .models import Credential, DisplayMode, DisplayProfile, Server

# Argon2id parameters. Fixed so a derived key is reproducible across runs for a given
# (master, salt). These are interactive-login-grade, not archival-grade.
_ARGON2_TIME_COST = 3
_ARGON2_MEMORY_COST = 64 * 1024  # 64 MiB
_ARGON2_PARALLELISM = 4
_KEY_LEN = 32  # AES-256
_SALT_LEN = 16
_NONCE_LEN = 12

# A constant sentinel encrypted under the derived key at create time. Decrypting it on
# open is how we tell a correct Master Password from a wrong one (authenticated failure).
_VERIFIER_PLAINTEXT = "better-rdp-verifier-v1"


def derive_key(master: str, salt: bytes) -> bytes:
    """Derive a 32-byte key from the Master Password via Argon2id. Deterministic for a
    given (master, salt)."""
    from argon2.low_level import Type, hash_secret_raw

    return hash_secret_raw(
        secret=master.encode("utf-8"),
        salt=salt,
        time_cost=_ARGON2_TIME_COST,
        memory_cost=_ARGON2_MEMORY_COST,
        parallelism=_ARGON2_PARALLELISM,
        hash_len=_KEY_LEN,
        type=Type.ID,
    )


def encrypt_secret(plaintext: str, key: bytes) -> bytes:
    """AES-GCM encrypt. Returns nonce || ciphertext || tag."""
    from cryptography.hazmat.primitives.ciphers.aead import AESGCM

    nonce = os.urandom(_NONCE_LEN)
    ct = AESGCM(key).encrypt(nonce, plaintext.encode("utf-8"), None)
    return nonce + ct


def decrypt_secret(blob: bytes, key: bytes) -> str:
    """AES-GCM decrypt. Raises (InvalidTag) if the key is wrong — never returns garbage."""
    from cryptography.hazmat.primitives.ciphers.aead import AESGCM

    nonce, ct = blob[:_NONCE_LEN], blob[_NONCE_LEN:]
    return AESGCM(key).decrypt(nonce, ct, None).decode("utf-8")


def _wrap(plaintext: str, key: bytes) -> str:
    """Inner AES-GCM then outer DPAPI, base64-encoded for JSON. See ADR 0001."""
    return base64.b64encode(dpapi.protect(encrypt_secret(plaintext, key))).decode("ascii")


def _unwrap(stored_b64: str, key: bytes) -> str:
    """Reverse of :func:`_wrap`: strip DPAPI, then AES-GCM decrypt."""
    return decrypt_secret(dpapi.unprotect(base64.b64decode(stored_b64)), key)


class Vault:
    """Holds Servers and Credentials; persists to a JSON file with encrypted secrets."""

    def __init__(self, path: Path, key: bytes, salt: bytes, data: dict) -> None:
        self._path = Path(path)
        self._key = key
        self._salt = salt
        self._data = data

    @classmethod
    def create(cls, path: Path, master: str) -> "Vault":
        """Create a new, empty Vault at ``path`` keyed by ``master`` (set-once for v1)."""
        salt = os.urandom(_SALT_LEN)
        key = derive_key(master, salt)
        data = {
            "kdf": {"salt": base64.b64encode(salt).decode("ascii")},
            "verifier": _wrap(_VERIFIER_PLAINTEXT, key),
            "credentials": [],
            "servers": [],
            "profiles": [],
        }
        return cls(path, key, salt, data)

    @classmethod
    def open(cls, path: Path, master: str) -> "Vault":
        """Open an existing Vault. Wrong master must fail loudly, not silently."""
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        salt = base64.b64decode(data["kdf"]["salt"])
        key = derive_key(master, salt)
        # Authenticated check: a wrong key makes this raise InvalidTag, never succeed.
        if _unwrap(data["verifier"], key) != _VERIFIER_PLAINTEXT:
            raise ValueError("Master password verification failed")
        # Tolerate vaults written before a section existed.
        data.setdefault("profiles", [])
        return cls(path, key, salt, data)

    def add_credential(self, credential: Credential) -> None:
        """Store a Credential; its plaintext password is encrypted, never written raw."""
        self._data["credentials"].append(
            {
                "id": credential.id,
                "username": credential.username,
                "domain": credential.domain,
                "secret": _wrap(credential.password or "", self._key),
            }
        )

    def get_password(self, credential_id: str) -> str:
        """Decrypt and return the plaintext password for a stored Credential."""
        for entry in self._data["credentials"]:
            if entry["id"] == credential_id:
                return _unwrap(entry["secret"], self._key)
        raise KeyError(credential_id)

    def credentials(self) -> list[Credential]:
        """All stored Credentials, *without* plaintext passwords (those stay encrypted on
        disk and are only decrypted on demand via :meth:`get_password`)."""
        return [
            Credential(id=e["id"], username=e["username"], domain=e["domain"])
            for e in self._data["credentials"]
        ]

    def add_server(self, server: Server) -> None:
        self._data["servers"].append(_server_to_dict(server))

    def servers(self) -> list[Server]:
        return [_server_from_dict(e) for e in self._data["servers"]]

    def update_server(self, server: Server) -> None:
        """Replace the stored Server matching ``server.name`` (e.g. to record last-used)."""
        for i, e in enumerate(self._data["servers"]):
            if e["name"] == server.name:
                self._data["servers"][i] = _server_to_dict(server)
                return
        raise KeyError(server.name)

    def add_profile(self, profile: DisplayProfile) -> None:
        self._data["profiles"].append(_profile_to_dict(profile))

    def profiles(self) -> list[DisplayProfile]:
        return [_profile_from_dict(e) for e in self._data["profiles"]]

    def save(self) -> None:
        """Persist to the JSON file on disk."""
        self._path.write_text(json.dumps(self._data, indent=2), encoding="utf-8")


# --- (de)serialisation of the non-secret records -----------------------------------

def _server_to_dict(server: Server) -> dict:
    return {
        "name": server.name,
        "address": server.address,
        "notes": server.notes,
        "last_credential_id": server.last_credential_id,
        "last_profile_name": server.last_profile_name,
    }


def _server_from_dict(e: dict) -> Server:
    return Server(
        name=e["name"],
        address=e["address"],
        notes=e.get("notes", ""),
        last_credential_id=e.get("last_credential_id"),
        last_profile_name=e.get("last_profile_name"),
    )


def _profile_to_dict(profile: DisplayProfile) -> dict:
    return {
        "name": profile.name,
        "mode": profile.mode.value,
        "monitors": profile.monitors,
        "width": profile.width,
        "height": profile.height,
        "scale_factor": profile.scale_factor,
    }


def _profile_from_dict(e: dict) -> DisplayProfile:
    return DisplayProfile(
        name=e["name"],
        mode=DisplayMode(e["mode"]),
        monitors=list(e.get("monitors", [])),
        width=e.get("width"),
        height=e.get("height"),
        scale_factor=e.get("scale_factor"),
    )
