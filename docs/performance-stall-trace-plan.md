# Frame stall trace plan

- Plan ID: 88e293ca95f24cfe989c69c6a880ea49.
- Repository: https://github.com/garrett-reynolds/hockey-game-test.
- Owned worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source.
- Integration commit: 71d67d4cb90ec9b1966a3e76a44bb0c5cbd838bd.
- Approval: the user explicitly approved this exact plan in this session with "approved" after its ID and limits were shown.
- Started by this session: yes. The slot was free. This session recorded the approved grant and launched the exact saved command. Runner shell session: 39390.
- Result: interrupted by a Windows graphics crash and reboot. No frame-trace.csv or frame-budget result was produced. Preserve unity.log. Do not reuse this grant.
- The user reported a computer crash. System Event 1001 at 2026-10-17 20:02:09 -04:00 records bugcheck 0x00000116; Event 41 records the unclean restart. The dump is C:\WINDOWS\Minidump\101726-15156-01.dmp. This confirms a graphics timeout/recovery failure, not the specific triggering app or underlying driver/hardware cause.
- Root matched this run ID, worktree, and command against its owned record, then issued stop --id 88e293ca95f24cfe989c69c6a880ea49 after reboot. The runner acknowledged the targeted stop. Follow-up read-only checks found this run inactive and zero Unity processes for the owned performance worktree. No other run was targeted.
- No further graphics test will start while the crash is investigated. The CPU/job guard does not cap GPU load. Timing suggests a possible link to this test; causation is not proved. The game lag remains unresolved.
- Limits: one worker; exclusive slot; 25 percent job CPU cap; host stop above 95 percent for 10 seconds; 600-second total limit.
- Effect: Unity and graphics work may slow the PC.
- Known time: a prior full-game check took 145.45 seconds. The latest combined three-phase run took 260.39 seconds. The traced run's duration is unknown.
- Purpose: one existing full-game check with opt-in stage timing. No crowd comparison or native view suite is repeated.
- It records game step, replay camera, shooter camera, GPU readback, current-thread CPU time, and completed collection deltas. Low thread CPU does not identify a wait's cause. Zero collection deltas cannot rule out incremental collection work. Tracing adds sampling overhead.
- Output folder: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\stall-trace-95. Created empty by this session.
- Native entry: Unity directly through the controlled runner. No launch wrapper.
- Source pins checked after integration against the clean compiler manifest: FrameTrace.cs 8746DB30346AFAC54E7C026AD8CF110642535C50BBD23F2B9D1245044DC2EEFA; FrameBudgetTool.cs 232CE29F8E42597CD9F33B159A76BECD14B0DE6E712E40136E685DD57520BA72; GoalRateTool.cs BCB1F893B4EB1453A455140CC8166D136568E2F31D3E7E9D5208D63CD4A45914.
- Preflight: four pure trace checks passed; four pure offline-reader checks passed; real changed Editor source compiled with Windows defines, compiler exit 0, zero errors, and a fresh DLL. Both code reviews found no actionable issue. Native trace proof remains open.
- Proof: docs/performance-proof/trace-green.txt, trace-compile.json, trace-compile.log, trace-summary-checks.txt. Original reproducible compiler inputs remain in the owned trace-worker worktree named by the compile manifest.
- Exit 2 is a completed measurement above the frame limits. It must not be reported as a test launch error or a performance pass.

Exact saved command in Windows PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -batchmode -job-worker-count 1 -projectPath 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source' -executeMethod HockeyGame.Editor.FrameBudgetTool.RunFromCommandLine -frameBudgetResult 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\stall-trace-95' -frameBudgetTrace 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\stall-trace-95\frame-trace.csv' -logFile 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\stall-trace-95\unity.log'
```

Start this command only with the saved controlled plan after explicit approval. Wait if another run holds capacity. Never stop that run. Recheck these source pins before launch. Do not reuse a consumed grant.

After completion, read the report and process the CSV with the pure offline reader at C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\summarize_frame_trace.py. Do not call the game lag fixed without actual proof.
