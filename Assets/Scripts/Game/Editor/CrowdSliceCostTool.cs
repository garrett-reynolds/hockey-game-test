using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HockeyGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>Paired Editor wall upper bounds, not GPU-only time or live FPS. Default crowd grouping stays unchanged.</summary>
    public static class CrowdSliceCostTool
    {
        public const int Width = 1920, Height = 1080, Rounds = 5, Frames = 30, SettleFrames = 2;
        static readonly int[] Sectors = { 4, 8, 16, 32 };
        [Serializable] public sealed class RoundCost
        {
            public int Round, Order;
            public bool CrowdFirst;
            public double OnMs, OffMs, CostMs;
        }
        [Serializable] public sealed class Variant
        {
            public int Sectors, TotalSlices, DrawCalls, Fans, DifferentPixels, MaxChannelError;
            public bool ImageMatches;
            public double MedianCostMs;
            public string Image;
            public List<RoundCost> Rounds = new List<RoundCost>();
        }
        [Serializable] public sealed class View
        {
            public string Name, ReferenceImage;
            public int ReferenceRepeatDifferentPixels, ReferenceRepeatMaxChannelError;
            public List<Variant> Variants = new List<Variant>();
        }
        [Serializable] public sealed class Result
        {
            public string Timing = "Editor wall upper bounds: render call plus checked GPU readback. Not GPU-only time or live FPS. DrawCalls counts submitted slices, not hardware draws.";
            public int Width = CrowdSliceCostTool.Width, Height = CrowdSliceCostTool.Height,
                Rounds = CrowdSliceCostTool.Rounds, Frames = CrowdSliceCostTool.Frames, SettleFrames = CrowdSliceCostTool.SettleFrames;
            public List<View> Views = new List<View>();
        }
        public static void RunFromCommandLine()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-crowdSliceCostResult");
                if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Supply -crowdSliceCostResult file.json");
                string file = Path.GetFullPath(args[index + 1]);
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                string images = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "-images");
                Directory.CreateDirectory(images);
                File.WriteAllText(file, JsonUtility.ToJson(Measure(images), true));
                EditorApplication.Exit(0);
            }
            catch (Exception error) { UnityEngine.Debug.LogException(error); EditorApplication.Exit(1); }
        }
        public static Result Measure(string images)
        {
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            RenderTexture target = null;
            Camera camera = null;
            Crowd crowd = null;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Rink.unity", OpenSceneMode.Single);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                RinkBuilder.Build(out camera);
                crowd = GameObject.Find("Rink").GetComponentInChildren<Crowd>();
                if (crowd == null) throw new InvalidOperationException("Crowd bodies or shaders are missing.");
                camera.targetTexture = target;
                crowd.FarCards = false;
                crowd.Show(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.ResultHold, Result = ArenaResult.Goal, ShotNumber = 1 }), 0.0);
                var result = new Result();
                Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, 0f, 0f);
                result.Views.Add(MeasureView("shooter", camera, target, crowd, images));
                camera.transform.SetPositionAndRotation(new Vector3(0, 4, 1), Quaternion.Euler(8, 180, 0));
                result.Views.Add(MeasureView("up-ice", camera, target, crowd, images));
                return result;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                if (crowd != null) { crowd.Regroup(Crowd.DefaultSectors); crowd.Drawn = true; crowd.FarCards = false; }
                if (camera != null) camera.targetTexture = null;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            }
        }
        static View MeasureView(string name, Camera camera, RenderTexture target, Crowd crowd, string images)
        {
            var view = new View { Name = name, ReferenceImage = Path.Combine(images, name + "-reference-16.png") };
            foreach (int sectors in Sectors)
            {
                crowd.Regroup(sectors);
                crowd.Drawn = true;
                for (int i = 0; i < 10; i++) Render(camera, target);
                view.Variants.Add(new Variant { Sectors = sectors, TotalSlices = crowd.SliceCount });
            }
            crowd.Regroup(16);
            Color32[] reference = Capture(camera, target, view.ReferenceImage);
            Color32[] repeat = Capture(camera, target, Path.Combine(images, name + "-reference-repeat-16.png"));
            Difference(reference, repeat, out view.ReferenceRepeatDifferentPixels, out view.ReferenceRepeatMaxChannelError);
            foreach (Variant variant in view.Variants)
            {
                crowd.Regroup(variant.Sectors);
                variant.Image = Path.Combine(images, name + "-" + variant.Sectors + ".png");
                Color32[] pixels = Capture(camera, target, variant.Image);
                Difference(reference, pixels, out variant.DifferentPixels, out variant.MaxChannelError);
                variant.ImageMatches = variant.DifferentPixels == 0 && view.ReferenceRepeatDifferentPixels == 0;
                variant.DrawCalls = crowd.LastDrawCalls;
                variant.Fans = crowd.LastDrawnFans;
            }
            for (int round = 0; round < Rounds; round++)
                for (int order = 0; order < Sectors.Length; order++)
                {
                    Variant variant = view.Variants[(round + order) % Sectors.Length];
                    crowd.Regroup(variant.Sectors);
                    bool first = round % 2 == 0;
                    var pair = new RoundCost { Round = round, Order = order, CrowdFirst = first };
                    if (first) { pair.OnMs = Time(camera, target, crowd, true); pair.OffMs = Time(camera, target, crowd, false); }
                    else { pair.OffMs = Time(camera, target, crowd, false); pair.OnMs = Time(camera, target, crowd, true); }
                    pair.CostMs = pair.OnMs - pair.OffMs;
                    variant.Rounds.Add(pair);
                }
            foreach (Variant variant in view.Variants)
            {
                var costs = new List<double>();
                foreach (RoundCost pair in variant.Rounds) costs.Add(pair.CostMs);
                variant.MedianCostMs = FrameBudget.Median(costs);
            }
            crowd.Drawn = true;
            return view;
        }
        static double Time(Camera camera, RenderTexture target, Crowd crowd, bool on)
        {
            crowd.Drawn = on;
            for (int i = 0; i < SettleFrames; i++) Render(camera, target);
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < Frames; i++) Render(camera, target);
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / Frames;
        }
        static void Render(Camera camera, RenderTexture target) { camera.Render(); FrameBudgetTool.WaitForGpu(target); }
        static Color32[] Capture(Camera camera, RenderTexture target, string file)
        {
            Render(camera, target);
            RenderTexture previous = RenderTexture.active;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply(false);
                File.WriteAllBytes(file, texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        }
        static void Difference(Color32[] expected, Color32[] actual, out int changed, out int maxError)
        {
            changed = 0; maxError = 0;
            if (actual.Length != expected.Length) throw new InvalidOperationException("Image sizes differ.");
            for (int i = 0; i < expected.Length; i++)
            {
                int error = Math.Max(Math.Max(Math.Abs(expected[i].r - actual[i].r), Math.Abs(expected[i].g - actual[i].g)),
                    Math.Max(Math.Abs(expected[i].b - actual[i].b), Math.Abs(expected[i].a - actual[i].a)));
                if (error > 0) changed++;
                maxError = Math.Max(maxError, error);
            }
        }
    }
}
