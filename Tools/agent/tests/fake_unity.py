"""Fake Unity CLI for the slot tool tests.

Usage mirrors the real one: fake_unity.py command --project-path <slot> [flags] <command> [args]
Replies come from $FAKE_DIR/scenario.json:
  {"<slot folder name>": {"<unity command>": [reply, reply, ...]}}
Each call pops the next reply; the last one repeats. A reply is
  {"out": "<stdout text>", "rc": 0, "sleep": 0.0}
Every call is appended to $FAKE_DIR/calls.log as one JSON list (the argv).
A slot or command missing from the scenario behaves like an Editor that does not answer (rc 1).
"""
import json
import os
import sys
import time


def main():
    argv = sys.argv[1:]
    d = os.environ["FAKE_DIR"]
    with open(os.path.join(d, "calls.log"), "a", encoding="utf-8") as f:
        f.write(json.dumps(argv) + "\n")
    if argv and argv[0] == "status":
        print("Port\tState\tProject\tVersion\tPID")
        return 0
    slot = None
    if "--project-path" in argv:
        slot = argv[argv.index("--project-path") + 1].replace("\\", "/").rstrip("/").split("/")[-1]
    cmd = None
    skip = {"--project-path", "--timeout"}
    i = 1
    while i < len(argv):
        if argv[i] in skip:
            i += 2
            continue
        if argv[i].startswith("--"):
            i += 1
            continue
        cmd = argv[i]
        break
    with open(os.path.join(d, "scenario.json"), encoding="utf-8") as f:
        scenario = json.load(f)
    replies = scenario.get(slot, {}).get(cmd)
    if not replies:
        print("Error: could not connect to the Editor", file=sys.stderr)
        return 1
    counter = os.path.join(d, f"count-{slot}-{cmd}")
    n = int(open(counter).read()) if os.path.exists(counter) else 0
    with open(counter, "w") as f:
        f.write(str(n + 1))
    r = replies[min(n, len(replies) - 1)]
    time.sleep(r.get("sleep", 0))
    sys.stdout.write(r.get("out", ""))
    return r.get("rc", 0)


if __name__ == "__main__":
    sys.exit(main())
