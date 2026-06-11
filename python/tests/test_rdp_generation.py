"""Q2 contract: field-level assertions on generated .rdp text, one test per display mode.

The password field is checked *structurally* (it's a non-deterministic DPAPI blob, so an
exact value can't be asserted). Each display mode emits a *different* field set — that is
where bugs hide, so each mode gets its own test.
"""

import binascii
import re

import pytest

from better_rdp import dpapi, rdp
from better_rdp.models import Credential, DisplayMode, DisplayProfile, Server
from conftest import requires_windows


def parse(text: str) -> dict[str, tuple[str, str]]:
    """Parse .rdp text into {key: (type, value)}. Lines are `key:type:value`."""
    fields: dict[str, tuple[str, str]] = {}
    for line in text.splitlines():
        line = line.strip()
        if not line:
            continue
        key, typ, value = line.split(":", 2)
        fields[key] = (typ, value)
    return fields


@pytest.fixture
def server():
    return Server(name="Prod box", address="prod01.intranet.local")


@pytest.fixture
def credential():
    return Credential(id="c1", username="alice", password="s3cret-pw")


def test_fullscreen_multimon_emits_selected_monitors(server, credential):
    profile = DisplayProfile(
        name="All three", mode=DisplayMode.FULLSCREEN_MULTIMON, monitors=[0, 1, 2]
    )
    fields = parse(rdp.generate(server, credential, profile, "s3cret-pw"))

    assert fields["full address"] == ("s", "prod01.intranet.local")
    assert fields["username"] == ("s", "alice")
    assert fields["screen mode id"] == ("i", "2")
    assert fields["use multimon"] == ("i", "1")
    assert fields["selectedmonitors"] == ("s", "0,1,2")


def test_windowed_fixed_emits_resolution_and_no_monitors(server, credential):
    profile = DisplayProfile(
        name="Window 1080p",
        mode=DisplayMode.WINDOWED_FIXED,
        width=1920,
        height=1080,
    )
    fields = parse(rdp.generate(server, credential, profile, "s3cret-pw"))

    assert fields["screen mode id"] == ("i", "1")
    assert fields["use multimon"] == ("i", "0")
    assert fields["desktopwidth"] == ("i", "1920")
    assert fields["desktopheight"] == ("i", "1080")
    # A windowed profile must NOT span monitors.
    assert "selectedmonitors" not in fields


def test_windowed_dynamic_follows_window_and_no_monitors(server, credential):
    profile = DisplayProfile(name="Dynamic", mode=DisplayMode.WINDOWED_DYNAMIC)
    fields = parse(rdp.generate(server, credential, profile, "s3cret-pw"))

    assert fields["screen mode id"] == ("i", "1")
    assert fields["dynamic resolution"] == ("i", "1")
    assert "selectedmonitors" not in fields


def test_domain_emitted_only_when_present(server):
    profile = DisplayProfile(name="Dynamic", mode=DisplayMode.WINDOWED_DYNAMIC)

    with_domain = Credential(id="c2", username="bob", domain="CORP", password="x")
    fields = parse(rdp.generate(server, with_domain, profile, "x"))
    assert fields["domain"] == ("s", "CORP")

    without_domain = Credential(id="c3", username="bob", password="x")
    fields = parse(rdp.generate(server, without_domain, profile, "x"))
    assert "domain" not in fields


def test_scale_factor_emitted_when_set(server, credential):
    profile = DisplayProfile(
        name="HiDPI",
        mode=DisplayMode.FULLSCREEN_MULTIMON,
        monitors=[0],
        scale_factor=150,
    )
    fields = parse(rdp.generate(server, credential, profile, "s3cret-pw"))
    assert fields["desktopscalefactor"] == ("i", "150")


def test_suppresses_remote_identity_prompt(server, credential):
    profile = DisplayProfile(name="Dynamic", mode=DisplayMode.WINDOWED_DYNAMIC)
    fields = parse(rdp.generate(server, credential, profile, "s3cret-pw"))
    # 0 = "connect and don't warn me" on a failed server-cert check (intranet scope).
    assert fields["authentication level"] == ("i", "0")


def test_password_field_is_structurally_a_dpapi_blob(server, credential):
    profile = DisplayProfile(name="Dynamic", mode=DisplayMode.WINDOWED_DYNAMIC)
    text = rdp.generate(server, credential, profile, "s3cret-pw")
    # mstsc expects: password 51:b:<uppercase hex of a DPAPI blob>
    assert re.search(r"^password 51:b:[0-9A-F]+$", text, flags=re.MULTILINE)


@requires_windows
def test_password_blob_roundtrips_via_dpapi():
    """The durable byproduct of the manual mstsc smoke: the blob we put in the .rdp must
    DPAPI-decrypt back to the UTF-16LE password mstsc expects. Locks the format mstsc
    validated so a refactor can't silently break it, even though we can't run mstsc here.
    """
    line = rdp.rdp_password_field("s3cret-pw")
    assert line.startswith("password 51:b:")
    hex_blob = line.split(":", 2)[2]
    recovered = dpapi.unprotect(binascii.unhexlify(hex_blob))
    assert recovered.decode("utf-16-le") == "s3cret-pw"
