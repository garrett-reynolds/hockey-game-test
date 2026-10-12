using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HockeyGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace HockeyGame.Editor
{
    /// <summary>
    /// The frame budget pass (#97): plays the game's two worst moments, the skate-in and the Replay after a Goal (the whole
    /// result hold: Goal Show, the look up, the Replay on both boards and back), with the whole arena, at 1920 x 1080, and
    /// judges them against FrameBudget: the slowest 1% of frames under 12 ms GPU and 8 ms CPU, the crowd under 2 ms GPU.
    /// <para>
    /// It builds the rink as RoundDriver.Awake does (the goalie character, Easy mode on: the glow is drawn) and steps it frame by
    /// frame as RoundDriver.Update and the views' LateUpdate do, at a fixed 1/60 s, with no sound, USB or input. Keep it in step
    /// with RoundDriver. A Gap-reading shooter (GoalRateTool.AimAtAGap) shoots hard 1 s into each shooting window; only Shots
    /// that are Goals count for the Replay moment. Each Shot's crowd is on or off in turn (on: as in the game; off: for the
    /// crowd's cost), so the machine's load and heat hit both alike. The first Shot only warms up.
    /// </para>
    /// <para>
    /// The times are wall-clock, as the crowd cost tool's (#95): CPU ms is the frame's game step plus the render calls; GPU ms
    /// is from the render calls until the frame is back from the GPU (a read-back waited for), so it is an upper bound on the
    /// GPU's own time. In the Editor's Play mode the dev readout (FrameReadout) shows the FrameTimingManager's GPU and CPU ms.
    /// The crowd cost is the median of the crowd-on Shots' mean GPU ms less that of the crowd-off Shots.
    /// </para>
    /// From the menu (Hockey Game/Frame Budget/Run) or batch mode (no -nographics: it must render):
    /// Unity -batchmode -projectPath ... -executeMethod HockeyGame.Editor.FrameBudgetTool.RunFromCommandLine
    /// [-frameBudgetResult folder] -logFile ... writes frame-budget.json and frame-budget.md (default FrameBudget/Latest);
    /// exits 0 within budget, 2 over, 1 on an error.
    /// </summary>
    public static class FrameBudgetTool
    {
        public const int Width = 1920, Height = 1080;
        public const string SkateIn = "skate-in", ReplayAfterGoal = "Replay after a Goal";
        public const string DefaultFolder = "FrameBudget/Latest";
        public const string JsonFile = "frame-budget.json", MarkdownFile = "frame-budget.md";
        public const int WithinExit = 0, ErrorExit = 1, OverExit = 2;

        /// <summary>Goals (and skate-ins) measured with the crowd on, and as many with it off.</summary>
        const int ShotsPerMode = 5;
        const int MaxShots = 80;
        const float FrameSeconds = 1f / 60f;
        /// <summary>Frames left out after the crowd switches on or off and at each moment's start.</summary>
        const int SettleFrames = 2;
        const double ShotAtWindowSeconds = 1.0, ShotSpeedKmh = 150.0;
        const int Seed = 97;
        const string RinkScene = "Assets/Scenes/Rink.unity";

        [Serializable]
        public sealed class Moment
        {
            public string Name;
            public int Frames, CrowdOffFrames, Shots;
            public double GpuSlowest1PctMs, CpuSlowest1PctMs, GpuMedianMs, CpuMedianMs;
            public double CrowdOffGpuSlowest1PctMs, CrowdCostMs, CrowdCostSlowest1PctMs;
            public bool GpuOver, CpuOver, CrowdOver;
        }

        [Serializable]
        public sealed class Result
        {
            public string Gpu, Cpu, When, Timing;
            public int Width, Height;
            public bool Within;
            public List<Moment> Moments = new List<Moment>();
        }

        // ---- Entry points ----

        [MenuItem("Hockey Game/Frame Budget/Run")]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneSetup[] scenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Result result = Measure();
                Write(result, DefaultFolder);
                Log(result);
            }
            finally
            {
                if (scenes.Length > 0 && Array.TrueForAll(scenes, s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(scenes);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }
        }

        public static void RunFromCommandLine()
        {
            int exit;
            try
            {
                Result result = Measure();
                Write(result, Arg("-frameBudgetResult") ?? DefaultFolder);
                Log(result);
                exit = result.Within ? WithinExit : OverExit;
            }
            catch (Exception e)
            {
                Debug.LogError("Frame budget: ERROR " + e);
                exit = ErrorExit;
            }
            EditorApplication.Exit(exit);
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static void Write(Result result, string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, JsonFile), JsonUtility.ToJson(result, true));
            File.WriteAllText(Path.Combine(folder, MarkdownFile), Markdown(result));
        }

        static void Log(Result result)
        {
            string text = "Frame budget:\n" + Markdown(result);
            if (result.Within) Debug.Log(text);
            else Debug.LogWarning(text);
        }

        // ---- The report ----

        /// <summary>The result as a markdown table, one row per moment, then the cut order.</summary>
        public static string Markdown(Result result)
        {
            var s = new StringBuilder();
            s.AppendLine("# Frame budget (#97)");
            s.AppendLine();
            s.AppendLine("- GPU: " + result.Gpu);
            if (!string.IsNullOrEmpty(result.Cpu)) s.AppendLine("- CPU: " + result.Cpu);
            if (!string.IsNullOrEmpty(result.When)) s.AppendLine("- When: " + result.When);
            s.AppendLine("- Picture: " + result.Width + " x " + result.Height);
            if (!string.IsNullOrEmpty(result.Timing)) s.AppendLine("- Timing: " + result.Timing);
            s.AppendLine("- Limits: slowest 1% under " + Ms(FrameBudget.GpuMs) + " ms GPU and " + Ms(FrameBudget.CpuMs) +
                         " ms CPU; crowd under " + Ms(FrameBudget.CrowdGpuMs) + " ms GPU");
            s.AppendLine("- Verdict: " + (result.Within ? "within budget" : "OVER budget"));
            s.AppendLine();
            s.AppendLine("| Moment | Frames | GPU slowest 1% ms | CPU slowest 1% ms | Crowd GPU ms | Verdict | GPU median ms | CPU median ms |");
            s.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (Moment m in result.Moments)
                s.AppendLine("| " + m.Name + " | " + m.Frames + " | " + Ms(m.GpuSlowest1PctMs) + " | " + Ms(m.CpuSlowest1PctMs) +
                             " | " + Ms(m.CrowdCostMs) + " | " + Verdict(m) + " | " + Ms(m.GpuMedianMs) + " | " + Ms(m.CpuMedianMs) + " |");
            s.AppendLine();
            s.AppendLine("A slowest 1% many times its median means a few frames stalled (another program, shader loading, heat): close " +
                         "other programs and run it again before cutting.");
            s.AppendLine();
            s.AppendLine("If over, cut in this order (never the Calm Area test or the goalie):");
            for (int i = 0; i < FrameBudget.CutOrder.Count; i++)
                s.AppendLine((i + 1) + ". " + FrameBudget.CutOrder[i]);
            return s.ToString();
        }

        static string Verdict(Moment m)
        {
            var over = new List<string>();
            if (m.GpuOver) over.Add("GPU");
            if (m.CpuOver) over.Add("CPU");
            if (m.CrowdOver) over.Add("crowd");
            return over.Count == 0 ? "within" : "over: " + string.Join(", ", over);
        }

        static string Ms(double ms) => ms.ToString("0.00", CultureInfo.InvariantCulture);

        // ---- Measuring ----

        /// <summary>A moment's frames, crowd on and off, and each Shot's mean GPU ms (for the crowd's cost).</summary>
        sealed class Frames
        {
            public readonly List<double> Gpu = new List<double>(), Cpu = new List<double>(), OffGpu = new List<double>();
            public readonly List<double> ShotMeans = new List<double>(), OffShotMeans = new List<double>();
            public int Goals, OffGoals; // Shots measured, crowd on and off

            public void Add(bool crowdOn, List<double> gpu, List<double> cpu)
            {
                if (gpu.Count == 0) return;
                if (crowdOn)
                {
                    Gpu.AddRange(gpu);
                    Cpu.AddRange(cpu);
                    ShotMeans.Add(gpu.Average());
                    Goals++;
                }
                else
                {
                    OffGpu.AddRange(gpu);
                    OffShotMeans.Add(gpu.Average());
                    OffGoals++;
                }
            }

            public Moment ToMoment(string name)
            {
                var m = new Moment
                {
                    Name = name, Frames = Gpu.Count, CrowdOffFrames = OffGpu.Count, Shots = Goals,
                    GpuSlowest1PctMs = FrameBudget.Slowest1Pct(Gpu), CpuSlowest1PctMs = FrameBudget.Slowest1Pct(Cpu),
                    GpuMedianMs = FrameBudget.Median(Gpu), CpuMedianMs = FrameBudget.Median(Cpu),
                    CrowdOffGpuSlowest1PctMs = FrameBudget.Slowest1Pct(OffGpu),
                    CrowdCostMs = FrameBudget.Median(ShotMeans) - FrameBudget.Median(OffShotMeans)
                };
                m.CrowdCostSlowest1PctMs = m.GpuSlowest1PctMs - m.CrowdOffGpuSlowest1PctMs;
                MomentVerdict v = FrameBudget.Judge(m.GpuSlowest1PctMs, m.CpuSlowest1PctMs, m.CrowdCostMs);
                m.GpuOver = v.GpuOver;
                m.CpuOver = v.CpuOver;
                m.CrowdOver = v.CrowdOver;
                return m;
            }
        }

        /// <summary>Opens the Rink scene (left unsaved), plays Shots until each moment has ShotsPerMode with the crowd on and off, and judges them.</summary>
        public static Result Measure()
        {
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            float timeScale = Time.timeScale;
            RenderTexture target = null;
            try
            {
                EditorSceneManager.OpenScene(RinkScene, OpenSceneMode.Single);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var game = new Game(target);
                var skateIn = new Frames();
                var replay = new Frames();
                game.Play(skateIn, replay);
                var result = new Result
                {
                    Gpu = SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")",
                    Cpu = SystemInfo.processorType + ", " + SystemInfo.processorCount + " threads",
                    When = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    Timing = "wall clock, Editor batch mode. CPU = game step + render calls; GPU = render calls until the frame is " +
                             "read back (an upper bound on the GPU's own time). Crowd = median Shot mean GPU ms, crowd on less off.",
                    Width = Width, Height = Height
                };
                result.Moments.Add(skateIn.ToMoment(SkateIn));
                result.Moments.Add(replay.ToMoment(ReplayAfterGoal));
                result.Within = result.Moments.All(m => !m.GpuOver && !m.CpuOver && !m.CrowdOver);
                return result;
            }
            finally
            {
                Time.timeScale = timeScale;
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        /// <summary>The game as RoundDriver builds and steps it, without sound, USB, input or the HUD.</summary>
        sealed class Game
        {
            readonly RenderTexture _target;
            readonly SaveData _save = new SaveData { EasyMode = true };
            readonly Camera _camera;
            readonly RibbonBoards _ribbons;
            readonly RoofLights _roofLights;
            readonly Crowd _crowd;
            readonly VideoBoards _boards;
            readonly GoalieView _goalie;
            readonly ShotEffects _effects;
            readonly EasyGlow _easyGlow = new EasyGlow();
            readonly ScreenFlow _flow;
            readonly Round _round;
            readonly IGoalieReach _reach;
            readonly BreakawayView _breakaway;
            readonly ReplayView _replay;
            double _clock;

            public Game(RenderTexture target)
            {
                _target = target;
                RinkBuilder.Build(out _camera, TeamSkin.Load(_save.TeamSkin));
                Transform rink = GameObject.Find("Rink").transform;
                _ribbons = rink.GetComponentInChildren<RibbonBoards>();
                _roofLights = new RoofLights(rink);
                _crowd = rink.GetComponentInChildren<Crowd>();
                if (_crowd == null) throw new InvalidOperationException("No crowd in the rink: a crowd body file or shader is missing.");
                _boards = rink.GetComponentInChildren<VideoBoards>();
                var character = new GoalieCharacter(rink);
                if (!character.Present) throw new InvalidOperationException("The goalie model did not load: the frame budget needs it.");
                _goalie = new GoalieView(rink, character);
                _effects = new ShotEffects(rink, _camera, null, _goalie);
                var settings = new RoundSettings();
                _reach = new GoalieCharacterReach(character, settings);
                _flow = new ScreenFlow(new GoalRateTool.SystemRandom(Seed), _save, _reach, settings);
                _round = _flow.Round;
                var host = new GameObject("Frame Budget Driver");
                _breakaway = host.AddComponent<BreakawayView>();
                _breakaway.Init(_round, _camera, _goalie);
                _replay = host.AddComponent<ReplayView>();
                _replay.Init(_round, _boards, _camera, _goalie.Body, _effects.SaveBlock, _effects.Puck);
                _camera.targetTexture = target;
            }

            /// <summary>Plays Shots, crowd on and off in turn, until both moments have ShotsPerMode Shots each way.</summary>
            public void Play(Frames skateIn, Frames replay)
            {
                var gpu = new List<double>();
                var cpu = new List<double>();
                bool crowdOn = true, warmUp = true;
                int shots = 0;
                bool shot = false, goal = false;
                RoundPhase last = RoundPhase.Idle;
                while (skateIn.Goals < ShotsPerMode || skateIn.OffGoals < ShotsPerMode ||
                       replay.Goals < ShotsPerMode || replay.OffGoals < ShotsPerMode)
                {
                    if (_flow.Screen == FlowScreen.Start || _flow.EndAcceptsInput) _flow.Press();
                    var clock = Stopwatch.StartNew();
                    Step(ref shot);
                    double stepMs = clock.Elapsed.TotalMilliseconds;
                    RoundPhase phase = _round.Phase;
                    bool playing = _flow.Screen == FlowScreen.Playing;

                    // A moment ends: its frames go to its Shot's crowd mode, before a new Shot picks the next.
                    if (last == RoundPhase.SkateIn && phase != RoundPhase.SkateIn)
                        Take(warmUp ? null : skateIn, crowdOn, gpu, cpu);
                    if (last == RoundPhase.ResultHold && phase != RoundPhase.ResultHold)
                        Take(warmUp || !goal ? null : replay, crowdOn, gpu, cpu);
                    if (playing && phase == RoundPhase.SkateIn && last != RoundPhase.SkateIn)
                    {
                        // A new Shot: the crowd mode the Replay moment still needs most (the warm-up Shot: on).
                        if (++shots > MaxShots)
                            throw new InvalidOperationException("Frame budget: too few Goals after " + MaxShots + " Shots (" +
                                                                replay.Goals + " crowd on, " + replay.OffGoals + " off).");
                        warmUp = shots == 1;
                        crowdOn = warmUp || replay.Goals <= replay.OffGoals; // every Shot skates in; only Goals get the Replay moment
                        _crowd.Drawn = crowdOn;
                        shot = false;
                    }
                    last = phase;
                    goal = _round.LastResult.HasValue && _round.LastResult.Value.Kind == ShotResultKind.Goal;

                    bool measure = playing && (phase == RoundPhase.SkateIn || phase == RoundPhase.ResultHold && goal);
                    if (!measure) continue;
                    clock.Restart();
                    if (_replay.Showing) _replay.ReplayCamera.Render(); // the game's Replay camera draws before the shooter's (depth -1)
                    _camera.Render();
                    double renderMs = clock.Elapsed.TotalMilliseconds;
                    // Waits for the GPU to finish the frame. One pixel only: a whole-picture read-back holds about 8 MB that is
                    // not freed until the Editor's loop runs, so thousands of frames would run out of memory.
                    AsyncGPUReadback.Request(_target, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
                    cpu.Add(stepMs + renderMs);
                    gpu.Add(clock.Elapsed.TotalMilliseconds);
                }
            }

            /// <summary>Hands a finished moment's frames (less the settling ones) to frames (null: dropped) and starts afresh.</summary>
            static void Take(Frames frames, bool crowdOn, List<double> gpu, List<double> cpu)
            {
                if (frames != null && gpu.Count > SettleFrames)
                    frames.Add(crowdOn, gpu.Skip(SettleFrames).ToList(), cpu.Skip(SettleFrames).ToList());
                gpu.Clear();
                cpu.Clear();
            }

            /// <summary>One game frame, as RoundDriver.Update then BreakawayView's and ReplayView's LateUpdate; shoots once in each window.</summary>
            void Step(ref bool shot)
            {
                float realDt = FrameSeconds;
                _clock += realDt;
                _replay.RestoreLive();
                _flow.Tick(realDt);

                if (_flow.Screen == FlowScreen.Playing && _round.Phase == RoundPhase.ShootingWindow && !shot &&
                    _round.PhaseSecondsElapsed >= ShotAtWindowSeconds)
                {
                    shot = true;
                    GoalRateTool.AimAtAGap(_round, _round.Settings, _reach, ShotSpeedKmh, out double x, out double y);
                    _flow.SubmitShot(x, y, ShotSpeedKmh, false);
                    _effects.Update(_round, 0f);
                }

                RoundPhase phase = _round.Phase;
                bool inShot = phase != RoundPhase.Idle && phase != RoundPhase.RoundOver;
                _goalie.Show(inShot || _goalie.HasCharacter);
                _goalie.ShowGaps(_easyGlow.Update(_save.EasyMode, _round));
                _effects.Update(_round, realDt);
                ArenaState show = ArenaShow.From(_flow);
                _ribbons.Show(show, _clock);
                _boards.Show(show, _clock);
                _effects.GoalLight.SetOn(show.GoalShow.GoalLight);
                _roofLights.Show(show.GoalShow);
                _crowd.Show(show, _clock);
                _breakaway.LookUp(show.CameraLookUp);
                _replay.Show(show);

                _breakaway.Step(realDt * Time.timeScale, realDt); // Time.deltaTime is scaled by the slow-down's time scale
                _replay.Step();
            }
        }
    }
}
