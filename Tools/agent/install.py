"""Install the slot tool: copy it from the repo to the live folder every slot uses.

    python Tools/agent/install.py            copy slot.py, slotlib/*.py and the commit guard
    python Tools/agent/install.py --check    report drift between the repo and the live copy (exit 1 if any)

Copies: Tools/agent/slot.py, Tools/agent/slotlib/*.py -> <dest>/, <dest>/slotlib/
        .githooks/pre-commit                          -> <dest>/githooks/pre-commit
The default <dest> is the notes folder (slotlib/common.py: NOTES_DEFAULT). --dest is for tests.
Line endings are ignored when comparing (git may check files out with CRLF).
"""
import argparse
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(HERE))
from slotlib import common  # noqa: E402


def pairs(dest):
    """[(source file, live file, name shown)] for everything that is installed."""
    out = [(HERE / "slot.py", dest / "slot.py", "slot.py")]
    for f in sorted((HERE / "slotlib").glob("*.py")):
        out.append((f, dest / "slotlib" / f.name, f"slotlib/{f.name}"))
    out.append((REPO / ".githooks" / "pre-commit", dest / "githooks" / "pre-commit", "githooks/pre-commit"))
    return out


def same(a, b):
    return b.exists() and a.read_bytes().replace(b"\r\n", b"\n") == b.read_bytes().replace(b"\r\n", b"\n")


def drift(dest):
    names = []
    for src, live, name in pairs(dest):
        if not same(src, live):
            names.append(name + (" (missing)" if not live.exists() else " (differs)"))
    wanted = {live.name for _, live, name in pairs(dest) if name.startswith("slotlib/")}
    live_lib = dest / "slotlib"
    if live_lib.is_dir():
        names += [f"slotlib/{f.name} (not in the repo)" for f in sorted(live_lib.glob("*.py")) if f.name not in wanted]
    return names


def main(argv=None):
    ap = argparse.ArgumentParser(description="Install the slot tool to the live folder.")
    ap.add_argument("--check", action="store_true", help="report drift, copy nothing; exit 1 if the copies differ")
    ap.add_argument("--dest", default=common.NOTES_DEFAULT, help="live folder (default: the notes folder)")
    args = ap.parse_args(argv)
    dest = Path(args.dest)
    if args.check:
        d = drift(dest)
        for n in d:
            print("drift:", n)
        print(f"{len(d)} file(s) differ between the repo and {dest}" if d else f"in step: the repo and {dest} match")
        return 1 if d else 0
    for src, live, name in pairs(dest):
        live.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, live)
    print(f"installed {len(pairs(dest))} files to {dest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
