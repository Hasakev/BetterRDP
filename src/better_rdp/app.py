"""Application service: the single seam between the GUI and the domain.

The Qt window never touches the Vault, rdp text, or mstsc directly — it goes through an
AppService. That keeps the orchestration (decrypt password -> generate .rdp -> launch ->
remember last-used) testable without spawning Qt or mstsc.
"""

from __future__ import annotations

import os
from pathlib import Path

from . import launch as _launch
from .launch import Runner
from .models import Credential, DisplayProfile, Server
from .vault import Vault


def default_vault_path() -> Path:
    """``%APPDATA%/BetterRDP/vault.json`` (falls back to the home dir off-Windows)."""
    base = os.environ.get("APPDATA") or str(Path.home())
    return Path(base) / "BetterRDP" / "vault.json"


class AppService:
    """Wraps an unlocked :class:`Vault` and exposes the operations the GUI needs."""

    def __init__(self, vault: Vault) -> None:
        self._vault = vault

    @classmethod
    def open_or_create(cls, path: Path, master: str) -> "AppService":
        """Open the Vault at ``path`` if it exists, else create a fresh one keyed by
        ``master``. Opening with the wrong Master Password fails loudly (see Vault.open)."""
        path = Path(path)
        if path.exists():
            return cls(Vault.open(path, master))
        path.parent.mkdir(parents=True, exist_ok=True)
        return cls(Vault.create(path, master))

    # --- reads the GUI renders -----------------------------------------------------

    def servers(self) -> list[Server]:
        return self._vault.servers()

    def credentials(self) -> list[Credential]:
        """Credentials without plaintext passwords — safe to hold in the UI layer."""
        return self._vault.credentials()

    def profiles(self) -> list[DisplayProfile]:
        return self._vault.profiles()

    # --- mutations -----------------------------------------------------------------

    def add_server(self, server: Server) -> None:
        self._vault.add_server(server)
        self._vault.save()

    def add_credential(self, credential: Credential) -> None:
        self._vault.add_credential(credential)
        self._vault.save()

    def add_profile(self, profile: DisplayProfile) -> None:
        self._vault.add_profile(profile)
        self._vault.save()

    def save(self) -> None:
        self._vault.save()

    # --- the launch path -----------------------------------------------------------

    def launch(
        self,
        server_name: str,
        credential_id: str,
        profile_name: str,
        *,
        runner: Runner | None = None,
    ) -> object:
        """Launch a Connection: decrypt the Credential, generate + run a temp .rdp, then
        record this Credential/Profile as the Server's new defaults."""
        server = self._find(self.servers(), "name", server_name)
        profile = self._find(self.profiles(), "name", profile_name)
        cred = self._find(self.credentials(), "id", credential_id)
        # Re-attach the decrypted password just for this launch; never persisted in memory
        # on the listed Credential objects.
        cred = Credential(
            id=cred.id,
            username=cred.username,
            domain=cred.domain,
            password=self._vault.get_password(credential_id),
        )

        result = _launch.launch(server, cred, profile, cred.password, runner=runner)

        server.last_credential_id = credential_id
        server.last_profile_name = profile_name
        self._vault.update_server(server)
        self._vault.save()
        return result

    @staticmethod
    def _find(items, attr, value):
        for item in items:
            if getattr(item, attr) == value:
                return item
        raise KeyError(f"{attr}={value!r}")
