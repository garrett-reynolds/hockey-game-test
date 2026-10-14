"""Slot tool entry: `python slot.py <status|test|close|open|play-check> ...`. One module per sub-command in slotlib/."""
import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from slotlib import closeopen, common, playcheck, status, testrun  # noqa: E402

COMMANDS = [status, testrun, closeopen, playcheck]


def main(argv=None):
    ap = argparse.ArgumentParser(prog="slot.py", description="Check slots and run tests safely.")
    sub = ap.add_subparsers(dest="cmd", required=True)
    for m in COMMANDS:
        m.add_parser(sub)
    args = ap.parse_args(argv)
    try:
        return args.func(args)
    except common.SlotError as e:
        print(e)
        return 2


if __name__ == "__main__":
    sys.exit(main())
