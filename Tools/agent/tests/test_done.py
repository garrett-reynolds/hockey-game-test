"""`slot.py done <slot>` (#188): git state, red/green runs, changed test files. Never talks to the Editor."""
import json
import os
import subprocess
import time

import pytest

OLD = "2020-01-01T10:00:00"  # commit times
RUN1 = "2020-06-01T10:00:00"  # run log times, after OLD
RUN2 = "2020-06-01T11:00:00"
FUTURE = "2099-01-01T00:00:00"
UNITY_TEST = "Assets/Scripts/Tests/GoalieTests.c" + "s"  # a Unity test file path
UNITY_TEST2 = "Assets/Tests/NewTests.c" + "s"
UNITY_CODE = "Assets/Scripts/Goalie.c" + "s"
PY_TEST = "Tools/agent/tests/test_x.py"


def epoch(text):
    return time.mktime(time.strptime(text, "%Y-%m-%dT%H:%M:%S"))


def git(path, *args, date=None):
    env = dict(os.environ)
    if date:
        env["GIT_COMMITTER_DATE"] = date
        env["GIT_AUTHOR_DATE"] = date
    return subprocess.run(["git", "-C", str(path), "-c", "user.name=t", "-c", "user.email=t@t", *args],
                          capture_output=True, text=True, check=True, env=env).stdout.strip()


def commit(path, name, text="x", date=OLD, msg=None):
    f = path / name
    f.parent.mkdir(parents=True, exist_ok=True)
    f.write_text(text, encoding="utf-8")
    git(path, "add", name)
    git(path, "commit", "-q", "-m", msg or f"add {name}", date=date)


@pytest.fixture
def repo(h, tmp_path):
    """w2 slot folder: main with one commit, then branch feat/x with one non-test commit."""
    path = tmp_path / "wt" / "w2"
    path.mkdir(parents=True)
    git(path, "init", "-q", "-b", "main")
    commit(path, "base.txt")
    git(path, "checkout", "-q", "-b", "feat/x")
    commit(path, "Tools/agent/code.py", msg="feature work")
    return path


def log_run(h, when, head, passed=5, failed=0, rc=0, status="green", branch="feat/x", result="r.txt"):
    h.results.mkdir(exist_ok=True)
    rec = {"time": when, "slot": "w2", "branch": branch, "head": head, "mode": "fast", "filter": None,
           "total": None if passed is None else passed + failed, "passed": passed, "failed": failed, "rc": rc,
           "status": status, "result_file": result}
    with open(h.results / "w2-runs.jsonl", "a", encoding="utf-8") as f:
        f.write(json.dumps(rec) + "\n")


def head(repo):
    return git(repo, "rev-parse", "HEAD")


def line(out, word):
    return [x for x in out.splitlines() if x.startswith(word)]


def test_unknown_slot_exits_2(h):
    rc, out = h.run("done", "w9")
    assert rc == 2 and "Unknown slot" in out


def test_all_ok_prints_one_line_per_check_and_never_calls_the_editor(h, repo):
    log_run(h, RUN1, head(repo))
    rc, out = h.run("done", "w2")
    assert rc == 0, out
    assert "OK git: branch feat/x, 0 uncommitted, 1 ahead of main" in out
    assert "feature work" in out  # the --oneline commit is listed
    assert len(line(out, "OK runs")) == 1 and len(line(out, "OK test files")) == 1
    assert out.strip().splitlines()[-1] == "done: all checks OK"
    assert h.calls() == []


def test_uncommitted_files_are_listed_but_do_not_fail_git(h, repo):
    log_run(h, RUN1, head(repo))
    (repo / "dirty.asset").write_text("d", encoding="utf-8")
    (repo / "base.txt").write_text("changed", encoding="utf-8")
    rc, out = h.run("done", "w2")
    assert rc == 0, out
    assert "OK git: branch feat/x, 2 uncommitted, 1 ahead of main" in out
    assert "dirty.asset" in out and "base.txt" in out


def test_git_failing_is_a_fail(h, tmp_path):
    (tmp_path / "wt" / "w2").mkdir(parents=True)  # a folder that is not a git repo
    rc, out = h.run("done", "w2")
    assert rc == 1
    assert line(out, "FAIL git")


def test_no_green_run_at_head_fails_runs(h, repo):
    log_run(h, RUN1, "0" * 40)  # green, but for an older head
    rc, out = h.run("done", "w2")
    assert rc == 1, out
    assert line(out, "FAIL runs")
    assert "done: 1 check(s) FAIL" in out


