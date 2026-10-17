# Plan-scoped host CPU guard

## Request

The user asked to raise the guard so this game can be tested. Two approved attempts stopped at the host CPU guard during Unity startup, before any game timing data was produced.

## Change

Add an immutable `host_cpu_stop_percent` value to saved test plans. Keep the default at 80. Permit an explicitly approved exclusive suite/stress plan to request up to 95. Keep the sustained-load timer at 10 seconds. Do not permit a raised threshold in focused or light plans. Legacy plans without the new field retain 80.

## Limits that stay

One worker, a 25 percent test-job CPU cap, a 300-second duration limit, shared capacity, run ownership, approval binding, and the normal shared state directory remain in place. A busy slot means wait.

## Source and install

Implement and test in a new isolated worktree of the runner source repository. Deploy only the reviewed `test_control.py` file to `C:\Users\garrett.reynolds\.codex\tools\test-control\test_control.py`. Do not edit the native Windows Job Object module. Verify that the installed old hash still matches the inspected version before replacement. Save a backup and exact restore command in the machine ledger.

## Checks

Run only focused pure unit checks for the new field, approval-bound digest, limits, legacy plans, watchdog reset, and sustained-load behavior. Do not run native control tests or alter any active run. Review the diff before install.

## Game check

After installation, save a new immutable plan for the owned game worktree using `--host-cpu-stop-percent 95`. Show its exact command and limits, then wait for approval of that plan ID. The earlier approvals do not authorize it. Measure the current patch first. A before/after timing comparison and native view proof remain open until actual reports exist.

## Installed result

- Source commit: 3933fd1d002fcbde5a669822938ed90c6309d32e on work/hockey-host-guard-20261017.
- Source worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\guard-source.
- Six selected pure unit tests passed. Both code reviews found no issues.
- Installed file SHA256: e84af35da1675cb498fd227ae45c02277e849399bf6ca6bd3df2f36e7ac0d564, matching reviewed source.
- Backup: C:\Users\garrett.reynolds\.codex\backups\hockey-host-guard-20261017\test_control.py.
- Machine ledger entry: Hockey plan-scoped host CPU cutoff - 2026-10-17.
- No other test run was stopped or changed.

## New game plan ownership

- Plan ID: bcfe7c9bd8d443229fc08e304b2db455.
- Repository: https://github.com/garrett-reynolds/hockey-game-test.
- Owned worktree: C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source.
- Approval: pending for this exact plan. The user approved raising the guard, not this saved run ID.
- Started by this session: no.
- Limits: one worker; exclusive slot; 25 percent test-job CPU cap; total-host stop above 95 percent for 10 seconds; 300-second duration limit.
- Duration: unknown. Earlier checks stopped during startup, so they do not establish a full-run duration.
- Effect: Unity startup, asset imports, and GPU rendering may make the PC less responsive.
- This plan tests the patched version. It does not supply an unpatched timing baseline.
- Command in Windows PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -batchmode -job-worker-count 1 -projectPath 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source' -executeMethod HockeyGame.Editor.FrameBudgetTool.RunFromCommandLine -frameBudgetResult 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\patched-95' -logFile 'C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\patched-95\unity.log'
```

If the shared slot is busy, wait. Do not stop or alter another run. Do not restart a consumed grant.
