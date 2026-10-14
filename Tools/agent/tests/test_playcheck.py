import base64
import json
import os

from conftest import PLAY, READY, reply

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
    h.set(slot, "editor_status", reply(READY), reply(PLAY))
    h.set(slot, "test_status", reply({"status": "no_tests"}))
    h.set(slot, "open_scene", reply({"success": True}))
    h.set(slot, "console_status", reply(CONSOLE_BEFORE), reply(console_after))
    h.set(slot, "editor_play", reply({"success": True}))
    h.set(slot, "editor_stop", reply({"success": True}))
    h.set(slot, "capture_game_view", reply({"base64": PNG}))
    # eval order: prepare, [action], read, cleanup
    h.set(slot, "eval", ev("ok"), *extra_evals, ev(data), ev("ok"))


def check(h, *extra, evals=()):
    return h.run("play-check", "w2", "--scene", SCENE, "--seconds", "0.1", *extra)


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
