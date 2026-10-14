# Slot tool

Checks the slot Editors (w1 = folder 111, w2, w3) and runs EditMode tests on them safely.
Live copy: `C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py` (with the `slotlib/` folder beside it).

```
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py status
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py test w2
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py test w2 --filter Goalie
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py test w2 --full
```

- `status`: one line per slot: free, busy (compiling, test run, slot tool run), in Play, or not answering. A stale lock is shown, never cleared here.
- `test`: one run per Editor (lock file per slot, held for the whole run). Waits until the Editor answers and is done compiling. Refuses when the Editor is in Play, when a run is live, or when the Editor never answers. Clears a stale lock only after the Editor answers.
- Time limits are fixed: fast 900 s; `--full` 1,800 s with the inner `-- --timeout 1800`. There is no flag to change them.
- Full output goes to `_notes/results/<slot>-<fast|full>-<time>.json`. The tool prints one result line and the file path. Exit 0 = all passed, 1 = failures or no result, 2 = refused.

- `close <slot> [--force]`: runs `unity close`, then confirms with `unity status`. Prints "closed" and exits 0 only when status no longer lists the Editor; else "not closed, still open", exit 1 (the `false none` case). Refuses (exit 2) while a slot tool run or a test run is live, unless `--force`. Already closed: says so, exit 0.
- `open <slot>`: opens the Editor and waits until it answers (limit 600 s), then prints "ready" (exit 0), or "not ready: not answering / still compiling" (exit 1). If the Editor is already open it never starts a second one; it only waits for it. If `unity status` cannot be read it refuses (exit 2).
- `test` refuses at once, with "not open; run: slot.py open <slot>", when the slot is not in `unity status`.
- Hang watch (inside `test`): each minute the tool reads the CPU of the slot Editor plus its worker processes. Two low minutes in a row (under 1 CPU second each) while the run is live prints `HANG` with the next step (`slot.py close <slot>`, then `slot.py open <slot>`) and exits 3. The run_tests CLI is not killed. A minute whose CPU cannot be read is skipped, never counted as low. This replaces `_notes/hangwatch.py`: do not start it beside agents.

Environment overrides (used by the tests): `SLOT_CLOSE_WAIT_S`, `SLOT_OPEN_WAIT_S`, `SLOT_CPU_SOURCE` (a `.py` that prints cumulative CPU seconds for a slot), `SLOT_CPU_INTERVAL_S`, `SLOT_CPU_MIN`, `SLOT_UNITY_EXE` (a `.py` file is run with Python), `SLOT_ROOT`, `SLOT_LOCK_DIR`, `SLOT_RESULTS_DIR`, `SLOT_POLL_S`, `SLOT_WAIT_S`, `SLOT_STATUS_TIMEOUT_S`.

Layout: `slot.py` (entry), `slotlib/common.py` (Unity CLI call, slot list, lock, Editor state), one module per sub-command (`status.py`, `testrun.py`), `closeopen.py` (close, open), `editors.py` (open Editors from `unity status`), `cpu.py` (CPU reader), `watch.py` (hang watch).

Tests: `python -m pytest Tools/agent/tests` from the repo root. They run the command line against `tests/fake_unity.py`.

## play-check (#180)

```
python C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/_notes/slot.py play-check w2 --scene Assets/Scenes/Rink.unity [--action clip:<name>|shot] [--seconds 8] [--crowd]
```

- Refuses a slot with a live run, a compiling or busy Editor, or one already in Play. Holds the slot lock (kind `play-check`). Always runs `editor_stop` at the end, also on failure.
- Opens the scene, enters Play, hides the crowd and mutes sound (unless `--crowd`), runs the action, samples every frame (Editor-side sampler registered by one eval, removed by the last eval), saves one picture to `<project>/Logs/playcheck-<slot>-<time>.png`.
- Prints a fixed block: facing (limit 45 deg), skate (lowest blade vs ice, limit >= -0.01 m), jump (largest per-frame hips move, limit 0.3 m), console (errors during Play, limit 0), picture path, detail file. Exit 0 = PASS, 1 = FAIL, 2 = refused. Detail (all samples) in `_notes/results/<slot>-playcheck-<time>.json`.
- `clip:<name>` needs the AI clip branch (`GoalieView.Clips`); `shot` presses, waits for the Shooting Window and submits one Shot.
