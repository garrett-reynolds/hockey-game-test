# Game performance work

## Scope

Reduce play lag. Keep the game rules, goalie timing, crowd detail, and current user edits.

## Source

- Repository: https://github.com/garrett-reynolds/hockey-game-test
- Owned branch: work/perf-crowd-20261017
- Owned worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source
- Base: c0493e5, matching the committed game code in the open Unity project.
- The open project has local package and settings changes. This worktree does not contain those changes. Results here cannot prove their effect.

## Review findings

- Crowd.Draw calls WorldBounds for each slice for every camera.
- WorldBounds makes nine TransformPoint calls per slice, even when the crowd transform has not changed.
- A possible change is to retain each world bound until the full local-to-world matrix changes. This must also handle a moved, rotated, or scaled parent.
- This is a source finding. It is not a measured cause of the reported lag.
- Replay picture size is already 512 x 280. Do not reduce it without evidence.
- Historical frame reports from October 12 are over budget. They are not current performance proof.

## Check path

Use the existing whole-game FrameBudgetTool before any whole-game performance claim. Compare the same source, machine, CPU limits, and camera paths before and after a change. Check crowd visibility under moved, rotated, and scaled parents before accepting bounds reuse. Crowd changes remain deferred until the baseline is available. A separate pure gap-merge slice has a direct functional regression check and does not need Unity to run that check; its whole-game effect remains unproved.

## Test ownership

- Earlier plan dce4dd9b455a4800ab585e518372f6a7 was approved and started by this session.
- Its project was C:\Users\garrett.reynolds\Projects\Personal\hockey-spec84\111.
- Its command ran HockeyGame.Editor.FrameBudgetTool.RunFromCommandLine in Unity 6000.6.5f1 with one worker, a 25 percent CPU cap, and a 300-second limit.
- It ended with runner exit 126 after about 27 seconds. Reason: host_cpu_above_80_percent_for_10_seconds.
- It produced no frame report. Its grant is consumed. It must not restart.
- No other session's run is owned or adopted here. A busy slot means wait.

## Current status

The checked runtime performance changes are applied to C:\Users\garrett.reynolds\Projects\Personal\hockey-wt\111. The final approved run completed in 146.24 seconds: 15 native tests passed; pictures and fan counts stayed identical; paired crowd cost fell 44.6% in the shooter view and 6.9% up-ice. Full-game frame times improved, but some limits remain unmet. The human play check and overall frame-budget acceptance remain open. See docs/performance-result.md for exact proof, paths, backups, and limits.

The delivered runtime changes include immutable gap reuse, cached world bounds, and visible-pair crowd merging. The grouping stays at 16 sectors; no graphics quality cut was made. Flat cards and a global 8-sector change were rejected by measurements. The guard supports an approved plan-scoped 95 percent cutoff; defaults stay at 80. A prior trace run coincided with a graphics crash; causation is not proved. The final approved run recorded no new system crash/display event. No remote write occurred.

## Focused review

Latest diagnosis: the first optional trace run was active during a Windows graphics crash (0x116); the cause is not proved. The user approved retry 4bd143298e894fa8836abe8e335f3bba. It completed in 172.83 seconds without a new system crash/display event and saved the full trace. Crowd-on/off median combined wall time was 11.2109/6.6829 ms in skate-in and 13.0608/7.8634 ms in replay. Rendering accounts for much of the cost. No completed collection overlapped the selected slowest frames, but incremental work is not ruled out. Coarse thread CPU timing cannot identify an external wait's cause. The next narrow experiment compares sector grouping while keeping all fan data and appearance. No default grouping change or game speed gain is claimed.

- Spec review found no merge correctness issue. It checked range clamping, empty/null/overflow behavior, counts, immutability, and the existing view early return.
- Standards review found fixed machine/worktree paths in the test harness and missing Unity test metadata. Both were corrected and cleared by the reviewer.
- Root review required the focused harness to fail if it does not discover exactly five tests, so a zero-test run cannot pass. The corrected harness passed all five tests.

## Focused proof

- RED: docs/performance-proof/gap-refresh-red.txt, 3 passed and 2 failed. Both failures were the expected unchanged-result reference checks.
- GREEN: docs/performance-proof/gap-refresh-green.txt, 5 passed and 0 failed using the corrected harness.
- These are pure C# tests. They do not start Unity, render the game, or measure frame time.
- The source has one production With caller, EasyGlow.Update. GoalieView.ShowGaps takes its existing early return when that caller returns the same gap object.
- The production fix has no API or hint value changes. It avoids the two gap-array copies and the existing 96-cell refresh on an unchanged selected run.
- Before/after game timing and native view checks remain required. Do not close the native acceptance item or call the reported lag fixed.

Exact focused check command in Windows PowerShell (the reviewed source files are identical to those tested in the worker):

```powershell
powershell -NoProfile -File "C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source\Tools\Test-NetFaceGapsRefresh.ps1" -OutputPath "C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source\docs\performance-proof\gap-refresh-rerun.txt" -NUnitDll "C:\Users\garrett.reynolds\Projects\Personal\hockey-wt\111\Library\PackageCache\com.unity.ext.nunit@0198eae3b53e\net472\unity-custom\nunit.framework.dll" -UnityEditorRoot "C:\Program Files\Unity\Hub\Editor\6000.6.5f1"
```

## Proposed owned baseline

- Plan ID: 7eb2db4bd05a4420baafd93a8187f625
- Repository: https://github.com/garrett-reynolds/hockey-game-test
- Worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source
- Approval: the user explicitly approved this plan ID and its limits in this session.
- Started by this session: yes. The controlled runner launch returned session 58084.
- Result: exit 126 after about 43 seconds, host_cpu_above_80_percent_for_10_seconds. No frame report was produced. This grant is consumed. Do not restart it.
- Limits: one worker, exclusive slot, 25 percent CPU, 300 seconds.
- Duration: unknown. This fresh worktree may need asset imports before measurement.
- Command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -batchmode -job-worker-count 1 -projectPath 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source' -executeMethod HockeyGame.Editor.FrameBudgetTool.RunFromCommandLine -frameBudgetResult 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\baseline-owned' -logFile 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\baseline-owned\unity.log'
```

If another run holds the slot, wait. Do not change any other run's state. A consumed grant must not restart this plan.
