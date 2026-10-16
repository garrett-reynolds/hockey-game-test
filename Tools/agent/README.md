# Slot tool

Checks the slot Editors (w1 = folder 111, w2, w3) and runs EditMode tests on them safely.
Live copy: `C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py` (with the `slotlib/` folder beside it).

```
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py status
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py test w2
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py test w2 --filter Goalie
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py test w2 --full
```

- `status`: one line per slot: free, busy (compiling, test run, slot tool run), in Play, `not open` (not in `unity status`), or `not answering (timed out)` / `not answering (<short reason>)`. An open Editor shows its port. A stale lock is shown, never cleared here.
- `test`: one run per Editor (lock file per slot, held for the whole run). Waits until the Editor answers and is done compiling. Refuses when the Editor is in Play, when a run is live, or when the Editor never answers. Clears a stale lock only after the Editor answers.
- Time limits are fixed: fast 900 s with the inner `-- --timeout 900`; `--full` 1,800 s with the inner `-- --timeout 1800`. There is no flag to change them.
- Full output goes to `_notes/results/<slot>-<fast|full>-<time>.json`. The tool prints one result line and the file path. Exit 0 = all passed, 1 = failures or no result, 2 = refused.

- `close <slot> [--force]`: runs `unity close`, then confirms with `unity status`. Prints "closed" and exits 0 only when status no longer lists the Editor; else "not closed, still open", exit 1 (the `false none` case). Refuses (exit 2) while a slot tool run or a test run is live, unless `--force`. With `--force`, when `unity close` does not take (a jammed Editor), it also kills the Editor process (PID from `unity status`) with its AssetImportWorker children, then confirms with `unity status`. Already closed: says so, exit 0.
- `open <slot>`: opens the Editor and waits until it answers (limit 600 s), then prints "ready" (exit 0), or "not ready: not answering / still compiling" (exit 1). If the Editor is already open it never starts a second one; it only waits for it, and if it never answers it says to run `slot.py close <slot> --force` then `open`. If `unity status` cannot be read it refuses (exit 2).
- `test` refuses at once, with "not open; run: slot.py open <slot>", when the slot is not in `unity status`.
- Memory check (#219): `test` and `play-check` stop at once (exit 2) when free physical memory is under 3.0 GB, naming the free and needed amounts. `--skip-memory-check` skips it. Reason for the limit: `slotlib/memory.py`.
- Dead Editor (#219): while `test` or `play-check` wait for the Editor to answer, they stop within a few seconds when the Editor process has exited or left `unity status`, instead of waiting 600 s.
- `play-check` also saves `<picture>-small.png` (640 px wide, needs the installed Pillow) and prints its path first; the full picture stays.
- Hang watch (inside `test`): each minute the tool reads the CPU of the slot Editor plus its worker processes. Two low minutes in a row (under 1 CPU second each) while the run is live prints `HANG` with the next step (`slot.py close <slot>`, then `slot.py open <slot>`) and exits 3. The tool stops the run_tests CLI child before it releases the lock. A minute whose CPU cannot be read is skipped, never counted as low. This replaces `_notes/hangwatch.py`: do not start it beside agents.

Environment overrides (used by the tests): `SLOT_CLOSE_WAIT_S`, `SLOT_OPEN_WAIT_S`, `SLOT_CPU_SOURCE` (a `.py` that prints cumulative CPU seconds for a slot), `SLOT_CPU_INTERVAL_S`, `SLOT_CPU_MIN`, `SLOT_UNITY_EXE` (a `.py` file is run with Python), `SLOT_ROOT`, `SLOT_LOCK_DIR`, `SLOT_RESULTS_DIR`, `SLOT_POLL_S`, `SLOT_WAIT_S`, `SLOT_STATUS_TIMEOUT_S`.

## Two copies and the install step

The code lives in the repo (`Tools/agent/`, `.githooks/pre-commit`). Every slot runs the live copy in `_notes`. After a change, run:

```
python Tools/agent/install.py          # copies slot.py, slotlib/ and githooks/pre-commit to _notes
python Tools/agent/install.py --check  # lists drift between the two copies; exit 1 if any
```

Layout: `slot.py` (entry), `slotlib/common.py` (Unity CLI call, slot list, lock, Editor state), one module per sub-command (`status.py`, `testrun.py`), `closeopen.py` (close, open), `editors.py` (open Editors from `unity status`), `cpu.py` (CPU reader), `watch.py` (hang watch), `done.py` (the `done` check).

Tests: `python -m pytest Tools/agent/tests` from the repo root. They run the command line against `tests/fake_unity.py`.

## play-check (#180)

```
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py play-check w2 --scene Assets/Scenes/Rink.unity [--action clip:<name>|shot] [--seconds 8] [--crowd]
```

- Refuses a slot with a live run, a compiling or busy Editor, or one already in Play. Holds the slot lock (kind `play-check`). Always runs `editor_stop` at the end, also on failure.
- Opens the scene, enters Play, hides the crowd and mutes sound (unless `--crowd`; it never turns sound on, and afterwards restores the Editor's earlier mute, or mutes if unknown), runs the action, samples every frame (Editor-side sampler registered by one eval, removed by the last eval), saves one picture to `<project>/Logs/playcheck-<slot>-<time>.png`.
- Prints a fixed block: facing (limit 45 deg), skate (lowest blade vs ice, limit >= -0.03 m), jump (largest per-frame hips move, limit 0.3 m), console (errors during Play, limit 0), picture path, detail file. Exit 0 = PASS, 1 = FAIL, 2 = refused. Detail (all samples) in `_notes/results/<slot>-playcheck-<time>.json`.
- `clip:<name>` needs the AI clip branch (`GoalieView.Clips`); `shot` presses, waits for the Shooting Window and submits one Shot.

## done (#188, #189)

```
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py done w2
```

- Run before a hand-off. Prints one line per check (`OK` or `FAIL`), then `done: all checks OK` or `done: N check(s) FAIL`. Never talks to the Editor, and nothing is killed.
- `git`: branch, uncommitted count and commits ahead of `main`. Lists at most 10 uncommitted names and at most 10 commits, then `+N more`. Fails only when git cannot be read.
- `runs`: reads the run log `_notes/results/<slot>-runs.jsonl` for this branch. Passes when a green run (passed > 0, failed 0, exit 0) is logged at the current HEAD. A run with a `--filter` never counts as green (fast or full both count when unfiltered); the FAIL line then says filtered green runs do not count. Prints the first red run and the last green run with their result files.
- `test files`: only `.cs` files under a `Tests` folder in `Assets/` count (pytest runs are never in the run log). Fails when one changed (committed since `main`, or uncommitted) after the last logged run, or when there is no run at all.
- `wait loops`: lists shell or python processes with an `until`/`while` and a `sleep` that name the slot folder or `slot.py`. Fails when there is one; prints its PID.
- Exit 0 = all checks OK, 1 = at least one FAIL, 2 = unknown slot.
