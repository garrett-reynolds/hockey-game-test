"""Shared parts of the slot tool: Unity CLI call, slot list, lock, Editor state.

Every sub-command module (status, test, later close/open/play-check) imports from here.
Everything environment-specific is read from an env var so tests can point it at a fake CLI.
"""
import ctypes
import json
import os
import subprocess
import sys
import time
from pathlib import Path

NOTES_DEFAULT = "C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes"
ROOT_DEFAULT = "C:/Users/garrett.reynolds/Projects/Personal/hockey-wt"
UNITY_DEFAULT = "C:/Users/garrett.reynolds/AppData/Local/Unity/bin/unity.exe"

# Slot name -> folder under SLOT_ROOT. Keep in step with the guard hook's slot list.
SLOTS = {"w1": "111", "w2": "w2", "w3": "w3"}

# Fixed time limits (seconds). Callers cannot change them: a run cut short jams the Editor.
FAST_TIMEOUT = 900
FULL_TIMEOUT = 1800


class SlotError(Exception):
    """A refusal or failure with a clear message; the entry script prints it and exits 2."""


def unity_exe():
    return os.environ.get("SLOT_UNITY_EXE", UNITY_DEFAULT)


def lock_dir():
    return Path(os.environ.get("SLOT_LOCK_DIR", NOTES_DEFAULT + "/locks"))


def results_dir():
    return Path(os.environ.get("SLOT_RESULTS_DIR", NOTES_DEFAULT + "/results"))


def slot_root():
    return os.environ.get("SLOT_ROOT", ROOT_DEFAULT)


def poll_s():
    return float(os.environ.get("SLOT_POLL_S", "3"))


def wait_s():
    return float(os.environ.get("SLOT_WAIT_S", "600"))


def status_timeout_s():
    return int(os.environ.get("SLOT_STATUS_TIMEOUT_S", "20"))


def label(slot):
    return slot if SLOTS[slot] == slot else f"{slot} ({SLOTS[slot]})"


def slot_path(slot):
    return f"{slot_root()}/{SLOTS[slot]}"


def check_slot(slot):
    if slot not in SLOTS:
        raise SlotError(f"Unknown slot '{slot}'. Slots: {', '.join(SLOTS)}.")


def unity_argv(*args):
    exe = unity_exe()
    if exe.lower().endswith(".py"):
        return [sys.executable, exe, *args]
    return [exe, *args]


def unity_command(slot, *args, timeout=None):
    """Argv for `unity command --project-path <slot> [--timeout N] --result-only <args>`."""
    argv = ["command", "--project-path", slot_path(slot)]
    if timeout is not None:
        argv += ["--timeout", str(timeout)]
    argv += ["--result-only", *args]
    return unity_argv(*argv)


def query(slot, command):
    """Run a short read-only Editor command. Returns the parsed JSON dict, or None if the Editor does not answer."""
    t = status_timeout_s()
    try:
        p = subprocess.run(unity_command(slot, command, timeout=t), capture_output=True, text=True,
                           timeout=t + 15, encoding="utf-8", errors="replace")
    except (subprocess.TimeoutExpired, OSError):
        return None
    if p.returncode != 0:
        return None
    try:
        data = json.loads(p.stdout)
    except ValueError:
        return None
    return data if isinstance(data, dict) else None


# ---- Editor state ---------------------------------------------------------------------------

def editor_state(slot):
    """One of: 'no_answer', 'compiling', 'play', 'test_run', 'ready' (plus the raw detail)."""
    ed = query(slot, "editor_status")
    if ed is None:
        return "no_answer"
    if ed.get("playMode", "stopped") != "stopped":
        return "play"
    if ed.get("compiling") or ed.get("domainReloadInProgress") or ed.get("status") not in ("ready", None):
        return "compiling"
    ts = query(slot, "test_status")
    if ts is not None and ts.get("status") == "running":
        return "test_run"
    return "ready"


# ---- Lock -----------------------------------------------------------------------------------

def lock_file(slot):
    return lock_dir() / f"{slot}.lock"


def pid_alive(pid):
    if not isinstance(pid, int) or pid <= 0:
        return False
    if os.name != "nt":
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            return False
        except PermissionError:
            return True
        return True
    k32 = ctypes.windll.kernel32
    k32.OpenProcess.restype = ctypes.c_void_p
    h = k32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
    if not h:
        return False
    code = ctypes.c_ulong()
    ok = k32.GetExitCodeProcess(ctypes.c_void_p(h), ctypes.byref(code))
    k32.CloseHandle(ctypes.c_void_p(h))
    return bool(ok) and code.value == 259  # STILL_ACTIVE


def read_lock(slot):
    """None (no lock), or dict with the lock data plus 'live' (bool)."""
    f = lock_file(slot)
    if not f.exists():
        return None
    try:
        data = json.loads(f.read_text(encoding="utf-8"))
    except (ValueError, OSError):
        data = {}
    data["live"] = pid_alive(data.get("pid"))
    return data


def acquire_lock(slot, kind):
    lock_dir().mkdir(parents=True, exist_ok=True)
    try:
        fd = os.open(lock_file(slot), os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        raise SlotError(f"{label(slot)}: a run is already running (lock {lock_file(slot)}). Wait for it.")
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        json.dump({"pid": os.getpid(), "kind": kind, "started": time.strftime("%Y-%m-%d %H:%M:%S")}, f)


def release_lock(slot):
    try:
        lock_file(slot).unlink()
    except FileNotFoundError:
        pass
