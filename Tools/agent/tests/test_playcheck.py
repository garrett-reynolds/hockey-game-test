import base64
import json
import os
import subprocess
import time

import pytest

from conftest import PLAY, READY, SUMMARY_OK, dead_pid, reply

SCENE = "Assets/Scenes/Rink.unity"
PNG = base64.b64encode(b"\x89PNG fake picture").decode()


def ev(text, rc=0):
    return reply({"success": rc == 0, "result": text}, rc=rc)


def samples(*rows):
    """rows of (yaw, x, y, z, skate); time is 1/60 s apart."""
    return ";".join(f"{i / 60:.4f},{r[0]},{r[1]},{r[2]},{r[3]},{r[4]}" for i, r in enumerate(rows))


GOOD = samples(*[(0 + i * 0.1, 0.0, 1.0, 0.0, 0.002) for i in range(30)])
TURNED = samples(*[(i * 6.0, 0.0, 1.0, 0.0, 0.002) for i in range(31)])  # ends 180 deg round
SUNK = samples(*[(0.0, 0.0, 1.0, 0.0, -0.05) for i in range(30)])
JUMPY = samples((0, 0.0, 1.0, 0.0, 0.0), (0, 0.0, 1.0, 0.0, 0.0), (0, 1.5, 1.0, 0.0, 0.0), (0, 1.5, 1.0, 0.0, 0.0))

CONSOLE_BEFORE = {"cursor": 10, "counts": {"error": 0, "warn": 5, "log": 3}, "entries": []}
CONSOLE_CLEAN = {"cursor": 12, "counts": {"error": 0, "warn": 5, "log": 4}, "entries": []}
CONSOLE_ERR = {"cursor": 14, "counts": {"error": 2, "warn": 6, "log": 4}, "entries": []}


def setup(h, data, console_after=CONSOLE_CLEAN, extra_evals=()):
    slot = "w2"
    # probes: ready wait, ready wait after the refresh, then Play (#187)
    h.set(slot, "editor_status", reply(READY), reply(READY), reply(PLAY))
    for command, answer in (("menu", {"ok": True}), ("recompile", {"ok": True}),
                            ("recompile_status", {"status": "completed", "errors": []})):
        h.set(slot, command, reply(answer))
    h.set(slot, "test_status", reply({"status": "no_tests"}))
    h.set(slot, "open_scene", reply({"success": True}))
    h.set(slot, "console_status", reply(CONSOLE_BEFORE), reply(console_after))
    h.set(slot, "editor_play", reply({"success": True}))
    h.set(slot, "editor_stop", reply({"success": True}))
    h.set(slot, "capture_game_view", reply({"base64": PNG}))
    # eval order: prepare, [action], read, cleanup
    h.set(slot, "eval", ev("ok"), *extra_evals, ev(data), ev("ok"))


def check_raw(h, *extra):
    return h.run("play-check", "w2", "--scene", SCENE, "--seconds", "0.1", *extra)


def check(h, *extra, evals=()):
    """The run's output from the PLAY CHECK block on (a Refresh note may come first, #187)."""
    rc, out = check_raw(h, *extra)
    i = out.find("PLAY CHECK")
    return rc, out[i:] if i > 0 else out


def commands(h):
    out = []
    for argv in h.calls():
        for a in argv:
            if a in ("editor_play", "editor_stop", "eval", "open_scene", "capture_game_view", "console_status"):
                out.append(a)
                break
    return out


def test_good_run_passes_and_prints_the_fixed_block(h):
    setup(h, GOOD)
    rc, out = check(h)
    assert rc == 0, out
    ls = [x for x in out.splitlines() if x.strip()]
    assert ls[0] == f"PLAY CHECK w2 {SCENE} idle  PASS"
    assert ls[1].startswith("facing") and "PASS" in ls[1] and "limit 45" in ls[1]
    assert ls[2].startswith("skate") and "PASS" in ls[2]
    assert ls[3].startswith("jump") and "PASS" in ls[3]
    assert ls[4].startswith("console") and "PASS" in ls[4]
    assert ls[5].startswith("picture")
    assert any(x.startswith("detail") or ".json" in x for x in ls[6:])
    cmds = commands(h)
    assert cmds.index("editor_play") < cmds.index("editor_stop")
    assert cmds[-1] == "editor_stop"


