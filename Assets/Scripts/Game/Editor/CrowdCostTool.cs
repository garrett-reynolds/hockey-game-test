using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HockeyGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace HockeyGame.Editor
{
    /// <summary>
    /// Measures the 3D crowd's GPU cost (the 2 ms crowd limit). It renders two views of the rink with every fan standing
    /// and cheering (a Goal: the most moving, most covering pose) at 1920 x 1080 many times: without the crowd, with it
    /// (far fans as the light body) and with the far fans as flat cards, waiting for the GPU after each frame. The crowd's
    /// cost is the difference in frame time: the median of several rounds (each mode in turn, so the machine's load hits
    /// all alike), and the slowest 1% of frames with the crowd against the slowest 1% without.
    /// Views: "shooter" (the middle skate-in point at the Shot, toward the near net) and "up-ice" (from in front of the
    /// near net, looking up the ice at the far end and both sides: most of the bowl, as a Replay might show it).
    /// Batch mode (rendering, no -nographics):
    ///   Unity.exe -batchmode -projectPath &lt;project&gt; -executeMethod HockeyGame.Editor.CrowdCostTool.RunFromCommandLine
    ///     -crowdCostResult &lt;file.json&gt; -logFile &lt;log&gt;
    /// Exit 0 measured, 1 error.
    /// </summary>
    public static class CrowdCostTool
    {
        public const int Width = 1920, Height = 1080;
        const int Frames = 60, Rounds = 7, WarmUp = 30, SettleFrames = 2;
        const string RinkScene = "Assets/Scenes/Rink.unity";

        enum Mode { NoCrowd, Bodies, FarCards }

        [Serializable]
        public sealed class ViewCost
        {
            public string View;
            public int FansDrawn;
            public double NoCrowdMs, BodiesMs, FarCardsMs;
            public double BodiesCostMs, FarCardsCostMs;
            public double NoCrowdSlowest1PctMs, BodiesSlowest1PctMs, FarCardsSlowest1PctMs;
            public double BodiesCostSlowest1PctMs, FarCardsCostSlowest1PctMs;
            public List<double> BodiesCostRounds = new List<double>(), FarCardsCostRounds = new List<double>();
        }

        [Serializable]
        public sealed class CrowdCost
        {
            public string Gpu;
            public int Width, Height, Fans, NearFans, FarFans, NearTriangles, FarTriangles;
            public List<ViewCost> Views = new List<ViewCost>();
        }

        public static void RunFromCommandLine()
        {
            int exit = 0;
            try
            {
                CrowdCost cost = Measure();
                string json = JsonUtility.ToJson(cost, true);
                string file = Arg("-crowdCostResult");
                if (file != null) File.WriteAllText(file, json);
                Debug.Log("Crowd cost: " + json);
            }
            catch (Exception e)
            {
                Debug.LogError("Crowd cost: ERROR " + e);
                exit = 1;
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

        public static CrowdCost Measure()
        {
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            RenderTexture target = null;
            try
            {
                EditorSceneManager.OpenScene(RinkScene, OpenSceneMode.Single);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                RinkBuilder.Build(out Camera camera);
                Crowd crowd = GameObject.Find("Rink").GetComponentInChildren<Crowd>();
                if (crowd == null) throw new InvalidOperationException("No crowd in the rink: a crowd body file or shader is missing.");
                camera.targetTexture = target;
                crowd.Show(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.ResultHold, Result = ArenaResult.Goal, ShotNumber = 1 }), 0.0);

                int near = Enumerable.Range(0, crowd.FanCount).Count(i => CrowdSeats.IsNearFan(crowd.SeatOf(i)));
                var cost = new CrowdCost
                {
                    Gpu = SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")",
                    Width = Width, Height = Height, Fans = crowd.FanCount, NearFans = near, FarFans = crowd.FanCount - near,
                    NearTriangles = crowd.BodyOf(0, 0).Mesh.triangles.Length / 3,
                    FarTriangles = crowd.BodyOf(0, 1).Mesh.triangles.Length / 3
                };

                Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, 0f, 0f);
                cost.Views.Add(MeasureView("shooter", camera, target, crowd));
                camera.transform.SetPositionAndRotation(new Vector3(0f, 4f, 1f), Quaternion.Euler(8f, 180f, 0f));
                cost.Views.Add(MeasureView("up-ice", camera, target, crowd));

                crowd.Drawn = true;
                crowd.FarCards = false;
                return cost;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        static ViewCost MeasureView(string name, Camera camera, RenderTexture target, Crowd crowd)
        {
            var modes = (Mode[])Enum.GetValues(typeof(Mode));
            var frames = modes.ToDictionary(m => m, m => new List<double>());
            var rounds = modes.ToDictionary(m => m, m => new List<double>());
            foreach (Mode m in modes) Time(camera, target, crowd, m, WarmUp, null);
            for (int round = 0; round < Rounds; round++)
                foreach (Mode m in modes)
                    rounds[m].Add(Time(camera, target, crowd, m, Frames, frames[m]));
            Time(camera, target, crowd, Mode.Bodies, 1, null);
            var v = new ViewCost
            {
                View = name, FansDrawn = crowd.LastDrawnFans,
                NoCrowdMs = Median(rounds[Mode.NoCrowd]), BodiesMs = Median(rounds[Mode.Bodies]), FarCardsMs = Median(rounds[Mode.FarCards]),
                NoCrowdSlowest1PctMs = Slowest1Pct(frames[Mode.NoCrowd]), BodiesSlowest1PctMs = Slowest1Pct(frames[Mode.Bodies]),
                FarCardsSlowest1PctMs = Slowest1Pct(frames[Mode.FarCards]),
                BodiesCostRounds = rounds[Mode.Bodies].Zip(rounds[Mode.NoCrowd], (a, b) => a - b).ToList(),
                FarCardsCostRounds = rounds[Mode.FarCards].Zip(rounds[Mode.NoCrowd], (a, b) => a - b).ToList()
            };
            v.BodiesCostMs = Median(v.BodiesCostRounds);
            v.FarCardsCostMs = Median(v.FarCardsCostRounds);
            v.BodiesCostSlowest1PctMs = v.BodiesSlowest1PctMs - v.NoCrowdSlowest1PctMs;
            v.FarCardsCostSlowest1PctMs = v.FarCardsSlowest1PctMs - v.NoCrowdSlowest1PctMs;
            return v;
        }

        /// <summary>Mean ms per frame over frames renders, each waited for on the GPU (a read-back of the frame); each
        /// frame's ms after the first SettleFrames goes into each when given (for the slowest 1%).</summary>
        static double Time(Camera camera, RenderTexture target, Crowd crowd, Mode mode, int frames, List<double> each)
        {
            crowd.Drawn = mode != Mode.NoCrowd;
            crowd.FarCards = mode == Mode.FarCards;
            var clock = Stopwatch.StartNew();
            double last = 0;
            for (int i = 0; i < frames; i++)
            {
                camera.Render();
                AsyncGPUReadback.Request(target).WaitForCompletion();
                double now = clock.Elapsed.TotalMilliseconds;
                if (i >= SettleFrames) each?.Add(now - last); // not the first frames after a mode switch
                last = now;
            }
            return clock.Elapsed.TotalMilliseconds / frames;
        }

        /// <summary>The mean of the slowest 1% of values (at least one).</summary>
        static double Slowest1Pct(List<double> values)
        {
            List<double> sorted = values.OrderByDescending(v => v).ToList();
            return sorted.Take(Math.Max(1, sorted.Count / 100)).Average();
        }

        static double Median(List<double> values)
        {
            List<double> sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
        }
    }
}
