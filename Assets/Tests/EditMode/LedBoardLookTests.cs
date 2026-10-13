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

        static readonly float LedCell = (float)RinkLayout.VideoBoardLedCell;

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

        static void SavePng(Texture2D t, string name)
        {
            if (!Directory.Exists(Notes)) return; // only the dev checkout has a _notes folder; the assertions run either way
            File.WriteAllBytes(PngPath(name), t.EncodeToPNG());
        }

        static string PngPath(string name) => Path.Combine(Notes, "t121-after-" + name + ".png");

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

        // Pixels of the frame inside the (shrunk) screen polygon, and how many of them are bright (words or a picture, not the dark back).
        static void Count(Color32[] pixels, Vector2[] polygon, int width, out int inside, out int bright)
        {
            inside = 0;
            bright = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (!Inside(polygon, i % width, i / width)) continue;
                inside++;
                Color32 c = pixels[i];
                if (c.r > 128 || c.g > 128 || c.b > 128) bright++;
            }
        }

        // The shooter-eye frames are pictures only: the board is mostly above the frame, so nothing about it is asserted there.
        void FromShooter(double u, string pngName)
        {
            Breakaway.SkatePoint(AttackSide.Middle, u, out double x, out double distance);
            BreakawayView.PlaceShooterCamera(_eye.transform, (float)x, (float)distance, 0f, 0f);
            _camera.fieldOfView = 60f;
            Render(960, 540, pngName);
            Assert.IsTrue(!Directory.Exists(Notes) || File.Exists(PngPath(pngName)), pngName + ": the picture file was written");
        }

        void CheckFrame(VideoBoards boards, int screen, int width, int height, string pngName)
        {
            Color32[] pixels = Render(width, height, pngName);
            Assert.IsTrue(!Directory.Exists(Notes) || File.Exists(PngPath(pngName)), pngName + ": the picture file was written");
            Vector2[] polygon = ScreenPolygon(_camera, boards, screen, width, height);
            foreach (Vector2 corner in polygon)
                Assert.IsTrue(corner.x >= 0 && corner.x <= width && corner.y >= 0 && corner.y <= height, pngName + ": the whole board is in the frame (corner " + corner + ")");
            Count(pixels, polygon, width, out int inside, out int bright);
            Assert.GreaterOrEqual(inside, width * height * 0.15f, pngName + ": the board covers at least 15 % of the frame (" + inside + " px)");
            Assert.GreaterOrEqual(bright, inside * 0.01f, pngName + ": words or a picture show on the board (" + bright + " bright px of " + inside + ")");
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
            FromShooter(0.0, "score-0.0");
            FromShooter(1.0, "score-1.0");
            CloseUp(boards, 0, 12f, "closeup-score");
            CloseUp(boards, 1, 6f, "centre-score");

            _replay = MakeReplayTexture();
            boards.ShowPicture(_replay);
            FromShooter(1.0, "replay-1.0");
            CloseUp(boards, 0, 12f, "closeup-replay");
            boards.ShowPicture(null);

            boards.Show(Show(RoundPhase.ShootingWindow), 0.0);
            FromShooter(1.0, "calm-1.0");
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void The_Video_Board_picture_is_a_grid_of_flat_cells_with_dark_gaps()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            // One texel per cell across the board, alternating bright and dim: a smooth board blends them (far from flat inside a
            // cell); an LED board shows each cell as one flat colour with a big step to the next.
            int cellsAcross = Mathf.RoundToInt(boards.SizeOf(0).x / LedCell), cellsUp = Mathf.RoundToInt(boards.SizeOf(0).y / LedCell);
            _ramp = new Texture2D(cellsAcross, 4, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < cellsAcross; x++) _ramp.SetPixel(x, y, new Color(x % 2 == 0 ? 0.9f : 0.3f, 0f, 0f));
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
            // The board leans forward, so its lower edge is along the camera's up (the board's up), not along the world's.
            Vector3 lowerLeft = centre - _eye.transform.right * (boards.SizeOf(0).x / 2f) - _eye.transform.up * (boards.SizeOf(0).y / 2f);
            Vector3 corner = _camera.WorldToViewportPoint(lowerLeft);
            float left = corner.x * width, bottom = corner.y * height;
            // The row through the middle of a cell row (never on a horizontal gap), from the board geometry and the cell size.
            int middleRow = cellsUp / 2;
            int row = Mathf.RoundToInt(bottom + (middleRow + 0.5f) * cellPixels);

            int first = cellsAcross / 2 - 1; // cells first .. first + 2, with first even (bright)
            if (first % 2 != 0) first--;
            Color32 At(float x) => pixels[row * width + Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 1)];
            var failures = new System.Collections.Generic.List<string>(); // every check reports, so a smooth board shows both the flat-cell and the gap failures
            {
                float[] centreRed = new float[3];
                for (int n = 0; n < 3; n++)
                {
                    int k = first + n;
                    float cellCentre = left + (k + 0.5f) * cellPixels, boundary = left + k * cellPixels;
                    float here = At(cellCentre).r, leftEdge = At(boundary + cellPixels * 0.3f).r, rightEdge = At(boundary + cellPixels * 0.7f).r;
                    float gap = Mathf.Min(At(boundary - 1f).r, Mathf.Min(At(boundary).r, At(boundary + 1f).r));
                    centreRed[n] = here;
                    // Each side of the cell against its centre (left against right alone would pass on a symmetric blend).
                    if (Mathf.Abs(leftEdge - here) > 4f || Mathf.Abs(rightEdge - here) > 4f)
                        failures.Add("cell " + k + ": not a flat cell (30 % across " + leftEdge + ", centre " + here + ", 70 % across " + rightEdge + " of 255)");
                    if (gap >= here * 0.4f)
                        failures.Add("cell " + k + ": no dark gap on the cell boundary (darkest within 1 px of it " + gap + ", centre " + here + " of 255)");
                }
                if (Mathf.Abs(centreRed[0] - centreRed[1]) <= 40f)
                    failures.Add("cells " + first + " and " + (first + 1) + " centres differ little (" + centreRed[0] + " vs " + centreRed[1] + " of 255): not blocky cells");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // The picture from the game's own Shooter Camera (RinkBuilder's camera and settings) at the end of the skate-in, to judge the
        // words from where the player stands. Only the file is checked; the look is judged by eye.
        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Picture_from_the_real_Shooter_Camera()
        {
            Transform arena = RinkBuilder.Build(out Camera shooter);
            RenderTexture target = null;
            try
            {
                arena.GetComponentInChildren<VideoBoards>().Show(Show(RoundPhase.SkateIn), 0.0);
                Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(shooter.transform, (float)x, (float)distance, 0f, 0f);
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                shooter.targetTexture = target;
                shooter.aspect = 1920f / 1080f;
                shooter.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                SavePng(texture, "shooter");
                Object.DestroyImmediate(texture);
            }
            finally
            {
                shooter.targetTexture = null;
                if (target != null) Object.DestroyImmediate(target);
                Object.DestroyImmediate(shooter.gameObject);
                Object.DestroyImmediate(arena.gameObject);
            }
            Assert.IsTrue(!Directory.Exists(Notes) || File.Exists(PngPath("shooter")), "the picture file was written");
        }

        // ---- Words on the Centre Board with a chosen string and grid phase (the tests below guard LedBoard.shader's glyph-quad corner) ----

        const int WordsW = 1920, WordsH = 1080;

        /// <summary>Shows the Skate-in words, keeps only the Centre Board's first Headline on, and points the camera at that screen from 6 m.</summary>
        TextMesh CentreHeadline(VideoBoards boards)
        {
            boards.Show(Show(RoundPhase.SkateIn), 0.0);
            TextMesh best = null;
            float bestDistance = float.MaxValue;
            foreach (TextMesh t in boards.CentreBoard.GetComponentsInChildren<TextMesh>(true))
            {
                t.GetComponent<Renderer>().enabled = false;
                if (!t.name.Contains("Headline")) continue;
                float d = Vector3.Distance(t.transform.position, boards.CentreOf(1));
                if (d < bestDistance) { bestDistance = d; best = t; }
            }
            best.GetComponent<Renderer>().enabled = true;
            _eye.transform.position = boards.CentreOf(1) + boards.FacingOf(1) * 6f;
            _eye.transform.LookAt(boards.CentreOf(1));
            _camera.fieldOfView = 60f;
            return best;
        }

        /// <summary>Renders text with the words' origin moved by (right, up) metres from where it was put; the lit pixels inside the screen.</summary>
        bool[] LitWords(VideoBoards boards, TextMesh words, Vector3 origin, string text, float right, float up)
        {
            words.text = text;
            words.font.RequestCharactersInTexture(text, words.fontSize, FontStyle.Bold);
            BoardWordsMaterial.Follow(words.GetComponent<Renderer>().sharedMaterial, words.font);
            words.transform.position = origin + words.transform.right * right + words.transform.up * up;
            Color32[] pixels = Render(WordsW, WordsH, null);
            Vector2[] polygon = ScreenPolygon(_camera, boards, 1, WordsW, WordsH);
            var lit = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                lit[i] = (c.r > 128 || c.g > 128 || c.b > 128) && Inside(polygon, i % WordsW, i / WordsW);
            }
            return lit;
        }

        float CellPixels(VideoBoards boards, TextMesh words)
        {
            Vector3 a = _camera.WorldToViewportPoint(boards.CentreOf(1)), b = _camera.WorldToViewportPoint(boards.CentreOf(1) + words.transform.right * LedCell);
            return Mathf.Abs(b.x - a.x) * WordsW;
        }

        /// <summary>The letters as runs of lit columns (a gap under 0.35 cell is inside a letter: a cell gap is 0.2), each with its lit pixel rows.</summary>
        static System.Collections.Generic.List<(int Left, int Right, int Top, int Bottom, int[] RowCounts)> Letters(bool[] lit, float cellPixels)
        {
            var columns = new int[WordsW];
            for (int i = 0; i < lit.Length; i++) if (lit[i]) columns[i % WordsW]++;
            var letters = new System.Collections.Generic.List<(int Left, int Right, int Top, int Bottom, int[] RowCounts)>();
            int start = -1, last = -1;
            for (int x = 0; x <= WordsW; x++)
            {
                bool on = x < WordsW && columns[x] > 0;
                if (on) { if (start < 0) start = x; last = x; }
                else if (start >= 0 && (x == WordsW || x - last > 0.35f * cellPixels))
                {
                    var rows = new int[WordsH];
                    int top = -1, bottom = WordsH;
                    for (int y = 0; y < WordsH; y++)
                    {
                        for (int cx = start; cx <= last; cx++) if (lit[y * WordsW + cx]) rows[y]++;
                        if (rows[y] > 0) { top = Mathf.Max(top, y); bottom = Mathf.Min(bottom, y); }
                    }
                    letters.Add((start, last, top, bottom, rows));
                    start = -1;
                }
            }
            return letters;
        }

        // Guards the cell-centre-in-glyph-quad rule in LedBoard.shader (the corner from SV_VertexID, and the trim of round letters' overshoot).
        // Flat letters (E L T 4 5) must keep their cap and baseline rows whatever the phase of the cell grid under them; round ones (O S 6)
        // must not light a cell above the cap row or below the baseline row.
        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Flat_letters_keep_their_cap_and_baseline_rows_and_round_letters_stay_inside_at_every_grid_phase()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            TextMesh words = CentreHeadline(boards);
            Vector3 origin = words.transform.position;
            const string text = "E L T 4 O S 6 5";
            const string shown = "ELT4OS65"; // the letters in order; spaces keep neighbours apart so each is found as one run
            var failures = new System.Collections.Generic.List<string>();
            for (int phase = 0; phase < 8; phase++)
            {
                bool[] lit = LitWords(boards, words, origin, text, 0f, phase / 8f * LedCell);
                float cell = CellPixels(boards, words);
                // A cell row is a whole cell (about 12 px) away; a lit cell cut by its letter's quad edge is a few px shorter, so half a cell is the tolerance.
                float tolerance = 0.5f * cell;
                var letters = Letters(lit, cell);
                if (letters.Count != shown.Length) { failures.Add("phase " + phase + ": " + letters.Count + " letters found, not " + shown.Length); continue; }
                int Widest(int index) { int w = 0; foreach (int r in letters[index].RowCounts) w = Mathf.Max(w, r); return w; }
                bool TopWide(int i) => letters[i].RowCounts[letters[i].Top] >= 0.6f * Widest(i);
                bool BottomWide(int i) => letters[i].RowCounts[letters[i].Bottom] >= 0.6f * Widest(i);
                string at = "phase " + phase + " (" + phase / 8f + " cell up): ";
                if (!TopWide(0)) failures.Add(at + "E has no full top bar row");
                if (!BottomWide(0)) failures.Add(at + "E has no full bottom bar row");
                if (!BottomWide(1)) failures.Add(at + "L has no full foot row");
                if (!TopWide(2)) failures.Add(at + "T has no full top bar row");
                if (!TopWide(7)) failures.Add(at + "5 has no full top bar row");
                int capTop = Mathf.Max(letters[0].Top, Mathf.Max(letters[1].Top, letters[2].Top)), baseBottom = Mathf.Min(letters[0].Bottom, Mathf.Min(letters[1].Bottom, letters[2].Bottom));
                if (Mathf.Abs(letters[0].Top - letters[2].Top) > tolerance) failures.Add(at + "E and T cap rows differ (" + letters[0].Top + " and " + letters[2].Top + ")");
                foreach (int round in new[] { 4, 5, 6 })
                {
                    if (letters[round].Top > capTop + tolerance) failures.Add(at + "letter " + shown[round] + " lights a cell above the cap row (its top pixel row " + letters[round].Top + ", flat letters' " + capTop + ")");
                    if (letters[round].Bottom < baseBottom - tolerance) failures.Add(at + "letter " + shown[round] + " lights a cell below the baseline row (its bottom pixel row " + letters[round].Bottom + ", flat letters' " + baseBottom + ")");
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // Guards the glyph-quad corner in LedBoard.shader (assumes TextMesh's 4 vertices per letter go round the corners). A cell whose centre
        // is outside its letter's quad read the neighbouring letter's atlas pixels and showed as a thin bar (under a third of a cell wide) at the
        // quad's edge. Every lit run of pixels along a row must be a cell (or a cell cut by the quad's edge, over half a cell), at every phase
        // of the grid under the words. (A wrong vertex order would also fail the full bars of E, T, L in the test above: the corner would be
        // wrong across the quad. TextMesh's own mesh is not readable in EditMode, so the order is proved by these renders.)
        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Words_show_no_thin_bars_at_any_grid_phase()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            TextMesh words = CentreHeadline(boards);
            Vector3 origin = words.transform.position;
            const string text = "GOALS 2 SHOT 4 OF 6";
            var failures = new System.Collections.Generic.List<string>();
            for (int phase = 0; phase < 8; phase++)
            {
                bool[] lit = LitWords(boards, words, origin, text, phase / 8f * LedCell, 0f);
                float cell = CellPixels(boards, words);
                int bars = 0, total = 0;
                string first = "";
                for (int y = 0; y < WordsH; y++)
                {
                    int run = 0;
                    for (int x = 0; x <= WordsW; x++)
                    {
                        if (x < WordsW && lit[y * WordsW + x]) { run++; total++; continue; }
                        if (run > 0 && run < 0.3f * cell) { bars++; if (first.Length == 0) first = " (first at x " + (x - run) + ", y " + y + ", " + run + " px wide, cell " + cell + " px)"; }
                        run = 0;
                    }
                }
                Assert.Greater(total, 0, "the words are lit");
                // The bleed made 60 to 127 such pixel rows (5 to 10 cells) in 3 of the 8 phases. One cell is 12 pixel rows; a single sliver
                // cell is left in one phase where a cell's centre falls on a quad edge (known, reported), so the limit is 1 cell.
                if (bars > 14) failures.Add("phase " + phase + " (" + phase / 8f + " cell right): " + bars + " pixel rows with a bar under 0.3 cell wide" + first);
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Centre_Board_words_light_no_cells_from_neighbouring_glyphs()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            boards.Show(Show(RoundPhase.SkateIn), 0.0); // "GOALS 2 . SHOT 4 OF 6": capitals and digits, no descenders
            Vector3 spot = boards.CentreOf(1) + boards.FacingOf(1) * 6f;
            _eye.transform.position = spot;
            _eye.transform.LookAt(boards.CentreOf(1));
            _camera.fieldOfView = 60f;
            const int width = 1920, height = 1080;
            Color32[] pixels = Render(width, height, null);
            Vector2[] polygon = ScreenPolygon(_camera, boards, 1, width, height);

            // Bright pixels per row inside the board. The words are one line of equal-height letters, so the letters' rows are the
            // rows with a good share of the busiest row's count; a lit cell outside them came from another glyph.
            var perRow = new int[height];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                if ((c.r > 128 || c.g > 128 || c.b > 128) && Inside(polygon, i % width, i / width)) perRow[i / width]++;
            }
            int busiest = 0;
            for (int y = 0; y < height; y++) busiest = Mathf.Max(busiest, perRow[y]);
            Assert.Greater(busiest, 0, "the Centre Board shows words");
            int bottom = -1, top = height;
            for (int y = 0; y < height; y++)
                if (perRow[y] >= busiest * 0.3f) { bottom = Mathf.Max(bottom, y); top = Mathf.Min(top, y); }
            int stray = 0;
            for (int y = 0; y < height; y++)
                if (y < top - 2 || y > bottom + 2) stray += perRow[y];
            Assert.AreEqual(0, stray, "bright pixels outside the letters' rows " + top + " to " + bottom + " (cells from a neighbouring glyph)");
        }

        [Test]
        public void A_far_Video_Board_keeps_the_colour_of_its_cells()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            _ramp = new Texture2D(1, 1, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _ramp.SetPixel(0, 0, new Color(0.8f, 0.8f, 0.8f));
            _ramp.Apply();
            boards.ShowPicture(_ramp);

            Vector3 centre = boards.CentreOf(0), facing = boards.FacingOf(0);
            _eye.transform.position = centre + facing * 5f;
            _eye.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;

            // Near: a cell centre is the picture's colour. Far: a 200 x 112 render holds the whole board in well under 2 px per cell.
            const int nearW = 1900, nearH = 1040, farW = 200, farH = 112;
            _camera.orthographicSize = boards.SizeOf(0).y / 2f + VideoBoards.FrameMargin;
            Color32[] near = Render(nearW, nearH, null);
            float nearCentre = 0f; // the middle may fall in a gap (the board is a whole number of cells tall): take the brightest pixel nearby
            for (int dy = -15; dy <= 15; dy++)
                for (int dx = -15; dx <= 15; dx++) nearCentre = Mathf.Max(nearCentre, near[(nearH / 2 + dy) * nearW + nearW / 2 + dx].r);

            float cellPixels = LedCell * farH / (2f * _camera.orthographicSize);
            Assert.Less(cellPixels, 1.5f, "the far render has to be far: a cell is " + cellPixels + " px");
            Color32[] far = Render(farW, farH, null);
            float sum = 0f;
            int n = 0;
            for (int y = farH / 2 - 8; y < farH / 2 + 8; y++)
                for (int x = farW / 2 - 16; x < farW / 2 + 16; x++) { sum += far[y * farW + x].r; n++; }
            float farMean = sum / n;
            Assert.AreEqual(nearCentre, farMean, nearCentre * 0.08f, "a far board is as bright as its cells (a gap-averaged fade is about 36 % darker)");
        }
    }
}
