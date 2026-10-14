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


def next_reply(d, key, replies):
    counter = os.path.join(d, f"count-{key}")
    n = int(open(counter).read()) if os.path.exists(counter) else 0
    with open(counter, "w") as f:
        f.write(str(n + 1))
    r = replies[min(n, len(replies) - 1)]
    time.sleep(r.get("sleep", 0))
    return r


def scenario_of(d):
    path = os.path.join(d, "scenario.json")
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def fake_status(d):
    """`unity status`: header plus rows. Default: slots 111, w2, w3 all open. Scenario key "_status" ->
    "status": replies whose "out" is the row text; no rows = rc 6 like the real CLI."""
    replies = scenario_of(d).get("_status", {}).get("status")
    if replies is None:
        root = os.environ.get("SLOT_ROOT", "").replace("/", "\\")
        rows = "".join(f"7800\tready\t{root}\\{f}\t6000.6.5f1\t4242\n" for f in ("111", "w2", "w3"))
    else:
        r = next_reply(d, "_status", replies)
        if r.get("raw"):  # a reply with no table header: the CLI failed in some other way
            sys.stdout.write(r.get("out", ""))
            return r.get("rc", 1)
        rows = r.get("out", "")
    print("Port\tState\tProject\tVersion\tPID")
    sys.stdout.write(rows)
    return 0 if rows else 6


def fake_action(d, argv):
    """`unity close <project>` / `unity open <project>`: logged; reply from scenario[slot][close|open], default rc 0."""
    paths = [a for a in argv[1:] if "/" in a or "\\" in a]
    slot = paths[0].replace("\\", "/").rstrip("/").split("/")[-1] if paths else None
    replies = scenario_of(d).get(slot, {}).get(argv[0])
    if not replies:
        return 0
    r = next_reply(d, f"{slot}-{argv[0]}", replies)
    sys.stdout.write(r.get("out", ""))
    return r.get("rc", 0)


def main():
    argv = sys.argv[1:]
    d = os.environ["FAKE_DIR"]
    with open(os.path.join(d, "calls.log"), "a", encoding="utf-8") as f:
        f.write(json.dumps(argv) + "\n")
    if argv and argv[0] in ("close", "open"):
        return fake_action(d, argv)
    if argv and argv[0] == "status":
        return fake_status(d)
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
