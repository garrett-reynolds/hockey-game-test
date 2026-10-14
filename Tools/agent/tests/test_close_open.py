"""close, open and the 'not open' refusal of test (tickets #178 and the #174 gap). Command line against the fake CLI."""
import os
import time

from conftest import COMPILING, PLAY, READY, SUMMARY_OK, reply

import pytest


@pytest.fixture(autouse=True)
def short_waits(h):
    h.env["SLOT_CLOSE_WAIT_S"] = "0.3"
    h.env["SLOT_OPEN_WAIT_S"] = "3"


FOLDER = {"w1": "111", "w2": "w2", "w3": "w3"}


def rows(h, *slots):
    """The `unity status` text with the given slots open (backslash paths, like the real CLI)."""
    root = h.env["SLOT_ROOT"].replace("/", "\\")
    return "".join(f"7800\tready\t{root}\\{FOLDER[s]}\t6000.6.5f1\t4242\n" for s in slots)


def status_says(h, *per_call):
    """Each argument is a tuple of open slots for one `unity status` call; the last one repeats."""
    h.set("_status", "status", *[{"out": rows(h, *s), "rc": 0 if s else 6} for s in per_call])


def actions(h, name):
    return [c for c in h.calls() if c and c[0] == name]


# ---- test refuses at once when the Editor is not open ---------------------------------------

def test_test_refuses_at_once_when_editor_not_open(h):
    status_says(h, ("w2",))  # w3 is not open
    start = time.time()
    rc, out = h.run("test", "w3")
    assert rc == 2
    assert "not open; run: slot.py open w3" in out
    assert time.time() - start < 1.5  # SLOT_WAIT_S is 2: it must not wait that out
    assert h.run_tests_calls() == []
    assert not (h.locks / "w3.lock").exists()


def test_test_still_runs_when_editor_is_open(h):
    status_says(h, ("w2",))
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert rc == 0, out


# ---- close ----------------------------------------------------------------------------------

def test_close_reports_closed_only_when_status_no_longer_shows_editor(h):
    status_says(h, ("w2",), ())  # open before, gone after
    h.ready("w2")
    rc, out = h.run("close", "w2")
    assert rc == 0, out
    assert "w2: closed" in out
    assert len(actions(h, "close")) == 1


def test_close_that_does_not_take_is_not_reported_closed(h):
    status_says(h, ("w2",))  # still open however often we look
    h.ready("w2")
    h.set("w2", "close", {"out": "false none", "rc": 0})
    rc, out = h.run("close", "w2")
    assert rc == 1
    assert "still open" in out
    assert "closed" not in out.replace("not closed", "")


def test_close_of_an_editor_that_is_not_open_says_so(h):
    status_says(h, ())
    rc, out = h.run("close", "w2")
    assert rc == 0
    assert "already closed" in out
    assert actions(h, "close") == []


def test_close_refused_while_slot_tool_run_is_live(h):
    status_says(h, ("w2",))
    h.ready("w2")
    h.write_lock("w2", os.getpid())
    rc, out = h.run("close", "w2")
    assert rc == 2
    assert "--force" in out
    assert actions(h, "close") == []


def test_close_refused_while_a_test_run_is_live_in_the_editor(h):
    status_says(h, ("w2",))
    h.set("w2", "editor_status", reply(READY))
    h.set("w2", "test_status", reply({"status": "running"}))
    rc, out = h.run("close", "w2")
    assert rc == 2
    assert actions(h, "close") == []


def test_close_force_closes_a_slot_with_a_live_run(h):
    status_says(h, ("w2",), ())
    h.ready("w2")
    h.write_lock("w2", os.getpid())
    rc, out = h.run("close", "w2", "--force")
    assert rc == 0, out
    assert "closed" in out
    assert len(actions(h, "close")) == 1


def test_close_when_status_cannot_be_read_is_not_reported_closed(h):
    h.set("_status", "status", {"out": "garbage", "rc": 1, "raw": True})
    rc, out = h.run("close", "w2")
    assert rc == 1
    assert "closed" not in out.replace("not closed", "")


def test_close_unknown_slot_rejected(h):
    rc, out = h.run("close", "w9")
    assert rc == 2


# ---- open -----------------------------------------------------------------------------------

def test_open_waits_for_a_slow_editor_then_reports_ready(h):
    status_says(h, (), ("w2",))  # not open at first
    no = reply({}, rc=1)
    h.set("w2", "editor_status", no, no, reply(COMPILING), reply(READY))
    h.set("w2", "test_status", reply({"status": "no_tests"}))
    rc, out = h.run("open", "w2")
    assert rc == 0, out
    assert "w2: ready" in out
    assert len(actions(h, "open")) == 1
    # the open command was given the slot folder
    assert actions(h, "open")[0][-1].replace("\\", "/").endswith("/w2")


def test_open_gives_up_with_the_reason_after_its_limit(h):
    status_says(h, ())
    h.set("w2", "editor_status", reply(COMPILING))
    h.env["SLOT_OPEN_WAIT_S"] = "1"
    rc, out = h.run("open", "w2")
    assert rc == 1
    assert "not ready" in out
    assert "compiling" in out


def test_open_gives_up_when_the_editor_never_answers(h):
    status_says(h, ())
    h.env["SLOT_OPEN_WAIT_S"] = "1"
    rc, out = h.run("open", "w2")
    assert rc == 1
    assert "not answering" in out


def test_open_never_starts_a_second_editor_beside_an_open_one(h):
    status_says(h, ("w2",))
    h.ready("w2")
    rc, out = h.run("open", "w2")
    assert rc == 0, out
    assert "already open" in out
    assert "ready" in out
    assert actions(h, "open") == []


def test_open_of_an_editor_in_play_reports_it_without_waiting(h):
    status_says(h, ("w2",))
    h.set("w2", "editor_status", reply(PLAY))
    start = time.time()
    rc, out = h.run("open", "w2")
    assert rc == 0
    assert "Play" in out
    assert time.time() - start < 1.5
    assert actions(h, "open") == []


def test_open_refuses_when_status_cannot_be_read(h):
    h.set("_status", "status", {"out": "garbage", "rc": 1, "raw": True})
    rc, out = h.run("open", "w2")
    assert rc == 2
    assert actions(h, "open") == []
