"""Domain models. Glossary terms live in CONTEXT.md.

These dataclasses are intentionally thin and dependency-free so the test suite can
import them without any third-party packages installed.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum


class DisplayMode(str, Enum):
    """How a Display Profile renders. See CONTEXT.md > Display Profile."""

    FULLSCREEN_MULTIMON = "fullscreen_multimon"
    WINDOWED_FIXED = "windowed_fixed"
    WINDOWED_DYNAMIC = "windowed_dynamic"


@dataclass
class Credential:
    """A username + password (+ optional domain) you authenticate *with*.

    ``password`` is the transient plaintext, held in memory only. It is never serialized
    to the Vault as plaintext — the Vault stores an encrypted blob (see ADR 0001).
    """

    id: str
    username: str
    domain: str | None = None
    password: str | None = None


@dataclass
class DisplayProfile:
    """A named, reusable display layout selected at launch. See CONTEXT.md."""

    name: str
    mode: DisplayMode
    monitors: list[int] = field(default_factory=list)  # selected mstsc monitor ids
    width: int | None = None
    height: int | None = None
    scale_factor: int | None = None  # 100/125/150/175/200


@dataclass
class Server:
    """A remote host you connect to. See CONTEXT.md."""

    name: str
    address: str
    notes: str = ""
    last_credential_id: str | None = None
    last_profile_name: str | None = None
