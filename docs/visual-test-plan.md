# Jersey and net test plan

Plan ID: `239bb6cab0b742459ace6650486dc60c`

Owner: this #220/#221 build session. User approved this exact plan in chat. Waiting for the busy shared slot. This session has not started this run.

Repository: `garrett-reynolds/hockey-game-test`

Owned test worktree: `C:\Users\garrett.reynolds\Projects\Personal\hockey-perf-20261017\source`

Integration worktree: `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\source`

## Exact command

Run by the controlled runner in the owned test worktree:

```powershell
& 'C:\Users\garrett.reynolds\AppData\Local\Programs\Python\Python312\python.exe' 'C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\run-visual-check.py' '58e5ebc0af0d4af7549bc002f5b8f374c4bbcbf04cc6736fa9eef7a19e4d5c81'
```

## Limits

- One worker; exclusive slot.
- 25 percent job CPU cap.
- Stop if total host CPU stays at 95 percent for 10 seconds.
- Ten-minute total stop limit. The runner calls this mode `stress` because its `suite` mode permits at most five minutes. This command adds no stress generator.
- No graphics load cap. Unity can slow the PC. An earlier Unity run coincided with a graphics driver crash. Another crash is possible.
- Earlier cached-project checks took 146.24 seconds for three different phases. This new four-phase run has unknown duration.
- Wait if another session owns the slot. Do not stop or clear it.

## Four phases, one at a time

1. Capture old jersey front and back views with the same editor tool. Only the owned cached GoalieCharacter source is temporarily restored to its old version. The wrapper restores the candidate in `finally`. A hard job stop can prevent `finally`; compare hashes and restore from the integration copy before any future test.
2. Run six focused EditMode fixtures. Require every selected test to pass, including the five named new checks.
3. Capture new jersey front and back views.
4. Run one explicit native cloth test. It enters Play Mode, uses two impact points, and compares real stored cloth shapes against replay. Save paired 512 by 280 frames with light ice and a visible puck. Save recording/apply timings and storage size.

The native net fixture is a controlled impact demonstration, not footage of an old game build. Its settled-net reference shows the old missing-motion behavior. The full-game visual check remains open.

## Saved source and recovery records

- `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\cached-copy-manifest.json`
- `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\cached-project-before-visual`
- `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\native-source-pins.json`
- Source manifest SHA256: `881d794b11f01d960de73b473c787589629e92bac376ba153dc9acff8b4440b9`
- Fresh compile: `visual-compile-10c6f52a97af43e089c191db71525b9d.json`. 279 sources, exit 0, errors 0.

No user game files were changed. The user editor remains open in `C:\Users\garrett.reynolds\Projects\Personal\hockey-wt\111`.

No old test grant applies. Approval must name this saved plan.

## First run result

This session started run `239bb6cab0b742459ace6650486dc60c` after the other session released the slot. It has ended with child exit. No stop or cleanup action was used.

- Old jersey capture: exit 0, 71.703 seconds.
- Focused tests: 29 total, 28 passed, 1 failed, 0 skipped. 50.844 seconds.
- Failure: a triangle UV span was 0.50000006 against a strict 0.5 float limit. The assertion now permits 0.000001 of float rounding (less than 0.001 pixel in this map). Runtime code is unchanged.
- The baseline camera cropped the torso. The proof tool now uses the same wider camera for both views.
- Candidate capture and native net proof did not run.
- Candidate GoalieCharacter was restored. Its hash matches the integration file.
- Fresh compile after the test/proof fixes: exit 0, errors 0; manifest `visual-compile-0877f8c40df44e53bccb74af0d72fc2c.json`.

## Remaining check plan

New plan ID: `235ad856ac8e4819896a7f6352b1d5cd`

Owner: this session. Approval pending. This session has not started this plan.

Same repository, owned cached worktree, one worker, exclusive slot, 25 percent job CPU, 95 percent total CPU guard for ten seconds, and 600-second total limit. Graphics load has no cap. Unity can slow the PC. Another graphics driver crash is possible. New total duration is unknown.

This plan captures the old jersey again with the wider camera, reruns only the failed seam test, captures the new jersey, then runs the native net proof. It does not repeat the 28 passed checks. Prior phase times are listed above.

Exact command, through the controlled runner:

```powershell
& 'C:\Users\garrett.reynolds\AppData\Local\Programs\Python\Python312\python.exe' 'C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\run-visual-remainder.py' '18c2e57783b586d436eaa023ebd977185056f347f556993f874dbff3a63d23bf'
```

Source pins: `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\native-source-pins-02.json`

Pins SHA256: `f7bb5e2f63cc1ee3592b743bbaf6e9994ad4902859cd71f25fa4ba3b9b130d08`

Output: `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\native-proof-02`

## Second run result and next plan

Run `235ad856ac8e4819896a7f6352b1d5cd` ended after old jersey capture (exit 0, 20.75 seconds) and the seam test (exit 2, 26.906 seconds). The test passed the jersey and then found a genuine broad UV span on Pants triangle 109 (0.694436). Runtime mapping now applies only to the jersey. Pants/socks retain their original UV, color, texture, scale, and offset with the existing matte setting. A new test checks that preservation. No live game files changed.

Review found no blockers. Fresh compile passed: `visual-compile-efc9258b002f46cf88e94171be7283e1.json`, 0 errors.

Next saved plan: `bf20208a6fbf40f594a21c8a9ce60daa`

Owner: this session. Approval pending. This session has not started this plan.

Same repository and owned cached worktree. One worker; exclusive slot; 25 percent test CPU; stop at 95 percent host CPU sustained for ten seconds; ten-minute total limit. Graphics load has no cap. Unity can slow the PC. Another graphics driver crash is possible. Duration is unknown. Prior image capture took 20.75 seconds; one seam check took 26.906 seconds.

This run reuses the saved baseline images from run 02. It captures the new jersey, runs six focused clothing checks, then runs the real net motion fixture. All three phases produce results even if an independent check fails. Failures still fail the whole run. It does not repeat the passed replay/net EditMode checks.

Exact command, through the controlled runner:

```powershell
& 'C:\Users\garrett.reynolds\AppData\Local\Programs\Python\Python312\python.exe' 'C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\run-visual-check03.py' '8e1540a5d958c5a24ae040a26df86bf8e23a43c6c020072de90dedee339e8dd2'
```

Source manifest: `native-source-pins-03.json`. SHA256: `a00af32de11426c769f1c9b824db736c5d4f932d6d07194d8c3aebc6adcda7bf`.

Output: `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\native-proof-03`.

Run launch requested by this session after the shared slot became free. User approval is recorded. Run ID: 239bb6cab0b742459ace6650486dc60c.

User approved plan 235ad856ac8e4819896a7f6352b1d5cd in chat. This session requested its launch after the shared slot was free.

User approved plan bf20208a6fbf40f594a21c8a9ce60daa in chat. This session requested its launch after the shared slot was free.
