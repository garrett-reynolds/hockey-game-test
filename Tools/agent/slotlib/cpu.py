"""CPU seconds used so far by a slot's Editor plus its worker processes (the hang watch reads this each minute).

SLOT_CPU_SOURCE (tests only) names a .py script run as `python <script> <slot>`; it prints the number.
Without it the number comes from Windows process data. None means "could not read": the watch skips that sample.
"""
import json
import os
import subprocess
import sys

from . import editors

PS = ("Get-CimInstance Win32_Process | ForEach-Object { [pscustomobject]@{ id=[int]$_.ProcessId; "
      "parent=[int]$_.ParentProcessId; cpu=([double]$_.UserModeTime + [double]$_.KernelModeTime) / 1e7 } } "
      "| ConvertTo-Json -Compress")


def tree_cpu(procs, root):
    """Sum of cpu for root and every descendant. procs: [{'id','parent','cpu'}]."""
    kids = {}
    for p in procs:
        kids.setdefault(p["parent"], []).append(p)
    by_id = {p["id"]: p for p in procs}
    if root not in by_id:
        return None
    total, stack, seen = 0.0, [root], set()
    while stack:
        pid = stack.pop()
        if pid in seen:
            continue
        seen.add(pid)
        total += by_id[pid]["cpu"]
        stack += [k["id"] for k in kids.get(pid, [])]
    return total


def sample(slot):
    src = os.environ.get("SLOT_CPU_SOURCE")
    try:
        if src:
            p = subprocess.run([sys.executable, src, slot], capture_output=True, text=True, timeout=30)
            return float(p.stdout.strip()) if p.returncode == 0 else None
        known, pid = editors.editor_pid(slot)
        if not known or pid is None:
            return None
        p = subprocess.run(["powershell", "-NoProfile", "-Command", PS], capture_output=True, text=True, timeout=60)
        data = json.loads(p.stdout or "[]")
        return tree_cpu(data if isinstance(data, list) else [data], pid)
    except (subprocess.TimeoutExpired, OSError, ValueError):
        return None
