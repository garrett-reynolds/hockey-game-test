"""`slot.py close <slot> [--force]` and `slot.py open <slot>`: close and open a slot Editor, and trust the answer.

close: runs `unity close`, then confirms with `unity status`. Says "closed" only when status no longer lists the Editor.
open: never starts a second Editor beside an open one; waits until the Editor answers (self-recover rule).
"""
import os
import subprocess
import sys

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
    return c.env_float("SLOT_CLOSE_WAIT_S", 15)


def open_wait_s():
    return c.env_float("SLOT_OPEN_WAIT_S", 600)


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
    done, (known, pid) = wait_closed(slot)
    if done:
        print(f"{c.label(slot)}: closed (confirmed by `unity status`).")
        return 0
    if args.force and known and pid is not None:
        # The close did not take: the Editor is jammed. End its process tree (Editor + AssetImportWorkers).
        print(f"{c.label(slot)}: `unity close` did not take; killing the Editor process (pid {pid}) and its workers.")
        kill_tree(pid)
        done, (known, pid) = wait_closed(slot)
        if done:
            print(f"{c.label(slot)}: killed and closed (confirmed by `unity status`).")
            return 0
    why = f"still open (pid {pid})" if known else "not confirmed (`unity status` could not be read)"
    tail = ("" if args.force else f" If the Editor is jammed (not answering), run: slot.py close {slot} --force. ")
    print(f"{c.label(slot)}: not closed, {why}. The close did not take. {tail}"
          f"Check the Editor window for an unsaved scene or a dialog before trying again.")
    return 1


def wait_closed(slot):
    """Poll `unity status` until the slot Editor is gone. Returns (done, (known, pid))."""
    def check():
        known, pid = editors.editor_pid(slot)
        return (known and pid is None), (known, pid)
    return c.poll_until(check, close_wait_s())


def kill_tree(pid):
    """End a process and its children. SLOT_KILLER (a python script taking the pid) is the test seam."""
    killer = os.environ.get("SLOT_KILLER")
    argv = [sys.executable, killer, str(pid)] if killer else ["taskkill", "/PID", str(pid), "/T", "/F"]
    try:
        subprocess.run(argv, capture_output=True, text=True, timeout=60)
    except (subprocess.TimeoutExpired, OSError):
        pass  # the verdict comes from `unity status`


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
    note = " (already open)" if already else ""

    def check():
        state = c.editor_state(slot)
        return state in ("ready", "play", "test_run"), state

    done, state = c.poll_until(check, open_wait_s())
    if state == "ready":
        print(f"{c.label(slot)}: ready{note}.")
    elif state == "play":
        print(f"{c.label(slot)}: open and answering, in Play{note}.")
    elif state == "test_run":
        print(f"{c.label(slot)}: open and answering, a test run is live{note}.")
    else:
        hint = (f" Jammed? Run: slot.py close {slot} --force  then  slot.py open {slot}"
                if already and state == "no_answer" else "")
        print(f"{c.label(slot)}: not ready after {open_wait_s():.0f} s: Editor {WHY[state]}.{hint}")
        return 1
    return 0