def test_picture_is_saved_inside_the_project(h):
    setup(h, GOOD)
    rc, out = check(h)
    pic = [x for x in out.splitlines() if x.startswith("picture")][0].split(None, 1)[1].strip()
    root = (h.dir.parent / "wt" / "w2").as_posix()
    assert pic.replace("\\", "/").startswith(root + "/")
    assert os.path.getsize(pic) > 0


def test_turned_goalie_fails_facing(h):
    setup(h, TURNED, extra_evals=(ev("ok"),))
    rc, out = check(h, "--action", "clip:clip-16-slump")
    assert rc == 1
    assert "FAIL" in out.splitlines()[0]
    facing = [x for x in out.splitlines() if x.startswith("facing")][0]
    assert "FAIL" in facing and "180.0 deg" in facing
    assert "PASS" in [x for x in out.splitlines() if x.startswith("skate")][0]


def test_sunk_skates_fail_skate(h):
    setup(h, SUNK)
    rc, out = check(h)
    assert rc == 1
    assert "FAIL" in [x for x in out.splitlines() if x.startswith("skate")][0]
    assert "-0.050 m" in out


def test_big_jump_fails_jump(h):
    setup(h, JUMPY)
    rc, out = check(h)
    assert rc == 1
    assert "FAIL" in [x for x in out.splitlines() if x.startswith("jump")][0]


def timed(*rows):
    """rows of (time s, x): hips only move along x."""
    return ";".join(f"{t:.4f},0.0,{x},1.0,0.0,0.002" for t, x in rows)


# 3.2 m/s all the way, but one frame takes 0.126 s (an Editor hitch): that frame steps 0.403 m (the old per-frame
# check failed at 0.3 m).
HITCH = timed((0.0, 0.0), (0.0167, 0.053), (0.0333, 0.107), (0.1593, 0.510),
              *[(0.1593 + i * 0.0167, 0.510 + i * 0.053) for i in range(1, 25)])
# The same slow frame, but the hips really snap 1.5 m.
HITCH_JUMP = timed((0.0, 0.0), (0.0167, 0.053), (0.0333, 0.107), (0.1593, 1.607),
                   *[(0.1593 + i * 0.0167, 1.607 + i * 0.053) for i in range(1, 25)])


def test_one_slow_frame_does_not_fail_jump(h):
    setup(h, HITCH)
    rc, out = check(h)
    assert rc == 0, out
    line = [x for x in out.splitlines() if x.startswith("jump")][0]
    assert "PASS" in line and "m/s" in line


def test_real_jump_in_a_slow_frame_still_fails_jump(h):
    setup(h, HITCH_JUMP)
    rc, out = check(h)
    assert rc == 1
    assert "FAIL" in [x for x in out.splitlines() if x.startswith("jump")][0]


def replay_rows(*rows):
    """rows of (x, replay 0/1): 60 fps, hips along x, the 7th column is ReplayView.Showing."""
    return ";".join(f"{i / 60:.4f},0.0,{x},1.0,0.0,0.002,{rp}" for i, (x, rp) in enumerate(rows))


def jump_line(out):
    return [x for x in out.splitlines() if x.startswith("jump")][0]


# 30 calm frames (3.2 m/s), 5 Replay frames where the hips pop 0.4 m in and 0.4 m out, then calm again.
REPLAY_CUT = replay_rows(*[(i * 0.053, 0) for i in range(30)], *[(1.6 + 0.4, 1)] * 5,
                         *[(1.6 + 0.053 * i, 0) for i in range(1, 10)])
REAL_POP = replay_rows(*[(i * 0.053, 0) for i in range(30)], *[(1.6 + 1.5 + i * 0.053, 0) for i in range(10)])


def test_replay_cut_is_not_a_jump_and_skips_are_printed(h):
    setup(h, REPLAY_CUT)
    rc, out = check(h)
    assert rc == 0, out
    line = jump_line(out)
    assert "PASS" in line and "Replay" in line
    assert "skipped 6 " in line  # the pair into the Replay, 4 inside it, the pair out of it


def test_no_replay_means_zero_skipped(h):
    setup(h, GOOD)
    rc, out = check(h)
    assert "skipped 0 " in jump_line(out)


