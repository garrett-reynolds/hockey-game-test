using System.Collections.Generic;
using HockeyGame.Core;
using Unity.Collections;
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
    /// The words are drawn in the LED font (LedFont), one texel of a small texture per LED cell, so every letter is whole cells. The texture
    /// is written only when the words change, and each screen shows it as a picture on its LED grid (#121).
    /// </summary>
    [ExecuteAlways] // OnDestroy frees the materials in edit mode too (the editor tools and EditMode tests)
    public sealed class VideoBoards : MonoBehaviour
    {
        /// <summary>The dark frame round each screen (metres each side) and the boards' depth.</summary>
        public const float FrameMargin = (float)RinkLayout.VideoBoardFrameMargin, Depth = (float)RinkLayout.VideoBoardDepth;
        /// <summary>Seconds each ad slide shows before the next.</summary>
        public const float AdSeconds = 5f;

        /// <summary>Word heights as shares of the screen's height: the capitals of a line fit this much (LedWords picks the whole-number scale).</summary>
        public const double HeadlineShare = 0.24, DetailsShare = 0.11;
        /// <summary>The Headline's middle is this share of the screen's height above the screen's centre, the Details' middle this share below.</summary>
        public const double HeadlineY = 0.14, DetailsY = 0.22;
        const float CableWidth = 0.08f;
        /// <summary>The name of the cables the boards hang from.</summary>
        public const string CablePart = "Cable";
        static readonly Color CableColour = new Color32(14, 15, 18, 255);

        sealed class Screen
        {
            public Vector3 Centre, Facing, Up; // Facing: the way the picture faces; Up: the screen's up as seen from the front
            public float Width, Height;
            public int CellsX, CellsY;         // the screen in LED cells
            public Renderer Picture;
            public string Headline = "", Details = "";
            public Color WordsColour;
            public Material WordsMaterial; // this screen's own: the board's words texture as a picture, in this screen's colours
            public WordsBoard Board;
        }

        /// <summary>
        /// The words of one board as a picture: a texture with exactly one R8 texel per LED cell (255 lit, 0 not), point filtered, no mips.
        /// The Video Board has one; the four Centre Board screens share one (they always show the same words), laid out from its first screen.
        /// </summary>
        sealed class WordsBoard
        {
            public Texture2D Texture;
            public byte[] Texels;
            public bool[] Cells;
            public Screen Master;  // the screen whose words are written
            public bool Stale;     // its words changed since they were written
            public bool Written;   // its texture holds WrittenHeadline and WrittenDetails
            public string WrittenHeadline, WrittenDetails;
        }

        readonly List<Screen> _screens = new List<Screen>();
        readonly List<WordsBoard> _boards = new List<WordsBoard>();
        Material _picture, _frame, _cable, _stillPicture;
        bool _still; // the Video Board shows the Calm Area's one dim still colour (CalmStill)
        double _cell;
        int _wordsUploads;
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
        /// <summary>The way up on the screen's picture, as the player sees it.</summary>
        public Vector3 UpOf(int screen) => _screens[screen].Up;
        /// <summary>The screen's size in whole LED cells.</summary>
        public int CellsAcross(int screen) => _screens[screen].CellsX;
        public int CellsUp(int screen) => _screens[screen].CellsY;
        /// <summary>The words the screens show now (the strings the LED cells are drawn from).</summary>
        public string HeadlineOn(int screen) => WordsVisible ? _screens[screen].Headline : "";
        public string DetailsOn(int screen) => WordsVisible ? _screens[screen].Details : "";
        public Color WordsColourOn(int screen) => _screens[screen].WordsColour;
        /// <summary>The screen's picture: its own words material, the shared still or the shared Replay picture.</summary>
        public Material PictureOn(int screen) => _screens[screen].Picture.sharedMaterial;
        /// <summary>The colour the screens show behind the words (or the tint over a picture).</summary>
        public Color BackColour => _picture.color;
        public Color FrameColour => _frame.color;
        /// <summary>How many times a board's texture has been uploaded (cost: one per board per change of words; colours cost none).</summary>
        public int WordsUploads => _wordsUploads;
        /// <summary>How many words textures there are, and the bytes they take (R8, one byte per LED cell).</summary>
        public int WordsTextureCount => _boards.Count;
        public long WordsTextureBytes
        {
            get
            {
                long bytes = 0;
                foreach (WordsBoard b in _boards) bytes += b.Texels.Length;
                return bytes;
            }
        }
        /// <summary>The words texture of a screen (the Centre Board's four share one).</summary>
        public Texture2D WordsTextureOn(int screen) => _screens[screen].Board.Texture;
        /// <summary>The cells the screen's words texture has lit, read from the texels themselves (index y * CellsAcross + x, row 0 at the bottom).</summary>
        public bool[] LitCellsOn(int screen)
        {
            NativeArray<byte> texels = _screens[screen].Board.Texture.GetRawTextureData<byte>();
            var lit = new bool[texels.Length];
            for (int i = 0; i < lit.Length; i++) lit[i] = texels[i] != 0;
            return lit;
        }
        /// <summary>Colours the words of one screen (a test aid: the other screens keep theirs).</summary>
        public void PaintWords(int screen, Color colour)
        {
            Screen s = _screens[screen];
            s.WordsColour = colour;
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
            double cell = RinkLayout.VideoBoardLedCell;
            boards._cell = cell;
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
            if (state.VideoBoard == VideoBoardContent.StartCard)
            {
                // The Shootout logo's look (#151) as far as one colour of LED words allows: its white letters on its red band.
                back = LookC.Red;
                fore = LookC.White;
            }
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
            if (newWords) ApplyWords(headline, details);
            foreach (Screen s in _screens)
            {
                s.WordsColour = fore;
                LedBoardMaterial.SetWordsColours(s.WordsMaterial, fore, back); // colours are material values: no upload
            }
            DrawStaleWords();
        }

        /// <summary>
        /// Shows these words on every screen, as Show does for the Arena Show's: in the LED font, in whole cells (see LedWords), and
        /// written to the boards' textures. For text the Arena Show does not give (mixed case).
        /// </summary>
        public void ShowWords(string headline, string details)
        {
            _headline = headline;
            _details = details;
            ApplyWords(headline, details);
            DrawStaleWords();
        }

        void ApplyWords(string headline, string details)
        {
            foreach (Screen s in _screens)
            {
                s.Headline = headline;
                s.Details = details;
            }
            foreach (WordsBoard b in _boards) b.Stale = true;
        }

        /// <summary>
        /// Shows picture (for example the Replay's render texture) on every screen instead of the words; null goes back to the
        /// Arena Show's words. A picture is drawn sharp (smoothly filtered, no LED cells or gaps); words always keep the LED look.
        /// </summary>
        public void ShowPicture(Texture picture)
        {
            _shownPicture = picture;
            _picture.mainTexture = picture;
            _picture.color = picture != null ? Color.white : _back;
            LedBoardMaterial.SetPlain(_picture, picture != null); // the Replay is sharp, with no LED cells (#127); the words keep theirs
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
                if (b.Stale) WriteWords(b);
        }

        /// <summary>
        /// Writes the board master's Headline and Details into the board's texels from the font table (LedWords), and uploads the
        /// texture once. The Headline is placed by HeadlineShare and HeadlineY, the Details by DetailsShare and DetailsY.
        /// </summary>
        void WriteWords(WordsBoard board)
        {
            Screen s = board.Master;
            if (board.Written && s.Headline == board.WrittenHeadline && s.Details == board.WrittenDetails)
            {
                board.Stale = false; // the texture already holds these words (for example after the Calm Area): no upload
                return;
            }
            board.Written = true;
            board.WrittenHeadline = s.Headline;
            board.WrittenDetails = s.Details;
            System.Array.Clear(board.Cells, 0, board.Cells.Length);
            LedWords.Plot(board.Cells, s.CellsX, s.CellsY, LedWords.Place(s.Headline, s.CellsX, s.CellsY, HeadlineShare, HeadlineY));
            LedWords.Plot(board.Cells, s.CellsX, s.CellsY, LedWords.Place(s.Details, s.CellsX, s.CellsY, DetailsShare, -DetailsY));
            for (int i = 0; i < board.Cells.Length; i++) board.Texels[i] = board.Cells[i] ? (byte)255 : (byte)0;
            board.Texture.SetPixelData(board.Texels, 0);
            board.Texture.Apply(false);
            board.Stale = false;
            _wordsUploads++;
        }

        void SetFrame(Color colour)
        {
            if (colour == _frameColour) return;
            _frameColour = colour;
            _frame.color = colour;
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

        // The words' texture: exactly one texel per LED cell (the screens are whole cells), R8, point filtered, clamped, no mips.
        WordsBoard NewWordsBoard(string name, float width, float height)
        {
            int cellsX = Mathf.RoundToInt((float)(width / _cell)), cellsY = Mathf.RoundToInt((float)(height / _cell));
            var texture = new Texture2D(cellsX, cellsY, TextureFormat.R8, false, true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var board = new WordsBoard { Texture = texture, Texels = new byte[cellsX * cellsY], Cells = new bool[cellsX * cellsY], Stale = true };
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
            var screen = new Screen
            {
                Centre = centre, Facing = facing, Width = width, Height = height, Picture = renderer, Board = board, WordsColour = _skinWords,
                CellsX = Mathf.RoundToInt((float)(width / _cell)), CellsY = Mathf.RoundToInt((float)(height / _cell))
            };
            screen.WordsMaterial = LedBoardMaterial.WordsOnScreen("Video Board Words " + index, _cell, board.Texture, _skinWords, _skinBack);
            renderer.sharedMaterial = screen.WordsMaterial;
            if (board.Master == null) board.Master = screen;
            Vector3 up = turn * Vector3.up;
            screen.Up = up;
            // One LED grid per screen, starting at its lower-left corner (as the player sees it): whole cells at the edges.
            // The words' texture has one texel per cell, its first texel at that corner.
            Vector3 corner = centre - turn * Vector3.right * (width / 2f) - up * (height / 2f);
            LedBoardMaterial.Anchor(renderer, corner);
            _screens.Add(screen);
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

        /// <summary>Frees the materials the boards made (not the shown picture: the Replay owns it) and the words textures.</summary>
        void OnDestroy()
        {
            foreach (Material m in new[] { _picture, _frame, _cable, _stillPicture })
                if (m != null) SceneObjects.DestroySafely(m);
            foreach (Screen s in _screens)
                if (s.WordsMaterial != null) SceneObjects.DestroySafely(s.WordsMaterial);
            foreach (WordsBoard b in _boards)
                if (b.Texture != null) SceneObjects.DestroySafely(b.Texture);
        }

        void AddCable(Transform parent, Vector3 bottom)
        {
            float length = (float)RinkLayout.RoofHeight - bottom.y;
            AddBox(parent, CablePart, bottom + Vector3.up * length / 2f, Quaternion.identity, new Vector3(CableWidth, length, CableWidth), _cable);
        }

        static Vector3 ToVector(RinkPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
    }
}
