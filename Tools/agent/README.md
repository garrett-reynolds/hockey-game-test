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

Environment overrides (used by the tests): `SLOT_UNITY_EXE` (a `.py` file is run with Python), `SLOT_ROOT`, `SLOT_LOCK_DIR`, `SLOT_RESULTS_DIR`, `SLOT_POLL_S`, `SLOT_WAIT_S`, `SLOT_STATUS_TIMEOUT_S`.

Layout: `slot.py` (entry), `slotlib/common.py` (Unity CLI call, slot list, lock, Editor state), one module per sub-command (`status.py`, `testrun.py`), `slotlib/watch.py` (seam for the hang watch / CPU sampler, ticket #179).

Tests: `python -m pytest Tools/agent/tests` from the repo root. They run the command line against `tests/fake_unity.py`.
