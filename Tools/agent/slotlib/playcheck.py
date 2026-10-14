"""`slot.py play-check <slot> --scene <path> [--action clip:<name>|shot] [--seconds N] [--crowd]`.

Enters Play on a free slot Editor, optionally triggers one action, samples the goalie every frame
(an Editor-side sampler that one eval registers and later evals read back), takes one picture,
and always stops Play. Prints a fixed short block; the full detail goes to a results file.
All Editor-side C# lives here as eval strings. Nothing is added to Assets.
"""
import base64
import json
import math
import os
import re
import subprocess
import time

from . import common as c

# Limits. Fixed on purpose: a check that can be loosened per run stops meaning anything.
FACING_LIMIT_DEG = 45.0
SKATE_LIMIT_M = -0.01
JUMP_LIMIT_M = 0.3
CALL_TIMEOUT_S = 60
PLAY_WAIT_S = 60
SHOT_WAIT_S = 30

BF = "System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public"

# ---- Editor-side code (C# for the Unity CLI eval; statements, ends in return) --------------------

PREPARE = """
int crowd=@CROWD@;
var root=UnityEngine.GameObject.Find("Goalie Character");
if(root==null) return "error: no Goalie Character in the scene (is the game running?)";
var an=root.GetComponentInChildren<UnityEngine.Animator>();
if(an==null||!an.isHuman) return "error: the goalie has no humanoid Animator";
var hips=an.GetBoneTransform(UnityEngine.HumanBodyBones.Hips);
var l=an.GetBoneTransform(UnityEngine.HumanBodyBones.LeftUpperLeg);
var r=an.GetBoneTransform(UnityEngine.HumanBodyBones.RightUpperLeg);
var skates=new System.Collections.Generic.List<UnityEngine.Renderer>();
foreach(var x in root.GetComponentsInChildren<UnityEngine.Renderer>(true)) if(x.name.ToLower().Contains("skate")) skates.Add(x);
if(skates.Count==0) return "error: no skate renderers under the goalie";
float ice=0f;
var rk=UnityEngine.GameObject.Find("Rink");
if(rk!=null) foreach(var x in rk.GetComponentsInChildren<UnityEngine.Renderer>()) if(x.name.StartsWith("Ice")){ice=x.bounds.max.y;break;}
foreach(var cr in UnityEngine.Object.FindObjectsByType<HockeyGame.Crowd>(UnityEngine.FindObjectsSortMode.None)) cr.Drawn=(crowd==1);
UnityEditor.EditorUtility.audioMasterMute=(crowd==0);
var sb=new System.Text.StringBuilder();
int last=-1;
System.Action cb=()=>{
 if(!UnityEditor.EditorApplication.isPlaying) return;
 int f=UnityEngine.Time.frameCount; if(f==last) return; last=f;
 var d=r.position-l.position; float yaw=UnityEngine.Mathf.Atan2(d.x,d.z)*UnityEngine.Mathf.Rad2Deg;
 float low=1e9f; foreach(var s in skates) if(s!=null&&s.enabled) low=UnityEngine.Mathf.Min(low,s.bounds.min.y);
 var p=hips.position; var ci=System.Globalization.CultureInfo.InvariantCulture;
 if(sb.Length>0) sb.Append(';');
 sb.Append(UnityEngine.Time.realtimeSinceStartup.ToString("F4",ci)).Append(',').Append(yaw.ToString("F2",ci)).Append(',').Append(p.x.ToString("F4",ci)).Append(',').Append(p.y.ToString("F4",ci)).Append(',').Append(p.z.ToString("F4",ci)).Append(',').Append((low-ice).ToString("F4",ci));
};
UnityEditor.EditorApplication.update+=cb;
System.AppDomain.CurrentDomain.SetData("pc.cb",cb);
System.AppDomain.CurrentDomain.SetData("pc.sb",sb);
return "ok ice="+ice;
"""

