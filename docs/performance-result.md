# Game performance result — 2026-10-17

The checked runtime changes are now in the user's Unity project:

`C:\Users\garrett.reynolds\Projects\Personal\hockey-wt\111`

The native checks show a useful saving. Some full-game frame limits remain unmet. A human play check is still needed before saying the reported lag is gone.

## What changed

- Reuse unchanged net-face gap results so the view can skip its existing 96-cell refresh.
- Cache crowd world bounds and material matrices until the full transform changes.
- Keep 16-sector crowd culling. Merge two adjacent submissions only when both slices are already visible, use the same body/material group, and have contiguous fan ranges. This reduces calls without adding fans. Keep the full 3D look.

## Proof

Approved plan `3df008d422404f4db63f65c12a58b697` completed in 146.24 seconds with exit 2 because the final frame-budget report remains above some limits. All three phases finished. No new system crash/display event was recorded during the run.

- Native crowd tests: 15 passed, 0 failed, 0 skipped.
- Comparison pictures: exact matches in both views, including repeated reference checks.
- Submitted fan counts: unchanged.
- Policy tests: 4 passed. Launcher tests: 6 passed. Earlier gap tests: 5 passed.
- Real-source compile: 128 actual source files, exit 0, no compiler errors, fresh DLL. Both source reviews and launcher review cleared.

Paired crowd comparison in the same process, five rounds per view:

| View | Old crowd cost | Merged crowd cost | Saving | Old/new submissions | Fans |
| --- | ---: | ---: | ---: | ---: | ---: |
| Shooter | 4.0097 ms | 2.2234 ms | 44.6% | 96 / 48 | 3657 / 3657 |
| Up-ice | 4.8026 ms | 4.4712 ms | 6.9% | 276 / 156 | 9588 / 9588 |

Both views met the pre-set gate: at least 3% lower paired median cost and wins in at least four of five rounds. The full-game phase then ran with the fix enabled.

Full-game Editor checks before and after, from separate runs:

| Moment / metric | Before | After |
| --- | ---: | ---: |
| Skate-in median CPU wall | 6.9934 ms | 4.3487 ms |
| Skate-in median GPU upper bound | 10.1681 ms | 7.7035 ms |
| Replay median CPU wall | 8.9283 ms | 4.9282 ms |
| Replay median GPU upper bound | 11.7329 ms | 7.5005 ms |
| Skate-in slowest 1% CPU wall | 17.2930 ms | 8.9355 ms |
| Skate-in slowest 1% GPU upper bound | 20.7928 ms | 13.3446 ms |
| Replay slowest 1% CPU wall | 28.1995 ms | 9.3431 ms |
| Replay slowest 1% GPU upper bound | 26.1541 ms | 11.4233 ms |

These values are Editor batch wall times. They include native waits and are not live FPS or GPU-only execution times. Separate full-game runs do not isolate changes in machine load. The paired same-process crowd comparison is stronger evidence for the batching saving.

The 8 ms CPU / 12 ms GPU / 2 ms crowd limits are not all met. Final crowd cost was 3.5874 ms in skate-in and 2.1656 ms in replay. Do not close the overall frame-budget acceptance item or claim all lag fixed.

## Applied files and recovery

Unity was closed for the target project. Its HEAD was `c0493e53d1162576209c4abafdc5473bdffb571e`. Both existing runtime files were clean. New helper paths and GUIDs had no collision. `git apply --check` passed before application. All six applied file contents match the tested source, allowing line-ending differences. The applied diff check passed.

Applied only NetFaceGaps.cs, Crowd.cs, CrowdSliceOrder.cs/.meta, and VisibleCrowdPair.cs/.meta. Existing package edits were outside the patch. Runtime changes remain unstaged in the target worktree. No remote write occurred.

- Tested integration code: `0c73c18a1e0223c4ccf811840e3e8fdd2b7468fc`.
- Exact old-file backup: `C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\live-runtime-backup-20261017-203654`.
- Before/after hashes and exact paths: `C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\live-runtime-application.json`.
- Reversible six-file patch: `C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\validated-runtime-performance.patch`.
- Raw run evidence: `C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\crowd-visible-pairs-95`.
- Checked report copies: `docs/performance-proof/visible-pairs-*` and `live-runtime-application.json`.

For recovery, first check whether the target files changed after application. Use the recorded backup or reverse only this patch. Do not reset the target worktree or alter its package edits.

## Next human check

Open `C:\Users\garrett.reynolds\Projects\Personal\hockey-wt\111` in Unity. Play one round. Check whether motion feels smoother. The agent has not reopened this project or started another graphics run after application.
