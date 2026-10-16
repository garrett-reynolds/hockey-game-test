"""#219: memory check before test/play-check, fast exit when the Editor process is gone, the mute guard,
and the small play-check picture. Editor process and memory reads are faked; no live Editor."""
import base64
import io
import json
import re
import time
from pathlib import Path

import pytest

from conftest import READY, SUMMARY_OK, dead_pid, reply
from slotlib import memory

AGENT = Path(__file__).resolve().parent.parent
SCENE = "Assets/Scenes/Rink.unity"


# ---- memory check --------------------------------------------------------------------------------

def low(h, mb=1000):
    h.env["SLOT_FREE_MEM_MB"] = str(mb)


def test_test_stops_at_once_when_free_memory_is_under_the_limit(h):
    h.ready("w2")
    low(h, 1000)
    t0 = time.time()
    rc, out = h.run("test", "w2")
    assert rc == 2, out
    assert "1.0 GB free" in out and f"{memory.MIN_FREE_GB:.1f} GB needed" in out
    assert "--skip-memory-check" in out
    assert h.calls() == []  # nothing was asked of the Editor
    assert not (h.locks / "w2.lock").exists()
    assert time.time() - t0 < 10


def test_skip_flag_lets_test_run_on_low_memory(h):
    h.ready("w2")
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    low(h, 1000)
    rc, out = h.run("test", "w2", "--skip-memory-check")
    assert rc == 0, out
    assert len(h.run_tests_calls()) == 1


def test_play_check_stops_at_once_when_free_memory_is_under_the_limit(h):
    h.ready("w2")
    low(h, 1000)
    rc, out = h.run("play-check", "w2", "--scene", SCENE)
    assert rc == 2, out
    assert "GB free" in out and "GB needed" in out
    assert h.calls() == []


def test_play_check_has_the_skip_flag(h):
    h.ready("w2")
    low(h, 1000)
    rc, out = h.run("play-check", "w2", "--scene", SCENE, "--skip-memory-check", "--seconds", "0.1")
    assert "GB needed" not in out  # got past the memory check (it fails later: no Play in the fake)


def test_exactly_the_limit_passes_and_just_under_fails():
    need_mb = int(memory.MIN_FREE_GB * 1024)
    assert memory.check(free_mb=need_mb) is None
    assert "GB free" in memory.check(free_mb=need_mb - 1)


def test_the_limit_has_its_reason_written_next_to_it():
    text = (AGENT / "slotlib" / "memory.py").read_text(encoding="utf-8")
    assert "MIN_FREE_GB" in text and "Editor" in text and "GB" in text.split("MIN_FREE_GB", 1)[0]


def test_real_free_memory_read_gives_a_positive_number():
    assert memory.free_mb() > 0


# ---- dead Editor ---------------------------------------------------------------------------------

def status_row(h, pid, slot_folder="w2"):
    root = str(h.dir.parent / "wt").replace("/", "\\")
    row = f"7800\tready\t{root}\\{slot_folder}\t6000.6.5f1\t{pid}\n"
    h.scenario["_status"] = {"status": [reply_raw(row)]}
    (h.dir / "scenario.json").write_text(json.dumps(h.scenario), encoding="utf-8")


def reply_raw(rows):
    return {"out": rows, "rc": 0}


def test_exited_editor_process_is_reported_in_seconds_not_the_full_wait(h):
    h.env["SLOT_WAIT_S"] = "60"
    status_row(h, dead_pid())  # the Editor is listed, but its process is gone; it never answers
    t0 = time.time()
    rc, out = h.run("test", "w2")
    assert rc == 2, out
    assert "process" in out and "exited" in out
    assert time.time() - t0 < 10, out
    assert h.run_tests_calls() == []
    assert not (h.locks / "w2.lock").exists()


def test_editor_that_leaves_the_status_list_is_reported_in_seconds(h):
    h.env["SLOT_WAIT_S"] = "60"
    live = reply_raw("")  # placeholder, replaced below
    root = str(h.dir.parent / "wt").replace("/", "\\")
    first = reply_raw(f"7800\tready\t{root}\\w2\t6000.6.5f1\t{__import__('os').getpid()}\n")
    h.scenario["_status"] = {"status": [first, live]}  # listed once, then gone (rc 6, header only)
    (h.dir / "scenario.json").write_text(json.dumps(h.scenario), encoding="utf-8")
    t0 = time.time()
    rc, out = h.run("test", "w2")
    assert rc == 2, out
    assert "no longer open" in out or "exited" in out
    assert time.time() - t0 < 10, out


def test_an_editor_that_answers_slowly_is_still_waited_for(h):
    h.env["SLOT_WAIT_S"] = "20"
    h.ready("w2")
    h.set("w2", "editor_status", reply({"status": "compiling", "compiling": True, "domainReloadInProgress": False,
                                         "playMode": "stopped"}), reply(READY))
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert rc == 0, out


# ---- mute guard ----------------------------------------------------------------------------------

BAD = re.compile(r"audioMasterMute\s*=\s*false", re.IGNORECASE)


def violations():
    found = []
    for p in AGENT.rglob("*"):
        if not p.is_file() or "__pycache__" in p.parts or p.suffix in (".pyc", ".png"):
            continue
        try:
            text = p.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        if BAD.search(text):
            found.append(str(p.relative_to(AGENT)))
    return found


def test_no_file_under_tools_agent_sets_the_editor_mute_to_false():
    assert violations() == []


M = "audioMasterMute"  # the bad lines are built from pieces so this file does not trip its own guard


@pytest.mark.parametrize("line", ["EditorUtility." + M + "=" + "false;", M + " = " + "false",
                                  M + "   =   " + "FALSE", M.upper() + "=" + "False"])
def test_the_mute_pattern_catches_every_spacing_and_case(line):
    assert BAD.search(line)


def test_the_mute_pattern_allows_restoring_the_old_value():
    assert not BAD.search("EditorUtility.audioMasterMute = old;")
    assert not BAD.search("audioMasterMute = true;")


# ---- small picture -------------------------------------------------------------------------------

def big_png(width=1920, height=1080):
    PIL = pytest.importorskip("PIL.Image")
    buf = io.BytesIO()
    PIL.new("RGB", (width, height), (10, 120, 200)).save(buf, "PNG")
    return buf.getvalue()


def play_setup(h, png_bytes):
    from test_playcheck import GOOD, setup
    setup(h, GOOD)
    h.set("w2", "capture_game_view", reply({"base64": base64.b64encode(png_bytes).decode()}))


def test_play_check_saves_a_640_wide_copy_and_prints_it_first(h):
    play_setup(h, big_png())
    rc, out = h.run("play-check", "w2", "--scene", SCENE, "--seconds", "0.1")
    line = [x for x in out.splitlines() if x.startswith("picture")][0]
    paths = re.findall(r"\S+\.png", line)
    assert len(paths) == 2, line
    small, full = paths
    assert small.endswith("-small.png") and not full.endswith("-small.png")
    assert line.index(small) < line.index(full)
    from PIL import Image
    assert Image.open(small).size == (640, 360)
    assert Image.open(full).size == (1920, 1080)  # the full picture stays


def test_a_picture_already_small_is_not_blown_up(h):
    play_setup(h, big_png(400, 300))
    rc, out = h.run("play-check", "w2", "--scene", SCENE, "--seconds", "0.1")
    line = [x for x in out.splitlines() if x.startswith("picture")][0]
    small = re.findall(r"\S+\.png", line)[0]
    from PIL import Image
    assert Image.open(small).size == (400, 300)
