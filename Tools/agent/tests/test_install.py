"""install.py copies the slot tool to the live folder; --check reports drift (exit 1)."""
import subprocess
import sys
from pathlib import Path

INSTALL = Path(__file__).resolve().parent.parent / "install.py"
REPO = Path(__file__).resolve().parents[3]


def run(dest, *args):
    p = subprocess.run([sys.executable, str(INSTALL), "--dest", str(dest), *args], capture_output=True, text=True)
    return p.returncode, p.stdout + p.stderr


def test_check_before_install_reports_drift(tmp_path):
    rc, out = run(tmp_path / "live", "--check")
    assert rc == 1
    assert "slot.py" in out


def test_install_copies_the_tool_and_the_commit_guard(tmp_path):
    live = tmp_path / "live"
    rc, out = run(live)
    assert rc == 0, out
    assert (live / "slot.py").read_bytes() == (REPO / "Tools/agent/slot.py").read_bytes()
    assert (live / "slotlib" / "common.py").exists()
    assert not (live / "slotlib" / "__pycache__").exists()
    assert (live / "githooks" / "pre-commit").read_bytes() == (REPO / ".githooks/pre-commit").read_bytes()
    assert run(live, "--check")[0] == 0


def test_check_finds_a_changed_file_and_a_missing_one(tmp_path):
    live = tmp_path / "live"
    run(live)
    (live / "slotlib" / "status.py").write_text("# edited\n", encoding="utf-8")
    (live / "githooks" / "pre-commit").unlink()
    rc, out = run(live, "--check")
    assert rc == 1
    assert "slotlib/status.py" in out and "githooks/pre-commit" in out


def test_check_ignores_line_endings(tmp_path):
    live = tmp_path / "live"
    run(live)
    f = live / "slot.py"
    f.write_bytes(f.read_bytes().replace(b"\r\n", b"\n").replace(b"\n", b"\r\n"))
    assert run(live, "--check")[0] == 0
