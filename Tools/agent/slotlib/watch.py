"""Hang watch for `slot.py test` (ticket #179).

`test` calls watch_run() once the run_tests CLI process has started and the slot lock is held.
It returns the CLI exit code when the process exits, or HANG when the Editor looks hung.
Each minute it reads the Editor's CPU (Editor plus workers). Two low minutes in a row while the run is
live means a hang: about 2 minutes. A busy Editor, or a minute where the CPU could not be read, is not a hang.
Never kills the CLI process from here: a run cut short jams the Editor. Closing the Editor ends it.
"""
import os
import subprocess
import time

from . import cpu

HANG = "hang"
HANG_EXIT = 3
LOW_SAMPLES = 2


def interval_s():
    return float(os.environ.get("SLOT_CPU_INTERVAL_S", "60"))


def min_cpu_s():
    """CPU seconds an Editor must use in one interval to count as busy (an idle Editor uses well under this)."""
    return float(os.environ.get("SLOT_CPU_MIN", "1.0"))


def watch_run(slot, proc):
    last = cpu.sample(slot)
    due = time.time() + interval_s()
    lows = 0
    while True:
        try:
            return proc.wait(timeout=0.1)
        except subprocess.TimeoutExpired:
            pass
        if time.time() < due:
            continue
        due += interval_s()
        now = cpu.sample(slot)
        if now is None or last is None:
            last = now if now is not None else last
            continue
        lows = lows + 1 if now - last < min_cpu_s() else 0
        last = now
        if lows >= LOW_SAMPLES:
            return HANG
