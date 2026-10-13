using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HockeyGame;
using HockeyGame.Core;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Measures the crowd's GPU cost and memory at the Round's poses, the Replay camera and the 5 start-screen wide shots
    /// (#115). It renders the rink at 1920 x 1080 with the crowd off and on (Goal pose, the most costly), waiting for the GPU
    /// each frame, and writes hockey-wt\_notes\t116-crowd.json and one PNG per wide shot. It runs only when the flag file
    /// hockey-wt\_notes\t115-measure.go exists, so the full run skips it and changes no files.
    /// </summary>
    public class CrowdMeasureTests
    {
        const int Width = 1920, Height = 1080, Frames = 60, Rounds = 7, WarmUp = 30, SettleFrames = 2;
        const int ShotWidth = 1280, ShotHeight = 720;

        enum Mode { NoCrowd, Full }

        [Serializable]
        public sealed class ViewCost
        {
            public string View;
            public int FansDrawn, DrawCallsNoCrowd, DrawCallsFull;
            public double NoCrowdMs, FullMs, CrowdCostMs, NoCrowdSlowest1PctMs, FullSlowest1PctMs, CrowdCostSlowest1PctMs;
            public List<double> CrowdCostRounds = new List<double>();
        }

        [Serializable]
        public sealed class Measurement
        {
            public string Gpu, Note, LoadNote;
            public int Width, Height, Fans;
            public long CrowdFanDataBytes, CrowdAssetBytes, CrowdOwnBytes, TotalAllocatedBytes, GraphicsDriverBytes;
            public List<ViewCost> Views = new List<ViewCost>();
        }

        static string Notes => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_notes"));

        [Test, Category("Measure"), Explicit("Measure: run by hand for #115")]
        public void Measure_the_crowd_at_the_game_view_and_the_wide_shots()
        {
            if (!File.Exists(Path.Combine(Notes, "t115-measure.go"))) Assert.Ignore("Measure: create _notes\\t115-measure.go to run it");

            bool asyncShaders = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            RenderTexture target = null, replayTarget = null, shotTarget = null;
            Camera camera = null, replayCamera = null;
            Crowd crowd = null;
            try
            {
                camera = null;
                RinkBuilder.Build(out camera);
                crowd = GameObject.Find("Rink").GetComponentInChildren<Crowd>();
                Assert.IsNotNull(crowd, "No crowd in the rink: a crowd body file or shader is missing.");
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                camera.targetTexture = target;
                crowd.Show(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.ResultHold, Result = ArenaResult.Goal, ShotNumber = 1 }), 0.0);

                var result = new Measurement
                {
                    Gpu = SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")",
                    Note = "Editor, EditMode render; not a player build",
                    LoadNote = "other Unity Editors were busy during the measure",
                    Width = Width, Height = Height, Fans = crowd.FanCount
                };

                var settings = new RoundSettings();
                double fullDrift = Breakaway.Drift(AttackSide.Left, settings.SlowDownSeconds, settings.ShootingWindowSeconds, settings);

                result.Views.Add(MeasureRound("Round Middle u=0 (start position)", camera, target, crowd, AttackSide.Middle, 0.0, 0f, 0f));
                result.Views.Add(MeasureRound("Round Left u=1 full drift", camera, target, crowd, AttackSide.Left, 1.0, (float)fullDrift, 0f));
                result.Views.Add(MeasureRound("Round Right u=1", camera, target, crowd, AttackSide.Right, 1.0, 0f, 0f));
                result.Views.Add(MeasureRound("Round Middle u=1 look-up", camera, target, crowd, AttackSide.Middle, 1.0, 0f, 1f));

                // The Replay camera for the Middle start eye (ReplayView's spot, aim and field of view; its small picture).
                replayTarget = new RenderTexture(ReplayView.PictureWidth, ReplayView.PictureHeight, 24);
                var replayObject = new GameObject("Replay Camera (measure)");
                replayCamera = replayObject.AddComponent<Camera>();
                replayCamera.CopyFrom(camera);
                replayCamera.fieldOfView = ReplayView.FieldOfView;
                replayCamera.targetTexture = replayTarget;
                replayCamera.aspect = ReplayView.PictureWidth / (float)ReplayView.PictureHeight;
                Breakaway.SkatePoint(AttackSide.Middle, 0.0, out double rx, out double rd);
                Vector3 eye = new Vector3((float)rx, (float)Breakaway.EyeHeight, (float)-rd);
                replayCamera.transform.SetPositionAndRotation(ReplayView.CameraSpot,
                    Quaternion.LookRotation(ReplayView.AimPoint(eye) - ReplayView.CameraSpot));
                camera.enabled = false;
                result.Views.Add(MeasureView("Replay camera (Middle start eye)", replayCamera, replayTarget, crowd));
                camera.enabled = true;
                UnityEngine.Object.DestroyImmediate(replayObject);
                replayCamera = null;

                // Each wide shot at its mid time.
                shotTarget = new RenderTexture(ShotWidth, ShotHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                double start = 0;
                for (int i = 0; i < StartShots.All.Length; i++)
                {
                    double mid = start + StartShots.All[i].Seconds / 2;
                    start += StartShots.All[i].Seconds;
                    StartShotPose pose = StartShots.At(mid);
                    Assert.AreEqual(i, pose.Shot);
                    camera.transform.position = new Vector3((float)pose.Eye.X, (float)pose.Eye.Y, (float)pose.Eye.Z);
                    camera.transform.LookAt(new Vector3((float)pose.Look.X, (float)pose.Look.Y, (float)pose.Look.Z));
                    camera.fieldOfView = (float)pose.FieldOfViewDegrees;
                    camera.targetTexture = target;
                    result.Views.Add(MeasureView("Wide shot " + (i + 1) + ": " + StartShots.All[i].Name, camera, target, crowd));
                    SavePng(camera, crowd, shotTarget, Path.Combine(Notes, "t116-shot-" + (i + 1) + ".png"));
                    camera.targetTexture = target;
                }

                // Memory, once, with the crowd on.
                crowd.Drawn = true;
                result.CrowdFanDataBytes = (long)crowd.FanCount * 96; // 6 Vector4s per fan (Crowd.cs FanData)
                var counted = new HashSet<Object>();
                for (int b = 0; b < Crowd.BodyFiles.GetLength(0); b++)
                    for (int l = 0; l < Crowd.BodyFiles.GetLength(1); l++)
                    {
                        CrowdBody body = crowd.BodyOf(b, l);
                        if (body == null) continue;
                        foreach (Object o in new Object[] { body.Mesh, body.Vat, body.Cards })
                            if (o != null && counted.Add(o)) result.CrowdAssetBytes += Profiler.GetRuntimeMemorySizeLong(o);
                    }
                result.CrowdOwnBytes = result.CrowdFanDataBytes + result.CrowdAssetBytes;
                result.TotalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
                result.GraphicsDriverBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();

                string json = JsonUtility.ToJson(result, true);
                string file = Path.Combine(Notes, "t116-crowd.json");
                File.WriteAllText(file, json);
                Debug.Log("Crowd measure: " + json);

                Assert.IsTrue(File.Exists(file));
                foreach (ViewCost v in result.Views) Assert.Greater(v.FansDrawn, 0, v.View + " drew no fans");
            }
            finally
            {
                UnityEditor.ShaderUtil.allowAsyncCompilation = asyncShaders;
                if (crowd != null) crowd.Drawn = true;
                if (camera != null) camera.targetTexture = null;
                if (replayCamera != null) Object.DestroyImmediate(replayCamera.gameObject);
                foreach (RenderTexture rt in new[] { target, replayTarget, shotTarget })
                    if (rt != null)
                    {
                        rt.Release();
                        Object.DestroyImmediate(rt);
                    }
                Object.DestroyImmediate(GameObject.Find("Rink"));
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
            }
        }

        static ViewCost MeasureRound(string name, Camera camera, RenderTexture target, Crowd crowd, AttackSide side, double u, float drift, float lookUp)
        {
            Breakaway.SkatePoint(side, u, out double x, out double distance);
            BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, drift, 0f);
            BreakawayView.LookUpTo(camera.transform, BreakawayView.ReplayLookTarget, lookUp);
            return MeasureView(name, camera, target, crowd);
        }

        static ViewCost MeasureView(string name, Camera camera, RenderTexture target, Crowd crowd)
        {
            var frames = new Dictionary<Mode, List<double>> { { Mode.NoCrowd, new List<double>() }, { Mode.Full, new List<double>() } };
            var rounds = new Dictionary<Mode, List<double>> { { Mode.NoCrowd, new List<double>() }, { Mode.Full, new List<double>() } };
            var modes = new[] { Mode.NoCrowd, Mode.Full };
            foreach (Mode m in modes) Time(camera, target, crowd, m, WarmUp, null);
            for (int round = 0; round < Rounds; round++)
                foreach (Mode m in modes)
                    rounds[m].Add(Time(camera, target, crowd, m, Frames, frames[m]));
            Time(camera, target, crowd, Mode.Full, 1, null);
            int fans = crowd.LastDrawnFans;
            var v = new ViewCost
            {
                View = name, FansDrawn = fans,
                NoCrowdMs = FrameBudget.Median(rounds[Mode.NoCrowd]), FullMs = FrameBudget.Median(rounds[Mode.Full]),
                NoCrowdSlowest1PctMs = FrameBudget.Slowest1Pct(frames[Mode.NoCrowd]), FullSlowest1PctMs = FrameBudget.Slowest1Pct(frames[Mode.Full])
            };
            for (int i = 0; i < Rounds; i++) v.CrowdCostRounds.Add(rounds[Mode.Full][i] - rounds[Mode.NoCrowd][i]);
            v.CrowdCostMs = FrameBudget.Median(v.CrowdCostRounds);
            v.CrowdCostSlowest1PctMs = v.FullSlowest1PctMs - v.NoCrowdSlowest1PctMs;
            v.DrawCallsNoCrowd = DrawCalls(camera, target, crowd, Mode.NoCrowd);
            v.DrawCallsFull = DrawCalls(camera, target, crowd, Mode.Full);
            return v;
        }

        /// <summary>Draw calls of one render (the Render profiler's counter), or -1 when the Editor reports none.</summary>
        static int DrawCalls(Camera camera, RenderTexture target, Crowd crowd, Mode mode)
        {
            crowd.Drawn = mode != Mode.NoCrowd;
            using (ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"))
            {
                camera.Render();
                WaitForGpu(target);
                return recorder.Valid && recorder.LastValue > 0 ? (int)recorder.LastValue : -1;
            }
        }

        static double Time(Camera camera, RenderTexture target, Crowd crowd, Mode mode, int frames, List<double> each)
        {
            crowd.Drawn = mode != Mode.NoCrowd;
            var clock = Stopwatch.StartNew();
            double last = 0;
            for (int i = 0; i < frames; i++)
            {
                camera.Render();
                WaitForGpu(target);
                double now = clock.Elapsed.TotalMilliseconds;
                if (i >= SettleFrames) each?.Add(now - last);
                last = now;
            }
            return clock.Elapsed.TotalMilliseconds / frames;
        }

        // Copied from FrameBudgetTool.WaitForGpu (internal): one pixel read back, checked.
        static void WaitForGpu(RenderTexture target)
        {
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(target, 0, 0, 1, 0, 1, 0, 1);
            request.WaitForCompletion();
            if (request.hasError) throw new InvalidOperationException("GPU read-back failed: the frame's time is unknown.");
        }

        static void SavePng(Camera camera, Crowd crowd, RenderTexture shotTarget, string path)
        {
            crowd.Drawn = true;
            camera.targetTexture = shotTarget;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = shotTarget;
            var texture = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
            }
        }
    }
}