READ = """
var sb=System.AppDomain.CurrentDomain.GetData("pc.sb");
return sb==null?"":sb.ToString();
"""

CLEANUP = """
var cb=System.AppDomain.CurrentDomain.GetData("pc.cb") as System.Action;
if(cb!=null) UnityEditor.EditorApplication.update-=cb;
System.AppDomain.CurrentDomain.SetData("pc.cb",null);
System.AppDomain.CurrentDomain.SetData("pc.sb",null);
UnityEditor.EditorUtility.audioMasterMute=false;
return "ok";
"""

FIND_DRIVER = """
var bf=@BF@;
var drv=UnityEngine.Object.FindFirstObjectByType<HockeyGame.RoundDriver>();
if(drv==null) return "error: no RoundDriver in the scene";
"""

CLIP = FIND_DRIVER + """
var gv=typeof(HockeyGame.RoundDriver).GetField("_goalie",bf).GetValue(drv);
var pr=gv.GetType().GetProperty("Clips");
var clips=pr==null?null:pr.GetValue(gv);
if(clips==null) return "error: this branch has no AI clips (GoalieView.Clips)";
var ct=clips.GetType();
var clip=ct.GetMethod("Load",bf).Invoke(clips,new object[]{"@NAME@"});
if(clip==null) return "error: clip not found: @NAME@";
ct.GetMethod("Begin",bf).Invoke(clips,new object[]{clip,"Idle",false,0f});
return "ok";
"""

SHOT_PRESS = FIND_DRIVER + """
var flow=typeof(HockeyGame.RoundDriver).GetField("_flow",bf).GetValue(drv);
flow.GetType().GetMethod("Press").Invoke(flow,null);
return "ok";
"""

SHOT_PHASE = FIND_DRIVER + """
var flow=typeof(HockeyGame.RoundDriver).GetField("_flow",bf).GetValue(drv);
var round=flow.GetType().GetProperty("Round").GetValue(flow);
return round.GetType().GetProperty("Phase").GetValue(round).ToString();
"""

SHOT_SUBMIT = FIND_DRIVER + """
var flow=typeof(HockeyGame.RoundDriver).GetField("_flow",bf).GetValue(drv);
flow.GetType().GetMethod("SubmitShot").Invoke(flow,new object[]{0.0,0.5,100.0,false});
return "ok";
"""


def code(template, **kw):
    text = template.replace("@BF@", BF)
    for k, v in kw.items():
        text = text.replace(f"@{k}@", str(v))
    return " ".join(line.strip() for line in text.strip().splitlines())


# ---- CLI plumbing --------------------------------------------------------------------------------

def add_parser(sub):
    p = sub.add_parser("play-check", help="enter Play on a slot, measure facing, skates, jumps and errors, stop Play")
    p.add_argument("slot", help="w1, w2 or w3")
    p.add_argument("--scene", required=True, help="scene path inside the project, e.g. Assets/Scenes/Rink.unity")
    p.add_argument("--action", default="idle", help="idle (default), clip:<name> or shot")
    p.add_argument("--seconds", type=float, default=8.0, help="how long to sample after the action (default 8)")
    p.add_argument("--crowd", action="store_true", help="leave the crowd and sound on (default: hidden, muted)")
    p.set_defaults(func=run)


def call(slot, *args, timeout=CALL_TIMEOUT_S):
    """Run one Unity CLI command. Returns the parsed JSON (dict) or raises SlotError with the reason."""
    try:
        p = subprocess.run(c.unity_command(slot, *args, timeout=timeout), capture_output=True, text=True,
                           timeout=timeout + 20, encoding="utf-8", errors="replace")
    except (subprocess.TimeoutExpired, OSError) as e:
        raise c.SlotError(f"{args[0]} did not finish: {e}")
    if p.returncode != 0:
        raise c.SlotError(f"{args[0]} failed (exit {p.returncode}): {(p.stdout + p.stderr).strip()[:300]}")
    try:
        data = json.loads(p.stdout)
    except ValueError:
        data = {"raw": p.stdout}
    if isinstance(data, dict) and data.get("success") is False:
        raise c.SlotError(f"{args[0]} failed: {json.dumps(data.get('errors', data))[:300]}")
    return data if isinstance(data, dict) else {"raw": data}


