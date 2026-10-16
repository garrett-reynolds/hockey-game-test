import json
import io
import os
import subprocess
import sys
from pathlib import Path
from contextlib import redirect_stderr, redirect_stdout
from types import SimpleNamespace

import pytest

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
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
            SLOT_FREE_MEM_MB="16000",  # #219: plenty, so the memory check never depends on this PC
            SLOT_POLL_S="0.05",
            SLOT_WAIT_S="2",
            SLOT_STATUS_TIMEOUT_S="5",
            PYTHONIOENCODING="utf-8",
        )

    def set(self, slot, command, *replies):
        self.scenario.setdefault(slot, {})[command] = list(replies)
        (self.dir / "scenario.json").write_text(json.dumps(self.scenario), encoding="utf-8")

    def ready(self, slot):
        self.set(slot, "editor_status", reply(READY))
        self.set(slot, "test_status", reply({"status": "no_tests"}))
        # #186: `test` refreshes and recompiles first when the code changed; these are the defaults it needs.
        for command, answer in (("menu", {"ok": True}), ("recompile", {"ok": True}),
                                ("recompile_status", {"status": "completed", "errors": []})):
            if command not in self.scenario.get(slot, {}):
                self.set(slot, command, reply(answer))

    def run(self, *args, timeout=60):
        p = subprocess.run([sys.executable, str(SLOT_PY), *args], env=self.env, capture_output=True,
                           text=True, timeout=timeout)
        return p.returncode, (p.stdout + p.stderr)

    def run_with_clock(self, monkeypatch, *args):
        """Run the public CLI with a logical wait clock; fake CLI children stay real.

        Only slotlib.common's clock changes. Process startup and host load cannot
        consume the polling deadline. Recorded sleeps still expose unwanted waits.
        """
        import slot
        from slotlib import common

        self.waits = []
        elapsed = 0.0

        def sleep(seconds):
            nonlocal elapsed
            self.waits.append(seconds)
            elapsed += seconds

        output = io.StringIO()
        with monkeypatch.context() as patch:
            for key, value in self.env.items():
                patch.setenv(key, value)
            patch.setattr(common, "time", SimpleNamespace(time=lambda: elapsed, sleep=sleep))
            with redirect_stdout(output), redirect_stderr(output):
                rc = slot.main(list(args))
        return rc, output.getvalue()

    def calls(self):
        f = self.dir / "calls.log"
        if not f.exists():
            return []
        return [json.loads(x) for x in f.read_text(encoding="utf-8").splitlines()]

    def run_tests_calls(self):
        return [c for c in self.calls() if "run_tests" in c]

    def write_lock(self, slot, pid, cli_pid=None):
        self.locks.mkdir(exist_ok=True)
        data = {"pid": pid, "kind": "fast"}
        if cli_pid is not None:
            data["cli_pid"] = cli_pid
        (self.locks / f"{slot}.lock").write_text(json.dumps(data), encoding="utf-8")


@pytest.fixture
def h(tmp_path):
    return Harness(tmp_path)


def dead_pid():
    p = subprocess.Popen([sys.executable, "-c", "pass"])
    p.wait()
    return p.pid
