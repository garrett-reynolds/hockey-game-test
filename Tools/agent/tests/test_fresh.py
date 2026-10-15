"""Refresh + recompile (#186): `slot.py test` refreshes and recompiles by itself when the code changed."""
import json
import os
import subprocess

import pytest

from conftest import SUMMARY_OK, reply

DONE = {"status": "completed", "errors": []}
CODE = "." + "cs"  # a code file ending the stamp counts
SHADER = ".shader"
NET = {"out": "Network error", "rc": 1, "sleep": 0}


def git(path, *args):
    return subprocess.run(["git", "-C", str(path), *args], capture_output=True, text=True, check=True).stdout.strip()


def commit(path, name, text="x"):
    (path / name).write_text(text, encoding="utf-8")
    git(path, "add", name)
    git(path, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", name)


@pytest.fixture
def repo(h, tmp_path):
    path = tmp_path / "wt" / "w2"
    path.mkdir(parents=True)
    git(path, "init", "-q")
    git(path, "checkout", "-q", "-b", "feat/x")
    commit(path, "a.txt", "a")
    return path


def names(h):
    """Unity command names called so far, in order (menu, recompile, recompile_status, run_tests ...)."""
    out = []
    for argv in h.calls():
        if "--result-only" in argv:
            rest = argv[argv.index("--result-only") + 1:]
            out.append(rest[0])
    return out


def go(h):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    return h.run("test", "w2")


def refreshes(h):
    return names(h).count("menu")


def test_first_run_refreshes_in_order_then_tests(h, repo):
    rc, out = go(h)
    assert rc == 0, out
    seq = [n for n in names(h) if n in ("menu", "recompile", "recompile_status", "run_tests")]
    assert seq[:3] == ["menu", "recompile", "recompile_status"] and seq[-1] == "run_tests"
    menu = next(a for a in h.calls() if "menu" in a)
    assert menu[menu.index("--path") + 1] == "Assets/Refresh"
    assert "w2: code changed since the last compile (first run); Refresh + recompile" in out


def test_unchanged_code_skips_refresh(h, repo):
    go(h)
    rc, out = go(h)
    assert rc == 0, out
    assert refreshes(h) == 1
    assert "code changed" not in out


def test_new_head_refreshes(h, repo):
    go(h)
    commit(repo, "b.txt")
    rc, out = go(h)
    assert rc == 0, out
    assert refreshes(h) == 2
    assert "(new HEAD); Refresh + recompile" in out


def test_changed_code_file_refreshes_and_counts_files(h, repo):
    go(h)
    (repo / "Assets").mkdir()
    for n in ("A" + CODE, "B" + SHADER):
        (repo / "Assets" / n).write_text("x", encoding="utf-8")
    rc, out = go(h)
    assert rc == 0, out
    assert "(2 changed files); Refresh + recompile" in out
    assert refreshes(h) == 2
    go(h)
    assert refreshes(h) == 2  # same files, same mtimes: no third refresh
    p = repo / "Assets" / ("A" + CODE)
    st = p.stat()
    os.utime(p, (st.st_atime, st.st_mtime + 50))
    rc, out = go(h)
    assert "(1 changed files); Refresh + recompile" in out
    assert refreshes(h) == 3


def test_manifest_json_counts(h, repo):
    go(h)
    (repo / "Packages").mkdir()
    (repo / "Packages" / "manifest.json").write_text("{}", encoding="utf-8")
    rc, out = go(h)
    assert "(1 changed files); Refresh + recompile" in out


def test_other_files_do_not_refresh(h, repo):
    go(h)
    (repo / "Assets").mkdir()
    (repo / "Assets" / "pic.png").write_text("x", encoding="utf-8")
    (repo / "Assets" / "Notes.md").write_text("x", encoding="utf-8")
    (repo / ("Outside" + CODE)).write_text("x", encoding="utf-8")  # not under Assets/
    rc, out = go(h)
    assert rc == 0, out
    assert refreshes(h) == 1


def test_polls_recompile_status_until_completed(h, repo):
    h.ready("w2")
    h.set("w2", "recompile_status", reply({"status": "compiling"}), reply({"status": "compiling"}), reply(DONE))
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    assert names(h).count("recompile_status") == 3


def test_up_to_date_counts_as_done(h, repo):
    h.ready("w2")
    h.set("w2", "recompile_status", reply({"status": "up_to_date"}))
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert rc == 0, out


@pytest.mark.parametrize("bad", [
    {"status": "completed", "errors": ["Foo(1,1): error CS1002", "e2", "e3", "e4", "e5", "e6", "e7"]},
    {"status": "failed"},
])
def test_compile_error_stops_with_exit_1_no_tests_no_stamp(h, repo, bad):
    h.ready("w2")
    h.set("w2", "recompile_status", reply(bad))
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert rc == 1, out
    assert h.run_tests_calls() == []
    assert not (h.locks / "w2.stamp.json").exists()
    if "errors" in bad:
        assert "error CS1002" in out and "e5" in out and "e6" not in out
    h.set("w2", "recompile_status", reply(DONE))  # next run: refreshes again because no stamp was saved
    rc, out = go(h)
    assert rc == 0, out
    assert refreshes(h) == 2
    assert "first run" in out


def test_network_error_is_retried_once_per_call(h, repo):
    h.ready("w2")
    h.set("w2", "recompile", NET, reply({"ok": True}))
    h.set("w2", "menu", NET, reply({"ok": True}))
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    assert names(h).count("recompile") == 2 and names(h).count("menu") == 2


def test_git_unreadable_refreshes_every_run_and_does_not_fail(h):
    rc, out = go(h)  # the slot folder is not a git repo
    assert rc == 0, out
    rc, out = go(h)
    assert rc == 0, out
    assert refreshes(h) == 2


def test_stamp_file_sits_beside_the_lock_files(h, repo):
    go(h)
    stamp = json.loads((h.locks / "w2.stamp.json").read_text(encoding="utf-8"))
    assert stamp["head"] == git(repo, "rev-parse", "HEAD")


def test_editor_in_play_is_refused_before_any_refresh(h, repo):
    from conftest import PLAY
    h.ready("w2")
    h.set("w2", "editor_status", reply(PLAY))
    rc, out = h.run("test", "w2")
    assert rc == 2 and refreshes(h) == 0


def test_busy_slot_is_refused_before_any_refresh(h, repo):
    h.write_lock("w2", os.getpid())
    rc, out = go(h)
    assert rc == 2 and refreshes(h) == 0


# ---- fix round 1: positive proof of a clean compile (real reply shape: status, failed, errors, compilationFailed)

REAL_CLEAN = {"status": "completed", "failed": False, "errors": [], "compilationFailed": False}


def with_status(h, *replies):
    h.ready("w2")
    h.set("w2", "recompile_status", *replies)
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    return h.run("test", "w2")


def test_real_clean_reply_passes_and_idle_triggered_are_waited_out(h, repo):
    rc, out = with_status(h, reply({"status": "idle"}), reply({"status": "triggered"}),
                          reply({"status": "compiling"}), reply(REAL_CLEAN))
    assert rc == 0, out
    assert names(h).count("recompile_status") == 4


@pytest.mark.parametrize("bad", [
    {"status": "completed", "compile_errors": ["x"]},
    {"status": "completed", "failed": True, "errors": ["Foo(1,1): error CS1002"]},
    {"status": "completed", "compilationFailed": True},
    {"status": "completed", "errorCount": 3},
    {"status": "completed", "success": False},
    {"status": "error"},
    {"status": "compile_failed", "message": "boom"},
])
def test_anything_but_positive_proof_stops_with_exit_1_and_no_stamp(h, repo, bad):
    rc, out = with_status(h, reply(bad))
    assert rc == 1, out
    assert h.run_tests_calls() == []
    assert not (h.locks / "w2.stamp.json").exists()
    assert not (h.locks / "w2.lock").exists()


def test_unknown_status_shows_the_raw_reply(h, repo):
    rc, out = with_status(h, reply({"status": "exploded", "message": "boom"}))
    assert rc == 1 and "exploded" in out


def test_recompile_itself_answering_failed_stops(h, repo):
    h.ready("w2")
    h.set("w2", "recompile", reply({"status": "failed", "message": "Scripts still have compile errors"}))
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert rc == 1 and "compile errors" in out
    assert h.run_tests_calls() == [] and not (h.locks / "w2.stamp.json").exists()


# ---- fix round 1: second Network error is a clear message

@pytest.mark.parametrize("step", ["menu", "recompile", "recompile_status"])
@pytest.mark.parametrize("rc", [0, 1])
def test_second_network_error_names_the_step(h, repo, step, rc):
    h.ready("w2")
    h.set("w2", step, {"out": "Network error", "rc": rc, "sleep": 0})
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    code, out = h.run("test", "w2")
    assert code == 2, out
    assert f"Network error twice during {step}; the Editor may be reloading. Run slot.py test again." in out
    assert h.run_tests_calls() == []
    assert not (h.locks / "w2.lock").exists()


# ---- fix round 1: the run lock is held during the refresh

def test_second_test_is_refused_while_a_refresh_is_live(h, repo):
    import subprocess
    import sys
    import time
    from conftest import SLOT_PY
    h.ready("w2")
    h.set("w2", "menu", reply({"ok": True}, sleep=2.5))
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    first = subprocess.Popen([sys.executable, str(SLOT_PY), "test", "w2"], env=h.env, stdout=subprocess.PIPE,
                             stderr=subprocess.STDOUT, text=True)
    try:
        deadline = time.time() + 20
        while time.time() < deadline and refreshes(h) == 0:
            time.sleep(0.05)
        assert refreshes(h) == 1
        rc, out = h.run("test", "w2")
        assert rc == 2 and "already running" in out, out
        assert names(h).count("recompile") == 0  # the second run sent no recompile under the first
    finally:
        first_out = first.communicate(timeout=60)[0]
    assert first.returncode == 0, first_out
    assert not (h.locks / "w2.lock").exists()
