import json
import os

from conftest import COMPILING, PLAY, READY, SUMMARY_FAIL, SUMMARY_OK, dead_pid, reply


def test_fast_run_passes_and_writes_results_file(h):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    lines = [x for x in out.splitlines() if x.strip() and "code changed" not in x]  # #186 adds a refresh line
    assert len(lines) == 2
    assert "w2 fast: 5 passed, 0 failed of 5" in lines[0]
    path = lines[1].strip()
    assert path.startswith(str(h.results))
    assert json.loads(open(path, encoding="utf-8-sig").read())["Summary"]["Total"] == 5
    assert not (h.locks / "w2.lock").exists()


def test_fast_run_uses_900s_limit_and_edit_mode(h):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    h.run("test", "w2", "--filter", "Goalie")
    c = h.run_tests_calls()[0]
    assert c[c.index("--timeout") + 1] == "900"
    assert "--result-only" in c
    assert c[c.index("--mode") + 1] == "EditMode"
    assert c[c.index("--filter") + 1] == "Goalie"
    assert "--include_explicit" not in c
    i = c.index("--")
    assert c[i + 1:] == ["--timeout", "900"]


def test_full_run_uses_1800s_with_inner_timeout(h):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2", "--full")
    assert rc == 0, out
    c = h.run_tests_calls()[0]
    assert c[c.index("--timeout") + 1] == "1800"
    assert c[c.index("--include_explicit") + 1] == "true"
    i = c.index("--")
    assert c[i + 1:] == ["--timeout", "1800"]
    assert "w2 full:" in out


def test_caller_cannot_pass_a_timeout(h):
    h.ready("w2")
    rc, out = h.run("test", "w2", "--timeout", "30")
    assert rc != 0
    assert h.run_tests_calls() == []


def test_failed_tests_give_nonzero_exit(h):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_FAIL))
    rc, out = h.run("test", "w2")
    assert rc == 1
    assert "4 passed, 1 failed of 5" in out


def test_second_run_refused_while_lock_is_live(h):
    h.ready("w2")
    h.write_lock("w2", os.getpid())
    rc, out = h.run("test", "w2")
    assert rc == 2
    assert "already running" in out
    assert h.run_tests_calls() == []
    assert (h.locks / "w2.lock").exists()


def test_run_still_going_after_slot_tool_was_killed_is_refused(h):
    # The tool timeout killed slot.py (its pid is gone) but the run_tests CLI child (cli_pid) still runs.
    h.ready("w2")
    h.write_lock("w2", dead_pid(), cli_pid=os.getpid())
    rc, out = h.run("test", "w2")
    assert rc == 2
    assert "still going" in out
    assert h.run_tests_calls() == []
    assert (h.locks / "w2.lock").exists()


def test_lock_with_both_pids_gone_does_not_block(h):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    h.write_lock("w2", dead_pid(), cli_pid=dead_pid())
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    assert "stale lock" in out


def test_running_test_records_the_cli_pid_and_blocks_a_second_run(h):
    import subprocess
    import sys
    import time
    from conftest import SLOT_PY
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK, sleep=3))
    first = subprocess.Popen([sys.executable, str(SLOT_PY), "test", "w2"], env=h.env,
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        lock = h.locks / "w2.lock"
        data = {}
        end = time.time() + 20
        while time.time() < end and "cli_pid" not in data:
            time.sleep(0.1)
            try:
                data = json.loads(lock.read_text(encoding="utf-8"))
            except (OSError, ValueError):
                data = {}
        assert isinstance(data.get("cli_pid"), int)
        rc, out = h.run("test", "w2")
        assert rc == 2 and "already running" in out
    finally:
        first.wait(timeout=30)
    assert not (h.locks / "w2.lock").exists()


def test_waits_out_a_recompile_then_runs(h):
    h.set("w2", "editor_status", reply(COMPILING), reply(COMPILING), reply(READY))
    h.set("w2", "test_status", reply({"status": "no_tests"}))
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    h.set("w2", "menu", reply({"ok": True}))  # #186: the first run refreshes and recompiles
    h.set("w2", "recompile", reply({"ok": True}))
    h.set("w2", "recompile_status", reply({"status": "completed", "errors": []}))
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    # run_tests was called only after the third editor_status answer
    calls = h.calls()
    first_run = next(i for i, c in enumerate(calls) if "run_tests" in c)
    status_before = [c for c in calls[:first_run] if "editor_status" in c]
    assert len(status_before) >= 3


def test_gives_up_if_editor_never_finishes_compiling(h):
    h.set("w2", "editor_status", reply(COMPILING))
    h.set("w2", "test_status", reply({"status": "no_tests"}))
    rc, out = h.run("test", "w2")
    assert rc == 2
    assert "compiling" in out
    assert h.run_tests_calls() == []
    assert not (h.locks / "w2.lock").exists()


def test_refuses_when_editor_is_in_play(h):
    h.set("w2", "editor_status", reply(PLAY))
    h.set("w2", "test_status", reply({"status": "no_tests"}))
    rc, out = h.run("test", "w2")
    assert rc == 2
    assert "Play" in out
    assert h.run_tests_calls() == []
    assert not (h.locks / "w2.lock").exists()


def test_refuses_when_a_test_run_is_live_elsewhere(h):
    h.set("w2", "editor_status", reply(READY))
    h.set("w2", "test_status", reply({"status": "running"}))
    rc, out = h.run("test", "w2")
    assert rc == 2
    assert "test run" in out
    assert h.run_tests_calls() == []


def test_editor_not_answering_is_refused(h):
    rc, out = h.run("test", "w3")
    assert rc == 2
    assert "not answering" in out
    assert h.run_tests_calls() == []


def test_stale_lock_cleared_only_after_editor_answers(h):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    h.write_lock("w2", dead_pid())
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    assert "stale lock" in out


def test_stale_lock_kept_when_editor_does_not_answer(h):
    h.write_lock("w3", dead_pid())
    rc, out = h.run("test", "w3")
    assert rc == 2
    assert "stale lock" in out
    assert (h.locks / "w3.lock").exists()
    assert h.run_tests_calls() == []


def test_unknown_slot_rejected(h):
    rc, out = h.run("test", "w9")
    assert rc == 2
    assert "w1" in out


def test_lock_released_when_run_fails_to_give_json(h):
    h.ready("w2")
    h.set("w2", "run_tests", {"out": "Network error", "rc": 1})
    rc, out = h.run("test", "w2")
    assert rc == 1
    assert "no result" in out.lower() or "failed" in out.lower()
    assert not (h.locks / "w2.lock").exists()
