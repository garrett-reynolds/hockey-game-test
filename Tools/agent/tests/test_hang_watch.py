"""Hang watch during `slot.py test` (#179): fake Unity CLI plus fake CPU source (CPU sampled every 0.3 s here)."""
import json
import time
from pathlib import Path

import pytest

from conftest import SUMMARY_OK, reply

FAKE_CPU = Path(__file__).resolve().parent / "fake_cpu.py"


@pytest.fixture(autouse=True)
def fast_clock(h):
    h.env["SLOT_CPU_SOURCE"] = str(FAKE_CPU)
    h.env["SLOT_CPU_INTERVAL_S"] = "0.3"


def cpu(h, slot, values):
    (h.dir / "cpu.json").write_text(json.dumps({slot: values}), encoding="utf-8")


def test_hung_run_is_reported_as_hang_with_a_distinct_exit_code_within_two_samples(h):
    h.ready("w2")
    h.set("w2", "run_tests", {"out": json.dumps(SUMMARY_OK), "rc": 0, "sleep": 6})
    cpu(h, "w2", [100.0])  # never moves
    start = time.time()
    rc, out = h.run("test", "w2")
    assert rc == 3, out
    assert time.time() - start < 5  # reported long before the 6 s run would have ended
    assert "HANG" in out and "w2" in out
    assert "slot.py close w2" in out and "slot.py open w2" in out  # the next step
    assert not (h.locks / "w2.lock").exists()


def test_long_busy_run_is_not_a_hang(h):
    h.ready("w2")
    h.set("w2", "run_tests", {"out": json.dumps(SUMMARY_OK), "rc": 0, "sleep": 2})
    cpu(h, "w2", [30.0 * i for i in range(100)])
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    assert "HANG" not in out
    assert "5 passed" in out


def test_one_quiet_minute_between_busy_ones_is_not_a_hang(h):
    h.ready("w2")
    h.set("w2", "run_tests", {"out": json.dumps(SUMMARY_OK), "rc": 0, "sleep": 2.5})
    cpu(h, "w2", [0, 0, 50, 50, 100, 100, 150, 150, 200, 200, 250, 250])  # low, busy, low, busy ...
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    assert "HANG" not in out


def test_unreadable_cpu_is_never_called_a_hang(h):
    h.ready("w2")
    h.set("w2", "run_tests", {"out": json.dumps(SUMMARY_OK), "rc": 0, "sleep": 1.5})
    # no cpu.json at all: the fake source exits 1 every time
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    assert "HANG" not in out
