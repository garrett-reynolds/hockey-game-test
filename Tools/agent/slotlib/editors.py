"""Which slot Editors are open, from `unity status` (read-only). Used by close, open, test and the hang watch."""
import subprocess

from . import common as c


def _norm(path):
    return path.replace("\\", "/").rstrip("/").lower()


def open_rows():
    """{normalised project path: {"pid", "port"}} for every open Editor, or None if `unity status` could not be read.

    The real CLI exits non-zero when no Editor matches but still prints the table header, so the header
    (not the exit code) tells us the answer can be trusted.
    """
    try:
        p = subprocess.run(c.unity_argv("status"), capture_output=True, text=True, timeout=c.status_timeout_s() + 15,
                           encoding="utf-8", errors="replace")
    except (subprocess.TimeoutExpired, OSError):
        return None
    lines = [ln for ln in p.stdout.splitlines() if ln.strip()]
    if not any(ln.split()[:1] == ["Port"] for ln in lines):
        return None
    found = {}
    for ln in lines:
        parts = ln.split()
        # Port State Project... Version PID (the project path may hold spaces)
        if len(parts) >= 5 and parts[0].isdigit() and parts[-1].isdigit():
            found[_norm(" ".join(parts[2:-2]))] = {"pid": int(parts[-1]), "port": int(parts[0])}
    return found


def open_editors():
    """{normalised project path: pid}, or None if `unity status` could not be read."""
    rows = open_rows()
    return None if rows is None else {path: row["pid"] for path, row in rows.items()}


def editor_pid(slot):
    """(known, pid): known is False when `unity status` failed; pid is None when the slot Editor is not open."""
    found = open_editors()
    if found is None:
        return False, None
    return True, found.get(_norm(c.slot_path(slot)))


def slot_key(slot):
    return _norm(c.slot_path(slot))
