"""`slot.py test <slot> [--full] [--filter X]`: a safe EditMode run on one slot Editor."""
import json
import subprocess
import sys
import time
from pathlib import Path

from . import common as c
from . import editors, fresh, watch


def add_parser(sub):
    p = sub.add_parser("test", help="run EditMode tests on a slot (fast by default, --full adds explicit tests)")
    p.add_argument("slot", help="w1, w2 or w3")
    p.add_argument("--full", action="store_true", help="full run (--include_explicit), 1800 s")
    p.add_argument("--filter", default=None, help="run_tests filter pattern")
    p.set_defaults(func=run)


def wait_until_ready(slot):
    """Poll until the Editor answers, is done compiling, and has no test run. Refuse on Play or a live run."""
    def check():
        state = c.editor_state(slot)
        return state not in ("no_answer", "compiling"), state

    _, state = c.poll_until(check, c.wait_s())
    if state == "ready":
        return
    if state == "play":
        raise c.SlotError(f"{c.label(slot)}: the Editor is in Play. Refusing to test. Stop Play first.")
    if state == "test_run":
        raise c.SlotError(f"{c.label(slot)}: a test run is already live in this Editor. Refusing.")
    raise c.SlotError(f"{c.label(slot)}: Editor {c.STATE_TEXT[state]} after {c.wait_s():.0f} s. Not started.")


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
        wait_until_ready(slot)  # never refresh an Editor that is in Play, busy or silent
    except c.SlotError as e:
        if lock:
            raise c.SlotError(f"{e} Found a stale lock (pid {lock.get('pid')} gone); kept because the Editor "
                              f"did not answer.")
        raise
    if lock:
        print(f"{c.label(slot)}: stale lock (pid {lock.get('pid')} gone) cleared; the Editor answers.")
        c.release_lock(slot)
    kind = "full" if args.full else "fast"
    c.acquire_lock(slot, kind)  # held from here: a second `test` must not recompile under a refresh or a run
    try:
        code = fresh.ensure_fresh(slot)
        if code is not None:
            return code
        wait_until_ready(slot)  # the recompile reloads the Editor: wait for it again
        return execute(slot, kind, args.filter)
    finally:
        c.release_lock(slot)


def stop_cli(proc):
    """Stop the run_tests CLI child (only called for a hung Editor): terminate, then kill if it lingers."""
    proc.terminate()
    try:
        proc.wait(timeout=10)
    except subprocess.TimeoutExpired:
        proc.kill()
        proc.wait()


def git_read(slot, *args):
    """One git value from the slot folder, or None when git cannot answer (never fails the run)."""
    try:
        p = subprocess.run(["git", "-C", c.slot_path(slot), *args], capture_output=True, text=True, timeout=10)
    except (OSError, subprocess.SubprocessError):
        return None
    out = p.stdout.strip()
    return out if p.returncode == 0 and out else None


def log_run(slot, kind, flt, rc, status, counts, out_path):
    """Append one JSON line for this run to <results>/<slot>-runs.jsonl. A write error never fails the run."""
    total, passed, failed = counts
    rec = {
        "time": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "slot": slot,
        "branch": git_read(slot, "branch", "--show-current"),
        "head": git_read(slot, "rev-parse", "HEAD"),
        "mode": kind,
        "filter": flt,
        "total": total,
        "passed": passed,
        "failed": failed,
        "rc": rc,
        "status": status,
        "result_file": str(out_path),
    }
    try:
        with open(c.results_dir() / f"{slot}-runs.jsonl", "a", encoding="utf-8") as f:
            f.write(json.dumps(rec) + "\n")
    except OSError as e:
        print(f"{c.label(slot)}: run log not written ({e})", file=sys.stderr)


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
        stop_cli(proc)  # the lock is released after this; no run_tests CLI may outlive it
        log_run(slot, kind, flt, None, "hang", (None, None, None), out_path)
        script = Path(sys.argv[0]).resolve()
        print(f"{c.label(slot)} {kind}: HANG. The Editor used almost no CPU for 2 minutes while the run was live.")
        print(f"Next: close and reopen the slot: python {script} close {slot}  then  python {script} open {slot}")
        print("Do not retry the run before the Editor answers again. The run_tests CLI was stopped (the Editor was already hung).")
        return watch.HANG_EXIT
    try:
        summary = json.loads(out_path.read_text(encoding="utf-8-sig"))["Summary"]
        total, passed, failed = summary["Total"], summary["Passed"], summary["Failed"]
    except (ValueError, KeyError, TypeError, OSError):
        log_run(slot, kind, flt, rc, "no_result", (None, None, None), out_path)
        print(f"{c.label(slot)} {kind}: failed, no result (CLI exit {rc}); see the output file")
        print(out_path)
        return 1
    green = failed == 0 and rc == 0
    log_run(slot, kind, flt, rc, "green" if green else "red", (total, passed, failed), out_path)
    print(f"{c.label(slot)} {kind}: {passed} passed, {failed} failed of {total}")
    print(out_path)
    return 0 if green else 1
