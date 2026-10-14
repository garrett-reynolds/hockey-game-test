"""`slot.py status`: one short line per slot Editor."""
from . import common as c
from . import editors

def line_for(slot, rows):
    """rows: editors.open_rows() (None when `unity status` could not be read)."""
    lock = c.read_lock(slot)
    row = None if rows is None else rows.get(editors.slot_key(slot))
    port = f" (port {row['port']})" if row else ""
    if rows is not None and row is None:
        text = "not open (not in `unity status`)"
    else:
        state, why = c.editor_probe(slot)
        if state == "ready" and lock and lock["live"]:
            text = f"busy (slot tool run: {lock.get('kind', '?')}, pid {lock.get('pid')})"
        else:
            text = c.STATE_TEXT[state]
            if state == "no_answer":
                text += f" ({why})"
            if state == "no_answer" and lock and lock["live"]:
                text += f"; slot tool run live (pid {lock.get('pid')})"
        text += port
    if lock and not lock["live"]:
        text += f" [stale lock: pid {lock.get('pid')} gone; the next test clears it once the Editor answers]"
    return f"{c.label(slot)}: {text}"


def add_parser(sub):
    p = sub.add_parser("status", help="one line per slot: free, busy, in Play, or not answering")
    p.set_defaults(func=run)


def run(args):
    rows = editors.open_rows()
    for slot in c.SLOTS:
        print(line_for(slot, rows))
    return 0
