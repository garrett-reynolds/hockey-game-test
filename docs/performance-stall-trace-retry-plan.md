# Frame trace retry

- Plan ID: 4bd143298e894fa8836abe8e335f3bba.
- Repository: https://github.com/garrett-reynolds/hockey-game-test.
- Owned worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source.
- Integration commit: 71d67d4cb90ec9b1966a3e76a44bb0c5cbd838bd.
- User request: "restart your tests". A new exact heavy-run plan still requires approval under test-load.md.
- Approval: the user explicitly approved this exact retry plan with "app-roved" after its ID, limits, command record, and crash risk were shown.
- Started by this session: yes. The slot was free. This session recorded the approved grant and launched the exact saved command. Runner shell session: 41087.
- Result: completed in 172.83 seconds, runner exit 2, reason child_exit. The report is above budget. The trace contains 5,974 eligible frames across skate-in/replay and crowd-on/off groups. No guard stop, timeout, or system crash occurred during this retry. The grant is consumed.
- Trace summary: median step+render+readback wall time was 11.2109 ms crowd-on versus 6.6829 ms off for skate-in; 13.0608 ms on versus 7.8634 ms off for replay. This supports crowd rendering as a material cost. It does not prove live FPS.
- Slowest 1 percent of combined-wall frames: skate-in on 23.6497 ms (step 3.7931, shooter render 12.3770, readback 7.4795); replay on 34.3731 ms (step 9.1345, replay render 5.2228, shooter render 12.8142, readback 7.2016). These are diagnostic sample groups, not the original per-metric percentile values.
- No completed collection overlapped those selected slow frames. Incremental collection work is not ruled out. Windows thread-CPU samples show coarse increments, so do not treat an individual zero as no CPU work or use it to prove external scheduling.
- Existing full-game budget result: CPU slowest 1 percent 17.2930/28.1995 ms; GPU upper-bound slowest 1 percent 20.7928/26.1541 ms; crowd cost 4.9276/5.5715 ms for skate-in/replay. The game lag remains unresolved.
- Evidence: original CSV and unity.log in the output folder. Summary and full-game report copies are docs/performance-proof/stall-retry-trace-summary.json and stall-retry-frame-budget.json/.md.
- Limits: one worker; exclusive slot; 25 percent job CPU; host stop above 95 percent for 10 seconds; 600-second total limit. There is no GPU load cap.
- Risk: the prior run was active during a Windows graphics crash, bugcheck 0x116. The specific cause is not proved. Repeating this workload may crash the PC again.
- Known duration: an earlier full-game check took 145.45 seconds. The traced run did not finish. This retry's duration is unknown.
- Preflight: all three reviewed/compiled source hashes still match the original trace plan. No code changed. Prior pure checks and reviews remain valid. A fresh empty output folder preserves the interrupted run's log.
- Output: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\stall-trace-95-retry-01.

Exact saved command in Windows PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -batchmode -job-worker-count 1 -projectPath 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source' -executeMethod HockeyGame.Editor.FrameBudgetTool.RunFromCommandLine -frameBudgetResult 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\stall-trace-95-retry-01' -frameBudgetTrace 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\stall-trace-95-retry-01\frame-trace.csv' -logFile 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\stall-trace-95-retry-01\unity.log'
```

Use the controlled runner after exact-plan approval. Wait for a busy slot. Do not touch another run. Do not reuse the old consumed grant.
