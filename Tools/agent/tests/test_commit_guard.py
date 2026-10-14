"""Commit guard (#175): drives real `git commit` in a temporary repo with the hook installed."""
import shutil
import subprocess
from pathlib import Path

import pytest

HOOK = Path(__file__).resolve().parents[3] / ".githooks" / "pre-commit"


def git(repo, *args, check=True):
    return subprocess.run(
        ["git", "-C", str(repo), *args], capture_output=True, text=True, check=check
    )


@pytest.fixture
def repo(tmp_path):
    git(tmp_path, "init", "-q")
    git(tmp_path, "config", "user.email", "t@example.com")
    git(tmp_path, "config", "user.name", "T")
    git(tmp_path, "config", "commit.gpgsign", "false")
    hooks = tmp_path / ".git" / "hooks"
    hooks.mkdir(exist_ok=True)
    shutil.copy(HOOK, hooks / "pre-commit")
    return tmp_path


def stage(repo, rel, text="x\n"):
    p = repo / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text)
    git(repo, "add", "-f", "--", rel)


def commit(repo):
    return git(repo, "commit", "-q", "-m", "test: x", check=False)


BLOCKED = [
    "ProjectSettings/TagManager.asset",
    "Assets/UI Toolkit/PanelSettings.asset",
    "Assets/UI Toolkit.meta",
    "Assets/Temp/scratch.txt",
    "Assets/Temp.meta",
    "Captures/banner-test-01.png",
]


@pytest.mark.parametrize("path", BLOCKED)
def test_blocked_path_is_refused_with_list_and_unstage_command(repo, path):
    stage(repo, path)
    r = commit(repo)
    assert r.returncode != 0
    out = r.stdout + r.stderr
    assert path in out
    assert f'git restore --staged -- "{path}"' in out
    assert git(repo, "rev-parse", "--verify", "HEAD", check=False).returncode != 0


def test_all_blocked_files_listed_in_one_command(repo):
    stage(repo, "ProjectSettings/A.asset")
    stage(repo, "Assets/Temp/b.txt")
    stage(repo, "Assets/Scripts/fine.py")
    out = commit(repo)
    text = out.stdout + out.stderr
    assert out.returncode != 0
    assert 'git restore --staged -- "Assets/Temp/b.txt" "ProjectSettings/A.asset"' in text
    assert "fine.py" not in text


def test_normal_files_are_allowed(repo):
    stage(repo, "Assets/Scripts/Puck.py")
    stage(repo, "Tools/agent/tests/test_x.py")
    stage(repo, "notes/readme.md")
    stage(repo, "Assets/Resources/Ads/banner-award-mvp.png")
    r = commit(repo)
    assert r.returncode == 0, r.stdout + r.stderr


def test_deleting_a_blocked_path_is_allowed(repo):
    stage(repo, "ProjectSettings/A.asset")
    git(repo, "-c", "core.hooksPath=/nonexistent", "commit", "-q", "-m", "seed")
    git(repo, "rm", "-q", "--", "ProjectSettings/A.asset")
    r = commit(repo)
    assert r.returncode == 0, r.stdout + r.stderr


def test_unstaged_blocked_file_does_not_block(repo):
    (repo / "ProjectSettings").mkdir()
    (repo / "ProjectSettings" / "A.asset").write_text("x")
    stage(repo, "Assets/Scripts/fine.py")
    assert commit(repo).returncode == 0
