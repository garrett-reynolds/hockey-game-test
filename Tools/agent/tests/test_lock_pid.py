"""#209: a lock counts as live only if its pid is alive AND belongs to a python/py (slot tool) or unity (CLI) process.
Windows reuses pids, so a stale lock can name a live pid that is some other program."""
import os
import subprocess
import sys

import pytest

from conftest import SUMMARY_OK, reply
from slotlib import common


@pytest.fixture
def other_program():
    """A live process that is neither python nor unity."""
    cmd = ["ping", "-n", "60", "127.0.0.1"] if os.name == "nt" else ["sleep", "60"]
    p = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        yield p.pid
    finally:
        p.kill()
        p.wait()


def test_process_name_of_this_test_is_python():
    assert common.process_name(os.getpid()).lower().startswith("python")


def test_process_name_of_a_dead_pid_is_empty():
    from conftest import dead_pid
    assert common.process_name(dead_pid()) == ""


def test_slot_tool_pid_must_be_python_or_unity_named():
    assert common.is_slot_process(os.getpid())


def test_a_live_pid_of_another_program_is_not_a_slot_process(other_program):
    assert common.pid_alive(other_program)
    assert not common.is_slot_process(other_program)


def test_lock_whose_pids_belong_to_other_programs_is_not_live(h, other_program):
    h.write_lock("w2", other_program, cli_pid=other_program)
    old = dict(os.environ)
    os.environ.update({"SLOT_LOCK_DIR": str(h.locks)})
    try:
        lock = common.read_lock("w2")
    finally:
        os.environ.clear()
        os.environ.update(old)
    assert lock["live"] is False


def test_test_run_treats_a_lock_held_by_another_program_as_stale(h, other_program):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    h.write_lock("w2", other_program, cli_pid=other_program)
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    assert "stale lock" in out
