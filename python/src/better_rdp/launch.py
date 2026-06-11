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
Signer = Callable[[Sequence[str]], object]

SIGN_THUMBPRINT_ENV = "BETTER_RDP_SIGN_THUMBPRINT"
LEGACY_SIGN_CERT_ENV = "BETTER_RDP_SIGN_SHA256"


def _default_runner(argv: Sequence[str]) -> object:
    """Spawn mstsc and wait for it. mstsc reads the .rdp at startup, so once it returns we
    are safe to delete the temp file."""
    return subprocess.run(list(argv), check=True)


def _default_signer(argv: Sequence[str]) -> object:
    """Run rdpsign.exe to sign the generated .rdp file in-place.

    A signed .rdp lets mstsc verify and display the Publisher instead of showing the
    "Unknown remote connection / Unknown publisher" warning. The configured certificate
    must be trusted by Windows and available to the current user by certificate thumbprint.
    """
    return subprocess.run(list(argv), check=True, capture_output=True, text=True)


def _normalise_thumbprint(thumbprint: str) -> str:
    """Accept thumbprints copied with spaces and return the form rdpsign expects."""
    return "".join(thumbprint.split())


def _configured_signing_thumbprint() -> str | None:
    return os.environ.get(SIGN_THUMBPRINT_ENV) or os.environ.get(LEGACY_SIGN_CERT_ENV)


def launch(
    server: Server,
    credential: Credential,
    profile: DisplayProfile,
    plaintext_password: str,
    *,
    runner: Runner | None = None,
    mstsc: str = "mstsc.exe",
    signer: Signer | None = None,
    rdpsign: str = "rdpsign.exe",
    rdpsign_sha256: str | None = None,
) -> object:
    """Write a temp .rdp for this Connection, invoke ``runner([mstsc, rdp_path])``, then
    delete the temp file (even if the runner raises). Returns the runner's result.

    ``runner`` defaults to ``subprocess.run``-style execution; tests inject a fake.

    If ``rdpsign_sha256`` (or environment variable ``BETTER_RDP_SIGN_THUMBPRINT``) is set,
    the temp .rdp is signed with ``rdpsign.exe /sha256 <thumbprint>`` before mstsc sees it.
    Despite the rdpsign switch name, Windows expects the certificate's normal SHA-1
    thumbprint here. That verifies the .rdp publisher and prevents mstsc's
    unknown-publisher prompt when the signing certificate is trusted by Windows.
    """
    if runner is None:
        runner = _default_runner
    if signer is None:
        signer = _default_signer
    rdpsign_sha256 = rdpsign_sha256 or _configured_signing_thumbprint()

    text = rdp.generate(server, credential, profile, plaintext_password)
    fd, path = tempfile.mkstemp(suffix=".rdp", prefix="better_rdp_")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            f.write(text)
        if rdpsign_sha256:
            signer([rdpsign, "/sha256", _normalise_thumbprint(rdpsign_sha256), path])
            with open(path, encoding="utf-8") as f:
                if not any(line.lower().startswith("signature:s:") for line in f):
                    raise RuntimeError("rdpsign completed but the .rdp file does not contain a signature")
        return runner([mstsc, path])
    finally:
        # The temp .rdp carries a usable DPAPI password blob — delete it promptly.
        try:
            os.remove(path)
        except OSError:
            pass