def test_runs_prints_first_red_and_last_green_with_result_files(h, repo):
    hd = head(repo)
    log_run(h, "2020-06-01T09:00:00", "a" * 40, passed=4, failed=1, rc=1, status="red", result="red1.txt")
    log_run(h, "2020-06-01T09:30:00", "b" * 40, passed=4, failed=2, rc=1, status="red", result="red2.txt")
    log_run(h, "2020-06-01T10:00:00", "c" * 40, passed=5, result="green1.txt")
    log_run(h, "2020-06-01T11:00:00", hd, passed=7, result="green2.txt")
    rc, out = h.run("done", "w2")
    assert rc == 0, out
    assert "red1.txt" in out and "red2.txt" not in out
    assert "green2.txt" in out and "green1.txt" not in out
    assert "aaaaaaa" in out and hd[:7] in out
    assert "4/1" in out and "7/0" in out


def test_hang_and_no_result_lines_are_neither_red_nor_green(h, repo):
    log_run(h, RUN1, head(repo), passed=None, failed=None, rc=3, status="hang")
    log_run(h, RUN2, head(repo), passed=None, failed=None, rc=1, status="no_result")
    rc, out = h.run("done", "w2")
    assert rc == 1, out
    assert line(out, "FAIL runs")
    assert "first red" not in out


def test_green_needs_passed_over_zero_and_exit_0(h, repo):
    log_run(h, RUN1, head(repo), passed=0, failed=0, rc=0)
    log_run(h, RUN2, head(repo), passed=5, failed=0, rc=1)
    rc, out = h.run("done", "w2")
    assert rc == 1 and line(out, "FAIL runs")


def test_lines_for_other_branches_are_ignored(h, repo):
    log_run(h, RUN1, head(repo), branch="feat/other")
    rc, out = h.run("done", "w2")
    assert rc == 1 and line(out, "FAIL runs")


def test_committed_test_file_newer_than_last_run_fails(h, repo):
    commit(repo, UNITY_TEST, date="2020-06-01T12:00:00")
    log_run(h, RUN2, head(repo))  # run is before the commit of the test file
    rc, out = h.run("done", "w2")
    assert rc == 1, out
    assert line(out, "FAIL test files")
    assert "GoalieTests" in out


def test_committed_test_file_older_than_last_run_is_ok(h, repo):
    commit(repo, UNITY_TEST, date=OLD)
    commit(repo, PY_TEST, date=OLD)
    log_run(h, RUN1, head(repo))
    rc, out = h.run("done", "w2")
    assert rc == 0, out


def test_any_status_counts_as_the_last_run_for_test_files(h, repo):
    commit(repo, PY_TEST, date="2020-06-01T10:30:00")
    log_run(h, RUN1, "0" * 40)
    log_run(h, RUN2, head(repo), passed=None, failed=None, rc=3, status="hang")  # newest run, a hang
    rc, out = h.run("done", "w2")
    assert not line(out, "FAIL test files"), out
    assert line(out, "FAIL runs")


def test_uncommitted_test_file_uses_its_mtime(h, repo):
    log_run(h, RUN1, head(repo))
    f = repo / UNITY_TEST2
    f.parent.mkdir(parents=True)
    f.write_text("t", encoding="utf-8")
    os.utime(f, (epoch(OLD), epoch(OLD)))
    rc, out = h.run("done", "w2")
    assert rc == 0, out  # untracked but older than the run
    os.utime(f, (epoch(FUTURE), epoch(FUTURE)))
    rc, out = h.run("done", "w2")
    assert rc == 1 and line(out, "FAIL test files") and "NewTests" in out


def test_modified_tracked_test_file_uses_its_mtime(h, repo):
    commit(repo, PY_TEST, date=OLD)
    log_run(h, RUN1, head(repo))
    f = repo / PY_TEST
    f.write_text("edited", encoding="utf-8")
    os.utime(f, (epoch(FUTURE), epoch(FUTURE)))
    rc, out = h.run("done", "w2")
    assert rc == 1 and line(out, "FAIL test files")


def test_non_test_files_do_not_count(h, repo):
    commit(repo, UNITY_CODE, date="2020-06-01T12:00:00")
    commit(repo, "Assets/Tests/readme.txt", date="2020-06-01T12:00:00")
    log_run(h, RUN2, head(repo))
    rc, out = h.run("done", "w2")
    assert not line(out, "FAIL test files"), out


def test_test_file_with_no_run_at_all_fails(h, repo):
    commit(repo, PY_TEST, date=OLD)
    rc, out = h.run("done", "w2")
    assert rc == 1
    assert line(out, "FAIL test files") and line(out, "FAIL runs")
    assert "done: 2 check(s) FAIL" in out
