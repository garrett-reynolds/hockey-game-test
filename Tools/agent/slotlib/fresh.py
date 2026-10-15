"""ensure_fresh(slot): Refresh + recompile the slot's Editor when the code changed since the last good compile (#186).

One entry for `test` and play-check (#187), both through testrun.prepare. The stamp is the HEAD sha plus (path, mtime) of the uncommitted code
files under Assets/ and Packages/manifest.json. It is saved in lock_dir() as <slot>.stamp.json, and only after a
recompile with no compile error.
"""
import json
import os
import subprocess

from . import common as c

CODE_ENDINGS = tuple("." + e for e in ("cs", "shader", "hlsl", "cginc", "compute", "asmdef", "asmref", "uss", "uxml"))
NETWORK = "Network error"
DONE_STATES = ("completed", "up_to_date")
PENDING_STATES = ("idle", "triggered", "compiling", "running", "pending", "in_progress", "started")
MAX_ERROR_LINES = 5


def stamp_file(slot):
    return c.lock_dir() / f"{slot}.stamp.json"


def counted(path):
    p = path.replace("\\", "/")
    if p == "Packages/manifest.json":
        return True
    return p.startswith("Assets/") and p.lower().endswith(CODE_ENDINGS)


def git_out(slot, *args):
    """stdout of a git command in the slot folder, or None when git cannot answer."""
    try:
        p = subprocess.run(["git", "-C", c.slot_path(slot), *args], capture_output=True, text=True, timeout=30,
                           encoding="utf-8", errors="replace")
    except (OSError, subprocess.SubprocessError):
        return None
    return p.stdout if p.returncode == 0 else None


def current_stamp(slot):
    """{"head": sha, "files": {path: mtime}}, or None when git cannot be read."""
    head = git_out(slot, "rev-parse", "HEAD")
    status = git_out(slot, "status", "--porcelain", "-z", "-uall", "--", "Assets", "Packages/manifest.json")
    if head is None or status is None:
        return None
    parts = status.split("\0")
    paths = []
    i = 0
    while i < len(parts):
        entry = parts[i]
        i += 1
        if len(entry) < 4:
            continue
        paths.append(entry[3:])
        if entry[0] in "RC":  # a rename or copy is followed by the old path
            i += 1
    files = {}
    for p in sorted(set(paths)):
        if not counted(p):
            continue
        try:
            files[p] = os.stat(os.path.join(c.slot_path(slot), p)).st_mtime
        except OSError:
            files[p] = None  # deleted
    return {"head": head.strip(), "files": files}


def read_stamp(slot):
    try:
        data = json.loads(stamp_file(slot).read_text(encoding="utf-8"))
    except (ValueError, OSError):
        return None
    return data if isinstance(data, dict) else None


def why_changed(old, new):
    """None when nothing changed, else the short reason for the message."""
    if old is None:
        return "first run"
    if old.get("head") != new["head"]:
        return "new HEAD"
    before = old.get("files") or {}
    n = len([p for p in set(before) | set(new["files"]) if before.get(p, "x") != new["files"].get(p, "x")])
    return f"{n} changed files" if n else None


def save_stamp(slot, stamp):
    try:
        c.lock_dir().mkdir(parents=True, exist_ok=True)
        stamp_file(slot).write_text(json.dumps(stamp), encoding="utf-8")
    except OSError:
        pass  # a missing stamp only means one more refresh next time


def unity(slot, *args):
    """One Editor command, retried once on 'Network error'. Returns (rc, text); (None, reason) if it cannot run."""
    limit = int(c.wait_s())
    for attempt in (1, 2):
        try:
            p = subprocess.run(c.unity_command(slot, *args, timeout=limit), capture_output=True, text=True,
                               timeout=limit + c.status_grace_s(), encoding="utf-8", errors="replace")
        except subprocess.TimeoutExpired:
            return None, "timed out"
        except OSError as e:
            return None, f"cannot run the Unity CLI: {e.__class__.__name__}"
        text = ((p.stdout or "") + "\n" + (p.stderr or "")).strip()
        if NETWORK in text:
            if attempt == 1:
                continue
            return None, NETWORK  # twice in a row, whatever the exit code said
        return p.returncode, text
    return None, "unreachable"


