using System;
using System.IO;
using HockeyGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>
    /// Takes the roof banner pictures (#149): the shooter's view at 1920 x 1080 with the crowd off and the Editor muted.
    /// Writes PictureTest/Latest/Banners/shooter.png (PictureTest/Latest is git-ignored: tool output stays out of the repo, #152).
    /// The rink is built in Rink.unity and the Editor's scenes are put back after, so they are not left dirty (#152).
    /// </summary>
    public static class BannerPictureTool
    {
        public const int Width = 1920, Height = 1080;
        public const string Folder = "PictureTest/Latest/Banners";
        const string RinkScene = "Assets/Scenes/Rink.unity";

        /// <summary>
        /// Opens Rink.unity on its own (the rink is lit by the scene's own settings, so the pictures match the game's) and puts the
        /// Editor's scenes back on Dispose, loaded fresh from disk: the rink the tool built, and the dirty mark it left, go. It
        /// refuses to start when an open scene has unsaved changes, because putting the scenes back would throw them away.
        /// </summary>
        sealed class ScratchScene : IDisposable
        {
            readonly SceneSetup[] _setup;

            public ScratchScene()
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("An open scene has unsaved changes: save it first, the tool would throw them away.");
                _setup = EditorSceneManager.GetSceneManagerSetup();
                EditorSceneManager.OpenScene(RinkScene, OpenSceneMode.Single);
            }

            public void Dispose()
            {
                if (_setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(_setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [MenuItem("Hockey Game/Picture Test/Roof banner pictures")]
        public static void Take()
        {
            bool muted = EditorUtility.audioMasterMute;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            RenderTexture target = null;
            Camera camera = null;
            Crowd crowd = null;
            ScratchScene scratch = null;
            try
            {
                EditorUtility.audioMasterMute = true;
                ShaderUtil.allowAsyncCompilation = false;
                scratch = new ScratchScene();
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                crowd = RinkBuilder.Build(out camera).GetComponentInChildren<Crowd>();
                if (crowd != null) crowd.Drawn = false;
                camera.targetTexture = target;
                Directory.CreateDirectory(Folder);

                Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, 0f, 0f);
                Save(camera, target, "shooter");
            }
            finally
            {
                EditorUtility.audioMasterMute = muted;
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                if (crowd != null) crowd.Drawn = true;
                if (camera != null) camera.targetTexture = null;
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                scratch?.Dispose();
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
        }

        /// <summary>
        /// A cheap frame-time A/B for the banners (FrameBudgetTool needs Goals the build cannot make, #134): the shooter's view at
        /// 1920 x 1080 with the crowd ON, the banner row switched on and off in turn, median ms per frame over 7 rounds of 60 frames,
        /// each frame waited for on the GPU (the banners' wires switch with them). Writes PictureTest/Latest/Banners/frametime.txt.
        /// </summary>
        [MenuItem("Hockey Game/Picture Test/Roof banner frame time")]
        public static void FrameTime()
        {
            bool muted = EditorUtility.audioMasterMute;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            RenderTexture target = null;
            Camera camera = null;
            ScratchScene scratch = null;
            try
            {
                EditorUtility.audioMasterMute = true;
                ShaderUtil.allowAsyncCompilation = false;
                scratch = new ScratchScene();
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Transform rink = RinkBuilder.Build(out camera);
                Transform row = rink.Find(RoofBanners.Part), wires = rink.Find(RoofBanners.WiresPart);
                if (row == null) throw new System.InvalidOperationException("No roof banners to time.");
                camera.targetTexture = target;
                Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, 0f, 0f);

                var on = new System.Collections.Generic.List<double>();
                var off = new System.Collections.Generic.List<double>();
                Time(camera, target, row, wires, true, 30); // warm up
                for (int round = 0; round < 7; round++)
                {
                    on.Add(Time(camera, target, row, wires, true, 60));
                    off.Add(Time(camera, target, row, wires, false, 60));
                }
                on.Sort();
                off.Sort();
                string text = "banners on  (median of 7 rounds): " + on[3].ToString("0.000") + " ms\r\n" +
                              "banners off (median of 7 rounds): " + off[3].ToString("0.000") + " ms\r\n" +
                              "banners on rounds: " + string.Join(", ", on.ConvertAll(v => v.ToString("0.000"))) + "\r\n" +
                              "banners off rounds: " + string.Join(", ", off.ConvertAll(v => v.ToString("0.000"))) + "\r\n" +
                              "banner cost (on - off): " + (on[3] - off[3]).ToString("0.000") + " ms, GPU " + SystemInfo.graphicsDeviceName + "\r\n";
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "frametime.txt"), text);
                Debug.Log(text);
            }
            finally
            {
                EditorUtility.audioMasterMute = muted;
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                if (camera != null) camera.targetTexture = null;
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                scratch?.Dispose();
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
        }

        static double Time(Camera camera, RenderTexture target, Transform row, Transform wires, bool bannersOn, int frames)
        {
            row.gameObject.SetActive(bannersOn);
            if (wires != null) wires.gameObject.SetActive(bannersOn);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                camera.Render();
                FrameBudgetTool.WaitForGpu(target);
            }
            return clock.Elapsed.TotalMilliseconds / frames;
        }

        static void Save(Camera camera, RenderTexture target, string name)
        {
            for (int i = 0; i < 3; i++) camera.Render(); // a few frames: the first after a move can still be settling
            var old = RenderTexture.active;
            RenderTexture.active = target;
            var picture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            picture.Apply();
            RenderTexture.active = old;
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), picture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(picture);
            Debug.Log("Roof banner picture: " + Path.Combine(Folder, name + ".png"));
        }
    }
}
