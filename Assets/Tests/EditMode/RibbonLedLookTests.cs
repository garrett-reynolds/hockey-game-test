using System.Collections.Generic;
using System.IO;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The LED look of the Ribbon Boards (#122): the pictures for the look check, the grid test (a flat cell with a dark gap round it)
    /// and the steady-grid guard (the grid does not crawl when the camera moves a little). Same setup as LedBoardLookTests.
    /// </summary>
    public class RibbonLedLookTests
    {
        GameObject _rink, _eye;
        Camera _camera;
        RenderTexture _target;

        const int IsolatedLayer = 31;
        const float LedCell = (float)RinkLayout.RibbonBoardLedCell;

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

        // The picture set's name: "before" while the stage file says so, else "after".
        static string Stage
        {
            get
            {
                string file = Path.Combine(Notes, "t122-stage.txt");
                return File.Exists(file) ? File.ReadAllText(file).Trim() : "after";
            }
        }

        static string PngPath(string name) => Path.Combine(Notes, "t122-" + Stage + "-" + name + ".png");

        static void SavePng(Texture2D t, string name)
        {
            if (!Directory.Exists(Notes)) return; // only the dev checkout has a _notes folder; the assertions run either way
            File.WriteAllBytes(PngPath(name), t.EncodeToPNG());
        }

        static ArenaState Show(RoundPhase phase, ArenaResult result = ArenaResult.None) =>
            ArenaShow.At(new ArenaShowInput
            {
                Phase = phase,
                PhaseSeconds = 1.0,
                Result = result,
                ResultHidden = false,
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

        // The Shooter's eye at the end of the skate-in, as the real Shooter Camera is placed (field of view 60 as in LedBoardLookTests).
        void PlaceShooter()
        {
            Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
            BreakawayView.PlaceShooterCamera(_eye.transform, (float)x, (float)distance, 0f, 0f);
            _camera.orthographic = false;
            _camera.fieldOfView = 60f;
            _camera.aspect = 16f / 9f;
        }

        RibbonBoards BuildBoards()
        {
            VideoBoards.Build(_rink.transform);
            return RibbonBoards.Build(_rink.transform);
        }

        static Transform PanelOf(RibbonBoards ribbons, int panel) => ribbons.transform.Find("Ribbon Panel " + panel);

        // The panel's four corners in the world: lower left, lower right, upper right, upper left, as the Shooter sees them.
        static Vector3[] Corners(RibbonBoards ribbons, int panel)
        {
            Transform t = PanelOf(ribbons, panel);
            Vector3 c = ribbons.CentreOf(panel), right = t.right, up = Vector3.up;
            float hw = ribbons.LengthOf(panel) / 2f, hh = RibbonBoards.Height / 2f;
            return new[] { c - right * hw - up * hh, c + right * hw - up * hh, c + right * hw + up * hh, c - right * hw + up * hh };
        }

        // The corners in pixels, or null when one is behind the camera. The target is released after Render, so the viewport is scaled by hand.
        Vector2[] Pixels(Vector3[] world, int width, int height)
        {
            // Cut the part behind the camera off first (a panel that wraps round the camera has corners behind it).
            const float near = 0.3f;
            var kept = new List<Vector3>();
            for (int i = 0; i < world.Length; i++)
            {
                Vector3 a = _eye.transform.InverseTransformPoint(world[i]), b = _eye.transform.InverseTransformPoint(world[(i + 1) % world.Length]);
                if (a.z >= near) kept.Add(world[i]);
                if ((a.z >= near) != (b.z >= near))
                    kept.Add(Vector3.Lerp(world[i], world[(i + 1) % world.Length], (near - a.z) / (b.z - a.z)));
            }
            if (kept.Count < 3) return null;
            var polygon = new Vector2[kept.Count];
            for (int i = 0; i < kept.Count; i++)
            {
                Vector3 p = _camera.WorldToViewportPoint(kept[i]);
                polygon[i] = new Vector2(p.x * width, p.y * height);
            }
            return polygon;
        }

        static Vector2[] Shrunk(Vector2[] polygon, float share)
        {
            Vector2 average = Vector2.zero;
            foreach (Vector2 p in polygon) average += p / polygon.Length;
            var shrunk = new Vector2[polygon.Length];
            for (int i = 0; i < polygon.Length; i++) shrunk[i] = average + (polygon[i] - average) * share;
            return shrunk;
        }

        // The polygon's area inside the frame (Sutherland-Hodgman against the four sides, then the shoelace sum).
        static float ClippedArea(Vector2[] polygon, int width, int height)
        {
            List<Vector2> points = new List<Vector2>(polygon);
            for (int side = 0; side < 4 && points.Count > 0; side++)
            {
                var next = new List<Vector2>();
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 a = points[i], b = points[(i + 1) % points.Count];
                    bool aIn = Side(a, side, width, height) >= 0f, bIn = Side(b, side, width, height) >= 0f;
                    if (aIn) next.Add(a);
                    if (aIn != bIn)
                    {
                        float da = Side(a, side, width, height), db = Side(b, side, width, height);
                        next.Add(a + (b - a) * (da / (da - db)));
                    }
                }
                points = next;
            }
            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return Mathf.Abs(area) / 2f;
        }

        static float Side(Vector2 p, int side, int width, int height) =>
            side == 0 ? p.x : side == 1 ? width - p.x : side == 2 ? p.y : height - p.y;

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

        // Pixels of the frame inside the polygon, and how many are not black (lit panel; the clear colour and the unlit back are black).
        static void CountLit(Color32[] pixels, Vector2[] polygon, int width, out int inside, out int lit)
        {
            inside = 0;
            lit = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (!Inside(polygon, i % width, i / width)) continue;
                inside++;
                Color32 c = pixels[i];
                if (c.r > 5 || c.g > 5 || c.b > 5) lit++;
            }
        }

        /// <summary>
        /// The chosen panel: the panel with the largest projected area (inside the frame) in the Shooter's frame. Every zone counts: the
        /// Near and Far zone panels are out of the Shooter's view (the Ribbon hangs high round the stands; the Shooter sees the Calm Area
        /// behind the net), and all zones show the same ads and result words except in the shooting window, which no picture uses.
        /// Fails with the areas when none shows over 0.3 % of the frame (the look check needs a visible panel; the best one shows about
        /// 0.6 %, not the 2 % the plan guessed: each panel is about 160 x 75 px in a 1920 x 1080 frame).
        /// </summary>
        int ChoosePanel(RibbonBoards ribbons)
        {
            const int width = 1920, height = 1080;
            PlaceShooter();
            int best = -1;
            float bestArea = 0f;
            var areas = new List<string>();
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Vector2[] polygon = Pixels(Corners(ribbons, i), width, height);
                if (polygon == null) continue;
                float area = ClippedArea(polygon, width, height);
                areas.Add(i + " (" + ribbons.ZoneOf(i) + "): " + (area * 100f / (width * height)).ToString("F2") + " %");
                if (area > bestArea) { bestArea = area; best = i; }
            }
            Assert.IsTrue(best >= 0 && bestArea > width * height * 0.003f,
                "a Ribbon Board panel shows over 0.3 % of the Shooter's frame (the best shows 0.6 %: the ring is far); areas: " + string.Join(", ", areas));
            return best;
        }

        void ShooterFrame(RibbonBoards ribbons, int panel, string pngName, bool checkLit)
        {
            const int width = 1920, height = 1080;
            PlaceShooter();
            Color32[] pixels = Render(width, height, pngName);
            Assert.IsTrue(!Directory.Exists(Notes) || File.Exists(PngPath(pngName)), pngName + ": the picture file was written");
            if (!checkLit) return;
            Vector2[] polygon = Pixels(Corners(ribbons, panel), width, height);
            Assert.IsNotNull(polygon, pngName + ": the chosen panel " + panel + " is in front of the camera");
            CountLit(pixels, Shrunk(polygon, 0.85f), width, out int inside, out int lit);
            Assert.Greater(inside, 0, pngName + ": the chosen panel " + panel + " is in the frame");
            Assert.Greater(lit, inside * 0.5f, pngName + ": the chosen panel " + panel + " is lit (" + lit + " non-black px of " + inside + ")");
        }

        void CloseUp(RibbonBoards ribbons, int panel, string pngName)
        {
            Transform t = PanelOf(ribbons, panel);
            Vector3 centre = ribbons.CentreOf(panel);
            _camera.orthographic = false;
            _camera.fieldOfView = 60f;
            _eye.transform.position = centre - t.forward * 3f; // t.forward points outward; the panel shows its face inward
            _eye.transform.LookAt(centre);
            Render(1920, 1080, pngName);
            Assert.IsTrue(!Directory.Exists(Notes) || File.Exists(PngPath(pngName)), pngName + ": the picture file was written");
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Pictures_for_the_look_check()
        {
            RibbonBoards ribbons = BuildBoards();
            ribbons.Show(Show(RoundPhase.Idle), 0.0);
            int panel = ChoosePanel(ribbons);
            TestContext.Out.WriteLine("chosen Ribbon Board panel: " + panel);
            if (Directory.Exists(Notes)) File.WriteAllText(Path.Combine(Notes, "t122-chosen-panel.txt"), panel.ToString());

            ShooterFrame(ribbons, panel, "shooter-ads", true);
            CloseUp(ribbons, panel, "closeup-ad");

            double start = 0;
            for (int i = 0; i < StartShots.All.Length; i++)
            {
                double mid = start + StartShots.All[i].Seconds / 2;
                start += StartShots.All[i].Seconds;
                StartShotPose pose = StartShots.At(mid);
                _camera.orthographic = false;
                _eye.transform.position = new Vector3((float)pose.Eye.X, (float)pose.Eye.Y, (float)pose.Eye.Z);
                _eye.transform.LookAt(new Vector3((float)pose.Look.X, (float)pose.Look.Y, (float)pose.Look.Z));
                _camera.fieldOfView = (float)pose.FieldOfViewDegrees;
                string name = "wide-" + (i + 1);
                Render(1920, 1080, name);
                Assert.IsTrue(!Directory.Exists(Notes) || File.Exists(PngPath(name)), name + ": the picture file was written");
            }

            ribbons.Show(Show(RoundPhase.ResultHold, ArenaResult.Goal), 0.0);
            ShooterFrame(ribbons, panel, "shooter-goal", true);
            CloseUp(ribbons, panel, "closeup-goal");

            ribbons.Show(Show(RoundPhase.ResultHold, ArenaResult.Save), 0.0);
            ShooterFrame(ribbons, panel, "shooter-save", true);
        }

        static float Distance(Color32 a, Color32 b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void The_Ribbon_Board_panel_is_a_grid_of_flat_cells_with_dark_gaps()
        {
            RibbonBoards ribbons = BuildBoards();
            ribbons.Show(Show(RoundPhase.Idle), 0.0);
            int panel = ChoosePanel(ribbons);
            Transform t = PanelOf(ribbons, panel);
            Vector3 centre = ribbons.CentreOf(panel);
            // The shown background is the panel's _Backdrop colour as set (an sRGB colour, like the Video Board's), not an emission value.
            Color32 background = ribbons.BackOn(panel);

            // Face-on, orthographic, the panel's height filling the frame (a little over, to show the edge strips).
            const int width = 1920, height = 1080;
            float orthoSize = (float)RinkLayout.RibbonHeight / 2f + 0.05f;
            _eye.transform.position = centre - t.forward * 1.5f;
            _eye.transform.rotation = Quaternion.LookRotation(t.forward, Vector3.up);
            _camera.orthographic = true;
            _camera.orthographicSize = orthoSize;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;
            Color32[] pixels = Render(width, height, null);

            float cellPixels = LedCell * height / (2f * orthoSize);
            int cellsAcross = Mathf.FloorToInt(ribbons.LengthOf(panel) / LedCell);
            Vector3 lowerLeft = Corners(ribbons, panel)[0];
            Vector3 corner = _camera.WorldToViewportPoint(lowerLeft);
            float left = corner.x * width, bottom = corner.y * height;
            // A row with no word cells (the ad's words sit in the middle, the edge strip along the bottom): the fourth row up.
            int row = Mathf.RoundToInt(bottom + 3.5f * cellPixels);
            int first = cellsAcross / 2 - 1; // three cells that show in the middle of the frame
            Color32 At(float x) => pixels[row * width + Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 1)];

            var failures = new List<string>(); // every check reports, so a smooth panel shows both the flat-cell and the gap failures
            for (int n = 0; n < 3; n++)
            {
                int k = first + n;
                float cellCentre = left + (k + 0.5f) * cellPixels, boundary = left + k * cellPixels;
                Color32 here = At(cellCentre), side = At(cellCentre + 3f);
                Color32 edge = At(boundary);
                int gapMax = Mathf.Max(edge.r, Mathf.Max(edge.g, edge.b));
                int hereMax = Mathf.Max(here.r, Mathf.Max(here.g, here.b));
                if (Distance(here, side) > 4f)
                    failures.Add("cell " + k + ": not a flat cell (centre " + here + ", 3 px to the right " + side + ")");
                if (gapMax >= hereMax * 0.4f)
                    failures.Add("cell " + k + ": no dark gap on the cell boundary (brightest channel there " + gapMax + ", at the centre " + hereMax + " of 255)");
                if (Distance(here, background) > 30f)
                    failures.Add("cell " + k + ": centre " + here + " is not the ad background " + background);
            }
            Assert.IsEmpty(failures, "panel " + panel + " (row " + row + ", left " + left + ", cell " + cellPixels + " px):\n" + string.Join("\n", failures));
        }

        // Renders 8 frames with the camera moved sideways in 0.7 cm steps, looks at the band along the panel's top (below the edge strip,
        // above the words), and returns each frame's mean and the biggest spread of pixels inside one frame (0 to 255).
        void Steady(RibbonBoards ribbons, int panel, float cellPixels, out float[] means, out float spread)
        {
            const int width = 960, height = 540;
            const float fov = 60f;
            Transform t = PanelOf(ribbons, panel);
            Vector3 centre = ribbons.CentreOf(panel), right = t.right;
            float distance = LedCell * height / (2f * Mathf.Tan(fov * Mathf.Deg2Rad / 2f) * cellPixels);
            _camera.orthographic = false;
            _camera.fieldOfView = fov;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 1000f;
            float half = Mathf.Min(1.5f, ribbons.LengthOf(panel) / 2f - 0.3f);
            float top = RibbonBoards.Height / 2f;
            means = new float[8];
            spread = 0f;
            int x0 = 0, x1 = 0, y0 = 0, y1 = 0;
            for (int step = 0; step < 8; step++)
            {
                _eye.transform.position = centre - t.forward * distance + right * (0.007f * step);
                _eye.transform.rotation = Quaternion.LookRotation(t.forward, Vector3.up);
                Color32[] pixels = Render(width, height, null);
                if (step == 0)
                {
                    Vector3 a = _camera.WorldToViewportPoint(centre - right * half + Vector3.up * (top - 0.28f));
                    Vector3 b = _camera.WorldToViewportPoint(centre + right * half + Vector3.up * (top - 0.10f));
                    x0 = Mathf.CeilToInt(Mathf.Min(a.x, b.x) * width) + 1;
                    x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) * width) - 1;
                    y0 = Mathf.CeilToInt(Mathf.Min(a.y, b.y) * height) + 1;
                    y1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) * height) - 1;
                    Assert.IsTrue(x0 >= 0 && x1 < width && y0 >= 0 && y1 < height && x1 > x0 && y1 > y0,
                        "the band is in the frame and has pixels (x " + x0 + ".." + x1 + ", y " + y0 + ".." + y1 + ")");
                }
                float sum = 0f, low = 255f, high = 0f;
                int count = 0;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        Color32 c = pixels[y * width + x];
                        float grey = (c.r + c.g + c.b) / 3f;
                        sum += grey;
                        count++;
                        if (grey < low) low = grey;
                        if (grey > high) high = grey;
                    }
                means[step] = sum / count;
                spread = Mathf.Max(spread, high - low);
            }
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void The_grid_holds_steady_when_the_camera_moves_a_little()
        {
            RibbonBoards ribbons = BuildBoards();
            ribbons.Show(Show(RoundPhase.Idle), 0.0);
            int panel = ChoosePanel(ribbons);

            // Far: one cell is about 1.2 pixels, so the panel must be flat (no stripes) and its brightness must not change.
            Steady(ribbons, panel, 1.2f, out float[] farMeans, out float farSpread);
            float farRange = Mathf.Max(farMeans) - Mathf.Min(farMeans);
            Assert.LessOrEqual(farSpread, 6f, "far: the band's pixels differ from each other by at most 6/255 (flat, no stripes)");
            Assert.LessOrEqual(farRange, 4f, "far: the 8 frame means differ by at most 4/255 (means " + string.Join(", ", farMeans) + ")");

            // Near: the grid lines move but the average must not, at every size from 3 to 4.5 pixels per cell. 3.0, 4.0 and 4.5 are
            // the sizes where every cell has the same sub-pixel phase (a gap hits all pixel centres or none), the worst case for a
            // gap that is not filtered over the pixel's footprint; 4.3 is a size between them.
            var failures = new List<string>();
            foreach (float cellPixels in new[] { 3.0f, 4.0f, 4.3f, 4.5f })
            {
                Steady(ribbons, panel, cellPixels, out float[] nearMeans, out float _);
                float nearAverage = 0f;
                foreach (float m in nearMeans) nearAverage += m / nearMeans.Length;
                float nearRange = Mathf.Max(nearMeans) - Mathf.Min(nearMeans);
                if (nearRange > nearAverage * 0.06f)
                    failures.Add(cellPixels + " px per cell: the 8 frame means differ by " + (100f * nearRange / nearAverage).ToString("F1") + " % of their average "
                        + nearAverage + " (means " + string.Join(", ", nearMeans) + ")");
            }
            Assert.IsEmpty(failures, "near: the 8 frame means differ by at most 6 % of their average:\n" + string.Join("\n", failures));
        }
    }
}