def call(slot, step, *args, what="test"):
    """unity() for one step; a command that cannot run, or a second Network error, stops the run (exit 2)."""
    rc, text = unity(slot, *args)
    if rc is None:
        if text == NETWORK:
            raise c.SlotError(f"{c.label(slot)}: Network error twice during {step}; the Editor may be reloading. "
                              f"Run slot.py {what} again.")
        raise c.SlotError(f"{c.label(slot)}: {step} did not run ({text}). Not started.")
    return rc, text


def verdict(data, raw, allow_no_status=False):
    """Read a recompile / recompile_status reply. Returns ("clean", []), ("pending", []) or ("failed", lines).

    Real shape (com.unity.pipeline, build-and-compilation.md): status idle|triggered|compiling|completed|up_to_date,
    plus failed (bool), errors (string list), compilationFailed (bool); `recompile` itself can answer status
    "failed" when errors are standing. Only positive proof is clean: a done status AND no truthy key whose name
    holds "error" or "fail" AND success is not false. A status that is neither done nor pending is a failure."""
    if not isinstance(data, dict):
        return "pending", []
    status = str(data.get("status", "")).lower()
    bad = [k for k, v in data.items() if v and ("error" in k.lower() or "fail" in k.lower())]
    if status in DONE_STATES and not bad and data.get("success") is not False:
        return "clean", []
    if (status in PENDING_STATES or (allow_no_status and not status)) and not bad and data.get("success") is not False:
        return "pending", []
    lines = []
    errors = data.get("errors")
    for e in errors if isinstance(errors, list) else []:
        if isinstance(e, dict):
            e = e.get("message") or e.get("error") or json.dumps(e)
        lines.append(str(e))
    return "failed", lines or [x for x in raw.splitlines() if x.strip()] or ["(empty reply)"]


def refuse_on_errors(slot, lines):
    shown = "\n".join(lines[:MAX_ERROR_LINES])
    more = f"\n(+{len(lines) - MAX_ERROR_LINES} more)" if len(lines) > MAX_ERROR_LINES else ""
    print(f"{c.label(slot)}: compile error after Refresh + recompile; tests not run.\n{shown}{more}")
    return 1


def refresh(slot, what="test"):
    """Refresh, recompile, wait for the compile. Returns the compile error lines ([] = fine)."""
    rc, text = call(slot, "menu", "menu", "--path", "Assets/Refresh", what=what)
    if rc != 0:
        raise c.SlotError(f"{c.label(slot)}: Refresh failed ({text.splitlines()[0][:70] if text else rc}). Not started.")
    rc, text = call(slot, "recompile", "recompile", what=what)
    if rc != 0:
        raise c.SlotError(f"{c.label(slot)}: recompile failed ({text.splitlines()[0][:70] if text else rc}). Not started.")
    kind, lines = verdict(c.parse_json(text), text, allow_no_status=True)  # only the poll proves success
    if kind == "failed":
        return lines

    def check():
        rc, text = call(slot, "recompile_status", "recompile_status", what=what)
        d = c.parse_json(text) if rc == 0 else None
        if d is None:
            return False, None  # the Editor may be reloading; keep asking
        kind, lines = verdict(d, text)
        return kind != "pending", (kind, lines)

    done, result = c.poll_until(check, c.wait_s())
    if not done:
        raise c.SlotError(f"{c.label(slot)}: compile not finished after {c.wait_s():.0f} s. Not started.")
    return result[1] if result[0] == "failed" else []


def ensure_fresh(slot, what="test"):
    """Refresh + recompile when the code changed. Returns None when fine, else an exit code (1 = compile error).
    `what` names the command in the retry hint (test or play-check)."""
    new = current_stamp(slot)
    reason = "first run" if new is None else why_changed(read_stamp(slot), new)
    if reason is None:
        return None
    print(f"{slot}: code changed since the last compile ({reason}); Refresh + recompile")
    lines = refresh(slot, what)
    if lines:
        return refuse_on_errors(slot, lines)
    if new is not None:
        save_stamp(slot, new)
    return None
