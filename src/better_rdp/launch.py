"""Connection launch: materialise a temp `.rdp`, invoke mstsc, then delete the temp file.

The temp `.rdp` briefly contains a usable DPAPI password blob, so it must be deleted
immediately after mstsc has read it (see ADR 0001 > Consequences).
"""

from __future__ import annotations

import subprocess
from typing import Callable, Sequence

from .models import Credential, DisplayProfile, Server

# A runner takes the argv list and returns whatever the caller cares about. Injectable so
# tests can assert argument construction + temp-file cleanup without spawning a window.
Runner = Callable[[Sequence[str]], object]


def launch(
    server: Server,
    credential: Credential,
    profile: DisplayProfile,
    plaintext_password: str,
    *,
    runner: Runner | None = None,
    mstsc: str = "mstsc.exe",
) -> object:
    """Write a temp .rdp for this Connection, invoke ``runner([mstsc, rdp_path])``, then
    delete the temp file (even if the runner raises). Returns the runner's result.

    ``runner`` defaults to ``subprocess.Popen``-style execution; tests inject a fake.
    """
    raise NotImplementedError
