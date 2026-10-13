using System.IO;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The LED look of the Video Board and Centre Board (#121): the pictures for the look check, and the grid test
    /// (a flat cell with a dark gap round it). Same setup as BoardWordsRenderTests.
    /// </summary>
    public class LedBoardLookTests
    {
        GameObject _rink, _eye;
        Camera _camera;
        RenderTexture _target, _replay;
        Texture2D _ramp;

        const int IsolatedLayer = 31, ReplayLayer = 30;

        // replaced by RinkLayout.VideoBoardLedCell in Task 2
        const float LedCell = 0.1f;

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
            if (_replay != null) Object.DestroyImmediate(_replay);
            if (_ramp != null) Object.DestroyImmediate(_ramp);
        }

        static string Notes => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_notes"));

        // The stage file holds "before" while the before pictures are made; without it the pictures are "after".
        static string Stage
        {
            get
            {
                string file = Path.Combine(Notes, "t121-stage.txt");
                return Directory.Exists(Notes) && File.Exists(file) ? File.ReadAllText(file).Trim() : "after";
            }
        }

        static void SavePng(Texture2D t, string name)
        {
            if (!Directory.Exists(Notes)) return; // only the dev checkout has a _notes folder; the assertions run either way
            File.WriteAllBytes(Path.Combine(Notes, "t121-" + Stage + "-" + name + ".png"), t.EncodeToPNG());
        }

        static string PngPath(string name) => Path.Combine(Notes, "t121-" + Stage + "-" + name + ".png");

        static ArenaState Show(RoundPhase phase) =>
            ArenaShow.At(new ArenaShowInput
            {
                Phase = phase,
                PhaseSeconds = 1.0,
                Goals = 2,
                ShotNumber = 4,
                BestScore = 3
            });

        Color32[] Render(int width, int height, string pngName)
        {
            // Draw only what this test built, on a layer nothing else uses.
            foreach (Transform t in _rink.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IsolatedLayer;
            _camera.cullingMask = 1 << IsolatedLayer;
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

        /// <summary>The Replay picture as the game makes it: the Replay camera's real 512 x 280 render of the whole rink.</summary>
        RenderTexture MakeReplayTexture()
        {
            Transform arena = RinkBuilder.Build(out Camera shooterCamera);
            var replayObject = new GameObject("Replay Camera (look check)");
            try
            {
                foreach (Transform t in arena.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ReplayLayer;
                var texture = new RenderTexture(ReplayView.PictureWidth, ReplayView.PictureHeight, 24);
                Camera replayCamera = replayObject.AddComponent<Camera>();
                replayCamera.clearFlags = CameraClearFlags.SolidColor;
                replayCamera.backgroundColor = ArenaLighting.Backdrop;
                replayCamera.cullingMask = 1 << ReplayLayer;
                replayCamera.fieldOfView = ReplayView.FieldOfView;
                replayCamera.targetTexture = texture;
                replayCamera.aspect = ReplayView.PictureWidth / (float)ReplayView.PictureHeight;
                Breakaway.SkatePoint(AttackSide.Middle, 0.0, out double rx, out double rd);
                Vector3 eye = new Vector3((float)rx, (float)Breakaway.EyeHeight, (float)-rd);
                replayCamera.transform.SetPositionAndRotation(ReplayView.CameraSpot,
                    Quaternion.LookRotation(ReplayView.AimPoint(eye) - ReplayView.CameraSpot));
                replayCamera.Render();
                replayCamera.targetTexture = null;
                return texture;
            }
            finally
            {
                Object.DestroyImmediate(replayObject);
                Object.DestroyImmediate(shooterCamera.gameObject);
                Object.DestroyImmediate(arena.gameObject);
            }
        }

        /// <summary>The screen's four corners in pixels (shrunk to 85 % so the frame never counts). The target is released after Render, so the viewport is scaled by hand.</summary>
        static Vector2[] ScreenPolygon(Camera cam, VideoBoards boards, int screen, int width, int height)
        {
            Vector3 c = boards.CentreOf(screen), facing = boards.FacingOf(screen);
            Vector3 right = Vector3.Cross(Vector3.up, -facing).normalized, up = Vector3.up;
            float hw = boards.SizeOf(screen).x / 2f, hh = boards.SizeOf(screen).y / 2f;
            Vector3[] world = { c - right * hw - up * hh, c + right * hw - up * hh, c + right * hw + up * hh, c - right * hw + up * hh };
            var polygon = new Vector2[4];
            Vector2 average = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = cam.WorldToViewportPoint(world[i]);
                polygon[i] = new Vector2(p.x * width, p.y * height);
                average += polygon[i] / 4f;
            }
            for (int i = 0; i < 4; i++) polygon[i] = average + (polygon[i] - average) * 0.85f;
            return polygon;
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

        static float LitShare(Color32[] pixels, Vector2[] polygon, int width)
        {
            int inside = 0, lit = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (!Inside(polygon, i % width, i / width)) continue;
                inside++;
                Color32 c = pixels[i];
                if (c.r > 8 || c.g > 8 || c.b > 8) lit++;
            }
            return inside == 0 ? 0f : lit / (float)inside;
        }

        void FromShooter(VideoBoards boards, double u, string pngName)
        {
            Breakaway.SkatePoint(AttackSide.Middle, u, out double x, out double distance);
            BreakawayView.PlaceShooterCamera(_eye.transform, (float)x, (float)distance, 0f, 0f);
            _camera.fieldOfView = 60f;
            CheckFrame(boards, 0, 960, 540, pngName);
        }

        void CheckFrame(VideoBoards boards, int screen, int width, int height, string pngName)
        {
            Color32[] pixels = Render(width, height, pngName);
            Assert.IsTrue(!Directory.Exists(Notes) || File.Exists(PngPath(pngName)), pngName + ": the picture file was written");
            float share = LitShare(pixels, ScreenPolygon(_camera, boards, screen, width, height), width);
            Assert.Greater(share, 0.5f, pngName + ": the board is really in the frame (non-black share inside the screen)");
        }

        void CloseUp(VideoBoards boards, int screen, float metres, string pngName)
        {
            Vector3 spot = boards.CentreOf(screen) + boards.FacingOf(screen) * metres;
            _eye.transform.position = spot;
            _eye.transform.LookAt(boards.CentreOf(screen));
            _camera.fieldOfView = 60f;
            CheckFrame(boards, screen, 1920, 1080, pngName);
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Pictures_for_the_look_check()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            RibbonBoards.Build(_rink.transform);

            boards.Show(Show(RoundPhase.SkateIn), 0.0);
            FromShooter(boards, 0.0, "score-0.0");
            FromShooter(boards, 1.0, "score-1.0");
            CloseUp(boards, 0, 12f, "closeup-score");
            CloseUp(boards, 1, 6f, "centre-score");

            _replay = MakeReplayTexture();
            boards.ShowPicture(_replay);
            FromShooter(boards, 1.0, "replay-1.0");
            CloseUp(boards, 0, 12f, "closeup-replay");
            boards.ShowPicture(null);

            boards.Show(Show(RoundPhase.ShootingWindow), 0.0);
            FromShooter(boards, 1.0, "calm-1.0");
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void The_Video_Board_picture_is_a_grid_of_flat_cells_with_dark_gaps()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            _ramp = new Texture2D(256, 4, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 256; x++) _ramp.SetPixel(x, y, new Color(x / 255f, 0f, 0f));
            _ramp.Apply();
            boards.ShowPicture(_ramp);

            const int width = 1900, height = 1040;
            float orthoSize = boards.SizeOf(0).y / 2f + VideoBoards.FrameMargin;
            Vector3 centre = boards.CentreOf(0), facing = boards.FacingOf(0);
            _eye.transform.position = centre + facing * 5f;
            _eye.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
            _camera.orthographic = true;
            _camera.orthographicSize = orthoSize;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;

            Color32[] pixels = Render(width, height, "grid-closeup");

            float pixelsPerMetre = height / (2f * orthoSize), cellPixels = LedCell * pixelsPerMetre;
            Vector3 lowerLeft = centre - _eye.transform.right * (boards.SizeOf(0).x / 2f) - Vector3.up * (boards.SizeOf(0).y / 2f);
            Vector3 corner = _camera.WorldToViewportPoint(lowerLeft);
            float left = corner.x * width, bottom = corner.y * height;
            int row = Mathf.RoundToInt(bottom + boards.SizeOf(0).y / 2f * pixelsPerMetre);

            int first = Mathf.RoundToInt(boards.SizeOf(0).x / LedCell / 2f) - 1;
            Color32 At(float x) => pixels[row * width + Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 1)];
            float[] centreRed = new float[3];
            for (int n = 0; n < 3; n++)
            {
                int k = first + n;
                float cellCentre = left + (k + 0.5f) * cellPixels, boundary = left + k * cellPixels;
                float here = At(cellCentre).r, right = At(cellCentre + 3f).r, gap = At(boundary).r;
                centreRed[n] = here;
                Assert.LessOrEqual(Mathf.Abs(here - right), 4f, "cell " + k + ": flat cell (centre " + here + ", 3 px right " + right + " of 255)");
                Assert.Less(gap, here * 0.4f, "cell " + k + ": dark gap on the cell boundary (boundary " + gap + ", centre " + here + " of 255)");
            }
            Assert.AreNotEqual(centreRed[0], centreRed[2], "cell " + first + " and cell " + (first + 2) + " differ: the picture is still a ramp");
        }
    }
}
