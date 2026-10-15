"""Run log (#185): every `slot.py test` run appends one JSON line to <results>/<slot>-runs.jsonl."""
import json
import re
import subprocess

import pytest

from conftest import FAKE, SUMMARY_FAIL, SUMMARY_OK, reply
from test_hang_watch import FAKE_CPU

FIELDS = ["time", "slot", "branch", "head", "mode", "filter", "total", "passed", "failed", "rc", "status",
          "result_file"]


def git(path, *args):
    return subprocess.run(["git", "-C", str(path), *args], capture_output=True, text=True, check=True).stdout.strip()


@pytest.fixture
def repo(h, tmp_path):
    """The w2 slot folder as a small git repo on branch feat/x with one commit."""
    path = tmp_path / "wt" / "w2"
    path.mkdir(parents=True)
    git(path, "init", "-q")
    git(path, "checkout", "-q", "-b", "feat/x")
    (path / "a.txt").write_text("a", encoding="utf-8")
    git(path, "add", "a.txt")
    git(path, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "one")
    return path


def log_lines(h, slot="w2"):
    f = h.results / f"{slot}-runs.jsonl"
    if not f.exists():
        return []
    return [json.loads(x) for x in f.read_text(encoding="utf-8").splitlines()]


def test_green_run_adds_one_line_with_counts_branch_and_head(h, repo):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2", "--filter", "Goalie")
    assert rc == 0, out
    (rec,) = log_lines(h)
    assert list(rec) == FIELDS
    assert rec["slot"] == "w2" and rec["mode"] == "fast" and rec["filter"] == "Goalie"
    assert (rec["total"], rec["passed"], rec["failed"], rec["rc"]) == (5, 5, 0, 0)
    assert rec["status"] == "green"
    assert rec["branch"] == "feat/x"
    assert rec["head"] == git(repo, "rev-parse", "HEAD")
    assert re.match(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d", rec["time"])
    assert rec["result_file"] == out.splitlines()[-1].strip() or rec["result_file"] in out


def test_red_run_adds_a_line_and_runs_append(h, repo):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_FAIL))
    rc, out = h.run("test", "w2", "--full")
    assert rc == 1, out
    h.run("test", "w2")
    recs = log_lines(h)
    assert len(recs) == 2
    assert recs[0]["status"] == "red" and recs[0]["mode"] == "full"
    assert (recs[0]["total"], recs[0]["passed"], recs[0]["failed"]) == (5, 4, 1)
    assert recs[0]["filter"] is None


def test_no_result_run_adds_a_line_with_null_counts(h, repo):
    h.ready("w2")
    h.set("w2", "run_tests", {"out": "Network error", "rc": 1})
    rc, out = h.run("test", "w2")
    assert rc == 1, out
    (rec,) = log_lines(h)
    assert rec["status"] == "no_result"
    assert rec["total"] is None and rec["passed"] is None and rec["failed"] is None
    assert rec["rc"] == 1
    assert rec["branch"] == "feat/x"


def test_hang_run_adds_a_line(h, repo):
    h.env["SLOT_CPU_SOURCE"] = str(FAKE_CPU)
    h.env["SLOT_CPU_INTERVAL_S"] = "0.3"
    h.ready("w2")
    h.set("w2", "run_tests", {"out": json.dumps(SUMMARY_OK), "rc": 0, "sleep": 6})
    (h.dir / "cpu.json").write_text(json.dumps({"w2": [100.0]}), encoding="utf-8")
    rc, out = h.run("test", "w2")
    assert rc == 3, out
    (rec,) = log_lines(h)
    assert rec["status"] == "hang"
    assert rec["total"] is None and rec["failed"] is None
    assert rec["head"] == git(repo, "rev-parse", "HEAD")


def test_git_read_failure_writes_null_and_does_not_fail_the_run(h):
    # no git repo at the slot folder (and none above the temp dir)
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    (rec,) = log_lines(h)
    assert rec["status"] == "green"
    assert rec["branch"] is None and rec["head"] is None


def test_two_printed_lines_unchanged(h, repo):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert len([x for x in out.splitlines() if x.strip() and "code changed" not in x]) == 2  # #186 refresh line


def test_log_write_failure_does_not_fail_the_run(h, repo):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    h.results.mkdir()
    (h.results / "w2-runs.jsonl").mkdir()  # a folder where the log file should be: append fails
    rc, out = h.run("test", "w2")
    assert rc == 0, out
