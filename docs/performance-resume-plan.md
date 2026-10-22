# Replacement game check

- Plan ID: 9b2c6463b0ec4bf9b7668343665ee2a0.
- Repository: https://github.com/garrett-reynolds/hockey-game-test.
- Owned worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source.
- Approval: the user explicitly approved this exact plan ID and its shown limits in this session.
- Started by this session: yes. The controlled launch returned session 37118.
- Result: completed in 145.45 seconds, runner exit 2, child_exit. FrameBudgetTool wrote both reports; exit 2 means the measured game is over budget, not that measurement failed.
- Limits: one worker, exclusive slot, 25 percent test-job CPU cap, host stop above 95 percent for 10 seconds, 600-second time limit.
- Reason: replace the run the agent stopped in error. The user asked to continue work. The prior grant is consumed and cannot restart.
- Known timing: the previous run compiled scripts in 1.316 seconds and was stopped during asset and shader import after about 65 seconds. Full-run duration is unknown.
- Effect: Unity import and GPU rendering may make the PC slow.
- Preserve the existing import cache. Leave the open game and other runs alone. Wait if the slot is busy.
- Exact native command in Windows PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -batchmode -job-worker-count 1 -projectPath 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source' -executeMethod HockeyGame.Editor.FrameBudgetTool.RunFromCommandLine -frameBudgetResult 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\patched-95-resume' -logFile 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\patched-95-resume\unity.log'
```

Native report: patched-95-resume/frame-budget.json and frame-budget.md. Copies are under docs/performance-proof/patched-frame-budget.*. JSON SHA256: 3c499aef92340aded12a73c851fc61513a17af9883b1540d79eafb03ab6a82eb.

The 1920 x 1080 RTX 4070 Laptop test measured skate-in slowest-1-percent GPU upper bound 14.89 ms, CPU 12.09 ms, crowd upper bound 4.91 ms. Replay measured GPU upper bound 20.72 ms, CPU 18.85 ms, crowd upper bound 4.66 ms. Limits remain 12 ms GPU, 8 ms CPU, and 2 ms crowd. This is Editor batch timing, not live player FPS. The small gap-refresh fix is not enough to meet the whole-game budget. Do not claim the lag fixed.
