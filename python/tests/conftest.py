"""Shared test helpers."""

import sys

import pytest

# DPAPI is a real Windows API; these tests can't run on non-Windows CI. They skip cleanly
# rather than error. Argon2/AES tests are cross-platform and are NOT marked.
requires_windows = pytest.mark.skipif(
    sys.platform != "win32", reason="DPAPI is Windows-only"
)
