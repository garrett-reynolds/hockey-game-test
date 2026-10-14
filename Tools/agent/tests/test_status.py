import os

from conftest import COMPILING, PLAY, READY, reply


def lines(out):
    return [x for x in out.splitlines() if x.strip()]


def test_status_one_line_per_slot(h):
    h.ready("111")
    h.set("w2", "editor_status", reply(PLAY))
    h.set("w2", "test_status", reply({"status": "no_tests"}))
    # w3: no replies at all = Editor does not answer
    rc, out = h.run("status")
    assert rc == 0
    ls = lines(out)
    assert len(ls) == 3
    assert ls[0].startswith("w1 (111): free")
    assert ls[1].startswith("w2: in Play")
    assert ls[2].startswith("w3: not answering")


def test_status_busy_when_a_test_run_is_live(h):
    h.set("111", "editor_status", reply(READY))
    h.set("111", "test_status", reply({"status": "running"}))
    rc, out = h.run("status")
    assert "w1 (111): busy (test run" in out


def test_status_busy_when_compiling(h):
    h.set("111", "editor_status", reply(COMPILING))
    h.set("111", "test_status", reply({"status": "no_tests"}))
    rc, out = h.run("status")
    assert "w1 (111): busy (compiling" in out


def test_status_busy_when_a_live_lock_exists(h):
    h.ready("111")
    h.write_lock("w1", os.getpid())
    rc, out = h.run("status")
    assert "w1 (111): busy (slot tool run" in out


def test_status_reports_stale_lock_without_clearing_it(h):
    from conftest import dead_pid
    h.ready("111")
    h.write_lock("w1", dead_pid())
    rc, out = h.run("status")
    assert "stale lock" in out
    assert (h.locks / "w1.lock").exists()
