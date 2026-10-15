# Play-check tool (agents)

`HockeyGame.PlayCheck.PlayCheckRunner` (Editor only, `Assets/Editor/PlayCheck/`) plays an event script in a real Round in `Assets/Scenes/Rink.unity` with the Editor window NOT in front, writes a log, and exits Play. Crowd hidden and Editor audio muted unless `crowd: true`; both are restored after.

Start (returns at once), then poll every ~10 s until the answer starts with `{"type":"summary"`:

```bash
U="C:/Users/garrett.reynolds/AppData/Local/Unity/bin/unity.exe"; P=C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/<slot>
"$U" command --project-path $P --result-only eval --code 'return HockeyGame.PlayCheck.PlayCheckRunner.Start("goal, save:Glove, save:Pads, miss, idle:10", "Logs/playcheck.jsonl", "clip, bone:Head");'
"$U" command --project-path $P --result-only eval --code 'return HockeyGame.PlayCheck.PlayCheckRunner.Poll();'
```

If Start answers "Main thread operation timed out" (the first eval after a recompile can), call Poll before you retry: Start may already be running. The runner keeps its state in static fields across entering Play, so it needs Enter Play Mode Options with domain reload off (this project's setting).

The same Start line works from a file with `eval_file --file <path.cs>`. `PlayCheckRunner.Stop()` ends a run early (restores, exits Play, removes its callbacks). A relative log path is from the project folder (`Logs/` is not committed).

- Start arguments: `script`, `logPath`, `probes` (or null), `sampleEvery` (a frame line every N frames, default 10; 0 = none), `crowd` (default false), `scene` (default Rink).
- Events (split by `,` or new line): `goal`, `save:<Glove|Blocker|Pads|Stick|Body>`, `miss`, `toolate`, `idle:<seconds>`. A wrong word fails before Play. `replay:` is refused before Play ("replay: not supported yet"): the tool never writes the saved settings, and the game copies the saved Replay setting into the Round every frame.
- Each Shot event waits for the next Shooting Window (pressing Start, or End then Start, as a key does), shoots 0.5 s in through `ScreenFlow.SubmitShot` (a Goal or Save picks the Net Face cell whose goalie race gives it; a Miss goes wide), and physics gives the Whistle. `toolate` lets the window run out. `idle:` runs that many seconds with no Shot (a Round in progress carries on, so a window can run out into Too Late).
- Probes: `clip` (goalie Animator clip; empty when bones are posed from code), `bone:<HumanBodyBones>` (local Euler angles), `pos:<GameObject name>`. `screen` is always logged.
- Log: JSON lines, keys in this order: `t` (game s since start), `frame`, `type` (start, event, phase, shot, wait, result, holdEnd, done, timeout, error, end, frame), `event`, `phase`, `result` (Goal, Save:<reaction>, Miss, TooLate), `holdStart`, `holdEnd`, `probes`. The last line is the summary: `ok`, `error`, `events`, `eventsDone`, `asExpected`, `timeouts`, `results`, `frames`, `gameSeconds`, `wallSeconds`, `consoleErrors`, `consoleWarnings`, `firstErrors`, `log`.
- Limits: 60 game s per event, 270 wall s per run, 60 s for Play to start, 30 s for Play to exit (then the run finishes anyway).
- The crowd, mute, runInBackground and the runner's callbacks are put back however the run ends: script done, a failure, `Stop()`, Play stopped by hand, or an assembly reload.
- A missing game field the runner reads (`RoundDriver._flow`, `Round._reach`) fails Start at once: "PlayCheck: RoundDriver._flow not found (renamed?)".
- After the run the Editor stays in Edit mode with the run's scene (Rink.unity by default) open; the scene that was open before is not reopened.
