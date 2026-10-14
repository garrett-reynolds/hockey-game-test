"""`slot.py close <slot> [--force]` and `slot.py open <slot>`: close and open a slot Editor, and trust the answer.

close: runs `unity close`, then confirms with `unity status`. Says "closed" only when status no longer lists the Editor.
open: never starts a second Editor beside an open one; waits until the Editor answers (self-recover rule).
"""
import os
import subprocess
import time

from . import common as c
from . import editors


def add_parser(sub):
    p = sub.add_parser("close", help="close a slot Editor and confirm it is gone")
    p.add_argument("slot", help="w1, w2 or w3")
    p.add_argument("--force", action="store_true", help="close even if a test run is live on the slot")
    p.set_defaults(func=run_close)
    p = sub.add_parser("open", help="open a slot Editor and wait until it answers")
    p.add_argument("slot", help="w1, w2 or w3")
    p.set_defaults(func=run_open)


def close_wait_s():
    return float(os.environ.get("SLOT_CLOSE_WAIT_S", "15"))


def open_wait_s():
    return float(os.environ.get("SLOT_OPEN_WAIT_S", "600"))


def run_cli(*args, timeout):
    try:
        subprocess.run(c.unity_argv(*args), capture_output=True, text=True, timeout=timeout,
                       encoding="utf-8", errors="replace")
    except (subprocess.TimeoutExpired, OSError):
        pass  # the verdict comes from `unity status`, not from this call


def run_close(args):
    slot = args.slot
    c.check_slot(slot)
    known, pid = editors.editor_pid(slot)
    if known and pid is None:
        print(f"{c.label(slot)}: already closed (not in `unity status`).")
        return 0
    if not args.force:
        lock = c.read_lock(slot)
        if lock and lock["live"]:
            raise c.SlotError(f"{c.label(slot)}: a slot tool run is live (pid {lock.get('pid')}, "
                              f"{lock.get('kind', '?')}). Refusing to close. Wait for it, or add --force.")
        if known and c.editor_state(slot) == "test_run":
            raise c.SlotError(f"{c.label(slot)}: a test run is live in the Editor. Refusing to close. "
                              f"Wait for it, or add --force.")
    run_cli("close", c.slot_path(slot), "--timeout", "30", timeout=90)
    deadline = time.time() + close_wait_s()
    while True:
        known, pid = editors.editor_pid(slot)
        if known and pid is None:
            print(f"{c.label(slot)}: closed (confirmed by `unity status`).")
            return 0
        if time.time() >= deadline:
            break
        time.sleep(c.poll_s())
    why = f"still open (pid {pid})" if known else "not confirmed (`unity status` could not be read)"
    print(f"{c.label(slot)}: not closed, {why}. The close did not take. "
          f"Check the Editor window for an unsaved scene or a dialog before trying again.")
    return 1


WHY = {"no_answer": "not answering", "compiling": "still compiling or reloading"}


def run_open(args):
    slot = args.slot
    c.check_slot(slot)
    known, pid = editors.editor_pid(slot)
    if not known:
        raise c.SlotError(f"{c.label(slot)}: `unity status` could not be read; not opening "
                          f"(a second Editor beside a live one leaves an Error window).")
    already = pid is not None
    if not already:
        run_cli("open", c.slot_path(slot), timeout=60)
    deadline = time.time() + open_wait_s()
    note = " (already open)" if already else ""
    while True:
        state = c.editor_state(slot)
        if state == "ready":
            print(f"{c.label(slot)}: ready{note}.")
            return 0
        if state == "play":
            print(f"{c.label(slot)}: open and answering, in Play{note}.")
            return 0
        if state == "test_run":
            print(f"{c.label(slot)}: open and answering, a test run is live{note}.")
            return 0
        if time.time() >= deadline:
            print(f"{c.label(slot)}: not ready after {open_wait_s():.0f} s: Editor {WHY[state]}.")
            return 1
        time.sleep(c.poll_s())
