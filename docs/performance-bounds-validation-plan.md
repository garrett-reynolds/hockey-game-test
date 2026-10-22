# Crowd bounds validation

- Plan ID: 6144c16738434661890f8372960fae66.
- Repository: https://github.com/garrett-reynolds/hockey-game-test.
- Owned worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source.
- Approval: the user explicitly approved this exact plan ID and its shown limits in this session.
- Started by this session: yes. The slot became free and this session approved and launched the plan.
- Result: runner exit 1 after 1.17 seconds. The PowerShell call operator returned before the GUI Unity process finished, so the wrapper tried to read missing crowd-view.xml. No native tests ran and no crowd timing was written. This grant is consumed. The replacement wrapper uses Python subprocess.run to wait for each Unity process. Its pure launcher check and code review passed; a new exact plan is required.
- Source: reviewed bounds commits b9f18dc and ddac53b on work/perf-crowd-20261017.
- Limits: one worker; exclusive slot; 25 percent test-job CPU cap; host stop above 95 percent for 10 seconds; 600-second total duration.
- Phases run sequentially: native CrowdViewTests with three required named checks; the existing paired CrowdCostTool; the existing full-game FrameBudgetTool.
- Known time: the prior crowd measurement took 44.24 seconds and full-game measurement took 145.45 seconds. Native view tests and the combined duration are unknown.
- Effect: native tests and GPU rendering may make the PC slow.
- Output: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\bounds-validation-95.
- Wrapper: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\run-crowd-validation.ps1.
- Wrapper SHA256: 00931455a5fb46128175634d2587d84a8300de3047316c0ae5c113f15aea955d.
- The wrapper checks its hash, reviewed source hashes, native Job membership, and saved plan limits before launch. It rereads test rules before each test action. It cannot stop a process or change shared runner state.
- A failed native test blocks the later phases. A full-game exit of 2 is a measured over-budget result, not a pass.
- Exact command in Windows PowerShell:

```powershell
& 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -NonInteractive -File 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\run-crowd-validation.ps1' -ExpectedScriptHash '00931455a5fb46128175634d2587d84a8300de3047316c0ae5c113f15aea955d'
```

Wait if the slot is busy. Do not start without this plan's approval. Do not restart a consumed grant.
