"""Fake CPU source for the hang watch tests: `fake_cpu.py <slot>` prints cumulative CPU seconds.

Values come from $FAKE_DIR/cpu.json: {"<slot>": [v1, v2, ...]}. Each call pops the next; the last repeats.
A slot missing from the file behaves like an unreadable source (exit 1).
"""
import json
import os
import sys


def main():
    slot = sys.argv[1]
    d = os.environ["FAKE_DIR"]
    try:
        with open(os.path.join(d, "cpu.json"), encoding="utf-8") as f:
            values = json.load(f)[slot]
    except (OSError, KeyError, ValueError):
        return 1
    counter = os.path.join(d, f"cpucount-{slot}")
    n = int(open(counter).read()) if os.path.exists(counter) else 0
    with open(counter, "w") as f:
        f.write(str(n + 1))
    print(values[min(n, len(values) - 1)])
    return 0


if __name__ == "__main__":
    sys.exit(main())
