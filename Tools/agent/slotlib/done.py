"""`slot.py done <slot>` (#188): git state, red/green runs and changed test files. Never talks to the Editor."""
import json
import os
import re
import subprocess
import time
from collections import namedtuple
from pathlib import Path, PurePosixPath

from . import common as c
from . import cpu

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


Check = namedtuple("Check", "ok text detail")
MAX_LISTED = 10


def capped(lines):
    """At most MAX_LISTED lines, then '  +N more'."""
    if len(lines) <= MAX_LISTED:
        return lines
    return lines[:MAX_LISTED] + [f"  +{len(lines) - MAX_LISTED} more"]


GitState = namedtuple("GitState", "check branch head dirty")  # branch, head, dirty are None when git fails


def check_git(slot):
    try:
        branch = git(slot, "branch", "--show-current").strip()
        head = git(slot, "rev-parse", "HEAD").strip()
        dirty = uncommitted(slot)
        ahead = int(git(slot, "rev-list", "--count", f"{MAIN}..HEAD").strip())
        commits = git(slot, "log", "--oneline", f"{MAIN}..HEAD").splitlines()
    except (GitError, ValueError) as e:
        return GitState(Check(False, f"FAIL git: {e}", []), None, None, None)
    detail = capped([f"  uncommitted: {n}" for n in dirty]) + capped([f"  {x}" for x in commits])
    text = f"OK git: branch {branch}, {len(dirty)} uncommitted, {ahead} ahead of {MAIN}"
    return GitState(Check(True, text, detail), branch, head, dirty)


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


def is_green(r, ignore_filter=False):
    """Green needs an unfiltered run: a filtered run proves nothing about the rest."""
    if not ignore_filter and r.get("filter"):
        return False
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
        return Check(True, f"OK runs: green run at HEAD {head[:7]} on {branch}", detail)
    if any(is_green(r, ignore_filter=True) and r.get("head") == head for r in runs):
        return Check(False, f"FAIL runs: no unfiltered green run at HEAD {head[:7]} (filtered green runs do not count)", detail)
    return Check(False, f"FAIL runs: no green run at HEAD {head[:7]} on {branch}", detail)


def is_test_file(path):
    p = PurePosixPath(path)
    return bool(p.parts) and p.parts[0] == "Assets" and p.suffix == ".cs" and "Tests" in p.parts[:-1]


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
        return Check(False, f"FAIL test files: {e}", [])
    if not times:
        return Check(True, "OK test files: none changed", [])
    if not runs:
        late = sorted(times)
        why = "no run logged for this branch"
    else:
        last = max(r["epoch"] for r in runs)
        late = sorted(n for n, t in times.items() if t > last)
        why = "changed after the last run"
    if late:
        return Check(False, f"FAIL test files: {len(late)} {why}", [f"  {n}" for n in late])
    return Check(True, f"OK test files: {len(times)} changed, all before the last run", [])


SHELLS = {"bash", "sh", "python", "pythonw", "py", "powershell", "pwsh"}
LOOP_WORD = re.compile(r"\b(until|while)\b", re.I)
SLEEP_WORD = re.compile(r"\b(sleep|Start-Sleep|time\.sleep)\b", re.I)


def norm(text):
    return text.replace("\\", "/").lower()


def is_wait_loop(proc, slot_dir):
    name = str(proc.get("name") or "").lower()
    if name.endswith(".exe"):
        name = name[:-4]
    cmd = str(proc.get("cmd") or "")
    if name not in SHELLS or not LOOP_WORD.search(cmd) or not SLEEP_WORD.search(cmd):
        return False
    low = norm(cmd)
    return norm(slot_dir) in low or "slot.py" in low


def own_chain(procs):
    """This process and every ancestor, so `done` never lists itself or the shell that started it."""
    parent = {p["id"]: p["parent"] for p in procs}
    chain, pid = set(), os.getpid()
    while pid is not None and pid not in chain:
        chain.add(pid)
        pid = parent.get(pid)
    return chain


def check_wait_loops(slot):
    try:
        procs = cpu.read_processes(cpu.PS_NAMES)
        procs = [p for p in procs if isinstance(p, dict) and "id" in p]
    except (subprocess.SubprocessError, OSError, ValueError):
        return Check(False, "FAIL wait loops: could not read processes", [])
    skip = own_chain(procs)
    hits = [p for p in procs if p["id"] not in skip and is_wait_loop(p, c.slot_path(slot))]
    detail = [f"  PID {p['id']}  {str(p.get('cmd'))[:100]}" for p in hits]
    if hits:
        return Check(False, f"FAIL wait loops: {len(hits)}", detail)
    return Check(True, "OK wait loops: 0", [])


def add_parser(sub):
    p = sub.add_parser("done", help="check a slot is ready to hand off: git state, green run at HEAD, test files run")
    p.add_argument("slot")
    p.set_defaults(func=run)


def run(args):
    c.check_slot(args.slot)
    slot = args.slot
    state = check_git(slot)
    checks = [state.check]
    if state.check.ok:
        runs = read_runs(slot, state.branch)
        checks += [check_runs(runs, state.branch, state.head),
                   check_test_files(slot, runs, state.dirty),
                   check_wait_loops(slot)]
    lines = []
    for chk in checks:
        lines.append(chk.text)
        lines += chk.detail
    fails = sum(1 for chk in checks if not chk.ok)
    if not state.check.ok:
        print("\n".join(lines))
        print(f"done: {fails} check(s) FAIL")
        return 1
    print("\n".join(lines))
    print(f"done: {fails} check(s) FAIL" if fails else "done: all checks OK")
    return 1 if fails else 0
