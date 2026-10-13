using System;
using System.IO;
using HockeyGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame.Editor
{
    /// <summary>
    /// A picture of Test Mode (#156): the shooter's view in the shooting window with the goalie set, crowd off, taken a long
    /// (600 s) wait into the window to show the camera stands still; and for comparison the same Shot in a Round's window (slow-motion
    /// look). Menu: Hockey Game/Picture Test/Test Mode Picture. Writes only when the folder hockey-wt/_notes exists next to the project's slot.
    /// </summary>
    public static class TestModePictureTool
    {
        const int Width = 1280, Height = 720;
        const string RinkScene = "Assets/Scenes/Rink.unity";

        [MenuItem("Hockey Game/Picture Test/Test Mode Picture")]
        public static void Take()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_notes"));
            SceneSetup[] scenes = EditorSceneManager.GetSceneManagerSetup();
            bool muted = EditorUtility.audioMasterMute;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            EditorUtility.audioMasterMute = true; // rule 6: crowd off and audio muted
            ShaderUtil.allowAsyncCompilation = false;
            RenderTexture target = null;
            Texture2D texture = null;
            PhysicsShotEffects effects = null;
            try
            {
                EditorSceneManager.OpenScene(RinkScene, OpenSceneMode.Single);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                RinkBuilder.Build(out Camera camera);
                Transform rink = GameObject.Find("Rink").transform;
                var crowd = rink.GetComponentInChildren<Crowd>();
                if (crowd != null) crowd.Drawn = false;
                var character = new GoalieCharacter(rink);
                if (!character.Present) throw new InvalidOperationException("The goalie model did not load.");
                var goalie = new GoalieView(rink, character);
                effects = new PhysicsShotEffects(rink, camera, new GoalRateTool.SystemRandom(1), goalie);
                Volume look = BreakawayView.MakeLookVolume(camera.transform);
                goalie.Show(true);
                camera.targetTexture = target;

                var settings = new RoundSettings();
                settings.ViewFieldOfViewDegrees = camera.fieldOfView;
                var flow = new ScreenFlow(new GoalRateTool.SystemRandom(7), new SaveData(), new GoalieCharacterReach(character, settings), settings);
                flow.OpenSettings();
                flow.StartTestMode();
                Round round = flow.Round;
                flow.Tick(600); // a long wait in the window: the camera must not move, nothing runs out
                if (round.Phase != RoundPhase.ShootingWindow) throw new InvalidOperationException("Test Mode is not in the window: " + round.Phase);
                Pose(round, camera, goalie, look, 600, settings);
                string after = Path.Combine(folder, "t156-after.png");
                if (Directory.Exists(folder)) File.WriteAllBytes(after, Render(camera, target, texture));

                // The same Shot in a Round's window (1.5 s in), for comparison: the slow-motion look is on.
                settings.TestMode = false;
                Pose(round, camera, goalie, look, 1.5, settings);
                if (Directory.Exists(folder)) File.WriteAllBytes(Path.Combine(folder, "t156-round-window.png"), Render(camera, target, texture));
                Debug.Log("Test Mode picture: " + after + " (side " + round.AttackSide + ", Set-up " + round.SetUp.Kind + ")");
            }
            finally
            {
                EditorUtility.audioMasterMute = muted;
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                RenderTexture.active = null;
                effects?.Dispose();
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                if (scenes.Length > 0 && Array.TrueForAll(scenes, s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(scenes);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }
        }

        // The shooter's camera and the goalie as BreakawayView poses them in the shooting window, t real seconds in.
        static void Pose(Round round, Camera camera, GoalieView goalie, Volume look, double t, RoundSettings settings)
        {
            const RoundPhase phase = RoundPhase.ShootingWindow;
            AttackSide side = round.AttackSide;
            Breakaway.SkatePoint(side, 1.0, out double x, out double distance);
            float drift = (float)Breakaway.Drift(side, settings.SlowDownSeconds, t, settings);
            BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, drift, 0f);
            look.weight = (float)Breakaway.Look(phase, t, settings);
            RinkPoint spot = SetUps.Approach(round.SetUp, side, phase, t, settings);
            RinkPoint puck = Breakaway.Puck(side, phase, t, settings);
            float facing = (float)SetUps.Facing(spot, puck);
            goalie.Stand(new Vector3((float)spot.X, (float)spot.Y, (float)spot.Z));
            goalie.Face(facing, facing);
            goalie.ShowBeforeTheShot(new Vector3((float)puck.X, (float)puck.Y, (float)puck.Z), (float)BeforeTheShot.Watching(phase, t, settings),
                (float)BeforeTheShot.Settled(phase, t, settings), settings);
            goalie.Live(2f);
            goalie.SetProgress(0f);
            goalie.ShowGaps(NetFaceGaps.None);
        }

        static byte[] Render(Camera camera, RenderTexture target, Texture2D texture)
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            return texture.EncodeToPNG();
        }
    }
}
