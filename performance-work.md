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

The whole-game check is blocked by the host CPU guard. The narrow Easy mode gap-merge change is implemented on branch work/perf-crowd-20261017 in commits 240eb87 and 8126356. Its five pure checks passed after two deliberate no-change failures before the fix. The four reviewed code/test/harness files exactly match the worker's tested tip f53c12a0235bcfe2243c0c0b9adf286869008a36 (git diff exit 0). The open Unity project's NetFaceGaps source still matches c0493e5. No whole-game speed gain is proved. No change was pushed or applied to the open game.

## Focused review

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
