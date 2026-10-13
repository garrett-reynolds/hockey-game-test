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
        const float TextWidth = 0.9f;                   // share of a screen the words may fill
        const float CableWidth = 0.08f;
        /// <summary>The name of the cables the boards hang from.</summary>
        public const string CablePart = "Cable";
        const int FontSize = 64;
        static readonly Color CableColour = new Color32(14, 15, 18, 255);

        sealed class Screen
        {
            public Vector3 Centre, Facing; // Facing: the way the picture faces
            public float Width, Height;
            public Renderer Picture;
            public TextMesh Headline, Details;
        }

        readonly List<Screen> _screens = new List<Screen>();
        Material _picture, _frame, _cable, _stillPicture;
        bool _still; // the Video Board shows the Calm Area's one dim still colour (CalmStill)
        Font _font;
        Material _words;
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
        public string HeadlineOn(int screen) => WordsShown(_screens[screen].Headline);
        public string DetailsOn(int screen) => WordsShown(_screens[screen].Details);
        public Color WordsColourOn(int screen) => _screens[screen].Headline.color;
        /// <summary>The screen's picture: one material for every screen.</summary>
        public Material PictureOn(int screen) => _screens[screen].Picture.sharedMaterial;
        /// <summary>The one material every words object uses (depth-tested, front faces only).</summary>
        public Material WordsMaterial => _words;
        /// <summary>The colour the screens show behind the words (or the tint over a picture).</summary>
        public Color BackColour => _picture.color;
        public Color FrameColour => _frame.color;
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

        static string WordsShown(TextMesh t) => t.GetComponent<Renderer>().enabled ? t.text : "";

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
            boards._words = BoardWordsMaterial.Make(boards._font);
            // Unlit: the screens show only their own picture, with no arena light or shine on them.
            Material unlit = Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + ArenaLighting.UnlitMaterial);
            boards._picture = new Material(unlit) { name = "Video Board Picture" };
            boards._frame = new Material(unlit) { name = "Video Board Frame" };
            boards._stillPicture = new Material(unlit) { name = "Video Board Still", color = RibbonBoards.ColourOf(ArenaShow.CalmRibbonColour) };
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
            if (headline == _headline && details == _details && back == _back && fore == _fore) return;
            _headline = headline;
            _details = details;
            _back = back;
            _fore = fore;
            if (_shownPicture == null) _picture.color = back;
            foreach (Screen s in _screens)
            {
                SetWords(s.Headline, headline, fore, s.Height * HeadlineShare, s.Width * TextWidth);
                SetWords(s.Details, details, fore, s.Height * DetailsShare, s.Width * TextWidth);
            }
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
            foreach (Screen s in _screens)
            {
                bool words = picture == null && !_still;
                s.Headline.GetComponent<Renderer>().enabled = words;
                s.Details.GetComponent<Renderer>().enabled = words;
            }
        }

        // From goalie set to the Shot the boards show one dim still colour and no words (the Calm Area rules).
        void SetStill(bool still)
        {
            _still = still;
            foreach (Screen s in _screens)
            {
                s.Picture.sharedMaterial = still ? _stillPicture : _picture;
                bool words = !still && _shownPicture == null;
                s.Headline.GetComponent<Renderer>().enabled = words;
                s.Details.GetComponent<Renderer>().enabled = words;
            }
        }

        void SetFrame(Color colour)
        {
            if (colour == _frameColour) return;
            _frameColour = colour;
            _frame.color = colour;
        }

        void SetWords(TextMesh t, string text, Color colour, float height, float maxWidth)
        {
            t.text = text;
            t.color = colour;
            float scale = TextScale(text, height, maxWidth);
            t.transform.localScale = new Vector3(scale, scale, 1f);
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
                      (float)RinkLayout.VideoBoardWidth, (float)RinkLayout.VideoBoardHeight);
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
            foreach (Vector3 outward in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                AddScreen(_centreBoard, "Centre Board Screen", middle + outward * (w / 2f + 0.01f), outward, w, h);
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                AddCable(_centreBoard, middle + new Vector3(x * w * 0.4f, h / 2f + FrameMargin, z * w * 0.4f));
        }

        void AddScreen(Transform parent, string name, Vector3 centre, Vector3 facing, float width, float height)
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
            renderer.sharedMaterial = _picture;

            var screen = new Screen { Centre = centre, Facing = facing, Width = width, Height = height, Picture = renderer };
            Vector3 up = turn * Vector3.up;
            screen.Headline = AddWords(parent, name + " Headline " + index, centre + facing * TextLift + up * height * 0.14f, turn);
            screen.Details = AddWords(parent, name + " Details " + index, centre + facing * TextLift - up * height * 0.22f, turn);
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
        }

        void AddCable(Transform parent, Vector3 bottom)
        {
            float length = (float)RinkLayout.RoofHeight - bottom.y;
            AddBox(parent, CablePart, bottom + Vector3.up * length / 2f, Quaternion.identity, new Vector3(CableWidth, length, CableWidth), _cable);
        }

        static Vector3 ToVector(RinkPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
    }
}