def test_real_one_frame_pop_without_replay_still_fails(h):
    setup(h, REAL_POP)
    rc, out = check(h)
    assert rc == 1
    assert "FAIL" in jump_line(out) and "skipped 0 " in jump_line(out)


def test_play_check_refused_while_a_run_is_going(h):
    setup(h, GOOD)
    h.write_lock("w2", dead_pid(), cli_pid=os.getpid())
    rc, out = check_raw(h)
    assert rc == 2
    assert "still going" in out
    assert "editor_play" not in commands(h)


def test_console_errors_fail(h):
    setup(h, GOOD, console_after=CONSOLE_ERR)
    rc, out = check(h)
    assert rc == 1
    line = [x for x in out.splitlines() if x.startswith("console")][0]
    assert "FAIL" in line and "2 errors" in line and "1 warnings" in line


def test_play_is_stopped_when_a_call_fails(h):
    setup(h, GOOD)
    h.set("w2", "eval", ev("boom", rc=1))
    rc, out = check(h)
    assert rc == 1
    assert "FAIL" in out
    cmds = commands(h)
    assert "editor_play" in cmds and cmds[-1] == "editor_stop"
    assert not (h.locks / "w2.lock").exists()


def test_prepare_error_is_reported_and_play_stopped(h):
    setup(h, GOOD)
    h.set("w2", "eval", ev("error: no goalie in the scene"))
    rc, out = check(h)
    assert rc == 1
    assert "no goalie in the scene" in out
    assert commands(h)[-1] == "editor_stop"


def test_refused_under_a_live_test_lock(h):
    setup(h, GOOD)
    h.write_lock("w2", os.getpid())
    rc, out = check(h)
    assert rc == 2
    assert "already running" in out
    assert "editor_play" not in commands(h)


def test_refused_when_the_editor_is_already_in_play(h):
    setup(h, GOOD)
    h.set("w2", "editor_status", reply(PLAY))
    rc, out = check(h)
    assert rc == 2
    assert "editor_play" not in commands(h)


def test_crowd_flag_changes_the_prepare_code(h):
    setup(h, GOOD)
    check(h, "--crowd")
    first = [a for a in h.calls() if "eval" in a][0]
    assert "crowd=1" in " ".join(first)


def test_default_prepare_code_hides_the_crowd(h):
    setup(h, GOOD)
    check(h)
    first = [a for a in h.calls() if "eval" in a][0]
    assert "crowd=0" in " ".join(first)


def test_unknown_action_is_refused(h):
    setup(h, GOOD)
    rc, out = check(h, "--action", "dance")
    assert rc == 2
    assert "editor_play" not in commands(h)


def test_results_file_holds_the_detail(h):
    setup(h, GOOD)
    rc, out = check(h)
    path = [x for x in out.splitlines() if x.endswith(".json")][-1].split()[-1]
    data = json.loads(open(path, encoding="utf-8").read())
    assert data["verdict"] == "PASS" and len(data["samples"]) == 30


def test_shot_action_presses_waits_for_the_window_and_shoots(h):
    setup(h, GOOD, extra_evals=(ev("ok"), ev("SkateIn"), ev("ShootingWindow"), ev("ok")))
    rc, out = check(h, "--action", "shot")
    assert rc == 0, out
    assert out.splitlines()[0].endswith("shot  PASS")
    code = " ".join(" ".join(a) for a in h.calls() if "eval" in a)
    assert "Press" in code and "SubmitShot" in code


def test_shot_wait_polls_faster_than_the_window_is_open(h):
    # #214: the Shooting Window is open 3 s; a 3 s poll plus the eval time can step over it for a whole Round
    import sys
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    from slotlib import playcheck
    assert playcheck.SHOT_POLL_S <= 1.0


def test_shot_wait_failure_names_the_phases_seen(h):
    setup(h, GOOD, extra_evals=(ev("ok"),) + (ev("SkateIn"),) * 40 + (ev("screen=Playing phase=SkateIn"),))
    h.env["SLOT_SHOT_WAIT_S"] = "0.5"
    rc, out = check(h, "--action", "shot")
    assert rc == 1
    assert "never reached the Shooting Window" in out
    assert "phases seen" in out and "SkateIn" in out


