"""Contract for the orchestration layer that sits between the GUI and the Vault.

AppService is the only thing the Qt window talks to: it lists Servers / Credentials /
Display Profiles, launches a Connection, and remembers the last Credential + Profile used
per Server. These tests pin that behaviour with a fake mstsc runner so nothing spawns.

Anything that round-trips a real Vault touches DPAPI, so those tests are Windows-only.
"""

import pytest

from better_rdp.app import AppService
from better_rdp.models import Credential, DisplayMode, DisplayProfile, Server
from conftest import requires_windows


def _seed(svc: AppService) -> None:
    svc.add_credential(Credential(id="alice", username="alice", password="pw-alice"))
    svc.add_credential(Credential(id="bob", username="bob", password="pw-bob"))
    svc.add_profile(DisplayProfile(name="Dynamic", mode=DisplayMode.WINDOWED_DYNAMIC))
    svc.add_server(Server(name="Prod", address="prod01.intranet.local"))


@requires_windows
def test_open_or_create_creates_then_opens(tmp_path):
    path = tmp_path / "vault.json"
    created = AppService.open_or_create(path, "master")
    created.add_credential(Credential(id="c1", username="alice", password="pw"))
    created.save()
    assert path.exists()

    reopened = AppService.open_or_create(path, "master")
    assert [c.id for c in reopened.credentials()] == ["c1"]


@requires_windows
def test_lists_expose_what_the_gui_renders(tmp_path):
    svc = AppService.open_or_create(tmp_path / "vault.json", "master")
    _seed(svc)
    assert [s.name for s in svc.servers()] == ["Prod"]
    assert [c.id for c in svc.credentials()] == ["alice", "bob"]
    assert [p.name for p in svc.profiles()] == ["Dynamic"]
    # Credentials handed to the GUI must not carry plaintext passwords around.
    assert all(c.password is None for c in svc.credentials())


@requires_windows
def test_launch_passes_decrypted_password_into_the_rdp(tmp_path):
    svc = AppService.open_or_create(tmp_path / "vault.json", "master")
    _seed(svc)
    seen = {}

    def runner(argv):
        with open(argv[1], encoding="utf-8") as f:
            seen["text"] = f.read()
        return 0

    svc.launch("Prod", "bob", "Dynamic", runner=runner)

    # The generated .rdp must carry bob's username and a DPAPI password blob.
    assert "username:s:bob" in seen["text"]
    assert "password 51:b:" in seen["text"]


@requires_windows
def test_launch_remembers_last_credential_and_profile_per_server(tmp_path):
    path = tmp_path / "vault.json"
    svc = AppService.open_or_create(path, "master")
    _seed(svc)

    svc.launch("Prod", "bob", "Dynamic", runner=lambda argv: 0)

    reopened = AppService.open_or_create(path, "master")
    prod = next(s for s in reopened.servers() if s.name == "Prod")
    assert prod.last_credential_id == "bob"
    assert prod.last_profile_name == "Dynamic"
