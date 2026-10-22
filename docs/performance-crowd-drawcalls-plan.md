# Crowd draw-call comparison plan

- Plan ID: b84716980292423ba7cdd769cd7800d1.
- Repository: https://github.com/garrett-reynolds/hockey-game-test.
- Owned worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source.
- Integration commit: a2bc7a84c3342caabbd2c7897df3b1773558edf4.
- Approval: the user explicitly selected "Approve check" for this exact plan ID and its shown limits and graphics risk.
- Started by this session: yes. The slot was free. This session recorded the approved grant and launched the exact saved command. Runner shell session: 30351.
- Result: completed in 83.91 seconds, exit 0, child_exit. Fourteen native crowd tests passed, zero failures/skips. All four grouping images at both views matched the 16-sector reference exactly. Grant consumed.
- Shooter: sectors 4/8/16/32 submitted 48/48/96/164 calls and 6757/3657/3657/3596 fans. Paired median costs: 3.31423/2.71109/3.05822/3.52175 ms.
- Up-ice: sectors 4/8/16/32 submitted 115/163/276/500 calls and 14185/10909/9588/8844 fans. Paired median costs: 5.25752/5.40582/4.90447/5.94998 ms.
- Eight sectors beat 16 in all five shooter cost rounds but lost in all five up-ice rounds. Keep the 16-sector default. Next targeted change merges only pairs already visible at 16-sector culling; it must not add submitted fans.
- Evidence is in the owned output folder: crowd-view.xml, crowd-slices.json, summary.json, per-phase logs, and comparison images. The game lag remains unresolved.
- Limits: one worker; exclusive slot; 25 percent job CPU; host stop above 95 percent for 10 seconds; 600-second total limit. GPU load is not capped.
- Effect: Unity may slow the PC. A prior graphics workload coincided with bugcheck 0x116. The cause is not proved; another graphics run may crash the PC.
- Known duration: the prior crowd comparison took 44.24 seconds. The later full-game trace retry took 172.83 seconds and completed without a new system/display crash event. This two-phase run's duration is unknown.
- Phase 1: focused native CrowdViewTests. Require all prior visibility/transform checks and both new exact-data/regrouping checks to pass before continuing.
- Phase 2: CrowdSliceCostTool. Compare 4/8/16/32 sectors in shooter and up-ice views. Five paired rounds, 30 measured renders per side after two settling renders. Rotate variant order and alternate on/off order. Total 2,652 comparison renders including warm-up and image checks.
- Record submitted mesh-call counts, submitted fans, per-round paired costs, and exact image differences. These counters describe CPU mesh submissions, not measured hardware draw counts. Preserve all fan state. Default grouping stays at 16.
- Every image must match its 16-sector reference. Reference-repeat drift also blocks success. Reject nonfinite times or inconsistent paired arithmetic. Save evidence even when a candidate fails.
- Preflight: three pure grouping checks and four pure launcher checks passed. Both source reviews and launcher review found no actionable issue. Fresh full-source compile covered current Core/Game source, the new Editor tool, and both focused test sources: exit 0, zero compiler output, fresh 433152-byte DLL. All five pinned changed C# sources match that compile after integration.
- Wrapper: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\run-crowd-drawcalls.py.
- Wrapper SHA256: ea01db73bb86c9a06aebd159cd20bd026a5fc9b783be43bc9d630e3a3fc0d010.
- Source pins: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\drawcalls-source-pins.json, also embedded in the wrapper. The wrapper fails if any pin changes.
- Output: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\crowd-drawcalls-95. Created empty by this session.
- The wrapper uses the proven direct subprocess.run wait, validates its own hash and controlled Job membership/limits, reads test rules before each action, and has no stop or cleanup path.

Exact saved command in Windows PowerShell:

```powershell
& 'C:\Users\garrett.reynolds\AppData\Local\Programs\Python\Python312\python.exe' 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\run-crowd-drawcalls.py' 'ea01db73bb86c9a06aebd159cd20bd026a5fc9b783be43bc9d630e3a3fc0d010'
```

Wait for explicit approval of this exact plan. Wait if capacity is busy. Never touch another run. This comparison does not prove the full game meets budget; a selected production change still needs full-game validation.
