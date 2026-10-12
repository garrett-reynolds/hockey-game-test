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
    /// Measures the 3D crowd's GPU cost (#94, the 2 ms crowd limit). It renders the shooter's view of the rink (the middle
    /// skate-in point, at the Shot) at 1920 x 1080 many times without the crowd, with it, and with the stand drawn Copies
    /// times over side by side (to guess a full crowd), waiting for the GPU after each frame. The crowd's cost is the
    /// difference in frame time: the median of several rounds, each mode in turn so the machine's load hits all alike.
    /// Batch mode (rendering, no -nographics):
    ///   Unity.exe -batchmode -projectPath &lt;project&gt; -executeMethod HockeyGame.Editor.CrowdCostTool.RunFromCommandLine
    ///     -crowdCostResult &lt;file.json&gt; -logFile &lt;log&gt;
    /// Exit 0 measured, 1 error.
    /// </summary>
    public static class CrowdCostTool
    {
        public const int Width = 1920, Height = 1080;
        const int Frames = 60, Rounds = 7, WarmUp = 30;
        /// <summary>The full-crowd stand-in: the stand behind the near net drawn this many times (about 13,000 fans).</summary>
        public const int FullCrowdCopies = 38;
        const string RinkScene = "Assets/Scenes/Rink.unity";

        [Serializable]
        public sealed class CrowdCost
        {
            public string Gpu;
            public int Width, Height, Fans, FansPerTriangleMesh, FullCrowdFans;
            public double NoCrowdMs, CrowdMs, FullCrowdMs, CrowdCostMs, FullCrowdCostMs, LinearFullCrowdCostMs;
            public List<double> NoCrowdRounds = new List<double>(), CrowdRounds = new List<double>(), FullCrowdRounds = new List<double>();
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
                if (crowd == null) throw new InvalidOperationException("No crowd in the rink: the crowd body file or shader is missing.");
                Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, 0f, 0f);
                camera.targetTexture = target;
                crowd.Show(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.SkateIn, ShotNumber = 1 }), 0.0);

                var cost = new CrowdCost
                {
                    Gpu = SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")",
                    Width = Width, Height = Height, Fans = crowd.FanCount,
                    FansPerTriangleMesh = crowd.Body.Mesh.triangles.Length / 3,
                    FullCrowdFans = crowd.FanCount * FullCrowdCopies
                };
                Time(camera, target, crowd, false, 1, WarmUp);
                Time(camera, target, crowd, true, FullCrowdCopies, WarmUp);
                for (int round = 0; round < Rounds; round++)
                {
                    cost.NoCrowdRounds.Add(Time(camera, target, crowd, false, 1, Frames));
                    cost.CrowdRounds.Add(Time(camera, target, crowd, true, 1, Frames));
                    cost.FullCrowdRounds.Add(Time(camera, target, crowd, true, FullCrowdCopies, Frames));
                }
                cost.NoCrowdMs = Median(cost.NoCrowdRounds);
                cost.CrowdMs = Median(cost.CrowdRounds);
                cost.FullCrowdMs = Median(cost.FullCrowdRounds);
                cost.CrowdCostMs = Median(cost.CrowdRounds.Zip(cost.NoCrowdRounds, (a, b) => a - b).ToList());
                cost.FullCrowdCostMs = Median(cost.FullCrowdRounds.Zip(cost.NoCrowdRounds, (a, b) => a - b).ToList());
                cost.LinearFullCrowdCostMs = cost.CrowdCostMs * FullCrowdCopies;
                crowd.Drawn = true;
                crowd.Copies = 1;
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

        /// <summary>Mean ms per frame over frames renders, each waited for on the GPU (a read-back of the frame).</summary>
        static double Time(Camera camera, RenderTexture target, Crowd crowd, bool drawn, int copies, int frames)
        {
            crowd.Drawn = drawn;
            crowd.Copies = copies;
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                camera.Render();
                AsyncGPUReadback.Request(target).WaitForCompletion();
            }
            return clock.Elapsed.TotalMilliseconds / frames;
        }

        static double Median(List<double> values)
        {
            List<double> sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
        }
    }
}
