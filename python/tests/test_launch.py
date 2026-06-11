"""Q4 contract: the launch path with a mocked mstsc runner.

We never spawn a real mstsc window here. The injected runner lets us assert two things
that matter and are otherwise invisible: (1) the argv we hand mstsc, and (2) that the temp
.rdp exists *during* the call and is deleted *after* — including when the runner raises.
"""

import os

import pytest

from better_rdp import launch
from better_rdp.models import Credential, DisplayMode, DisplayProfile, Server


@pytest.fixture
def conn():
    return (
        Server(name="Prod", address="prod01.intranet.local"),
        Credential(id="c1", username="alice", password="pw"),
        DisplayProfile(name="Dynamic", mode=DisplayMode.WINDOWED_DYNAMIC),
    )


def test_launch_invokes_mstsc_with_a_rdp_file(conn):
    server, cred, profile = conn
    seen = {}

    def runner(argv):
        seen["argv"] = list(argv)
        seen["existed_during_call"] = os.path.exists(argv[1])
        return 0

    launch.launch(server, cred, profile, "pw", runner=runner, mstsc="mstsc.exe")

    assert seen["argv"][0] == "mstsc.exe"
    assert seen["argv"][1].endswith(".rdp")
    assert seen["existed_during_call"] is True


def test_temp_rdp_is_deleted_after_launch(conn):
    server, cred, profile = conn
    captured = {}

    def runner(argv):
        captured["path"] = argv[1]
        return 0

    launch.launch(server, cred, profile, "pw", runner=runner)

    assert not os.path.exists(captured["path"])


def test_temp_rdp_is_deleted_even_if_runner_raises(conn):
    server, cred, profile = conn
    captured = {}

    def runner(argv):
        captured["path"] = argv[1]
        raise RuntimeError("mstsc failed to start")

    with pytest.raises(RuntimeError):
        launch.launch(server, cred, profile, "pw", runner=runner)

    assert "path" in captured  # the temp file was created before the runner ran
    assert not os.path.exists(captured["path"])


def test_launch_returns_runner_result(conn):
    server, cred, profile = conn
    sentinel = object()
    result = launch.launch(server, cred, profile, "pw", runner=lambda argv: sentinel)
    assert result is sentinel


def test_launch_signs_rdp_before_mstsc_when_thumbprint_configured(conn):
    server, cred, profile = conn
    seen = {}

    def signer(argv):
        seen["signer_argv"] = list(argv)
        with open(argv[3], "a", encoding="utf-8") as f:
            f.write("signature:s:test-signature\n")
        return 0

    def runner(argv):
        seen["runner_argv"] = list(argv)
        with open(argv[1], encoding="utf-8") as f:
            seen["mstsc_input"] = f.read()
        return 0

    launch.launch(
        server,
        cred,
        profile,
        "pw",
        runner=runner,
        signer=signer,
        rdpsign="rdpsign.exe",
        rdpsign_sha256="AA BB CC",
    )

    assert seen["signer_argv"][:3] == ["rdpsign.exe", "/sha256", "AABBCC"]
    assert seen["signer_argv"][3] == seen["runner_argv"][1]
    assert "signature:s:test-signature" in seen["mstsc_input"]


def test_temp_rdp_is_deleted_even_if_signer_raises(conn):
    server, cred, profile = conn
    captured = {}

    def signer(argv):
        captured["path"] = argv[3]
        raise RuntimeError("rdpsign failed")

    with pytest.raises(RuntimeError):
        launch.launch(server, cred, profile, "pw", signer=signer, rdpsign_sha256="AABBCC")

    assert "path" in captured
    assert not os.path.exists(captured["path"])