def test_too_few_frames_is_not_trusted(h):
    setup(h, samples((0, 0.0, 1.0, 0.0, 0.0)))
    rc, out = check(h)
    assert rc == 1
    assert "only 1 frames sampled" in out


def test_play_wait_message_uses_the_real_limit(h):
    setup(h, GOOD)
    h.set("w2", "editor_status", reply(READY))  # Play never starts
    h.env["SLOT_PLAY_WAIT_S"] = "1"
    rc, out = check(h)
    assert rc == 1
    assert "Play did not start within 1 s" in out


def test_picture_that_was_not_saved_fails_the_block_with_the_reason(h):
    setup(h, GOOD)
    h.set("w2", "capture_game_view", reply({}, rc=1))
    rc, out = check(h)
    assert rc == 1
    assert out.splitlines()[0].endswith("FAIL")
    pic = [x for x in out.splitlines() if x.startswith("picture")][0]
    assert "ERROR" in pic and "capture_game_view" in pic
    assert "NOT SAVED" not in out


def test_picture_reply_without_a_file_fails_the_block(h):
    setup(h, GOOD)
    h.set("w2", "capture_game_view", reply({"success": True}))
    rc, out = check(h)
    assert rc == 1
    assert "picture  ERROR" in out or "ERROR" in [x for x in out.splitlines() if x.startswith("picture")][0]


# ---- Refresh + recompile before Play (#187) ------------------------------------------------

def git(path, *args):
    return subprocess.run(["git", "-C", str(path), *args], capture_output=True, text=True, check=True).stdout.strip()


@pytest.fixture
def repo(h, tmp_path):
    path = tmp_path / "wt" / "w2"
    path.mkdir(parents=True)
    git(path, "init", "-q")
    git(path, "checkout", "-q", "-b", "feat/x")
    (path / "a.txt").write_text("a", encoding="utf-8")
    git(path, "add", "a.txt")
    git(path, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "a")
    return path


def unity_names(h):
    out = []
    for argv in h.calls():
        if "--result-only" in argv:
            out.append(argv[argv.index("--result-only") + 1:][0])
    return out


def again(h):
    """Rewind every fake reply so a second run starts from the first reply again."""
    for f in h.dir.glob("count-*"):
        f.unlink()


def test_first_play_check_refreshes_then_enters_play(h, repo):
    setup(h, GOOD)
    rc, out = check_raw(h)
    assert rc == 0, out
    seq = [n for n in unity_names(h) if n in ("menu", "recompile", "recompile_status")]
    assert seq[:3] == ["menu", "recompile", "recompile_status"]
    assert "Refresh + recompile" in out
    all_calls = [a for argv in h.calls() for a in argv]
    assert all_calls.index("menu") < all_calls.index("editor_play")


def test_unchanged_code_sends_no_refresh(h, repo):
    setup(h, GOOD)
    assert check(h)[0] == 0
    again(h)
    h.set("w2", "editor_status", reply(READY), reply(READY), reply(PLAY))
    rc, out = check_raw(h)
    assert rc == 0, out
    assert "Refresh + recompile" not in out
    assert unity_names(h).count("menu") == 1


def test_changed_code_refreshes_again(h, repo):
    setup(h, GOOD)
    assert check(h)[0] == 0
    (repo / "b.txt").write_text("b", encoding="utf-8")
    git(repo, "add", "b.txt")
    git(repo, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "b")
    again(h)
    h.set("w2", "editor_status", reply(READY), reply(READY), reply(PLAY))
    assert check(h)[0] == 0
    assert unity_names(h).count("menu") == 2


def test_compile_error_stops_before_play(h, repo):
    setup(h, GOOD)
    h.set("w2", "recompile_status", reply({"status": "failed", "errors": ["Foo.cs(3,1): error CS1002: ; expected"]}))
    rc, out = check(h)
    assert rc == 1
    assert "compile error" in out and "CS1002" in out
    assert "editor_play" not in commands(h)
    assert not (h.locks / "w2.lock").exists()


def test_test_after_play_check_sends_no_refresh(h, repo):
    setup(h, GOOD)
    assert check(h)[0] == 0
    h.ready("w2")
    again(h)
    h.set("w2", "editor_status", reply(READY))
    h.set("w2", "run_tests", reply(SUMMARY_OK))
    rc, out = h.run("test", "w2")
    assert rc == 0, out
    assert unity_names(h).count("menu") == 1


