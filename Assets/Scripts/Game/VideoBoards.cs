using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The Video Board, hung in front of the near end's crowd at the Ribbon Board's height and tilted toward the player, and the
    /// 4-screen Centre Board over centre ice, at their Rink Layout places. They only follow the Arena Show (Show): one dim
    /// still colour with no words from goalie set to the Shot (the Calm Area rules), otherwise the start card, the score
    /// and the last Shot speed, the result, the final score or NEW BEST! (its frame flashes with the Goal Show lights), and
    /// brand ad slides. Every screen shares one picture (one material), so the Centre Board always shows what the Video
    /// Board shows. ShowPicture puts a texture on every screen instead of the words (the Replay's render texture, #91).
    /// The words (TextMeshes that no camera draws) are drawn into a RenderTexture per board, only when their text changes, and each screen
    /// shows that texture as a picture on its LED grid (#121).
    /// </summary>
    [ExecuteAlways] // OnDestroy frees the materials in edit mode too (the editor tools and EditMode tests)
    public sealed class VideoBoards : MonoBehaviour
    {
        /// <summary>The dark frame round each screen (metres each side) and the boards' depth.</summary>
        public const float FrameMargin = (float)RinkLayout.VideoBoardFrameMargin, Depth = (float)RinkLayout.VideoBoardDepth;
        /// <summary>Seconds each ad slide shows before the next.</summary>
        public const float AdSeconds = 5f;

        const float TextLift = 0.03f;                   // the words stand just in front of their screen
        const float HeadlineShare = 0.24f, DetailsShare = 0.11f; // word heights as shares of the screen's height
        const float HeadlineY = 0.14f, DetailsY = 0.22f;         // the Headline's origin is this share of the screen's height above its centre, the Details' below
        // Measured for the font the TextMesh uses (LegacyRuntime.ttf, Liberation Sans Bold) at FontSize 64, in font pixels (0.1 TextMesh
        // units each at scale 1): a one-line TextMesh anchored MiddleCenter has its baseline 23 below its origin, its capitals are 47
        // tall, and lines are 69 apart. LedBoardLookTests (flat letters on whole rows at phase 0) fail if these drift.
        const float BaselinePx = 23f, CapPx = 47f, LinePx = 69f;
        const float TextWidth = 0.9f;                   // share of a screen the words may fill
        const float CableWidth = 0.08f;
        /// <summary>The name of the cables the boards hang from.</summary>
        public const string CablePart = "Cable";
        const int FontSize = 64;
        static readonly Color CableColour = new Color32(14, 15, 18, 255);

        sealed class Screen
        {
            public Vector3 Centre, Facing, Up; // Facing: the way the picture faces; Up: the screen's up as seen from the front
            public float Width, Height;
            public Renderer Picture;
            public TextMesh Headline, Details;
            public Material WordsMaterial; // this screen's own: the board's words RenderTexture as a picture, in this screen's colours
            public WordsBoard Board;
        }

        /// <summary>
        /// The words of one board as a picture: a RenderTexture of the words' coverage (white on black), WordsPixelsPerCell pixels per
        /// LED cell, edges on the screen's cell grid, with mips so a shader reads one cell's average at one mip level. The Video Board has
        /// one; the four Centre Board screens share one (they always show the same words), drawn from its first screen's words objects.
        /// </summary>
        sealed class WordsBoard
        {
            public RenderTexture Texture;
            public Screen Master;  // the screen whose two words objects are drawn
            public bool Stale;     // its words changed since they were drawn
        }

        readonly List<Screen> _screens = new List<Screen>();
        readonly List<WordsBoard> _boards = new List<WordsBoard>();
        Material _picture, _frame, _cable, _stillPicture;
        bool _still; // the Video Board shows the Calm Area's one dim still colour (CalmStill)
        Font _font;
        Material _words; // draws the words objects' text into a RenderTexture (the words objects are never drawn by a camera)
        CommandBuffer _draw;
        double _cell;
        int _wordsRenders;
        Transform _videoBoard, _centreBoard;
        // The Team Skin's look: the words' background, the words, and the frame (dark, and lit for a new best).
        Color _skinBack, _skinWords, _frameDark, _frameLit;
        Texture _shownPicture;
        // What the screens show now, so Show changes only what changed (it runs every frame).
        string _headline, _details;
        Color _back, _fore, _frameColour;

        /// <summary>Screen 0 is the Video Board's; 1 to 4 are the Centre Board's.</summary>
        public int ScreenCount => _screens.Count;
        public Vector3 CentreOf(int screen) => _screens[screen].Centre;
        /// <summary>The way the screen's picture faces (into the arena).</summary>
        public Vector3 FacingOf(int screen) => _screens[screen].Facing;
        public Vector2 SizeOf(int screen) => new Vector2(_screens[screen].Width, _screens[screen].Height);
        public string HeadlineOn(int screen) => WordsVisible ? _screens[screen].Headline.text : "";
        public string DetailsOn(int screen) => WordsVisible ? _screens[screen].Details.text : "";
        public Color WordsColourOn(int screen) => _screens[screen].Headline.color;
        /// <summary>The screen's picture: one material for every screen.</summary>
        public Material PictureOn(int screen) => _screens[screen].Picture.sharedMaterial;
        /// <summary>The one material every words object has, and draws its text into the RenderTexture with (depth-tested, front faces only).</summary>
        public Material WordsMaterial => _words;
        /// <summary>The colour the screens show behind the words (or the tint over a picture).</summary>
        public Color BackColour => _picture.color;
        public Color FrameColour => _frame.color;
        /// <summary>How many times a board's words have been drawn into its RenderTexture (cost: one per board per change of words).</summary>
        public int WordsRenders => _wordsRenders;
        /// <summary>How many words RenderTextures there are, and the bytes they take (all mip levels).</summary>
        public int WordsTextureCount => _boards.Count;
        public long WordsTextureBytes
        {
            get
            {
                long bytes = 0;
                foreach (WordsBoard b in _boards)
                    bytes += (long)(b.Texture.width * b.Texture.height * BytesPerPixel(b.Texture.format) * 4L / 3L);
                return bytes;
            }
        }
        static int BytesPerPixel(RenderTextureFormat f) => f == RenderTextureFormat.R8 ? 1 : 4;
        /// <summary>The words RenderTexture of a screen (the Centre Board's four share one).</summary>
        public RenderTexture WordsTextureOn(int screen) => _screens[screen].Board.Texture;
        /// <summary>Draws every board's words again (after a words object was moved or its text set directly).</summary>
        public void RedrawWords()
        {
            foreach (WordsBoard b in _boards) DrawWords(b);
        }
        /// <summary>Colours the words of one screen (a test aid: the other screens keep theirs).</summary>
        public void PaintWords(int screen, Color colour)
        {
            Screen s = _screens[screen];
            s.Headline.color = colour;
            s.Details.color = colour;
            s.WordsMaterial.color = colour;
        }
        bool WordsVisible => !_still && _shownPicture == null;
        public Transform VideoBoard => _videoBoard;
        public Transform CentreBoard => _centreBoard;

        /// <summary>The Video Board's box in world space: screen, words and frame, not the cables above it up to the roof.</summary>
        public Bounds VideoBoardBounds
        {
            get
            {
                Bounds b = new Bounds(_screens[0].Centre, Vector3.zero);
                foreach (Renderer r in _videoBoard.GetComponentsInChildren<Renderer>())
                    if (r.name != CablePart) b.Encapsulate(r.bounds);
                return b;
            }
        }

        /// <summary>
        /// Adds the Video Board and the Centre Board under rink, in the skin's frame and colours (null: the League Skin's).
        /// They show the start card until told otherwise.
        /// </summary>
        public static VideoBoards Build(Transform rink, TeamSkin skin = null)
        {
            skin = TeamSkin.OrLeague(skin);
            var root = new GameObject("Video Boards");
            root.transform.SetParent(rink, false);
            var boards = root.AddComponent<VideoBoards>();
            boards._skinBack = skin.VideoBoardBack;
            boards._skinWords = skin.VideoBoardWords;
            boards._frameDark = skin.VideoBoardFrame;
            boards._frameLit = skin.VideoBoardFrameLit;
            boards._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            double cell = RinkLayout.VideoBoardLedCell;
            boards._cell = cell;
            boards._draw = new CommandBuffer { name = "Board words" };
            boards._words = BoardWordsMaterial.MakeCoverage(boards._font);
            // The screens are LED walls: only their own picture in square cells, with no arena light or shine on them.
            boards._picture = LedBoardMaterial.Screen("Video Board Picture", cell, Color.white);
            boards._stillPicture = LedBoardMaterial.Screen("Video Board Still", cell, RibbonBoards.ColourOf(ArenaShow.CalmRibbonColour));
            // Unlit: the frame and cables show only their own colour.
            Material unlit = Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + ArenaLighting.UnlitMaterial);
            boards._frame = new Material(unlit) { name = "Video Board Frame" };
            boards._cable = new Material(unlit) { name = "Video Board Cable", color = CableColour };
            boards.BuildVideoBoard();
            boards.BuildCentreBoard();
            boards.Show(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.Idle }), 0.0);
            return boards;
        }

        /// <summary>Shows the Arena Show's answer on every screen. seconds: a running clock (real time) that cycles the ad slides.</summary>
        public void Show(ArenaState state, double seconds)
        {
            SetStill(state.VideoBoard == VideoBoardContent.CalmStill);
            string headline = state.VideoBoardWords.Headline, details = state.VideoBoardWords.Details;
            Color back = _skinBack, fore = _skinWords;
            if (state.VideoBoard == VideoBoardContent.AdSlide)
            {
                Brands.Brand b = Brands.All[(int)System.Math.Floor(seconds / AdSeconds) % Brands.All.Length];
                headline = b.AdWords;
                details = "";
                back = RibbonBoards.ColourOf(b.AdBack);
                fore = RibbonBoards.ColourOf(b.AdFore);
            }
            // The small light show for a new best: the frame flashes with the Goal Show lights (never over 3 a second).
            bool lit = state.VideoBoard == VideoBoardContent.NewBest && state.GoalShow.Lit;
            SetFrame(lit ? _frameLit : _frameDark);
            if (headline == _headline && details == _details && back == _back && fore == _fore)
            {
                DrawStaleWords(); // words changed while the screens showed something else are drawn once they are back
                return;
            }
            bool newWords = headline != _headline || details != _details;
            _headline = headline;
            _details = details;
            _back = back;
            _fore = fore;
            if (_shownPicture == null) _picture.color = back;
            if (newWords) ApplyWords(headline, details, fore);
            foreach (Screen s in _screens)
            {
                s.Headline.color = s.Details.color = fore;
                LedBoardMaterial.SetWordsColours(s.WordsMaterial, fore, back); // colours are material values: no redraw
            }
            DrawStaleWords();
        }

        /// <summary>
        /// Shows these words on every screen, as Show does for the Arena Show's: sized to whole LED cells, with the first baseline and the
        /// lines on the cell grid, and drawn into the boards' RenderTextures. For text the Arena Show does not give (mixed case).
        /// </summary>
        public void ShowWords(string headline, string details)
        {
            _headline = headline;
            _details = details;
            ApplyWords(headline, details, _fore);
            DrawStaleWords();
        }

        void ApplyWords(string headline, string details, Color fore)
        {
            foreach (Screen s in _screens)
            {
                SetWords(s, s.Headline, headline, fore, s.Height * HeadlineY, s.Height * HeadlineShare);
                SetWords(s, s.Details, details, fore, -s.Height * DetailsY, s.Height * DetailsShare);
            }
            foreach (WordsBoard b in _boards) b.Stale = true;
        }

        /// <summary>
        /// Shows picture (for example the Replay's render texture) on every screen instead of the words; null goes back to the
        /// Arena Show's words.
        /// </summary>
        public void ShowPicture(Texture picture)
        {
            _shownPicture = picture;
            _picture.mainTexture = picture;
            _picture.color = picture != null ? Color.white : _back;
            ShowScreenMaterials();
            DrawStaleWords();
        }

        // What each screen draws with: the shared picture (the Replay's), the Calm Area's still colour, or its board's words.
        void ShowScreenMaterials()
        {
            foreach (Screen s in _screens)
            {
                Material m = _shownPicture != null ? _picture : _still ? _stillPicture : s.WordsMaterial;
                if (s.Picture.sharedMaterial != m) s.Picture.sharedMaterial = m;
            }
        }

        // From goalie set to the Shot the boards show one dim still colour and no words (the Calm Area rules).
        void SetStill(bool still)
        {
            _still = still;
            ShowScreenMaterials();
        }

        // Draws the words of each board whose words changed, if its screens show words (changes under a picture wait).
        void DrawStaleWords()
        {
            if (!WordsVisible) return;
            foreach (WordsBoard b in _boards)
                if (b.Stale || !b.Texture.IsCreated()) DrawWords(b);
        }

        /// <summary>Draws the board master's Headline and Details into the board's RenderTexture as coverage, then builds its mips.</summary>
        void DrawWords(WordsBoard board)
        {
            Screen s = board.Master;
            BoardWordsMaterial.Follow(_words, _font);
            if (!board.Texture.IsCreated()) board.Texture.Create();
            // Looks at the screen from 1 m in front, straight on, with the screen filling the picture: the words objects stand
            // 0.03 m in front of it, so what the texture holds is what a viewer facing the screen sees.
            Quaternion turn = Quaternion.LookRotation(-s.Facing);
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(s.Centre + s.Facing, turn, Vector3.one).inverse;
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-s.Width / 2f, s.Width / 2f, -s.Height / 2f, s.Height / 2f, 0.1f, 2f), false);
            _draw.Clear();
            _draw.SetRenderTarget(board.Texture);
            _draw.ClearRenderTarget(true, true, Color.black);
            _draw.SetViewProjectionMatrices(view, projection);
            foreach (TextMesh t in new[] { s.Headline, s.Details })
                if (t.text.Length > 0) _draw.DrawRenderer(t.GetComponent<Renderer>(), _words, 0, 0);
            _draw.GenerateMips(board.Texture);
            Graphics.ExecuteCommandBuffer(_draw);
            board.Stale = false;
            _wordsRenders++;
        }

        void SetFrame(Color colour)
        {
            if (colour == _frameColour) return;
            _frameColour = colour;
            _frame.color = colour;
        }

        /// <summary>
        /// Sets the words object's text and puts it on the LED grid: its capitals a whole number of cells tall (never taller than asked
        /// for, or wider than the screen allows), the baseline of the first line on a cell boundary and the lines a whole number of
        /// cells apart. A flat capital then covers whole cells, and a round letter's overshoot past the cap line or baseline (about
        /// a quarter of a cell) is under half of the cell it reaches into, so it does not light it.
        /// yOffset: where the words object's origin is above the screen's centre, before the baseline is moved onto the grid.
        /// </summary>
        void SetWords(Screen s, TextMesh t, string text, Color colour, float yOffset, float height)
        {
            t.text = text;
            t.color = colour;
            float cell = (float)_cell;
            float scale = TextScale(text, height, s.Width * TextWidth);
            int capCells = Mathf.Max(1, Mathf.FloorToInt(CapPx * 0.1f * scale / cell));
            scale = capCells * cell / (CapPx * 0.1f);
            t.transform.localScale = new Vector3(scale, scale, 1f);
            float lineMetres = LinePx * 0.1f * scale;
            float pitch = Mathf.Max(1f, Mathf.Round(lineMetres / cell)) * cell; // between two lines' baselines
            t.lineSpacing = pitch / lineMetres;
            int lines = 1;
            foreach (char c in text) if (c == '\n') lines++;
            // The first line's baseline above the screen's bottom edge (the grid's first row line), and the nudge that puts it on a row line.
            float baseline = s.Height / 2f + yOffset - BaselinePx * 0.1f * scale + (lines - 1) / 2f * pitch;
            float nudge = Mathf.Round(baseline / cell) * cell - baseline;
            t.transform.position = s.Centre + s.Facing * TextLift + s.Up * (yOffset + nudge);
        }

        /// <summary>The words' scale: each line height metres tall (one font size in TextMesh units is 0.1), narrowed so the longest line fits maxWidth.</summary>
        float TextScale(string text, float height, float maxWidth)
        {
            float scale = height / (FontSize * 0.1f);
            float widest = 0f;
            foreach (string line in text.Split('\n'))
            {
                if (line.Length == 0) continue;
                _font.RequestCharactersInTexture(line, FontSize, FontStyle.Bold);
                BoardWordsMaterial.Follow(_words, _font);
                float advance = 0f;
                foreach (char c in line)
                    if (_font.GetCharacterInfo(c, out CharacterInfo info, FontSize, FontStyle.Bold))
                        advance += info.advance;
                widest = Mathf.Max(widest, advance * 0.1f * scale);
            }
            return widest > maxWidth ? scale * maxWidth / widest : scale;
        }

        void BuildVideoBoard()
        {
            _videoBoard = new GameObject("Video Board").transform;
            _videoBoard.SetParent(transform, false);
            var centre = ToVector(RinkLayout.VideoBoard);
            // Faces the player (-z) and leans its face down toward his eye at the start of the skate-in (its top toward him).
            float tilt = (float)RinkLayout.VideoBoardTiltDegrees * Mathf.Deg2Rad;
            var facing = new Vector3(0f, -Mathf.Sin(tilt), -Mathf.Cos(tilt));
            AddScreen(_videoBoard, "Video Board Screen", centre, facing,
                      (float)RinkLayout.VideoBoardWidth, (float)RinkLayout.VideoBoardHeight, NewWordsBoard("Video Board words",
                      (float)RinkLayout.VideoBoardWidth, (float)RinkLayout.VideoBoardHeight));
            Screen s = _screens[0];
            Quaternion turn = Quaternion.LookRotation(-s.Facing);
            AddBox(_videoBoard, "Video Board Frame", centre + turn * new Vector3(0f, 0f, Depth / 2f + 0.01f), turn,
                   new Vector3(s.Width + 2f * FrameMargin, s.Height + 2f * FrameMargin, Depth), _frame);
            // Two cables up to the roof from the back of the frame's top (the side away from the light banks' row in front).
            Vector3 up = turn * Vector3.up, back = turn * Vector3.forward;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 top = centre + up * (s.Height / 2f + FrameMargin) + back * Depth + Vector3.right * side * s.Width * 0.35f;
                AddCable(_videoBoard, top);
            }
        }

        void BuildCentreBoard()
        {
            _centreBoard = new GameObject("Centre Board").transform;
            _centreBoard.SetParent(transform, false);
            var middle = ToVector(RinkLayout.CentreBoard);
            float w = (float)RinkLayout.CentreBoardScreenWidth, h = (float)RinkLayout.CentreBoardScreenHeight;
            AddBox(_centreBoard, "Centre Board Box", middle, Quaternion.identity, new Vector3(w, h + 2f * FrameMargin, w), _frame);
            WordsBoard words = NewWordsBoard("Centre Board words", w, h); // the four screens show the same words: one picture
            foreach (Vector3 outward in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                AddScreen(_centreBoard, "Centre Board Screen", middle + outward * (w / 2f + 0.01f), outward, w, h, words);
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                AddCable(_centreBoard, middle + new Vector3(x * w * 0.4f, h / 2f + FrameMargin, z * w * 0.4f));
        }

        // The words' RenderTexture: a whole number of LED cells (the screens are) at WordsPixelsPerCell pixels each, so its edges lie on the
        // screen's cell grid and a mip level averages exactly one cell. R8 holds the coverage (the words are white on black).
        WordsBoard NewWordsBoard(string name, float width, float height)
        {
            int p = LedBoardMaterial.WordsPixelsPerCell;
            int cellsX = Mathf.RoundToInt((float)(width / _cell)), cellsY = Mathf.RoundToInt((float)(height / _cell));
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
            var texture = new RenderTexture(cellsX * p, cellsY * p, 0, format, RenderTextureReadWrite.Linear)
            {
                name = name,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            var board = new WordsBoard { Texture = texture, Stale = true };
            _boards.Add(board);
            return board;
        }

        void AddScreen(Transform parent, string name, Vector3 centre, Vector3 facing, float width, float height, WordsBoard board)
        {
            int index = _screens.Count;
            // A quad shows its face to local -z: turn it so that face looks along facing.
            Quaternion turn = Quaternion.LookRotation(-facing);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name + " " + index;
            quad.transform.SetParent(parent, false);
            quad.transform.SetPositionAndRotation(centre, turn);
            quad.transform.localScale = new Vector3(width, height, 1f);
            SceneObjects.DestroySafely(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var screen = new Screen { Centre = centre, Facing = facing, Width = width, Height = height, Picture = renderer, Board = board };
            screen.WordsMaterial = LedBoardMaterial.WordsOnScreen("Video Board Words " + index, _cell, board.Texture, _skinWords, _skinBack);
            renderer.sharedMaterial = screen.WordsMaterial;
            if (board.Master == null) board.Master = screen;
            Vector3 up = turn * Vector3.up;
            screen.Up = up;
            screen.Headline = AddWords(parent, name + " Headline " + index, centre + facing * TextLift + up * height * HeadlineY, turn);
            screen.Details = AddWords(parent, name + " Details " + index, centre + facing * TextLift - up * height * DetailsY, turn);
            // One LED grid per screen, starting at its lower-left corner (as the player sees it): whole cells at the edges.
            // The words' RenderTexture has its edges there too. (The words objects are drawn into it, not into the arena.)
            Vector3 corner = centre - turn * Vector3.right * (width / 2f) - up * (height / 2f);
            LedBoardMaterial.Anchor(renderer, corner);
            _screens.Add(screen);
        }

        TextMesh AddWords(Transform parent, string name, Vector3 position, Quaternion turn)
        {
            var words = new GameObject(name);
            words.transform.SetParent(parent, false);
            words.transform.SetPositionAndRotation(position, turn);
            var text = words.AddComponent<TextMesh>();
            text.font = _font;
            text.fontSize = FontSize;
            text.characterSize = 1f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            var renderer = words.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _words;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // No camera draws the words objects (the Shooter Camera, the Replay Camera, a test's camera whatever its layer or mask):
            // they are only the source of the text VideoBoards draws into the board's RenderTexture. The screen shows that.
            renderer.enabled = false;
            return text;
        }

        void AddBox(Transform parent, string name, Vector3 centre, Quaternion turn, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.SetPositionAndRotation(centre, turn);
            box.transform.localScale = size;
            SceneObjects.DestroySafely(box.GetComponent<Collider>());
            var renderer = box.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = material;
        }

        /// <summary>Frees the materials the boards made (not the shown picture: the Replay owns it; the words' material is ours; not the font's).</summary>
        void OnDestroy()
        {
            foreach (Material m in new[] { _picture, _frame, _cable, _stillPicture, _words })
                if (m != null) SceneObjects.DestroySafely(m);
            foreach (Screen s in _screens)
                if (s.WordsMaterial != null) SceneObjects.DestroySafely(s.WordsMaterial);
            foreach (WordsBoard b in _boards)
                if (b.Texture != null)
                {
                    b.Texture.Release();
                    SceneObjects.DestroySafely(b.Texture);
                }
            _draw?.Release();
        }

        void AddCable(Transform parent, Vector3 bottom)
        {
            float length = (float)RinkLayout.RoofHeight - bottom.y;
            AddBox(parent, CablePart, bottom + Vector3.up * length / 2f, Quaternion.identity, new Vector3(CableWidth, length, CableWidth), _cable);
        }

        static Vector3 ToVector(RinkPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
    }
}
