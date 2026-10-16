"""Free physical memory check before `test` and `play-check` (#219).

The 2026-10-16 retro: an Editor stopped answering about 7 times in one day, and one full run was killed
for low memory. Facts for the limit, measured on this PC (31.6 GB RAM) on 2026-10-16 with three Editors
open: an idle slot Editor holds about 1.7 GB private memory, the Editor that was running tests held 3.6 GB.
So a run grows its Editor by about 2 GB. Add about 1 GB for Windows, the shader compilers and the test
worker. MIN_FREE_GB = 3.0 therefore means: a run can grow its Editor by 2 GB and still leave 1 GB free.
Under that, Windows starts paging the Editor out and it stops answering. `--skip-memory-check` skips this.

SLOT_FREE_MEM_MB (tests only) replaces the real read.
"""
import ctypes
import os

MIN_FREE_GB = 3.0


class _MemoryStatus(ctypes.Structure):
    _fields_ = [("dwLength", ctypes.c_ulong), ("dwMemoryLoad", ctypes.c_ulong),
                ("ullTotalPhys", ctypes.c_ulonglong), ("ullAvailPhys", ctypes.c_ulonglong),
                ("ullTotalPageFile", ctypes.c_ulonglong), ("ullAvailPageFile", ctypes.c_ulonglong),
                ("ullTotalVirtual", ctypes.c_ulonglong), ("ullAvailVirtual", ctypes.c_ulonglong),
                ("ullAvailExtendedVirtual", ctypes.c_ulonglong)]


def free_mb():
    """Free physical memory in MB (what Windows can hand out now), or None when it cannot be read."""
    fake = os.environ.get("SLOT_FREE_MEM_MB")
    if fake:
        return float(fake)
    try:
        if os.name == "nt":
            st = _MemoryStatus()
            st.dwLength = ctypes.sizeof(_MemoryStatus)
            if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(st)):
                return None
            return st.ullAvailPhys / (1024 * 1024)
        with open("/proc/meminfo", encoding="utf-8") as f:
            for line in f:
                if line.startswith("MemAvailable:"):
                    return int(line.split()[1]) / 1024
    except (OSError, ValueError, AttributeError):
        pass
    return None


def check(free_mb=None):
    """None when there is enough free memory (or it cannot be read: never block on a failed read),
    else the refusal text naming the free and needed amounts."""
    if free_mb is None:
        free_mb = globals()["free_mb"]()
    if free_mb is None:
        return None
    if free_mb >= MIN_FREE_GB * 1024:
        return None
    return (f"Not enough free memory: {free_mb / 1024:.1f} GB free, {MIN_FREE_GB:.1f} GB needed. "
            f"A run grows the Editor by about 2 GB and a short Editor stops answering. Close an idle Editor "
            f"or other programs and try again, or add --skip-memory-check to run anyway.")
