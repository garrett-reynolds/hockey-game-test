using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HockeyGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HockeyGame.PlayCheck
{
    /// <summary>
    /// The agent Play-check tool (#172): opens a scene, enters Play, runs frames without window focus (an EditorApplication.update
    /// callback that calls QueuePlayerLoopUpdate), plays an event script in a real Round through the game's own calls
    /// (ScreenFlow.Press, ScreenFlow.SubmitShot; physics gives the Whistle), logs JSON lines to a file and exits Play. It never
    /// writes the saved settings (SaveStore.Current). Start returns at once (an eval must not block); Poll reads the state and,
    /// at the end, the summary. The callbacks, crowd, mute and runInBackground are put back however the run ends (the end of
    /// the script, a failure, Stop(), Play stopped by hand, an assembly reload). Usage: docs/agents/play-check-tool.md.
    /// </summary>
    public static class PlayCheckRunner
    {
        public const string DefaultScene = "Assets/Scenes/Rink.unity";
        const double ShotSpeedKmh = 100.0;
        const double ShootAfterSeconds = 0.5; // into the shooting window, as a player would (the window is 3 s)
        const double EventTimeoutSeconds = 60.0; // game seconds one event may take
        const double RunTimeoutSeconds = 270.0; // wall seconds for the whole run (the ticket's limit is 5 min)
        const double PlayWaitSeconds = 60.0;
        const double StopWaitSeconds = 30.0; // wall seconds Play may take to exit before the run finishes anyway
        const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        enum Stage { None, WaitPlay, Running, Stopping, Done }

        static Stage _stage;
        static IReadOnlyList<PlayCheckEvent> _events;
        static IReadOnlyList<string> _probes;
        static int _index, _step, _sampleEvery, _frames, _lastFrame = -1, _lastHandledShot, _resultsAtShot, _errors, _warnings, _matched, _timeouts;
        static string _openScene;
        static bool _playRequested;
        static bool _crowd, _oldMute, _oldBackground, _muteSet, _backgroundSet;
        static double _wallStart, _stopStart, _gameStart, _eventStart, _holdStart, _idleUntil, _lastLoggedGameSeconds;
        static string _logPath, _expected, _failure, _summary;
        static StreamWriter _log;
        static RoundDriver _driver;
        static ScreenFlow _flow;
        static Round _round;
        static IGoalieReach _reach;
        static Animator _goalieAnimator;
        static RoundPhase? _lastPhase;
        static readonly List<Crowd> _hiddenCrowds = new List<Crowd>();
        static readonly List<string> _firstErrors = new List<string>();
        static readonly Dictionary<string, int> _results = new Dictionary<string, int>();

        /// <summary>
        /// Starts a run and returns at once: "started ..." or "error: ...". script: PlayCheckScript events; logPath: the file to
        /// write (made new); probes: PlayCheckScript probes (or null); sampleEvery: a frame line every this many frames (0: none);
        /// crowd: leave the crowd drawn and the sound on. Poll() reads the progress and, at the end, the summary.
        /// </summary>
        public static string Start(string script, string logPath, string probes = null, int sampleEvery = 10, bool crowd = false, string scene = DefaultScene)
        {
            if (_stage == Stage.WaitPlay || _stage == Stage.Running || _stage == Stage.Stopping) return "error: a play check is already running; Poll() or Stop()";
            if (EditorApplication.isPlaying) return "error: the Editor is already in Play; stop it first";
            if (EditorApplication.isCompiling) return "error: the Editor is compiling; try again when it is done";
            if (string.IsNullOrWhiteSpace(logPath)) return "error: no log path";
            try
            {
                _events = PlayCheckScript.Parse(script);
                _probes = PlayCheckScript.ParseProbes(probes);
            }
            catch (ArgumentException e) { return "error: " + e.Message; }
            string missing = GameFieldsMissing();
            if (missing != null) return "error: " + missing;

            var active = EditorSceneManager.GetActiveScene();
            bool open = active.path != scene;
            if (open && active.isDirty) return "error: the open scene " + active.path + " has unsaved changes; not opening " + scene;
            if (open && AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) return "error: no scene at " + scene;

            Reset();
            _logPath = Path.GetFullPath(logPath);
            try
            {
                string dir = Path.GetDirectoryName(_logPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                _log = new StreamWriter(_logPath, false, new UTF8Encoding(false));
            }
            catch (Exception e) { return "error: cannot write the log " + _logPath + ": " + e.Message; }
            _sampleEvery = Math.Max(0, sampleEvery);
            _crowd = crowd;
            _wallStart = EditorApplication.timeSinceStartup;
            _stage = Stage.WaitPlay;
            Application.logMessageReceived += OnLog;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _openScene = open ? scene : null;
            _playRequested = false;
            EditorApplication.update += Tick; // its first call opens the scene and enters Play, after this eval has answered (both can take seconds)
            return "started " + _events.Count + " events, log " + _logPath;
        }

        /// <summary>"idle", "running ..." while it runs, or the summary JSON once it is done.</summary>
        public static string Poll()
        {
            switch (_stage)
            {
                case Stage.None: return "idle (no run)";
                case Stage.Done: return _summary;
                default:
                    return "running " + _stage + ": event " + Math.Min(_index + 1, _events.Count) + "/" + _events.Count
                           + (_index < _events.Count ? " (" + _events[_index].Text + ")" : "") + ", frames " + _frames
                           + ", wall " + (EditorApplication.timeSinceStartup - _wallStart).ToString("F1", CultureInfo.InvariantCulture) + " s";
            }
        }

        /// <summary>Stops a run now: restores the crowd, sound and runInBackground, exits Play, removes the callbacks.</summary>
        public static string Stop()
        {
            if (_stage == Stage.None || _stage == Stage.Done) return "nothing running";
            Fail("stopped by Stop()");
            return "stopping";
        }

        /// <summary>Null when owner has the instance field name, else "PlayCheck: Owner.name not found (renamed?)".</summary>
        public static string FieldMissing(Type owner, string name) =>
            owner.GetField(name, FieldFlags) != null ? null : "PlayCheck: " + owner.Name + "." + name + " not found (renamed?)";

        /// <summary>Null when every game field the runner reads (RoundDriver._flow, Round._reach) exists, else the first FieldMissing message.</summary>
        public static string GameFieldsMissing() => FieldMissing(typeof(RoundDriver), "_flow") ?? FieldMissing(typeof(Round), "_reach");

        static void OnBeforeAssemblyReload()
        {
            if (_stage == Stage.None || _stage == Stage.Done) return;
            if (_failure == null) _failure = "an assembly reload came during the run";
            Write("error", _failure, null);
            Finish();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || _stage == Stage.None || _stage == Stage.Done) return;
            if (_stage != Stage.Stopping && _failure == null) { _failure = "Play stopped before the script ended"; Write("error", _failure, null); }
            Finish();
        }

        static void Reset()
        {
            _index = _step = _frames = _errors = _warnings = _matched = _timeouts = 0;
            _lastFrame = -1;
            _lastHandledShot = -1;
            _lastLoggedGameSeconds = 0;
            _driver = null; _flow = null; _round = null; _reach = null; _goalieAnimator = null;
            _lastPhase = null;
            _failure = null; _summary = null; _expected = null;
            _hiddenCrowds.Clear(); _firstErrors.Clear(); _results.Clear();
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Warning) { _warnings++; return; }
            if (type == LogType.Log) return;
            _errors++;
            if (_firstErrors.Count < 3) _firstErrors.Add(message.Length > 200 ? message.Substring(0, 200) : message);
        }

        static double GameNow => Time.unscaledTimeAsDouble - _gameStart;

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - _wallStart > RunTimeoutSeconds && _stage != Stage.Stopping) Fail("the run took over " + RunTimeoutSeconds + " s");
                switch (_stage)
                {
                    case Stage.WaitPlay:
                        if (!_playRequested)
                        {
                            _playRequested = true;
                            if (_openScene != null) EditorSceneManager.OpenScene(_openScene);
                            EditorApplication.isPlaying = true;
                        }
                        else if (EditorApplication.isPlaying) { EditorApplication.QueuePlayerLoopUpdate(); if (Setup()) _stage = Stage.Running; }
                        else if (EditorApplication.timeSinceStartup - _wallStart > PlayWaitSeconds) Fail("Play did not start in " + PlayWaitSeconds + " s");
                        break;
                    case Stage.Running:
                        if (!EditorApplication.isPlaying) { Fail("Play stopped before the script ended"); break; }
                        if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                        EditorApplication.QueuePlayerLoopUpdate();
                        int f = Time.frameCount;
                        if (f == _lastFrame) break;
                        _lastFrame = f;
                        _frames++;
                        Frame();
                        break;
                    case Stage.Stopping:
                        if (!EditorApplication.isPlaying) Finish();
                        else if (EditorApplication.timeSinceStartup - _stopStart > StopWaitSeconds)
                        {
                            if (_failure == null) _failure = "Play did not exit in " + StopWaitSeconds + " s";
                            Write("error", "Play did not exit in " + StopWaitSeconds + " s; finishing anyway", null);
                            Finish();
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Fail("exception: " + e.GetType().Name + ": " + e.Message);
            }
        }

        static bool Setup()
        {
            _driver = UnityEngine.Object.FindFirstObjectByType<RoundDriver>();
            if (_driver == null) return false; // the scene is still starting
            // the fields exist (Start checked them with GameFieldsMissing); a null value means RoundDriver.Start() has not run yet
            _flow = typeof(RoundDriver).GetField("_flow", FieldFlags).GetValue(_driver) as ScreenFlow;
            if (_flow == null) return false;
            _round = _flow.Round;
            _reach = typeof(Round).GetField("_reach", FieldFlags).GetValue(_round) as IGoalieReach;
            var goalie = GameObject.Find("Goalie Character");
            _goalieAnimator = goalie != null ? goalie.GetComponentInChildren<Animator>() : null;

            _oldBackground = Application.runInBackground;
            _backgroundSet = true;
            Application.runInBackground = true;
            if (!_crowd)
            {
                _oldMute = EditorUtility.audioMasterMute;
                _muteSet = true;
                EditorUtility.audioMasterMute = true;
                foreach (var c in UnityEngine.Object.FindObjectsByType<Crowd>(FindObjectsSortMode.None))
                    if (c.Drawn) { c.Drawn = false; _hiddenCrowds.Add(c); }
            }
            _gameStart = Time.unscaledTimeAsDouble;
            Write("start", "script " + string.Join(", ", Texts()) + (_reach == null ? " (no goalie reach found: shots aim blind)" : "")
                                     + (_goalieAnimator == null ? " (no Goalie Character Animator: clip and bone probes are empty)" : ""), null);
            BeginEvent();
            return true;
        }

        static IEnumerable<string> Texts() { foreach (var e in _events) yield return e.Text; }

        static void BeginEvent()
        {
            _step = 0;
            _eventStart = GameNow;
            _expected = null;
            if (_index >= _events.Count) { End(); return; }
            Write("event", _events[_index].Text, null);
        }

        static void NextEvent()
        {
            _index++;
            BeginEvent();
        }

        static void Frame()
        {
            if (_round.Phase != _lastPhase)
            {
                _lastPhase = _round.Phase;
                Write("phase", null, null);
            }
            if (_sampleEvery > 0 && _frames % _sampleEvery == 0) Write("frame", null, null);
            if (_stage != Stage.Running || _index >= _events.Count) return;

            var ev = _events[_index];
            if (GameNow - _eventStart > EventTimeoutSeconds + (ev.Kind == PlayCheckEventKind.Idle ? ev.Seconds : 0))
            {
                _timeouts++;
                Write("timeout", ev.Text + " did not finish in " + EventTimeoutSeconds + " game s (step " + _step + ")", null);
                NextEvent();
                return;
            }
            switch (ev.Kind)
            {
                case PlayCheckEventKind.Idle:
                    if (_step == 0) { _idleUntil = GameNow + ev.Seconds; _step = 1; }
                    if (GameNow >= _idleUntil) { Write("done", ev.Text, null); NextEvent(); }
                    break;
                default:
                    Shot(ev);
                    break;
            }
        }

        static void Shot(PlayCheckEvent ev)
        {
            switch (_step)
            {
                case 0: // get a fresh shooting window: start a Round from the start or end screen, as a key press does
                    if (_flow.Screen == FlowScreen.Start) { _flow.Press(); return; }
                    if (_flow.Screen == FlowScreen.End) { if (_flow.EndAcceptsInput) _flow.Press(); return; }
                    if (_round.Phase != RoundPhase.ShootingWindow || _round.AwaitingWhistle || _round.ShotNumber == _lastHandledShot) return;
                    if (ev.Kind == PlayCheckEventKind.TooLate)
                    {
                        _lastHandledShot = _round.ShotNumber;
                        _resultsAtShot = _round.Results.Count;
                        _expected = "TooLate";
                        Write("wait", ev.Text + ": no Shot, the window runs out (expect TooLate)", null);
                        _step = 1;
                        return;
                    }
                    if (_round.PhaseSecondsElapsed < Math.Min(ShootAfterSeconds, _round.Settings.ShootingWindowSeconds * 0.5)) return;
                    Aim(ev, out double x, out double y, out string expect, out string note);
                    _lastHandledShot = _round.ShotNumber;
                    _resultsAtShot = _round.Results.Count;
                    _expected = expect;
                    _flow.SubmitShot(x, y, ShotSpeedKmh, false); // the same call a real Shot makes (RoundDriver)
                    Write("shot", ev.Text + " at " + x.ToString("F1", CultureInfo.InvariantCulture) + "," + y.ToString("F1", CultureInfo.InvariantCulture)
                                  + " (expect " + expect + (note == null ? "" : ", " + note) + ")", null);
                    _step = 1;
                    return;
                case 1: // the Whistle (or Too Late): the result shows, the hold starts
                    if (_round.Results.Count <= _resultsAtShot) return;
                    string result = _round.Results[_round.Results.Count - 1].ToString();
                    if (_round.LastResult.HasValue && _round.LastResult.Value.Kind == ShotResultKind.Save && _round.LastShot.HasValue)
                        result = "Save:" + SaveReactions.For(_round.LastShot.Value.X, _round.LastShot.Value.Y);
                    _results[result] = _results.TryGetValue(result, out int n) ? n + 1 : 1;
                    bool match = result == _expected || (ev.Kind == PlayCheckEventKind.Save && result == "Save:" + ev.Reaction);
                    if (match) _matched++;
                    _holdStart = GameNow;
                    Write("result", ev.Text + (match ? " as expected" : " NOT as expected (" + _expected + ")"), result, _holdStart);
                    _step = 2;
                    return;
                case 2: // the hold ends: the next Shot, or the Round is over
                    if (_round.Phase == RoundPhase.ResultHold && _round.ShotNumber == _lastHandledShot) return;
                    Write("holdEnd", ev.Text, null, _holdStart, GameNow);
                    NextEvent();
                    return;
            }
        }

        /// <summary>
        /// Where to shoot: for a Goal or a Save, the Net Face cell whose goalie race (the same ShotRace the Round runs on the Shot)
        /// gives that result with the widest margin, and for a Save also the asked Save Reaction; a Miss goes wide of the net.
        /// </summary>
        static void Aim(PlayCheckEvent ev, out double x, out double y, out string expect, out string note)
        {
            note = null;
            if (ev.Kind == PlayCheckEventKind.Miss) { x = -20; y = 50; expect = "Miss"; return; }
            bool wantSave = ev.Kind == PlayCheckEventKind.Save;
            expect = wantSave ? "Save:" + ev.Reaction : "Goal";
            double best = double.NegativeInfinity;
            x = 50; y = 50;
            bool found = false;
            for (int c = 0; c < NetFaceGaps.Columns; c++)
                for (int r = 0; r < NetFaceGaps.Rows; r++)
                {
                    NetFaceGaps.CellCentre(c, r, out double cx, out double cy);
                    if (wantSave && SaveReactions.For(cx, cy) != ev.Reaction) continue;
                    if (_reach == null) { if (!found) { x = cx; y = cy; found = true; } continue; }
                    var race = ShotRace.At(_round.SetUp, _round.AttackSide, _round.PhaseSecondsElapsed, cx, cy, ShotSpeedKmh, _round.ReactionSeconds, _reach, _round.Settings);
                    double margin = wantSave ? race.FlightSeconds - race.ReachSeconds : race.ReachSeconds - race.FlightSeconds;
                    if (margin > best) { best = margin; x = cx; y = cy; }
                }
            if (_reach == null) note = "no goalie reach: aimed blind";
            else if (best < 0) note = "no cell gives it by the race; closest by " + (-best).ToString("F3", CultureInfo.InvariantCulture) + " s";
        }

        static void End()
        {
            Write("end", "script done", null);
            BeginStop();
        }

        static void Fail(string why)
        {
            if (_failure == null) _failure = why;
            Write("error", why, null);
            if (_stage == Stage.WaitPlay && !EditorApplication.isPlaying) { _stage = Stage.Stopping; Finish(); return; }
            BeginStop();
        }

        static void BeginStop()
        {
            if (_stage == Stage.Stopping || _stage == Stage.Done) return;
            Restore();
            _stage = Stage.Stopping;
            _stopStart = EditorApplication.timeSinceStartup;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else Finish();
        }

        /// <summary>Puts back each value the run changed, each on its own (a value never changed is left alone).</summary>
        static void Restore()
        {
            try
            {
                foreach (var c in _hiddenCrowds) if (c != null) c.Drawn = true;
                _hiddenCrowds.Clear();
            }
            catch (Exception e) { Write("error", "restore crowd: " + e.Message, null); }
            if (_muteSet) EditorUtility.audioMasterMute = _oldMute; // _muteSet stays: Finish puts it back again
            if (_backgroundSet) { Application.runInBackground = _oldBackground; _backgroundSet = false; }
        }

        /// <summary>Ends the run once (later calls do nothing): restores, removes every callback, writes the summary.</summary>
        static void Finish()
        {
            if (_stage == Stage.None || _stage == Stage.Done) return;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Restore();
            if (_muteSet) { EditorUtility.audioMasterMute = _oldMute; _muteSet = false; } // again: leaving Play can reset it
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("{\"type\":\"summary\"");
            sb.Append(",\"ok\":").Append(_failure == null && _timeouts == 0 && _errors == 0 ? "true" : "false");
            sb.Append(",\"error\":").Append(PlayCheckLog.Text(_failure));
            sb.Append(",\"events\":").Append(_events.Count).Append(",\"eventsDone\":").Append(Math.Min(_index, _events.Count));
            sb.Append(",\"asExpected\":").Append(_matched).Append(",\"timeouts\":").Append(_timeouts);
            sb.Append(",\"results\":{");
            bool first = true;
            foreach (var kv in _results) { if (!first) sb.Append(','); first = false; sb.Append(PlayCheckLog.Text(kv.Key)).Append(':').Append(kv.Value); }
            sb.Append("},\"frames\":").Append(_frames);
            sb.Append(",\"gameSeconds\":").Append(_lastLoggedGameSeconds.ToString("F1", ci));
            sb.Append(",\"wallSeconds\":").Append((EditorApplication.timeSinceStartup - _wallStart).ToString("F1", ci));
            sb.Append(",\"consoleErrors\":").Append(_errors).Append(",\"consoleWarnings\":").Append(_warnings);
            sb.Append(",\"firstErrors\":[").Append(string.Join(",", _firstErrors.ConvertAll(PlayCheckLog.Text))).Append(']');
            sb.Append(",\"log\":").Append(PlayCheckLog.Text(_logPath)).Append('}');
            _summary = sb.ToString();
            try { _log?.WriteLine(_summary); _log?.Dispose(); } catch { }
            _log = null;
            _driver = null; _flow = null; _round = null; _reach = null; _goalieAnimator = null;
            _stage = Stage.Done;
        }

        static void Write(string type, string evt, string result, double? holdStart = null, double? holdEnd = null)
        {
            if (_log == null) return;
            double t = 0;
            string phase = null;
            int frame = 0;
            List<KeyValuePair<string, string>> probes = null;
            if (EditorApplication.isPlaying && _round != null)
            {
                t = GameNow;
                _lastLoggedGameSeconds = t; // the summary's gameSeconds: Play (and GameNow) are gone by Finish
                frame = Time.frameCount;
                phase = _round.Phase.ToString();
                probes = Probes();
            }
            _log.WriteLine(PlayCheckLog.Line(t, frame, type, evt, phase, result, holdStart, holdEnd, probes));
            if (type != "frame" && type != "phase") _log.Flush();
        }

        static List<KeyValuePair<string, string>> Probes()
        {
            var list = new List<KeyValuePair<string, string>>(_probes.Count + 1)
            {
                new KeyValuePair<string, string>("screen", _flow.Screen.ToString()),
            };
            var ci = CultureInfo.InvariantCulture;
            foreach (string p in _probes)
            {
                string v = null;
                if (p == "clip")
                {
                    if (_goalieAnimator != null && _goalieAnimator.runtimeAnimatorController == null) v = ""; // bones posed from code: no clip (and asking would warn each frame)
                    else if (_goalieAnimator != null)
                    {
                        var clips = _goalieAnimator.GetCurrentAnimatorClipInfo(0);
                        v = clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "";
                    }
                }
                else if (p.StartsWith("bone:", StringComparison.Ordinal))
                {
                    if (_goalieAnimator != null && _goalieAnimator.isHuman)
                    {
                        var bone = (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), p.Substring(5));
                        var tr = _goalieAnimator.GetBoneTransform(bone);
                        if (tr != null) { var a = tr.localEulerAngles; v = a.x.ToString("F2", ci) + "," + a.y.ToString("F2", ci) + "," + a.z.ToString("F2", ci); }
                    }
                }
                else if (p.StartsWith("pos:", StringComparison.Ordinal))
                {
                    var go = GameObject.Find(p.Substring(4));
                    if (go != null) { var q = go.transform.position; v = q.x.ToString("F3", ci) + "," + q.y.ToString("F3", ci) + "," + q.z.ToString("F3", ci); }
                }
                list.Add(new KeyValuePair<string, string>(p, v));
            }
            return list;
        }
    }
}
