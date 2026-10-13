using System;
using System.Collections.Generic;
using System.IO;
using HockeyGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HockeyGame.Editor
{
    /// <summary>
    /// The picture test (#84, #85): renders the shooting-window view from fixed points, each with Easy mode on and off, and
    /// measures the Calm Area's contrast and edges and the Gaps' stand-out from the gear (PictureMeasures). It fails when any
    /// measure is more than 10% worse than the baseline (PictureCompare). Run it on every art change.
    /// <para>
    /// Each view is posed from pure inputs, never from a running clock or random numbers: a fixed Set-up and Attack Side,
    /// WindowSeconds into the shooting window (the goalie is set; a Late Settle has settled), no stride bob or roll, the
    /// goalie's Stance Life clock at LifeSeconds, at Width x Height. So an unchanged rink renders the same pixels each run
    /// on the same machine. It opens Assets/Scenes/Rink.unity and builds the rink as the game does, then leaves it unsaved.
    /// </para>
    /// <para>
    /// The Calm Area is the ice from x -4.27 to 4.27 m and from the crease front (z -1.9) to the end boards, and over the end
    /// boards a strip as wide, up to the top of the view. Its measures leave out the goalie and the Easy mode glow, so they
    /// measure the arena alone. Gap stand-out is measured for each Gap (a group of touching Gap cells) on its own: its pixels
    /// (less the gear) against the gear's pixels on the Net Face. An empty Calm Area or Gap is an error. Each run writes the pictures, a picture of the masks for each view, measures.json and result.json to
    /// PictureTest/Latest; the baseline (pictures and measures.json) lives in PictureTest/Baseline.
    /// </para>
    /// From the menu (Hockey Game/Picture Test/...) or batch mode (no -nographics: it must render):
    /// Unity -batchmode -projectPath ... -executeMethod HockeyGame.Editor.PictureTestTool.RunFromCommandLine
    /// [-pictureResult file] -logFile ... exits 0 on pass, 2 on fail, 1 on an error.
    /// TakeBaselineFromCommandLine re-takes the baseline: only when a ticket says so.
    /// </summary>
    public static class PictureTestTool
    {
        public const int Width = 1280, Height = 720;
        /// <summary>Seconds into the shooting window of every view: after a Late Settle has settled (RoundSettings.LateSettleSeconds).</summary>
        public const double WindowSeconds = 1.5;
        /// <summary>
        /// Metres every eye sits right of its skate-in point. The arena is mirror-symmetric, so from the middle (x = 0) pairs of
        /// lights and objects sit at exactly the same distance; the renderer then takes them in any order, which changes some
        /// pixels by one step from run to run. A centimetre breaks every such tie and does not show.
        /// </summary>
        public const float EyeNudge = 0.01f;
        /// <summary>The goalie's Stance Life clock in every view, fixed.</summary>
        public const float LifeSeconds = 2f;

        public const float CalmAreaHalfWidth = (float)RinkLayout.CalmAreaHalfWidth; // half the back of the trapezoid (8.53 m)
        public const float CalmAreaFrontZ = (float)RinkLayout.CalmAreaFrontZ;       // the crease front
        const float CalmAreaMaxClimb = 200f;          // metres: how far up the top of the view is looked for

        public const string BaselineFolder = "PictureTest/Baseline";
        public const string RunFolder = "PictureTest/Latest";
        public const string MeasuresFile = "measures.json";
        public const string ResultFile = "result.json";
        const string RinkScene = "Assets/Scenes/Rink.unity";

        public const int PassExit = 0, ErrorExit = 1, FailExit = 2;

        const int GoalieLayer = 30, GlowLayer = 31; // unnamed layers, used only in the picture test's own scene
        static readonly Color32 ClearColour = new Color32(255, 0, 255, 255);

        /// <summary>A fixed point the shooting-window view is rendered from.</summary>
        public readonly struct PicturePoint
        {
            public readonly string Name;
            public readonly AttackSide Side;
            public readonly SetUp SetUp;
            public PicturePoint(string name, AttackSide side, SetUp setUp) { Name = name; Side = side; SetUp = setUp; }
        }

        public static readonly PicturePoint[] Points =
        {
            new PicturePoint("middle-clean", AttackSide.Middle, SetUp.Clean),
            new PicturePoint("left-cheat", AttackSide.Left, SetUp.Cheat(Post.Left)),
            new PicturePoint("middle-depth", AttackSide.Middle, SetUp.Depth),
            new PicturePoint("right-late-settle", AttackSide.Right, SetUp.LateSettle(Post.Left)),
        };

        /// <summary>A picture-only skate-push view (#126): a move of MetresSigned along X, shown U (0 to 1) of the way through its time,
        /// or (Reaction) a Glove save far to +X at set 0.25. Not in the measures or the comparison.</summary>
        public readonly struct PushPicture
        {
            public readonly string Name;
            public readonly double MetresSigned;
            public readonly double U;
            public readonly bool Reaction;
            public PushPicture(string name, double metresSigned, double u, bool reaction) { Name = name; MetresSigned = metresSigned; U = u; Reaction = reaction; }
        }

        /// <summary>Last in the list is the reaction, so the others never see a reaction's leftovers.</summary>
        public static readonly PushPicture[] PushViews =
        {
            new PushPicture("push-shuffle", -0.2, 0.15, false),
            new PushPicture("push-tpush-lead", -0.5, 0.2, false),
            new PushPicture("push-tpush-square", -0.5, 0.8, false),
            new PushPicture("push-reaction", 0, 0, true),
        };

        const float PushCameraMetres = 2.5f, PushCameraHeight = 0.55f, PushLookHeight = 0.5f, PushFieldOfView = 50f, PushReactionSet = 0.25f;
        // Far to the glove side (+X): the Shot comes from 3 m out to that side at the net's corner, so the arm alone cannot reach and he
        // slides (0.15 m at set 0.25, 0.6 m at full). From the middle the same aim needs no slide.
        const float PushReactionFromX = 3f;
        const double PushReactionX = 100, PushReactionY = 60;

        public static string ViewName(PicturePoint point, bool easyMode) => point.Name + (easyMode ? "-easy-on" : "-easy-off");

        // ---- Entry points ----

        [MenuItem("Hockey Game/Picture Test/Run")]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneSetup[] scenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                PictureTestResult result = Run(null);
                Log(result);
            }
            finally
            {
                Restore(scenes);
            }
        }

        [MenuItem("Hockey Game/Picture Test/Take Baseline")]
        public static void TakeBaselineFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Take the picture test baseline",
                    "Replace PictureTest/Baseline with today's rink? Only take it before art changes, or when a ticket says so.",
                    "Replace", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneSetup[] scenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                TakeBaseline();
            }
            finally
            {
                Restore(scenes);
            }
        }

        /// <summary>Batch mode: runs the test against the baseline, writes result.json (or -pictureResult file), exits 0 pass, 2 fail, 1 error.</summary>
        public static void RunFromCommandLine()
        {
            int exit;
            try
            {
                PictureTestResult result = Run(Arg("-pictureResult"));
                Log(result);
                exit = result.Pass ? PassExit : FailExit;
            }
            catch (Exception e)
            {
                Debug.LogError("Picture test: ERROR " + e);
                exit = ErrorExit;
            }
            EditorApplication.Exit(exit);
        }

        /// <summary>Batch mode: re-takes the baseline from today's rink. Only before art changes, or when a ticket says so.</summary>
        public static void TakeBaselineFromCommandLine()
        {
            int exit = PassExit;
            try
            {
                TakeBaseline();
            }
            catch (Exception e)
            {
                Debug.LogError("Picture test: ERROR " + e);
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

        static void Log(PictureTestResult result)
        {
            string text = PictureCompare.Text(result);
            if (result.Pass) Debug.Log(text);
            else Debug.LogWarning(text);
        }

        static void Restore(SceneSetup[] scenes)
        {
            if (scenes.Length > 0 && Array.TrueForAll(scenes, s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(scenes);
            else
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        // ---- Run and baseline ----

        /// <summary>Renders and measures every view into PictureTest/Latest, compares with the baseline and writes the result file (null: PictureTest/Latest/result.json).</summary>
        public static PictureTestResult Run(string resultFile)
        {
            // Through the same JSON as the baseline's numbers, so unchanged numbers compare exactly equal.
            PictureMeasureSet now = JsonUtility.FromJson<PictureMeasureSet>(JsonUtility.ToJson(RenderAll(RunFolder)));
            PictureTestResult result;
            string baselineFile = Path.Combine(BaselineFolder, MeasuresFile);
            if (File.Exists(baselineFile))
            {
                result = PictureCompare.Compare(JsonUtility.FromJson<PictureMeasureSet>(File.ReadAllText(baselineFile)), now);
                foreach (ViewMeasures view in now.Views)
                {
                    string before = Path.Combine(BaselineFolder, view.View + ".png"), after = Path.Combine(RunFolder, view.View + ".png");
                    if (!File.Exists(before))
                    {
                        PictureCompare.AddProblem(result, "Baseline picture " + before + " is missing.");
                        continue;
                    }
                    result.Pictures.Add(new PictureChange
                    {
                        View = view.View,
                        PixelsChangedPercent = 100.0 * PictureMeasures.ChangedShare(ReadPng(before), ReadPng(after))
                    });
                }
            }
            else
            {
                result = new PictureTestResult();
                result.Problems.Add("No baseline at " + baselineFile + ".");
            }
            result.BaselineFolder = BaselineFolder;
            string path = string.IsNullOrEmpty(resultFile) ? Path.Combine(RunFolder, ResultFile) : resultFile;
            string folder = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("Picture test: result written to " + Path.GetFullPath(path));
            return result;
        }

        /// <summary>Renders every view and saves its pictures and measures as the baseline.</summary>
        public static void TakeBaseline()
        {
            PictureMeasureSet now = RenderAll(RunFolder);
            Directory.CreateDirectory(BaselineFolder);
            foreach (string old in Directory.GetFiles(BaselineFolder)) File.Delete(old);
            foreach (ViewMeasures view in now.Views)
                File.Copy(Path.Combine(RunFolder, view.View + ".png"), Path.Combine(BaselineFolder, view.View + ".png"));
            File.Copy(Path.Combine(RunFolder, MeasuresFile), Path.Combine(BaselineFolder, MeasuresFile));
            Debug.Log("Picture test: baseline taken in " + Path.GetFullPath(BaselineFolder));
        }

        static Color32[] ReadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadImage(File.ReadAllBytes(path));
                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // ---- Rendering ----

        /// <summary>The rink as the game builds it, with the goalie and the slow-down look, posed by hand for each view.</summary>
        sealed class Rig : IDisposable
        {
            readonly PhysicsShotEffects _effects;
            public readonly Camera Camera;
            public readonly GoalieView Goalie;
            public readonly RoundSettings Settings = new RoundSettings();
            readonly IGoalieReach _reach;
            readonly Volume _look;
            readonly RibbonBoards _ribbons;
            readonly Crowd _crowd;
            readonly VideoBoards _boards;

            public Rig(RenderTexture target)
            {
                RinkBuilder.Build(out Camera);
                Transform rink = GameObject.Find("Rink").transform;
                _ribbons = rink.GetComponentInChildren<RibbonBoards>();
                _crowd = rink.GetComponentInChildren<Crowd>();
                _boards = rink.GetComponentInChildren<VideoBoards>();
                if (_crowd == null) throw new InvalidOperationException("The 3D crowd did not load: the picture test needs it.");
                var character = new GoalieCharacter(rink);
                if (!character.Present) throw new InvalidOperationException("The goalie model did not load: the picture test needs it.");
                Goalie = new GoalieView(rink, character);
                _effects =new PhysicsShotEffects(rink, Camera, new GoalRateTool.SystemRandom(1), Goalie); // the goal light and netting, as the game has them
                _reach = new GoalieCharacterReach(character, Settings);
                _look = BreakawayView.MakeLookVolume(Camera.transform);
                SetLayer(character.Root.transform, GoalieLayer);
                SetLayer(Goalie.GapGlow.transform, GlowLayer);
                Goalie.Show(true);
                Camera.targetTexture = target; // before any projection: screen points are then picture pixels
            }

            public void Dispose() => _effects?.Dispose();

            static void SetLayer(Transform t, int layer)
            {
                t.gameObject.layer = layer;
                foreach (Transform child in t) SetLayer(child, layer);
            }

            /// <summary>Poses the camera, the look and the goalie WindowSeconds into the shooting window from this point; returns its Gaps.</summary>
            public NetFaceGaps Pose(PicturePoint point)
            {
                const RoundPhase phase = RoundPhase.ShootingWindow;
                const double t = WindowSeconds;
                Breakaway.SkatePoint(point.Side, 1.0, out double x, out double distance);
                float drift = (float)Breakaway.Drift(point.Side, Settings.SlowDownSeconds, t, Settings);
                BreakawayView.PlaceShooterCamera(Camera.transform, (float)x + EyeNudge, (float)distance, drift, 0f);
                _look.weight = (float)Breakaway.LookAmount(Breakaway.TimeScale(phase, t));

                // As the Round asks it (Round.GapsNow); it poses the goalie to measure, so pose him after.
                NetFaceGaps gaps = _reach.Gaps(point.SetUp, point.Side, t, Breakaway.ShotStart(point.Side, t, Settings), 0, NetFaceGaps.Cells);

                RinkPoint spot = SetUps.Approach(point.SetUp, point.Side, phase, t, Settings);
                RinkPoint puck = Breakaway.Puck(point.Side, phase, t, Settings);
                float facing = (float)SetUps.Facing(spot, puck);
                Goalie.Stand(ToVector(spot));
                Goalie.Face(facing, facing);
                Goalie.ShowBeforeTheShot(ToVector(puck), (float)BeforeTheShot.Watching(phase, t, Settings),
                    (float)BeforeTheShot.Settled(phase, t, Settings), Settings);
                Goalie.Live(LifeSeconds);
                Goalie.SetProgress((float)Breakaway.GoalieSet(phase, t, false, 0));

                // The arena as the Arena Show has it in the shooting window (the ads' clock fixed at 0).
                ArenaState arena = ArenaShow.At(new ArenaShowInput { Phase = phase, PhaseSeconds = t, ShotNumber = 1 });
                _ribbons.Show(arena, 0.0);
                _boards.Show(arena, 0.0); // the Video Board is one dim still colour in the shooting window
                _crowd.Show(arena, 0.0); // the Calm Area's fans still and dim
                return gaps;
            }

            /// <summary>Poses the close push camera and the goalie for a push view (a function of the push shown and the set).</summary>
            public void PosePush(PushPicture p)
            {
                const RoundPhase phase = RoundPhase.ShootingWindow;
                const double t = WindowSeconds;
                Goalie.ShowBeforeTheShot(Vector3.zero, 0f, 0f, null);
                Goalie.ShowGaps(NetFaceGaps.None);
                // (Task 4 seam: reset the push shown here, then Goalie.ShowPush(...) after Live below.)
                RinkPoint spot = SetUps.Approach(SetUp.Clean, AttackSide.Middle, phase, t, Settings);
                RinkPoint puck = Breakaway.Puck(AttackSide.Middle, phase, t, Settings);
                float toShooter = puck.Z >= spot.Z ? 1f : -1f;
                float x = 0f;
                if (!p.Reaction)
                {
                    var move = new SkatePush(p.MetresSigned, Settings);
                    x = (float)move.Position(p.U * move.TotalSeconds);
                }
                var at = new Vector3(x, (float)spot.Y, (float)spot.Z);
                Goalie.Stand(at);
                Goalie.Face(0f, 0f);
                Goalie.Live(LifeSeconds);
                Transform cam = Camera.transform;
                Camera.fieldOfView = PushFieldOfView;
                cam.position = new Vector3(at.x, at.y + PushCameraHeight, at.z + toShooter * PushCameraMetres);
                cam.LookAt(new Vector3(at.x, at.y + PushLookHeight, at.z));
                if (!p.Reaction) return;
                var result = new ShotResult(ShotResultKind.Save, true, PushReactionX, PushReactionY, 100, false);
                Goalie.React(result, new Vector3(PushReactionFromX, 1.15f, (float)puck.Z), SaveReaction.Glove, double.PositiveInfinity, Settings.LateMarginLimitSeconds);
                Goalie.SetProgress(PushReactionSet);
            }

            static Vector3 ToVector(RinkPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
        }

        static PictureMeasureSet RenderAll(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (string old in Directory.GetFiles(folder, "*.png")) File.Delete(old);
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false; // never a placeholder shader in a picture
            RenderTexture target = null;
            Texture2D texture = null;
            try
            {
                EditorSceneManager.OpenScene(RinkScene, OpenSceneMode.Single); // unloads unused objects: make the target after it
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                using var rig = new Rig(target);
                var set = new PictureMeasureSet { Width = Width, Height = Height };
                foreach (PicturePoint point in Points)
                {
                    NetFaceGaps gaps = rig.Pose(point);
                    if (!point.SetUp.IsMistake)
                    {
                        // A Clean Set-up has no Gaps to measure: only its picture, to set beside a Set-up Mistake's (not in the measures).
                        rig.Goalie.ShowGaps(NetFaceGaps.None);
                        WritePng(texture, Capture(rig.Camera, target, texture, ~0, true), Path.Combine(folder, ViewName(point, false) + ".png"));
                        continue;
                    }
                    if (gaps.Count == 0) throw new InvalidOperationException("View " + point.Name + " has no Gaps: pick a point with Gaps.");
                    foreach (bool easyMode in new[] { true, false })
                    {
                        rig.Goalie.ShowGaps(easyMode ? gaps : NetFaceGaps.None);
                        set.Views.Add(Measure(rig, target, texture, ViewName(point, easyMode), gaps, folder));
                    }
                }
                foreach (PushPicture push in PushViews)
                {
                    rig.PosePush(push); // picture only, like middle-clean: not in the measures or the comparison
                    WritePng(texture, Capture(rig.Camera, target, texture, ~0, true), Path.Combine(folder, push.Name + ".png"));
                }
                File.WriteAllText(Path.Combine(folder, MeasuresFile), JsonUtility.ToJson(set, true));
                return set;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                RenderTexture.active = null;
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        static ViewMeasures Measure(Rig rig, RenderTexture target, Texture2D texture, string view, NetFaceGaps gaps, string folder)
        {
            Color32[] picture = Capture(rig.Camera, target, texture, ~0, true);
            Color32[] again = Capture(rig.Camera, target, texture, ~0, true);
            if (PictureMeasures.ChangedShare(picture, again) > 0)
                throw new InvalidOperationException("View " + view + " rendered differently twice in a row: the picture test cannot compare it.");
            bool[] goalie = PictureMeasures.Differs(Capture(rig.Camera, target, texture, 1 << GoalieLayer, false), ClearColour);
            bool[] glow = PictureMeasures.Differs(Capture(rig.Camera, target, texture, 1 << GlowLayer, false), ClearColour);

            // The glow draws over the goalie (GoalieView: no depth test while he is set), so the gear seen is the goalie less the glow.
            bool[] gearSeen = PictureMeasures.Without(goalie, glow);
            bool[] inFront = (bool[])goalie.Clone();
            PictureMeasures.AddTo(inFront, glow);
            bool[] calm = PictureMeasures.Without(CalmArea(rig.Camera, view), inFront);
            bool[] gear = PictureMeasures.And(gearSeen, PictureMeasures.Polygon(NetFaceCorners(rig.Camera, 0, 0, 100, 100), Width, Height));
            if (PictureMeasures.InnerPixels(calm, Width, Height) == 0)
                throw new InvalidOperationException("View " + view + " shows no Calm Area: its contrast and edges cannot be measured.");

            float[] luma = PictureMeasures.Luma(picture);
            var measures = new ViewMeasures
            {
                View = view,
                Contrast = PictureMeasures.Contrast(luma, calm),
                Edges = PictureMeasures.Edges(luma, calm, Width, Height)
            };

            // Each Gap on its own: its cells' pixels (less the gear seen) against the gear's pixels on the Net Face.
            var isGap = new bool[NetFaceGaps.Cells];
            for (int column = 0; column < NetFaceGaps.Columns; column++)
            for (int row = 0; row < NetFaceGaps.Rows; row++)
                isGap[NetFaceGaps.Index(column, row)] = gaps.IsGap(column, row);
            var allGapPixels = new bool[Width * Height];
            foreach (List<int> cells in PictureMeasures.Groups(isGap, NetFaceGaps.Columns, NetFaceGaps.Rows))
            {
                string name = PictureMeasures.GapName(cells, NetFaceGaps.Columns);
                var gapCells = new bool[Width * Height];
                foreach (int cell in cells)
                {
                    NetFaceGaps.Cell(cell % NetFaceGaps.Columns, cell / NetFaceGaps.Columns, out double minX, out double minY, out double maxX, out double maxY);
                    PictureMeasures.AddTo(gapCells, PictureMeasures.Polygon(NetFaceCorners(rig.Camera, minX, minY, maxX, maxY), Width, Height));
                }
                bool[] gapPixels = PictureMeasures.Without(gapCells, gearSeen);
                double standOut = PictureMeasures.GapStandOut(luma, gapPixels, gear);
                if (double.IsNaN(standOut))
                    throw new InvalidOperationException("View " + view + ": " + name + " shows no pixels, or there are no gear pixels: its stand-out cannot be measured.");
                measures.Gaps.Add(new GapMeasure { Gap = name, Cells = cells.Count, StandOut = standOut });
                PictureMeasures.AddTo(allGapPixels, gapPixels);
            }
            if (measures.Gaps.Count == 0)
                throw new InvalidOperationException("View " + view + " has no Gaps: pick a point with Gaps.");

            WritePng(texture, picture, Path.Combine(folder, view + ".png"));
            WritePng(texture, Overlay(picture, calm, gear, allGapPixels), Path.Combine(folder, view + "-masks.png"));
            return measures;
        }

        /// <summary>Renders the camera into the target and reads it back (rows from the bottom). Only the layers in cullingMask
        /// draw; with post-processing off on the clear colour, so a mask render shows exactly what those layers drew.</summary>
        static Color32[] Capture(Camera camera, RenderTexture target, Texture2D texture, int cullingMask, bool asSeen)
        {
            int mask = camera.cullingMask;
            CameraClearFlags flags = camera.clearFlags;
            Color background = camera.backgroundColor;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            bool post = data.renderPostProcessing;
            try
            {
                camera.cullingMask = cullingMask;
                if (!asSeen)
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = ClearColour;
                    data.renderPostProcessing = false;
                }
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                return texture.GetPixels32();
            }
            finally
            {
                RenderTexture.active = null;
                camera.cullingMask = mask;
                camera.clearFlags = flags;
                camera.backgroundColor = background;
                data.renderPostProcessing = post;
            }
        }

        /// <summary>
        /// The Calm Area on screen: its patch of ice (x within CalmAreaHalfWidth, from the crease front to the end boards), and
        /// over the end boards a strip as wide as the back of the trapezoid, from the ice up to the top of the view.
        /// </summary>
        static bool[] CalmArea(Camera camera, string view)
        {
            var ice = new List<Vector2>();
            var strip = new List<Vector2>();
            foreach (float x in new[] { -CalmAreaHalfWidth, CalmAreaHalfWidth })
            {
                ice.Add(OnScreen(camera, new Vector3(x, 0f, CalmAreaFrontZ), view));
                Vector2 back = OnScreen(camera, new Vector3(x, 0f, RinkBoards.EndZ), view);
                ice.Add(back);
                strip.Add(back);
                strip.Add(AboveTheView(camera, x, RinkBoards.EndZ, view));
            }
            bool[] mask = PictureMeasures.Polygon(ice, Width, Height);
            PictureMeasures.AddTo(mask, PictureMeasures.Polygon(strip, Width, Height));
            return mask;
        }

        static Vector2 OnScreen(Camera camera, Vector3 world, string view)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0) throw new InvalidOperationException("View " + view + ": the Calm Area is behind the camera.");
            return new Vector2(screen.x, screen.y);
        }

        /// <summary>The screen point, just above the top of the view, of the vertical line at (x, z): climbs it a metre at a time.</summary>
        static Vector2 AboveTheView(Camera camera, float x, float z, string view)
        {
            for (float y = 1f; y <= CalmAreaMaxClimb; y += 1f)
            {
                Vector3 screen = camera.WorldToScreenPoint(new Vector3(x, y, z));
                if (screen.z <= 0) break;
                if (screen.y > Height) return new Vector2(screen.x, screen.y);
            }
            throw new InvalidOperationException("View " + view + ": the Calm Area's edge at x " + x + ", z " + z +
                                                " does not reach the top of the view from camera " + camera.transform.position + ".");
        }

        /// <summary>The screen corners of a Net Face rectangle (Net Face units, 0 to 100 each way).</summary>
        static List<Vector2> NetFaceCorners(Camera camera, double minX, double minY, double maxX, double maxY)
        {
            NetFaceMapping.ToMetres(minX, minY, out double left, out double bottom);
            NetFaceMapping.ToMetres(maxX, maxY, out double right, out double top);
            var corners = new List<Vector2>();
            foreach (double x in new[] { left, right })
            foreach (double y in new[] { bottom, top })
            {
                Vector3 screen = camera.WorldToScreenPoint(new Vector3((float)x, (float)y, 0f));
                corners.Add(new Vector2(screen.x, screen.y));
            }
            return corners;
        }

        /// <summary>The picture with the gear in orange, the Gap pixels in green and the rest of the Calm Area in blue: to check the masks by eye.</summary>
        static Color32[] Overlay(Color32[] picture, bool[] calm, bool[] gear, bool[] gaps)
        {
            var overlay = new Color32[picture.Length];
            for (int i = 0; i < picture.Length; i++)
            {
                Color32 p = picture[i];
                overlay[i] = gear[i] ? Color32.Lerp(p, new Color32(255, 140, 0, 255), 0.6f)
                    : gaps[i] ? Color32.Lerp(p, new Color32(0, 255, 60, 255), 0.6f) // Gap pixels show the Calm Area behind them too
                    : calm[i] ? Color32.Lerp(p, new Color32(0, 80, 255, 255), 0.45f)
                    : p;
            }
            return overlay;
        }

        static void WritePng(Texture2D texture, Color32[] pixels, string path)
        {
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
    }
}
