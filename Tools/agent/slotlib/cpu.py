"""CPU seconds used so far by a slot's Editor plus its worker processes (the hang watch reads this each minute).

SLOT_CPU_SOURCE (tests only) names a .py script run as `python <script> <slot>`; it prints the number.
Without it the number comes from Windows process data. None means "could not read": the watch skips that sample.
"""
import json
import os
import subprocess
import sys

from . import editors

_PS_HEAD = ("Get-CimInstance Win32_Process | ForEach-Object { [pscustomobject]@{ id=[int]$_.ProcessId; "
            "parent=[int]$_.ParentProcessId; ")
_PS_TAIL = " } } | ConvertTo-Json -Compress"

PS = _PS_HEAD + "cpu=([double]$_.UserModeTime + [double]$_.KernelModeTime) / 1e7" + _PS_TAIL
PS_NAMES = _PS_HEAD + "name=[string]$_.Name; cmd=[string]$_.CommandLine" + _PS_TAIL


def read_processes(query=PS):
    """Run one PowerShell process query and return its rows as a list of dicts. Raises OSError, ValueError or
    subprocess.SubprocessError when the query fails (callers decide what that means)."""
    p = subprocess.run(["powershell", "-NoProfile", "-Command", query], capture_output=True, text=True, timeout=60)
    if p.returncode != 0:
        raise OSError(p.stderr.strip() or "process query failed")
    data = json.loads(p.stdout or "[]")
    return data if isinstance(data, list) else [data]


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
        return tree_cpu(read_processes(), pid)
    except (subprocess.SubprocessError, OSError, ValueError):
        return None
