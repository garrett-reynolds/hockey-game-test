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
        public static void Take() => Run(false);

        /// <summary>
        /// The Test Mode screen (#157): after two Shots (a Goal try and a wide one) the next Shot's window with the TEST MODE label
        /// and the Score Box count drawn over it (the HUD is IMGUI at run time, so here it is drawn with two TextMeshes in a second
        /// camera at the HUD's places). Writes t157-after.png.
        /// </summary>
        [MenuItem("Hockey Game/Picture Test/Test Mode Screen Picture")]
        public static void TakeScreen() => Run(true);

        static void Run(bool screen)
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
                if (screen)
                {
                    foreach (double x in new[] { 50.0, -10.0 }) // two Shots: the count has something to show
                    {
                        flow.SubmitShot(x, x > 0 ? 90 : 50, 100, x < 0);
                        if (round.AwaitingWhistle) round.Whistle(x > 0 ? ShotResultKind.Goal : ShotResultKind.Miss);
                        flow.Tick(round.ResultHoldLength);
                    }
                    if (round.Phase != RoundPhase.ShootingWindow) throw new InvalidOperationException("Test Mode is not in the window: " + round.Phase);
                    Pose(round, camera, goalie, look, 2, settings);
                    string text = ArenaShow.From(flow).ScoreBox.Text;
                    if (!TestModeHud.LabelShown(flow)) throw new InvalidOperationException("The label is not shown in Test Mode.");
                    byte[] png = RenderWithHud(camera, target, texture, TestModeHud.LabelText, text);
                    string shown = Path.Combine(folder, "t157-after.png");
                    if (Directory.Exists(folder)) File.WriteAllBytes(shown, png);
                    Debug.Log("Test Mode screen picture: " + shown + " (\"" + text + "\")");
                    return;
                }
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

        // The HUD as RoundDriver.OnGUI places it: the label at the top left, the Score Box in its corner. Two TextMeshes and a quad
        // in layer 31, seen by an orthographic camera whose units are screen pixels.
        static byte[] RenderWithHud(Camera camera, RenderTexture target, Texture2D texture, string label, string score)
        {
            const int layer = 31;
            RenderTexture hudTarget = null;
            Texture2D hudTexture = null;
            var root = new GameObject("HudPicture");
            try
            {
                var hudCamera = new GameObject("HudCamera").AddComponent<Camera>();
                hudCamera.transform.SetParent(root.transform);
                hudCamera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
                hudCamera.orthographic = true;
                hudCamera.orthographicSize = Height / 2f;
                hudCamera.clearFlags = CameraClearFlags.SolidColor;
                hudCamera.backgroundColor = new Color(0, 0, 0, 0);
                hudCamera.cullingMask = 1 << layer;
                hudCamera.nearClipPlane = 0.1f;
                hudCamera.farClipPlane = 50f;
                hudTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
                hudCamera.targetTexture = hudTarget;
                hudCamera.enabled = false;

                Rect box = RoundDriver.ScoreBoxArea(Width);
                (Color back, Color text) = RoundDriver.ScoreBoxColours(null);
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.layer = layer;
                quad.transform.SetParent(root.transform);
                quad.transform.position = new Vector3(box.center.x - Width / 2f, Height / 2f - box.center.y, 1);
                quad.transform.localScale = new Vector3(box.width, box.height, 1);
                var mat = new Material(Shader.Find("Unlit/Color")) { color = new Color(back.r, back.g, back.b, 1) };
                quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
                MakeText(root.transform, font, layer, label, 1.2f, Color.white, TextAnchor.UpperLeft, new Vector3(10 - Width / 2f, Height / 2f - 10, 0));
                MakeText(root.transform, font, layer, score, 1.7f, text, TextAnchor.MiddleCenter, new Vector3(box.center.x - Width / 2f, Height / 2f - box.center.y, 0));

                int mask = camera.cullingMask;
                camera.cullingMask &= ~(1 << layer);
                camera.targetTexture = target;
                camera.Render();
                camera.cullingMask = mask;
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                RenderTexture.active = null;
                hudCamera.Render();
                RenderTexture.active = hudTarget;
                hudTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                hudTexture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                RenderTexture.active = null;
                Color[] scene = texture.GetPixels(), hud = hudTexture.GetPixels();
                for (int i = 0; i < scene.Length; i++) // the HUD over the scene, by its alpha
                    scene[i] = Color.Lerp(scene[i], new Color(hud[i].r, hud[i].g, hud[i].b, 1), hud[i].a);
                texture.SetPixels(scene);
                texture.Apply();
                UnityEngine.Object.DestroyImmediate(mat);
                return texture.EncodeToPNG();
            }
            finally
            {
                if (hudTexture != null) UnityEngine.Object.DestroyImmediate(hudTexture);
                if (hudTarget != null)
                {
                    hudTarget.Release();
                    UnityEngine.Object.DestroyImmediate(hudTarget);
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void MakeText(Transform parent, Font font, int layer, string words, float characterSize, Color colour, TextAnchor anchor, Vector3 position)
        {
            var go = new GameObject("HudText") { layer = layer };
            go.transform.SetParent(parent);
            go.transform.position = position;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 100;
            mesh.characterSize = characterSize;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Center;
            mesh.color = colour;
            mesh.text = words;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
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
