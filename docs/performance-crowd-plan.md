# Paired crowd check

- Plan ID: a9bd22af3ffe434da23da42d17f93b5b.
- Repository: https://github.com/garrett-reynolds/hockey-game-test.
- Owned worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source.
- Approval: the user explicitly approved this exact plan ID and its shown limits in this session.
- Started by this session: yes. The controlled launch returned session 51571.
- Result: completed in 44.24 seconds, runner exit 0. Both comparison views were measured. In the player view, 3D crowd cost was 4.4503 ms and flat far-card crowd cost was 4.2087 ms, a 0.2417 ms saving. Up-ice costs were 7.3623 and 5.5867 ms. Both options exceeded the 2 ms target. Keep the 3D look while testing repeated CPU work instead.
- Limits: one worker, exclusive slot, 25 percent test-job CPU cap, total-host stop above 95 percent for 10 seconds, 300-second duration limit.
- Purpose: run the existing CrowdCostTool. Compare no crowd, 3D far fans, and flat-card far fans in alternating rounds at two views. This measures the existing first cut in FrameBudget.CutOrder before changing the default.
- Known time: the complete game frame check took 145.45 seconds. This different tool's duration is unknown.
- Effect: GPU rendering may make the PC slow.
- This measurement changes no game source or live project. Wait if the shared slot is busy.
- Exact native command in Windows PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -batchmode -job-worker-count 1 -projectPath 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source' -executeMethod HockeyGame.Editor.CrowdCostTool.RunFromCommandLine -crowdCostResult 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\crowd-comparison-95\crowd-cost.json' -logFile 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\crowd-comparison-95\unity.log'
```

Do not start without this plan's approval. Do not restart a consumed grant.