def ev(slot, text):
    """Run an eval; returns its string result."""
    res = call(slot, "eval", "--code", text).get("result")
    res = "" if res is None else str(res)
    if res.startswith("error:"):
        raise c.SlotError(res)
    return res


def parse_action(action):
    if action in ("idle", "shot"):
        return action, None
    m = re.fullmatch(r"clip:([A-Za-z0-9_.\- ]+)", action)
    if m:
        return "clip", m.group(1)
    raise c.SlotError(f"Unknown action '{action}'. Use idle, shot or clip:<name>.")


def counts(slot):
    cs = call(slot, "console_status")
    cn = cs.get("counts", {})
    return {"error": int(cn.get("error", 0)), "warn": int(cn.get("warn", 0)), "cursor": cs.get("cursor")}


def wait_playing(slot):
    deadline = time.time() + PLAY_WAIT_S
    while time.time() < deadline:
        try:
            if call(slot, "editor_status").get("playMode", "stopped") != "stopped":
                return
        except c.SlotError:
            pass
        time.sleep(c.poll_s())
    raise c.SlotError("Play did not start within 60 s")


def take_picture(slot, label):
    folder = f"{c.slot_path(slot)}/Logs"
    path = f"{folder}/playcheck-{slot}-{time.strftime('%Y%m%d-%H%M%S')}.png"
    try:
        os.makedirs(folder, exist_ok=True)
        data = call(slot, "capture_game_view", "--save_path", path)
        if not os.path.exists(path):
            inner = data.get("data")
            b64 = data.get("base64") or (inner.get("base64") if isinstance(inner, dict) else None)
            if b64:
                with open(path, "wb") as f:
                    f.write(base64.b64decode(b64))
        return path if os.path.exists(path) else None
    except (c.SlotError, OSError):
        return None


# ---- Measures ------------------------------------------------------------------------------------

def parse_samples(text):
    rows = []
    for part in text.split(";"):
        bits = part.split(",")
        if len(bits) == 6:
            try:
                rows.append([float(b) for b in bits])
            except ValueError:
                pass
    return rows


def measure(rows, before, after):
    """rows: t, yaw, x, y, z, skate-minus-ice. Returns the four measures."""
    if rows:
        yaw0 = rows[0][1]
        facing = max(abs((r[1] - yaw0 + 180.0) % 360.0 - 180.0) for r in rows)
        skate = min(r[5] for r in rows)
        jump = max([math.dist(a[2:5], b[2:5]) for a, b in zip(rows, rows[1:])] or [0.0])
    else:
        facing = skate = jump = None
    return {
        "facing": {"value": facing, "limit": FACING_LIMIT_DEG, "pass": facing is not None and facing <= FACING_LIMIT_DEG},
        "skate": {"value": skate, "limit": SKATE_LIMIT_M, "pass": skate is not None and skate >= SKATE_LIMIT_M},
        "jump": {"value": jump, "limit": JUMP_LIMIT_M, "pass": jump is not None and jump <= JUMP_LIMIT_M},
        "console": {"errors": max(0, after["error"] - before["error"]), "warnings": max(0, after["warn"] - before["warn"]),
                    "limit": 0, "pass": after["error"] - before["error"] <= 0},
    }


