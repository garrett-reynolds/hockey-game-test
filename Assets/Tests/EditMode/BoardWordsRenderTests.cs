using System.IO;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame.Core.Tests
{
    public class BoardWordsRenderTests
    {
        GameObject _rink, _eye;
        Camera _camera;
        RenderTexture _target;

        [SetUp]
        public void MakeRink()
        {
            _rink = new GameObject("Rink");
            _eye = new GameObject("Eye");
            _camera = _eye.AddComponent<Camera>();
            _camera.aspect = 16f / 9f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.allowHDR = false;
        }

        [TearDown]
        public void RemoveRink()
        {
            Object.DestroyImmediate(_rink);
            Object.DestroyImmediate(_eye);
            if (_target != null) Object.DestroyImmediate(_target);
        }

        static string Notes => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_notes"));

        static string Stage
        {
            get
            {
                string path = Path.Combine(Notes, "t120-stage.txt");
                return File.Exists(path) ? File.ReadAllText(path).Trim() : "after";
            }
        }

        static bool IsGreen(Color32 c) => c.g > 204 && c.r < 90 && c.b < 90;
        static bool IsYellow(Color32 c) => c.r > 204 && c.g > 204 && c.b < 90;

        static void Paint(Component root, Color colour)
        {
            foreach (TextMesh t in root.GetComponentsInChildren<TextMesh>()) t.color = colour;
        }

        static ArenaState Words(ArenaResult result = ArenaResult.Goal) =>
            ArenaShow.At(new ArenaShowInput
            {
                Phase = RoundPhase.ResultHold,
                PhaseSeconds = 1.0,
                Result = result,
                Goals = 2,
                ShotNumber = 4,
                BestScore = 3,
                LastShotSpeedKmh = 92
            });

        Color32[] Render(int width, int height, string pngName)
        {
            _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            _camera.targetTexture = _target;
            _camera.aspect = width / (float)height;
            _camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                if (pngName != null) SavePng(texture, pngName);
                return texture.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                _camera.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(_target);
                _target = null;
            }
        }

        static void SavePng(Texture2D t, string name)
        {
            File.WriteAllBytes(Path.Combine(Notes, "t120-" + Stage + "-" + name + ".png"), t.EncodeToPNG());
        }

        static Vector2[] ScreenPolygon(Camera cam, VideoBoards boards, int width, int height)
        {
            Vector3 c = boards.CentreOf(0), facing = boards.FacingOf(0);
            Vector3 right = Vector3.right, up = Vector3.Cross(facing, right).normalized;
            if (up.y < 0f) up = -up;
            float hw = boards.SizeOf(0).x / 2f, hh = boards.SizeOf(0).y / 2f;
            Vector3[] world = { c - right * hw - up * hh, c + right * hw - up * hh, c + right * hw + up * hh, c - right * hw + up * hh };
            var screen = new Vector2[4];
            Vector2 average = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = cam.WorldToViewportPoint(world[i]); // the camera's target is released after Render, so scale the viewport ourselves
                screen[i] = new Vector2(p.x * width, p.y * height);
                average += screen[i] / 4f;
            }
            for (int i = 0; i < 4; i++) screen[i] = average + (screen[i] - average) * 0.85f;
            return screen;
        }

        static bool Inside(Vector2[] polygon, int x, int y)
        {
            bool positive = false, negative = false;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                float cross = (b.x - a.x) * (y - a.y) - (b.y - a.y) * (x - a.x);
                if (cross > 0f) positive = true;
                if (cross < 0f) negative = true;
            }
            return !(positive && negative);
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void From_the_Shooters_camera_no_Ribbon_Board_word_shows_inside_the_Video_Board()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            boards.Show(Words(), 0.0);
            ribbons.Show(Words(), 0.0);
            Paint(boards.VideoBoard, new Color(0.1f, 1f, 0.1f));
            Paint(ribbons, new Color(1f, 1f, 0.1f));
            string shader = ribbons.GetComponentInChildren<TextMesh>().GetComponent<Renderer>().sharedMaterial.shader.name;

            int through = 0, own = 0;            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            foreach (double u in new[] { 0.0, 0.5, 1.0 })
            {
                Breakaway.SkatePoint(side, u, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(_eye.transform, (float)x, (float)distance, 0f, 0f);
                bool keep = side == AttackSide.Middle && (u == 0.0 || u == 1.0);
                Color32[] pixels = Render(640, 360, keep ? "shooter-" + side + "-" + u.ToString("0.0") : null);
                Vector2[] screen = ScreenPolygon(_camera, boards, 640, 360);
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (!Inside(screen, i % 640, i / 640)) continue;
                    if (IsYellow(pixels[i])) through++;
                    if (IsGreen(pixels[i])) own++;
                }
            }
            Assert.That(own, Is.GreaterThan(0), "the Video Board's own words still show (the check is not empty)");
            Assert.AreEqual(0, through, "Ribbon Board word pixels inside the Video Board's screen; the words material uses shader " + shader);
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void From_the_High_corner_Centre_Board_screens_facing_away_show_no_word_pixels()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            boards.Show(Words(), 0.0);
            foreach (TextMesh t in boards.VideoBoard.GetComponentsInChildren<TextMesh>())
                t.GetComponent<Renderer>().enabled = false; // only the Centre Board's words can be seen
            string shader = boards.CentreBoard.GetComponentInChildren<TextMesh>().GetComponent<Renderer>().sharedMaterial.shader.name;

            double start = StartShots.All[0].Seconds + StartShots.All[1].Seconds;
            StartShotPose pose = StartShots.At(start + StartShots.All[2].Seconds / 2);
            Assert.AreEqual(2, pose.Shot, "wide shot 3");
            Assert.AreEqual("High corner", StartShots.All[2].Name);
            _eye.transform.position = new Vector3((float)pose.Eye.X, (float)pose.Eye.Y, (float)pose.Eye.Z);
            _eye.transform.LookAt(new Vector3((float)pose.Look.X, (float)pose.Look.Y, (float)pose.Look.Z));
            _camera.fieldOfView = (float)pose.FieldOfViewDegrees;

            // Centre Board screens are 1..4 (0 is the Video Board). Facing-away screens get yellow words, facing screens green.
            int away = 0;
            foreach (TextMesh t in boards.CentreBoard.GetComponentsInChildren<TextMesh>())
            {
                int index = int.Parse(t.name.Substring(t.name.LastIndexOf(' ') + 1));
                bool facingAway = Vector3.Dot(_eye.transform.position - boards.CentreOf(index), boards.FacingOf(index)) < 0f;
                t.color = facingAway ? new Color(1f, 1f, 0.1f) : new Color(0.1f, 1f, 0.1f);
                if (facingAway && t.name.Contains("Headline")) away++;
            }
            Assert.Greater(away, 0, "at least one Centre Board screen faces away from the eye");

            Color32[] pixels = Render(1280, 720, "shot3");
            int words = 0, own = 0;
            foreach (Color32 c in pixels)
            {
                if (IsYellow(c)) words++;
                if (IsGreen(c)) own++;
            }
            Assert.Greater(own, 0, "the facing Centre Board screens' words show (the check is not empty)");
            Assert.AreEqual(0, words, "word pixels seen through the Centre Board from screens facing away; the words material uses shader " + shader);
        }
    }
}
