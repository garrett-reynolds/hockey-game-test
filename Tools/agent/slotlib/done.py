"""`slot.py done <slot>` (#188): git state, red/green runs and changed test files. Never talks to the Editor."""
import json
import subprocess
import time
from pathlib import Path, PurePosixPath

from . import common as c

MAIN = "main"


class GitError(Exception):
    pass


def git(slot, *args):
    try:
        p = subprocess.run(["git", "-C", c.slot_path(slot), *args], capture_output=True, text=True, timeout=30)
    except (OSError, subprocess.SubprocessError) as e:
        raise GitError(str(e))
    if p.returncode != 0:
        raise GitError((p.stderr or p.stdout).strip() or f"git {' '.join(args)} failed")
    return p.stdout


def uncommitted(slot):
    """Paths with changes in the working tree (modified, added, untracked), from `status -z`."""
    parts = git(slot, "status", "--porcelain", "-z", "-uall").split("\0")
    names, i = [], 0
    while i < len(parts):
        entry = parts[i]
        i += 1
        if len(entry) < 4:
            continue
        names.append(entry[3:])
        if entry[0] in "RC":  # a rename or copy carries the old name as the next field
            i += 1
    return names


def check_git(slot):
    """Returns (ok, first line, detail lines, branch, head)."""
    try:
        branch = git(slot, "branch", "--show-current").strip()
        head = git(slot, "rev-parse", "HEAD").strip()
        dirty = uncommitted(slot)
        ahead = int(git(slot, "rev-list", "--count", f"{MAIN}..HEAD").strip())
        commits = git(slot, "log", "--oneline", f"{MAIN}..HEAD").splitlines()
    except (GitError, ValueError) as e:
        return False, f"FAIL git: {e}", [], None, None
    detail = [f"  uncommitted: {n}" for n in dirty] + [f"  {x}" for x in commits]
    return True, f"OK git: branch {branch}, {len(dirty)} uncommitted, {ahead} ahead of {MAIN}", detail, branch, head


def epoch(text):
    return time.mktime(time.strptime(text, "%Y-%m-%dT%H:%M:%S"))


def read_runs(slot, branch):
    """Run-log records for this branch, oldest first, each with an 'epoch' key. Bad lines are skipped."""
    f = c.results_dir() / f"{slot}-runs.jsonl"
    if not f.exists():
        return []
    recs = []
    for text in f.read_text(encoding="utf-8").splitlines():
        try:
            rec = json.loads(text)
            if rec.get("branch") != branch:
                continue
            rec["epoch"] = epoch(rec["time"])
        except (ValueError, KeyError, TypeError):
            continue
        recs.append(rec)
    return recs


def is_red(r):
    return isinstance(r.get("failed"), int) and r["failed"] > 0


def is_green(r):
    return r.get("failed") == 0 and isinstance(r.get("passed"), int) and r["passed"] > 0 and r.get("rc") == 0


def describe(r):
    return f"{r['time']} {str(r.get('head'))[:7]} {r.get('passed')}/{r.get('failed')} {r.get('result_file')}"


def check_runs(runs, branch, head):
    red = next((r for r in runs if is_red(r)), None)
    green = next((r for r in reversed(runs) if is_green(r)), None)
    detail = []
    if red:
        detail.append(f"  first red: {describe(red)}")
    if green:
        detail.append(f"  last green: {describe(green)}")
    if any(is_green(r) and r.get("head") == head for r in runs):
        return True, f"OK runs: green run at HEAD {head[:7]} on {branch}", detail
    return False, f"FAIL runs: no green run at HEAD {head[:7]} on {branch}", detail


def is_test_file(path):
    p = PurePosixPath(path)
    if p.parts and p.parts[0] == "Assets":
        return p.suffix == ".cs" and "Tests" in p.parts[:-1]
    return p.parts[:3] == ("Tools", "agent", "tests") and len(p.parts) == 4 and p.suffix == ".py"


def test_file_times(slot, dirty):
    """{path: change time in epoch seconds} for test files changed since main or uncommitted."""
    base = git(slot, "merge-base", MAIN, "HEAD").strip()
    times = {}
    for name in git(slot, "diff", "--name-only", "-z", base, "HEAD").split("\0"):
        if name and is_test_file(name):
            when = git(slot, "log", "-1", "--format=%ct", "--", name).strip()
            times[name] = float(when)
    root = Path(c.slot_path(slot))
    for name in dirty:
        if is_test_file(name) and (root / name).exists():
            times[name] = (root / name).stat().st_mtime
    return times


def check_test_files(slot, runs, dirty):
    try:
        times = test_file_times(slot, dirty)
    except (GitError, ValueError, OSError) as e:
        return False, f"FAIL test files: {e}", []
    if not times:
        return True, "OK test files: none changed", []
    if not runs:
        late = sorted(times)
        why = "no run logged for this branch"
    else:
        last = max(r["epoch"] for r in runs)
        late = sorted(n for n, t in times.items() if t > last)
        why = "changed after the last run"
    if late:
        return False, f"FAIL test files: {len(late)} {why}", [f"  {n}" for n in late]
    return True, f"OK test files: {len(times)} changed, all before the last run", []


def add_parser(sub):
    p = sub.add_parser("done", help="check a slot is ready to hand off: git state, green run at HEAD, test files run")
    p.add_argument("slot")
    p.set_defaults(func=run)


def run(args):
    c.check_slot(args.slot)
    slot = args.slot
    lines, fails = [], 0
    ok, text, detail, branch, head = check_git(slot)
    lines.append(text)
    lines += detail
    if not ok:
        fails += 1
        print("\n".join(lines))
        print(f"done: {fails} check(s) FAIL")
        return 1
    runs = read_runs(slot, branch)
    for ok, text, detail in (check_runs(runs, branch, head),
                             check_test_files(slot, runs, uncommitted(slot))):
        lines.append(text)
        lines += detail
        fails += 0 if ok else 1
    print("\n".join(lines))
    print(f"done: {fails} check(s) FAIL" if fails else "done: all checks OK")
    return 1 if fails else 0
