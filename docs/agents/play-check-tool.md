# Play-check tool (agents)

`HockeyGame.PlayCheck.PlayCheckRunner` (Editor only, `Assets/Editor/PlayCheck/`) plays an event script in a real Round in `Assets/Scenes/Rink.unity` with the Editor window NOT in front, writes a log, and exits Play. Crowd hidden and Editor audio muted unless `crowd: true`; both are restored after.

Start (returns at once), then poll every ~10 s until the answer starts with `{"type":"summary"`:

```bash
U="C:/Users/garrett.reynolds/AppData/Local/Unity/bin/unity.exe"; P=C:/Users/garrett.reynolds/Projects/Personal/hockey-wt/<slot>
"$U" command --project-path $P --result-only eval --code 'return HockeyGame.PlayCheck.PlayCheckRunner.Start("goal, save:Glove, save:Pads, miss, idle:10", "Logs/playcheck.jsonl", "clip, bone:Head");'
"$U" command --project-path $P --result-only eval --code 'return HockeyGame.PlayCheck.PlayCheckRunner.Poll();'
```

The same Start line works from a file with `eval_file --file <path.cs>`. `PlayCheckRunner.Stop()` ends a run early (restores, exits Play, removes its callbacks). A relative log path is from the project folder (`Logs/` is not committed).

- Start arguments: `script`, `logPath`, `probes` (or null), `sampleEvery` (a frame line every N frames, default 10; 0 = none), `crowd` (default false), `scene` (default Rink).
- Events (split by `,` or new line): `goal`, `save:<Glove|Blocker|Pads|Stick|Body>`, `miss`, `toolate`, `replay:on|off`, `idle:<seconds>`. A wrong word fails before Play.
- Each Shot event waits for the next Shooting Window (pressing Start, or End then Start, as a key does), shoots 0.5 s in through `ScreenFlow.SubmitShot` (a Goal or Save picks the Net Face cell whose goalie race gives it; a Miss goes wide), and physics gives the Whistle. `toolate` lets the window run out. `replay:` sets the saved Replay setting in memory. `idle:` runs that many seconds with no Shot (a Round in progress carries on, so a window can run out into Too Late).
- Probes: `clip` (goalie Animator clip; empty when bones are posed from code), `bone:<HumanBodyBones>` (local Euler angles), `pos:<GameObject name>`. `screen` is always logged.
- Log: JSON lines, keys in this order: `t` (game s since start), `frame`, `type` (start, event, phase, shot, wait, result, holdEnd, done, timeout, error, end, frame), `event`, `phase`, `result` (Goal, Save:<reaction>, Miss, TooLate), `holdStart`, `holdEnd`, `probes`. The last line is the summary: `ok`, `error`, `events`, `eventsDone`, `asExpected`, `timeouts`, `results`, `frames`, `gameSeconds`, `wallSeconds`, `consoleErrors`, `consoleWarnings`, `firstErrors`, `log`.
- Limits: 60 game s per event, 270 wall s per run, 60 s for Play to start.
