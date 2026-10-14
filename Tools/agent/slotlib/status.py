"""`slot.py status`: one short line per slot Editor."""
from . import common as c

DETAIL = {
    "no_answer": "not answering",
    "compiling": "busy (compiling or reloading)",
    "play": "in Play",
    "test_run": "busy (test run live in the Editor)",
    "ready": "free",
}


def line_for(slot):
    lock = c.read_lock(slot)
    state = c.editor_state(slot)
    if state == "ready" and lock and lock["live"]:
        text = f"busy (slot tool run: {lock.get('kind', '?')}, pid {lock.get('pid')})"
    else:
        text = DETAIL[state]
        if state == "no_answer" and lock and lock["live"]:
            text += f"; slot tool run live (pid {lock.get('pid')})"
    if lock and not lock["live"]:
        text += f" [stale lock: pid {lock.get('pid')} gone; the next test clears it once the Editor answers]"
    return f"{c.label(slot)}: {text}"


def add_parser(sub):
    p = sub.add_parser("status", help="one line per slot: free, busy, in Play, or not answering")
    p.set_defaults(func=run)


def run(args):
    for slot in c.SLOTS:
        print(line_for(slot))
    return 0
