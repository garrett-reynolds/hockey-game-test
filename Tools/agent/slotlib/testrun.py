"""`slot.py test <slot> [--full] [--filter X]`: a safe EditMode run on one slot Editor."""
import json
import subprocess
import sys
import time
from pathlib import Path

from . import common as c
from . import editors, watch


def add_parser(sub):
    p = sub.add_parser("test", help="run EditMode tests on a slot (fast by default, --full adds explicit tests)")
    p.add_argument("slot", help="w1, w2 or w3")
    p.add_argument("--full", action="store_true", help="full run (--include_explicit), 1800 s")
    p.add_argument("--filter", default=None, help="run_tests filter pattern")
    p.set_defaults(func=run)


def wait_until_ready(slot):
    """Poll until the Editor answers, is done compiling, and has no test run. Refuse on Play or a live run."""
    deadline = time.time() + c.wait_s()
    state = "no_answer"
    while True:
        state = c.editor_state(slot)
        if state == "ready":
            return
        if state == "play":
            raise c.SlotError(f"{c.label(slot)}: the Editor is in Play. Refusing to test. Stop Play first.")
        if state == "test_run":
            raise c.SlotError(f"{c.label(slot)}: a test run is already live in this Editor. Refusing.")
        if time.time() >= deadline:
            what = "not answering" if state == "no_answer" else "still compiling"
            raise c.SlotError(f"{c.label(slot)}: Editor {what} after {c.wait_s():.0f} s. Not started.")
        time.sleep(c.poll_s())


def run(args):
    slot = args.slot
    c.check_slot(slot)
    lock = c.read_lock(slot)
    if lock and lock["live"]:
        raise c.SlotError(f"{c.label(slot)}: a run is already running (pid {lock.get('pid')}, "
                          f"{lock.get('kind', '?')}). Refusing a second one.")
    known, pid = editors.editor_pid(slot)
    if known and pid is None:
        raise c.SlotError(f"{c.label(slot)}: not open; run: slot.py open {slot}")
    try:
        wait_until_ready(slot)
    except c.SlotError as e:
        if lock:
            raise c.SlotError(f"{e} Found a stale lock (pid {lock.get('pid')} gone); kept because the Editor "
                              f"did not answer.")
        raise
    if lock:
        print(f"{c.label(slot)}: stale lock (pid {lock.get('pid')} gone) cleared; the Editor answers.")
        c.release_lock(slot)
    kind = "full" if args.full else "fast"
    c.acquire_lock(slot, kind)
    try:
        return execute(slot, kind, args.filter)
    finally:
        c.release_lock(slot)


def execute(slot, kind, flt):
    full = kind == "full"
    limit = c.FULL_TIMEOUT if full else c.FAST_TIMEOUT
    cmd = ["run_tests", "--mode", "EditMode"]
    if flt:
        cmd += ["--filter", flt]
    if full:
        cmd += ["--include_explicit", "true"]
    # The inner limit (after "--") is the one run_tests itself obeys; without it a slow run stays at 300 s.
    cmd += ["--", "--timeout", str(limit)]
    argv = c.unity_command(slot, *cmd, timeout=limit)
    # c.unity_command puts --timeout/--result-only before the command name, as the real CLI wants.
    c.results_dir().mkdir(parents=True, exist_ok=True)
    out_path = c.results_dir() / f"{slot}-{kind}-{time.strftime('%Y%m%d-%H%M%S')}.json"
    with open(out_path, "wb") as out:
        proc = subprocess.Popen(argv, stdout=out, stderr=subprocess.STDOUT)
        rc = watch.watch_run(slot, proc)
    if rc == watch.HANG:
        script = Path(sys.argv[0]).resolve()
        print(f"{c.label(slot)} {kind}: HANG. The Editor used almost no CPU for 2 minutes while the run was live.")
        print(f"Next: close and reopen the slot: python {script} close {slot}  then  python {script} open {slot}")
        print("Do not retry the run before the Editor answers again. The run_tests CLI is left alone; closing the Editor ends it.")
        return watch.HANG_EXIT
    try:
        summary = json.loads(out_path.read_text(encoding="utf-8-sig"))["Summary"]
        total, passed, failed = summary["Total"], summary["Passed"], summary["Failed"]
    except (ValueError, KeyError, TypeError, OSError):
        print(f"{c.label(slot)} {kind}: failed, no result (CLI exit {rc}); see the output file")
        print(out_path)
        return 1
    print(f"{c.label(slot)} {kind}: {passed} passed, {failed} failed of {total}")
    print(out_path)
    return 0 if failed == 0 and rc == 0 else 1
