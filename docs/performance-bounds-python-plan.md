# Crowd bounds check: replacement launcher

- Plan ID: 64aa621eccb047d4937385e31d2c843f.
- Repository: https://github.com/garrett-reynolds/hockey-game-test.
- Owned worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source.
- Approval: the user explicitly approved this replacement plan in this session with "approved" after the exact plan ID and limits were shown.
- Started by this session: yes. This session recorded the approved grant and launched the saved command after the shared slot became free.
- Result: all three phases completed in 260.39 seconds. The runner exited 2 because the game remains over the frame limits. No launcher failure, timeout, or guard stop occurred. The grant is consumed.
- Native correctness: 12 passed, 0 failed, 0 skipped. All three required visibility/material checks passed.
- Performance: no clear speed gain. Player-view paired body cost was 4.6218 ms versus 4.4503 ms before this patch. Up-ice paired body cost was 10.9685 ms versus 7.3623 ms before. The samples vary too much to claim this cache makes the game faster.
- Whole-game result: slowest 1 percent CPU/GPU wall times were 86.17/84.09 ms in skate-in and 134.23/128.75 ms in replay. Crowd-off GPU wall times also rose to 48.57/73.50 ms. The report does not isolate GPU execution or prove which process caused the stalls.
- Evidence copies: docs/performance-proof/bounds-crowd-view.xml, bounds-crowd-cost.json, bounds-frame-budget.json, bounds-frame-budget.md, bounds-summary.json.
- The open Unity project remains unchanged. Do not call the reported lag fixed.
- Reason: the prior PowerShell launcher returned early. That run ended without test results. This Python launcher waits for each Unity process.
- Checks: the small launcher unit check passed with three exit-code cases. All process calls were mocked. Code review found no issues. Native checks remain open.
- Limits: one worker; exclusive slot; 25 percent test CPU cap; stop above 95 percent total CPU for 10 seconds; 600-second total limit.
- Phases: CrowdViewTests, crowd comparison, full-game frame check. Run each phase after the prior phase ends. A failed view test blocks later phases.
- Prior measured times: crowd comparison 44.24 seconds; full-game check 145.45 seconds. The new total time is unknown.
- Effect: Unity and graphics work may slow the PC.
- Output: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\bounds-validation-95-python.
- Wrapper SHA256: a8c40ef57e5c7866480e2e157816b420cce1e26501a7f73c1a0c2c1e8cd3b943.
- The wrapper checks its own hash, source hashes, approved limits, and controlled job membership. It has no stop or cleanup action.
- Full-game exit 2 means the measured frame cost is still above the set limit.

Exact command in Windows PowerShell:

```powershell
& 'C:\Users\garrett.reynolds\AppData\Local\Programs\Python\Python312\python.exe' 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\run-crowd-validation.py' 'a8c40ef57e5c7866480e2e157816b420cce1e26501a7f73c1a0c2c1e8cd3b943'
```

Wait for this exact plan's approval. Wait if the shared test slot is busy. Never restart a consumed grant.
