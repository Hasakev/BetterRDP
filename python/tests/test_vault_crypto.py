"""Q3 contract: the crypto/vault invariants from ADR 0001.

These are security guarantees, so a regression is a silent failure, not a crash. Argon2
and AES tests are cross-platform; tests that touch DPAPI or the on-disk Vault are marked
Windows-only.

On-disk Vault JSON schema these tests rely on (part of the contract):
    {
      "kdf":  {"salt": "<base64>"},
      "credentials": [ {"id","username","domain","secret": "<base64 of dpapi(aes_gcm)>"} ],
      "servers": [ ... ]
    }
"""

import base64
import json

import pytest

from better_rdp import dpapi, vault
from better_rdp.models import Credential
from conftest import requires_windows

SECRET = "hunter2-unique-marker-9f3a"


# --- 1. key derivation -------------------------------------------------------------

def test_key_derivation_is_deterministic_and_salt_sensitive():
    salt_a = b"0123456789abcdef"
    salt_b = b"fedcba9876543210"
    assert vault.derive_key("master", salt_a) == vault.derive_key("master", salt_a)
    assert vault.derive_key("master", salt_a) != vault.derive_key("master", salt_b)
    assert vault.derive_key("master", salt_a) != vault.derive_key("other", salt_a)


# --- 2 & 3. AES-GCM round-trip and authenticated failure ---------------------------

def test_aes_roundtrip():
    key = vault.derive_key("master", b"0123456789abcdef")
    blob = vault.encrypt_secret(SECRET, key)
    assert vault.decrypt_secret(blob, key) == SECRET


def test_wrong_key_fails_loudly_never_returns_garbage():
    key = vault.derive_key("master", b"0123456789abcdef")
    wrong = vault.derive_key("WRONG", b"0123456789abcdef")
    blob = vault.encrypt_secret(SECRET, key)
    # Authenticated encryption (GCM) => decrypt with the wrong key must raise, not return
    # corrupted plaintext.
    with pytest.raises(Exception):
        vault.decrypt_secret(blob, wrong)


# --- 4. no plaintext on disk -------------------------------------------------------

@requires_windows
def test_no_plaintext_password_on_disk(tmp_path):
    path = tmp_path / "vault.json"
    v = vault.Vault.create(path, "masterA")
    v.add_credential(Credential(id="c1", username="alice", password=SECRET))
    v.save()

    raw = path.read_bytes()
    assert SECRET.encode("utf-8") not in raw
    assert SECRET.encode("utf-16-le") not in raw


# --- 5. the ADR 0001 two-layer guarantee, made executable --------------------------

@requires_windows
def test_master_password_layer_holds_even_with_dpapi_stripped(tmp_path):
    """Prove the inner layer is real: an attacker who is the same Windows user (so DPAPI
    decrypts for them) still cannot recover the password without the Master Password."""
    path = tmp_path / "vault.json"
    v = vault.Vault.create(path, "masterA")
    v.add_credential(Credential(id="c1", username="alice", password=SECRET))
    v.save()

    data = json.loads(path.read_text())
    stored = base64.b64decode(data["credentials"][0]["secret"])
    inner = dpapi.unprotect(stored)  # strip the DPAPI layer, as the Windows user could

    # The DPAPI-stripped bytes are still AES-GCM ciphertext, not the plaintext.
    assert SECRET.encode("utf-8") not in inner
    assert SECRET.encode("utf-16-le") not in inner
    # And without the master-derived key, decryption fails.
    wrong = vault.derive_key("guessed-master", base64.b64decode(data["kdf"]["salt"]))
    with pytest.raises(Exception):
        vault.decrypt_secret(inner, wrong)


@requires_windows
def test_open_with_wrong_master_fails_loudly(tmp_path):
    path = tmp_path / "vault.json"
    v = vault.Vault.create(path, "masterA")
    v.add_credential(Credential(id="c1", username="alice", password=SECRET))
    v.save()

    with pytest.raises(Exception):
        vault.Vault.open(path, "wrong-master")


@requires_windows
def test_correct_master_round_trips_credential(tmp_path):
    path = tmp_path / "vault.json"
    v = vault.Vault.create(path, "masterA")
    v.add_credential(Credential(id="c1", username="alice", password=SECRET))
    v.save()

    reopened = vault.Vault.open(path, "masterA")
    assert reopened.get_password("c1") == SECRET


# --- 6. DPAPI round-trip -----------------------------------------------------------

@requires_windows
def test_dpapi_roundtrip():
    assert dpapi.unprotect(dpapi.protect(b"abc-123")) == b"abc-123"


# --- 7. profile persistence tolerates a bare-string mode ---------------------------

@requires_windows
def test_add_profile_accepts_string_mode_from_qt(tmp_path):
    """Qt hands back DisplayMode (a str subclass) as a bare str via QVariant; storing such
    a profile must not crash on ``mode.value`` (regression for the GUI add-profile path)."""
    from better_rdp.models import DisplayMode, DisplayProfile

    path = tmp_path / "vault.json"
    v = vault.Vault.create(path, "masterA")
    v.add_profile(DisplayProfile(name="P", mode="fullscreen_multimon", monitors=[0]))
    v.save()

    reopened = vault.Vault.open(path, "masterA")
    profile = reopened.profiles()[0]
    assert profile.mode is DisplayMode.FULLSCREEN_MULTIMON