def test_refuses_at_once_when_editor_not_open(h):
    setup(h, GOOD)
    root = h.env["SLOT_ROOT"].replace("/", "\\")
    row = f"7800\tready\t{root}\\w3\t6000.6.5f1\t4242\n"  # only w3 is open
    h.set("_status", "status", {"out": row, "rc": 0})
    start = time.time()
    rc, out = check(h)
    assert rc == 2
    assert "not open; run: slot.py open w2" in out
    assert time.time() - start < 1.5  # SLOT_WAIT_S is 2: it must not wait that out
    assert "editor_play" not in commands(h)
    assert not (h.locks / "w2.lock").exists()


def test_stale_lock_kept_message_when_editor_does_not_answer(h):
    h.set("w2", "editor_status", reply({}, rc=1))
    (h.locks).mkdir(exist_ok=True)
    h.write_lock("w2", dead_pid())
    rc, out = check(h)
    assert rc == 2
    assert "Found a stale lock" in out and "kept because the Editor did not answer" in out
    assert (h.locks / "w2.lock").exists()


def test_stale_lock_cleared_message_when_editor_answers(h, repo):
    setup(h, GOOD)
    (h.locks).mkdir(exist_ok=True)
    gone = dead_pid()
    h.write_lock("w2", gone)
    rc, out = check_raw(h)
    assert rc == 0, out
    assert f"stale lock (pid {gone} gone) cleared; the Editor answers." in out


def test_second_network_error_names_play_check(h, repo):
    setup(h, GOOD)
    h.set("w2", "menu", {"out": "Network error", "rc": 1, "sleep": 0})
    rc, out = check_raw(h)
    assert rc == 2, out
    assert "Network error twice during menu; the Editor may be reloading. Run slot.py play-check again." in out
    assert "editor_play" not in commands(h)
    assert not (h.locks / "w2.lock").exists()


def test_lock_released_when_refresh_fails(h, repo):
    setup(h, GOOD)
    h.set("w2", "menu", reply({}, rc=1))
    rc, out = check(h)
    assert rc != 0
    assert "editor_play" not in commands(h)
    assert not (h.locks / "w2.lock").exists()


def test_cleanup_restores_the_saved_mute_and_never_unmutes():
    from pathlib import Path
    src = (Path(__file__).resolve().parent.parent / "slotlib" / "playcheck.py").read_text(encoding="utf-8")
    assert "audioMasterMute=false" not in src.replace(" ", "")
    assert 'SetData("pc.mute",UnityEditor.EditorUtility.audioMasterMute)' in src
    cleanup = src.split("CLEANUP = ", 1)[1].split('"""', 2)[1]
    assert 'GetData("pc.mute")' in cleanup
    assert "(pm==null)?true:(bool)pm" in cleanup
    assert 'SetData("pc.mute",null)' in cleanup


def sampler_body():
    from slotlib import playcheck
    text = playcheck.PREPARE
    return text[text.index("cb=()=>{"):text.index("EditorApplication.update+=cb;")]


def test_sampler_stops_when_a_bone_is_destroyed():
    body = sampler_body()
    assert "hips==null" in body and "l==null" in body and "r==null" in body


def test_sampler_removes_itself_when_a_bone_is_destroyed():
    body = sampler_body()
    assert 'GetData("pc.cb")' in body and "EditorApplication.update-=me" in body


def test_eval_snippets_have_no_double_slash_comments():
    from slotlib import playcheck
    for name in ("PREPARE", "READ", "CLEANUP"):
        code = getattr(playcheck, name)
        assert "//" not in code.replace("http://", ""), name


def test_sampler_ends_quietly_on_any_exception_of_its_own():
    body = sampler_body()
    assert "try{" in body and "catch(System.Exception)" in body


def test_deliberate_game_error_still_fails_the_check(h):
    setup(h, GOOD, console_after={"cursor": 20, "counts": {"error": 1, "warn": 0, "log": 0}, "entries": []})
    rc, out = check(h)
    assert rc == 1
    line = [x for x in out.splitlines() if x.startswith("console")][0]
    assert "FAIL" in line and "1 errors" in line


def test_sampler_checks_every_object_it_reads_for_destroyed():
    body = sampler_body()
    assert "s!=null" in body
