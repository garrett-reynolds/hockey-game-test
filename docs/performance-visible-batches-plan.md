# Visible crowd-pair validation plan

- Plan ID: 3df008d422404f4db63f65c12a58b697.
- Repository: https://github.com/garrett-reynolds/hockey-game-test.
- Owned worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source.
- Integration commit: 0c73c18a1e0223c4ccf811840e3e8fdd2b7468fc.
- Approval: the user explicitly approved this exact plan with "approve" after its ID, limits, conditional stages, and graphics risk were shown.
- Started by this session: yes. The slot was free. This session recorded the approved grant and launched the exact saved command. Runner shell session: 19628.
- Result: all three phases completed in 146.24 seconds, runner exit 2, child_exit. Fifteen native tests passed with zero failures/skips. All comparison images matched exactly. Both views met the useful-gain gate, so the full-game check and stage trace ran. Final report remains above some limits. No new system crash/display event occurred. Grant consumed.
- Paired crowd cost: shooter 4.00967 to 2.22335 ms (44.6% lower); up-ice 4.80258 to 4.47122 ms (6.9% lower). Calls 96 to 48 and 276 to 156. Submitted fans stayed 3657 and 9588.
- Full-game median GPU upper bounds: skate-in 7.7035 ms, replay 7.50045 ms. Slowest 1% CPU wall: 8.9355/9.34309 ms; GPU upper bounds: 13.34459/11.42328 ms. Some CPU/GPU/crowd targets remain unmet.
- Checked runtime files applied to the closed target project with backups and hash checks. See docs/performance-result.md. A human play check remains open; do not claim all lag fixed.
- Limits: one worker; exclusive slot; 25 percent job CPU; host stop above 95 percent for 10 seconds; 600-second total limit. GPU load is not capped.
- Effect: Unity may slow the PC. A prior graphics test was active during a 0x116 crash; cause remains unproved. This graphics run may crash the PC again.
- Known times: the completed sector comparison plus view tests took 83.91 seconds; the completed full-game trace retry took 172.83 seconds. This new conditional combined duration is unknown.
- Phase 1: focused CrowdViewTests. Require all prior named checks and the new exact-pixel/fan-count visible-pair check. A failed test blocks all later work.
- Phase 2: compare merging off/on, both at 16-sector culling, at shooter/up-ice views. Five paired rounds, 30 measured renders per side after two settling renders. Rotate order, fix crowd state, check repeated reference and exact images. Total comparison workload: 1,328 renders including warm-up/images.
- Require identical submitted fan counts, no increased call counts, exact matching pixels, complete finite times and valid paired arithmetic.
- Phase 3 runs only if BOTH views show at least 3 percent lower median paired crowd cost and wins in at least four of five rounds. Then run the existing full-game FrameBudgetTool with the new fix enabled and save its optional stage trace. Otherwise save the comparison and skip the full-game phase.
- Summary is saved before phase 3 so a later error does not lose prior comparison evidence. Exit 2 means either the comparison did not meet the useful-gain gate, or a completed full-game report remains above budget; summary.json states which. It is not a launch failure.
- Preflight: four pure merge-policy checks and six pure launcher checks passed. Clean compile covered 128 actual source files, with Windows defines, compiler exit 0, zero-byte log, fresh 436224-byte DLL. Both source reviews and final launcher review found no actionable issue. Ten pinned critical C# sources match this compile after integration. The final pinned launcher syntax check passed.
- Wrapper: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\run-visible-pairs.py.
- Wrapper SHA256: 7287336e65601ed325f47c86ec7411dfe44d2615318145414ef11b81f8f09cf4.
- Source pins: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\visible-pairs-source-pins.json, also embedded in the wrapper.
- Output: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\crowd-visible-pairs-95. Created empty by this session.
- The wrapper waits for each native child, checks controlled Job membership and exact saved limits, reads test rules before each action, and has no stop or cleanup path.

Exact saved command in Windows PowerShell:

```powershell
& 'C:\Users\garrett.reynolds\AppData\Local\Programs\Python\Python312\python.exe' 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\run-visible-pairs.py' '7287336e65601ed325f47c86ec7411dfe44d2615318145414ef11b81f8f09cf4'
```

Wait for explicit approval of this plan. Wait if capacity is busy. Never touch another run or reuse a grant. Keep the user's live project unchanged until the performance evidence supports safe application.
