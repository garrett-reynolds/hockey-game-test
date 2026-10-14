import json
import os
import subprocess
import sys
from pathlib import Path

import pytest

HERE = Path(__file__).resolve().parent
SLOT_PY = HERE.parent / "slot.py"
FAKE = HERE / "fake_unity.py"

READY = {"status": "ready", "compiling": False, "domainReloadInProgress": False, "playMode": "stopped"}
COMPILING = {"status": "compiling", "compiling": True, "domainReloadInProgress": False, "playMode": "stopped"}
PLAY = {"status": "ready", "compiling": False, "domainReloadInProgress": False, "playMode": "playing"}
SUMMARY_OK = {"Summary": {"Total": 5, "Passed": 5, "Failed": 0, "Skipped": 0, "Inconclusive": 0}, "Results": []}
SUMMARY_FAIL = {"Summary": {"Total": 5, "Passed": 4, "Failed": 1, "Skipped": 0, "Inconclusive": 0}, "Results": []}


def reply(obj, rc=0, sleep=0):
    return {"out": json.dumps(obj), "rc": rc, "sleep": sleep}


class Harness:
    def __init__(self, tmp: Path):
        self.dir = tmp / "fake"
        self.dir.mkdir()
        self.locks = tmp / "locks"
        self.results = tmp / "results"
        self.scenario = {}
        self.env = dict(
            os.environ,
            FAKE_DIR=str(self.dir),
            SLOT_UNITY_EXE=str(FAKE),
            SLOT_ROOT=str(tmp / "wt"),
            SLOT_LOCK_DIR=str(self.locks),
            SLOT_RESULTS_DIR=str(self.results),
            SLOT_POLL_S="0.05",
            SLOT_WAIT_S="2",
            SLOT_STATUS_TIMEOUT_S="5",
        )

    def set(self, slot, command, *replies):
        self.scenario.setdefault(slot, {})[command] = list(replies)
        (self.dir / "scenario.json").write_text(json.dumps(self.scenario), encoding="utf-8")

    def ready(self, slot):
        self.set(slot, "editor_status", reply(READY))
        self.set(slot, "test_status", reply({"status": "no_tests"}))

    def run(self, *args, timeout=60):
        p = subprocess.run([sys.executable, str(SLOT_PY), *args], env=self.env, capture_output=True,
                           text=True, timeout=timeout)
        return p.returncode, (p.stdout + p.stderr)

    def calls(self):
        f = self.dir / "calls.log"
        if not f.exists():
            return []
        return [json.loads(x) for x in f.read_text(encoding="utf-8").splitlines()]

    def run_tests_calls(self):
        return [c for c in self.calls() if "run_tests" in c]

    def write_lock(self, slot, pid):
        self.locks.mkdir(exist_ok=True)
        (self.locks / f"{slot}.lock").write_text(json.dumps({"pid": pid, "kind": "fast"}), encoding="utf-8")


@pytest.fixture
def h(tmp_path):
    return Harness(tmp_path)


def dead_pid():
    p = subprocess.Popen([sys.executable, "-c", "pass"])
    p.wait()
    return p.pid