def block(slot, scene, action, m, picture, results_path):
    ok = all(v["pass"] for v in m.values())

    def word(p):
        return "PASS" if p else "FAIL"

    def fmt(name, v, unit, limit_text, digits):
        val = "no data" if v["value"] is None else f"{v['value']:.{digits}f} {unit}"
        return f"{name:<8} {word(v['pass'])}  {val}  ({limit_text})"
    con = m["console"]
    lines = [
        f"PLAY CHECK {slot} {scene} {action}  {word(ok)}",
        fmt("facing", m["facing"], "deg", f"limit {FACING_LIMIT_DEG:g}", 1),
        fmt("skate", m["skate"], "m", f"limit >= {SKATE_LIMIT_M:g}", 3),
        fmt("jump", m["jump"], "m", f"limit {JUMP_LIMIT_M:g}", 3),
        f"{'console':<8} {word(con['pass'])}  {con['errors']} errors, {con['warnings']} warnings  (limit {con['limit']} errors)",
        f"{'picture':<8} {picture or 'NOT SAVED'}",
        f"{'detail':<8} {results_path}",
    ]
    return ok, lines


# ---- Run -----------------------------------------------------------------------------------------

def run(args):
    slot = args.slot
    c.check_slot(slot)
    kind, name = parse_action(args.action)
    lock = c.read_lock(slot)
    if lock and lock["live"]:
        raise c.SlotError(f"{c.label(slot)}: a run is already running (pid {lock.get('pid')}, "
                          f"{lock.get('kind', '?')}). Refusing the Play check.")
    state = c.editor_state(slot)
    if state != "ready":
        why = {"no_answer": "the Editor is not answering", "compiling": "the Editor is busy compiling",
               "play": "the Editor is already in Play", "test_run": "a test run is live in the Editor"}[state]
        raise c.SlotError(f"{c.label(slot)}: {why}. Refusing the Play check.")
    if lock:
        c.release_lock(slot)  # stale: the Editor answers and its owner is gone
    c.acquire_lock(slot, "play-check")
    c.results_dir().mkdir(parents=True, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    results_path = c.results_dir() / f"{slot}-playcheck-{stamp}.json"
    detail = {"slot": slot, "scene": args.scene, "action": args.action, "seconds": args.seconds,
              "crowd": bool(args.crowd), "error": None}
    rows, picture, before, after, played = [], None, {"error": 0, "warn": 0}, {"error": 0, "warn": 0}, False
    try:
        try:
            call(slot, "open_scene", "--path", args.scene)
            before = counts(slot)
            played = True
            call(slot, "editor_play")
            wait_playing(slot)
            detail["prepare"] = ev(slot, code(PREPARE, CROWD=1 if args.crowd else 0))
            if kind == "clip":
                ev(slot, code(CLIP, NAME=name))
            elif kind == "shot":
                ev(slot, code(SHOT_PRESS))
                deadline = time.time() + SHOT_WAIT_S
                while ev(slot, code(SHOT_PHASE)) != "ShootingWindow":
                    if time.time() > deadline:
                        raise c.SlotError("the Round never reached the Shooting Window in 30 s")
                    time.sleep(c.poll_s())
                ev(slot, code(SHOT_SUBMIT))
            time.sleep(args.seconds)
            picture = take_picture(slot, name)
            rows = parse_samples(ev(slot, code(READ)))
            after = counts(slot)
        except c.SlotError as e:
            detail["error"] = str(e)
    finally:
        for step in (lambda: ev(slot, code(CLEANUP)), lambda: call(slot, "editor_stop") if played else None):
            try:
                step()
            except c.SlotError as e:
                detail["error"] = (detail["error"] + "; " if detail["error"] else "") + f"cleanup: {e}"
        c.release_lock(slot)
    if not rows and detail["error"] is None:
        detail["error"] = "no samples came back (did the game run frames?)"
    m = measure(rows, before, after)
    ok, lines = block(slot, args.scene, args.action, m, picture, results_path)
    if detail["error"]:
        ok = False
        lines[0] = lines[0].replace("PASS", "FAIL")
        lines.insert(1, f"ERROR    {detail['error']}")
    detail.update(verdict="PASS" if ok else "FAIL", measures=m, picture=picture, samples=rows,
                  console_before=before, console_after=after)
    results_path.write_text(json.dumps(detail, indent=1), encoding="utf-8")
    print("\n".join(lines))
    return 0 if ok else 1
