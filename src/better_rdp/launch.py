"""Connection launch: materialise a temp `.rdp`, invoke mstsc, then delete the temp file.

The temp `.rdp` briefly contains a usable DPAPI password blob, so it must be deleted
immediately after mstsc has read it (see ADR 0001 > Consequences).
"""

from __future__ import annotations

import os
import subprocess
import tempfile
from typing import Callable, Sequence

from . import rdp
from .models import Credential, DisplayProfile, Server

# A runner takes the argv list and returns whatever the caller cares about. Injectable so
# tests can assert argument construction + temp-file cleanup without spawning a window.
Runner = Callable[[Sequence[str]], object]


def _default_runner(argv: Sequence[str]) -> object:
    """Spawn mstsc and wait for it. mstsc reads the .rdp at startup, so once it returns we
    are safe to delete the temp file."""
    return subprocess.run(list(argv), check=True)


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

    ``runner`` defaults to ``subprocess.run``-style execution; tests inject a fake.
    """
    if runner is None:
        runner = _default_runner

    text = rdp.generate(server, credential, profile, plaintext_password)
    fd, path = tempfile.mkstemp(suffix=".rdp", prefix="better_rdp_")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            f.write(text)
        return runner([mstsc, path])
    finally:
        # The temp .rdp carries a usable DPAPI password blob — delete it promptly.
        try:
            os.remove(path)
        except OSError:
            pass
