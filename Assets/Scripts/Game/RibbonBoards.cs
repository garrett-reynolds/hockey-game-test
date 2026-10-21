using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The Ribbon Boards: LED panels all round the stands, on the arena model's ribbon ring between the tiers (it hides the
    /// model's own plain "Ribbon" part). Each panel is a grid of square LED cells (RinkLayout.RibbonBoardLedCell, with a dark gap)
    /// starting at its lower-left corner; its words are the LED font (LedFont) written into a small texture with one texel per
    /// whole cell, shown through the LED shader. They only follow the Arena Show (Show): brand ads that cycle, the result words
    /// (GOAL!, SAVE, OFF THE POST!, OFF THE BAR!), and in the Calm Area behind the near net one dim still colour from goalie
    /// set to the Shot. A panel is in the Calm Area when it stands behind the near end boards. A Team Skin may give the
    /// panels edges in a team colour: a thin strip along the top and bottom of each panel, never in the Calm Area (the
    /// League Skin has none). The ads are the fixed brands (Brands): no skin changes them.
    /// <para>
    /// The ring is the Rink Layout's full rink outline pushed out by Offset (and a further BenchDepth beside the benches and
    /// penalty boxes, where the stands step back), as Tools/make_arena.py builds the ribbon from the same Rink Layout
    /// numbers: every size here comes from RinkLayout.
    /// </para>
    /// </summary>
    [ExecuteAlways] // OnDestroy frees the materials in edit mode too (the editor tools and EditMode tests)
    public sealed class RibbonBoards : MonoBehaviour
    {
        /// <summary>How far the ribbon ring stands out from the inside of the boards (metres).</summary>
        public const float Offset = (float)RinkLayout.RibbonOffset;
        /// <summary>Height of the ribbon's bottom and the ribbon's height (metres).</summary>
        public const float Bottom = (float)RinkLayout.RibbonBottom, Height = (float)RinkLayout.RibbonHeight;
        /// <summary>The rink's corner radius (metres).</summary>
        public const float CornerRadius = (float)RinkLayout.CornerRadius;
        /// <summary>About how long one panel is along the ring (metres).</summary>
        public const float PanelLength = 6f;
        /// <summary>Seconds each ad shows before the next.</summary>
        public const float AdSeconds = 5f;

        const float Inset = 0.05f;       // the panels stand just inside the model's ribbon
        const float TextLift = 0.02f;    // the edge strips stand just in front of their panel
        // The largest scale (cells per font dot) each kind of words may take: the panel is 24 cells tall, so 3 is 21 rows of capitals.
        const int BigScale = 3, SmallScale = 2, AdScale = 2;
        /// <summary>The height of a team-coloured edge strip (metres), on the panel's top and bottom.</summary>
        public const float EdgeHeight = 0.08f;
        /// <summary>The name of the team-coloured edge strips.</summary>
        public const string EdgePart = "Ribbon Edge";
        const float AdGlow = 1f;         // an ad's background shows this much of its colour (1: the same as the Video Board shows the brand; ruling F1)
        static readonly Color WordsBack = new Color32(14, 16, 22, 255);
        static readonly float Cell = (float)RinkLayout.RibbonBoardLedCell;
        static readonly int CellsUp = Mathf.RoundToInt(Height / Cell);
        static readonly int BackdropId = Shader.PropertyToID("_Backdrop");

        sealed class Panel
        {
            public Vector3 Centre;
            public float Length;
            public ArenaZone Zone;
            public Material Back;          // the LED material: the words texture in picture mode, the words colour and the backdrop
            public Texture2D Texture;      // one R8 texel per whole LED cell along the panel and up it
            public byte[] Texels;
            public int CellsX;
            public LedWords.Fitted Fit;
            // What it shows now, so Show changes only what changed (it runs every frame).
            public string Text;
            public int MaxScale;
            public Color Fore, Backdrop;
            public float WordsScaleX;      // the words texture's uv stretch (its texels on whole cells)
            // The scrolling ad picture (#223): where the panel sits round the ring, which way the viewer's right runs, and where
            // its left-hand end is along the reading line (metres).
            public double Along, ReadStart;
            public bool RightAlongRing;
            public Texture2D Picture;      // the ad picture shown now; null: the words texture
        }

        readonly List<Panel> _panels = new List<Panel>();
        bool[] _cells = new bool[0];
        int _wordsUploads;
        Material _edge; // the skin's edge colour; null: no edges

        public int PanelCount => _panels.Count;
        /// <summary>How many times a panel's words texture was uploaded (only when a panel's words change).</summary>
        public int WordsUploads => _wordsUploads;
        /// <summary>The ring's panels left out because the Video Board hides them wholly (their two ends), for tests.</summary>
        public readonly List<(Vector2 From, Vector2 To)> LeftOut = new List<(Vector2 From, Vector2 To)>();
        public Vector3 CentreOf(int panel) => _panels[panel].Centre;
        public float LengthOf(int panel) => _panels[panel].Length;
        public ArenaZone ZoneOf(int panel) => _panels[panel].Zone;
        /// <summary>The words the Arena Show asked the panel for (a long ad may show fewer letters: FitOn).</summary>
        public string TextOn(int panel) => _panels[panel].Text ?? "";
        /// <summary>The colour of the panel's lit cells.</summary>
        public Color TextColourOn(int panel) => _panels[panel].Back.color;
        /// <summary>The colour of the panel's cells with no words (what it shows as its background).</summary>
        public Color BackOn(int panel) => _panels[panel].Back.GetColor(BackdropId);
        /// <summary>Cells per font dot of the panel's words (0: none).</summary>
        public float TextScaleOn(int panel) => _panels[panel].Fit.Scale;
        /// <summary>Whole LED cells along the panel (the texture's width); the partial cell at the right-hand end shows the backdrop colour.</summary>
        public int CellsAcross(int panel) => _panels[panel].CellsX;
        /// <summary>The panel's words texture (R8, one texel per cell, row 0 at the bottom).</summary>
        public Texture2D WordsTextureOn(int panel) => _panels[panel].Texture;
        /// <summary>Where the panel's words sit: the letters kept, their scale, left column and baseline row.</summary>
        public LedWords.Fitted FitOn(int panel) => _panels[panel].Fit;

        public static Color ColourOf(ArenaColour c) => new Color32(c.R, c.G, c.B, 255);

        /// <summary>How fast the ad picture scrolls to the left round the ring (metres a second, #223): like a news ticker.</summary>
        public const float ScrollMetresPerSecond = 1.5f;
        static readonly int LitAboveId = Shader.PropertyToID("_LitAbove"), WrapId = Shader.PropertyToID("_Wrap");
        double _ring, _loop = 1.0, _seconds;
        Texture2D _loopOf;

        /// <summary>The brand's Ribbon ad picture: Resources Ads/ribbon-&lt;id&gt; (a strip that tiles), else its panel art; null: none.</summary>
        public static Texture2D PictureOf(Brands.Brand brand) => ArtLookup.Ribbon(brand);

        /// <summary>The ad picture's tiling on the panel at the last Show: x its scale, z its offset (the scroll), along the panel.</summary>
        public Vector4 PictureSTOn(int panel)
        {
            Panel p = _panels[panel];
            double offset = (p.ReadStart + ScrollMetresPerSecond * _seconds) / _loop;
            return new Vector4((float)(p.Length / _loop), 1f, (float)(offset - System.Math.Floor(offset)), 0f);
        }

        /// <summary>One repeat of the ad picture along the ribbon (metres): its own shape at the ribbon's height, stretched a
        /// little so a whole number of repeats fills the ring (no seam where the loop closes).</summary>
        public double LoopMetres => _loop;
        /// <summary>The ring's length round its middle (metres), the panels left out behind the Video Board counted.</summary>
        public double RingMetres => _ring;
        /// <summary>The panel whose left-hand end meets this panel's right-hand end, as the viewer sees them; -1: none (a left-out
        /// panel behind the Video Board between them).</summary>
        public int RightNeighbourOf(int panel)
        {
            Panel p = _panels[panel];
            for (int i = 0; i < _panels.Count; i++)
            {
                double gap = (_panels[i].ReadStart - (p.ReadStart + p.Length)) % _ring;
                if (System.Math.Abs(gap) < 1e-3 || System.Math.Abs(System.Math.Abs(gap) - _ring) < 1e-3) return i;
            }
            return -1;
        }

        void SetLoopFor(Texture2D picture)
        {
            if (picture == _loopOf) return;
            _loopOf = picture;
            double own = Height * picture.width / (double)picture.height;
            int repeats = System.Math.Max(1, (int)System.Math.Round(_ring / own));
            _loop = _ring / repeats;
        }

        // The ad picture on the panel, scrolling left: no upload, only material values (the words texture keeps its words).
        void ShowPicture(int panel, Brands.Brand brand)
        {
            Panel p = _panels[panel];
            Texture2D picture = PictureOf(brand);
            if (picture == null) { ShowWords(p); Apply(p, brand.AdWords, ColourOf(brand.AdFore), ColourOf(brand.AdBack), AdGlow, AdScale); return; }
            SetLoopFor(picture);
            if (p.Picture != picture)
            {
                p.Picture = picture;
                p.Back.mainTexture = picture;
                p.Back.SetFloat(LitAboveId, 0f);
                p.Back.SetFloat(WrapId, 1f);
                p.Back.color = Color.white;
                p.Fore = p.Backdrop = Color.clear; // the words colours are written again when words come back
                p.Text = brand.AdWords;
                p.MaxScale = -1;
            }
            Vector4 st = PictureSTOn(panel);
            p.Back.mainTextureScale = new Vector2(st.x, 1f);
            p.Back.mainTextureOffset = new Vector2(st.z, 0f);
        }

        // Back from the ad picture to the words texture.
        void ShowWords(Panel p)
        {
            if (p.Picture == null) return;
            p.Picture = null;
            p.Back.mainTexture = p.Texture;
            p.Back.SetFloat(LitAboveId, 0.5f);
            p.Back.SetFloat(WrapId, 0f);
            p.Back.mainTextureScale = new Vector2(p.WordsScaleX, 1f);
            p.Back.mainTextureOffset = Vector2.zero;
        }

        /// <summary>Frees what the boards made: each panel's LED material and words texture, and the edge strips' material.</summary>
        void OnDestroy()
        {
            foreach (Panel p in _panels)
            {
                if (p.Back != null) SceneObjects.DestroySafely(p.Back);
                if (p.Texture != null) SceneObjects.DestroySafely(p.Texture);
            }
            if (_edge != null) SceneObjects.DestroySafely(_edge);
        }

        /// <summary>Adds the Ribbon Boards under rink, with the skin's edges (null: the League Skin's), and hides the arena
        /// model's own ribbon. They show ads until told otherwise.</summary>
        public static RibbonBoards Build(Transform rink, TeamSkin skin = null)
        {
            skin = TeamSkin.OrLeague(skin);
            foreach (Renderer r in rink.GetComponentsInChildren<Renderer>())
                if (r.name == ArenaStands.GlowPart) r.enabled = false;

            var root = new GameObject("Ribbon Boards");
            root.transform.SetParent(rink, false);
            var ribbons = root.AddComponent<RibbonBoards>();
            if (skin.RibbonEdgesOn)
            {
                ribbons._edge = ArenaLighting.Glow(Color.black, skin.RibbonEdge);
                ribbons._edge.SetFloat("_Smoothness", 0f);
            }
            List<(Vector2 From, Vector2 To)> ring = Ring(Offset - Inset);
            int index = 0;
            double along = 0; // metres round the ring to this piece's `from` end, the left-out pieces counted (the ad scroll runs round them)
            foreach ((Vector2 from, Vector2 to) in ring)
            {
                double pieceLength = Vector2.Distance(from, to);
                // A panel wholly behind the Video Board is never seen: leave it out.
                if (VideoBoardCover.HidesRibbonPanel(from.x, from.y, to.x, to.y)) ribbons.LeftOut.Add((from, to));
                else ribbons.AddPanel(index++, from, to, along);
                along += pieceLength;
            }
            ribbons._ring = along;
            // Where each panel's left-hand end (as the viewer sees it) is along the reading line: round the ring with it, or against.
            foreach (Panel p in ribbons._panels)
                p.ReadStart = p.RightAlongRing ? p.Along : along - (p.Along + p.Length);
            ribbons.Show(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.Idle }), 0.0);
            return ribbons;
        }

        /// <summary>Shows the Arena Show's answer. seconds: a running clock (real time) that cycles the ads.</summary>
        public void Show(ArenaState state, double seconds)
        {
            _seconds = seconds;
            for (int i = 0; i < _panels.Count; i++)
            {
                Panel p = _panels[i];
                RibbonBoard board = state.RibbonIn(p.Zone);
                switch (board.Message)
                {
                    case RibbonMessage.Ads:
                        ShowPicture(i, Brands.Current); // one brand all Round, its picture scrolling left round the ring (#223)
                        break;
                    case RibbonMessage.CalmStill:
                        ShowWords(p);
                        Color still = ColourOf(board.Colour);
                        Apply(p, "", still, still, 1f, AdScale);
                        break;
                    default:
                        ShowWords(p);
                        Apply(p, board.Text, ColourOf(board.Colour), WordsBack, 1f, board.Small ? SmallScale : BigScale);
                        break;
                }
            }
        }

        // The colours are material values (no upload); the words are written into the panel's texels, and uploaded, only when they change.
        void Apply(Panel p, string text, Color fore, Color back, float glow, int maxScale)
        {
            var backdrop = new Color(back.r * glow, back.g * glow, back.b * glow, 1f);
            if (p.Fore != fore || p.Backdrop != backdrop)
            {
                p.Fore = fore;
                p.Backdrop = backdrop;
                LedBoardMaterial.SetWordsColours(p.Back, fore, backdrop);
            }
            if (p.Text == text && p.MaxScale == maxScale) return;
            p.Text = text;
            p.MaxScale = maxScale;
            WriteWords(p);
        }

        // Lays the words out in whole cells (LedWords.PlaceWhole: descenders counted, whole letters only) and uploads the texture once.
        void WriteWords(Panel p)
        {
            int count = p.CellsX * CellsUp;
            if (_cells.Length < count) _cells = new bool[count];
            System.Array.Clear(_cells, 0, count);
            p.Fit = LedWords.PlaceWhole(p.Text, p.CellsX, CellsUp, p.MaxScale);
            LedWords.Plot(_cells, p.CellsX, CellsUp, p.Fit);
            for (int i = 0; i < count; i++) p.Texels[i] = _cells[i] ? (byte)255 : (byte)0;
            p.Texture.SetPixelData(p.Texels, 0);
            p.Texture.Apply(false);
            _wordsUploads++;
        }

        void AddPanel(int index, Vector2 from, Vector2 to, double alongRing)
        {
            Vector2 mid = (from + to) / 2f;
            float cx = RinkBoards.HalfWidth - CornerRadius;
            var core = new Vector2(Mathf.Clamp(mid.x, -cx, cx),
                                   Mathf.Clamp(mid.y, RinkBoards.FarZ + CornerRadius, RinkBoards.EndZ - CornerRadius));
            Vector2 out2 = (mid - core).normalized;
            var outward = new Vector3(out2.x, 0f, out2.y);
            var along = new Vector3(to.x - from.x, 0f, to.y - from.y);
            // Each panel faces into the rink (a quad shows its face to local -z); on a corner the outward line through the
            // middle of the chord is square to it, so the panel spans from `from` to `to`.
            var facing = Quaternion.LookRotation(outward);
            var centre = new Vector3(mid.x, Bottom + Height / 2f, mid.y);
            float length = along.magnitude;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Ribbon Panel " + index;
            quad.transform.SetParent(transform, false);
            quad.transform.SetPositionAndRotation(centre, facing);
            quad.transform.localScale = new Vector3(length, Height, 1f);
            SceneObjects.DestroySafely(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // An LED panel: a grid of square cells with a dark gap, the grid starting at the panel's lower-left corner as the viewer
            // sees it (the right-hand end cell is partial). The words are a texture with one texel per whole cell, shown in picture mode;
            // the texture is a little narrower than the quad, so its uv is stretched to put every texel on its cell (the partial end
            // reads the last texel, which is blank). The shader needs no light: the panel shows only its cells.
            int cellsX = Mathf.Max(1, Mathf.FloorToInt(length / Cell));
            var texture = new Texture2D(cellsX, CellsUp, TextureFormat.R8, false, true)
            {
                name = "Ribbon Panel words " + index,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Material back = LedBoardMaterial.WordsOnScreen("Ribbon Panel " + index, Cell, texture, Color.white, Color.black);
            back.mainTextureScale = new Vector2(length / (cellsX * Cell), 1f);
            renderer.sharedMaterial = back;
            Vector3 corner = centre - quad.transform.right * (length / 2f) - Vector3.up * (Height / 2f);
            LedBoardMaterial.Anchor(renderer, corner);

            ArenaZone zone = centre.z > RinkBoards.EndZ ? ArenaZone.CalmArea
                : centre.z > (RinkBoards.EndZ + RinkBoards.FarZ) / 2f ? ArenaZone.Near
                : ArenaZone.Far;
            _panels.Add(new Panel { Centre = centre, Length = length, Zone = zone, Back = back, Texture = texture,
                                 Texels = new byte[cellsX * CellsUp], CellsX = cellsX, WordsScaleX = back.mainTextureScale.x,
                                 Along = alongRing, RightAlongRing = Vector3.Dot(quad.transform.right, along) >= 0f });
            if (_edge != null && zone != ArenaZone.CalmArea) // the Calm Area's panels stay one dim still colour
                foreach (float side in new[] { 1f, -1f })
                    AddEdge(centre + Vector3.up * side * (Height - EdgeHeight) / 2f - outward * TextLift / 2f, facing, length);
        }

        void AddEdge(Vector3 centre, Quaternion facing, float length)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = EdgePart;
            quad.transform.SetParent(transform, false);
            quad.transform.SetPositionAndRotation(centre, facing);
            quad.transform.localScale = new Vector3(length, EdgeHeight, 1f);
            SceneObjects.DestroySafely(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = _edge;
        }

        /// <summary>
        /// The panels' two ends round the rink outline pushed out by offset, as Tools/make_arena.py walks it: anticlockwise
        /// from above, up the +x side toward the net end. Straight runs and corners are cut into about PanelLength pieces. The
        /// ring is one smooth rounded rectangle: it does not step back beside the benches and penalty boxes.
        /// </summary>
        static List<(Vector2 From, Vector2 To)> Ring(float offset)
        {
            var panels = new List<(Vector2, Vector2)>();
            float hw = RinkBoards.HalfWidth, r = CornerRadius, cx = hw - r;
            float ze = RinkBoards.EndZ - r, zn = RinkBoards.FarZ + r, radius = r + offset;

            void Line(Vector2 a, Vector2 b)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / PanelLength));
                for (int i = 0; i < n; i++) panels.Add((Vector2.Lerp(a, b, i / (float)n), Vector2.Lerp(a, b, (i + 1) / (float)n)));
            }

            void Arc(float x, float z, float a0, float a1)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(radius * (a1 - a0) / PanelLength));
                Vector2 At(int i) => new Vector2(x + radius * Mathf.Cos(a0 + (a1 - a0) * i / n), z + radius * Mathf.Sin(a0 + (a1 - a0) * i / n));
                for (int i = 0; i < n; i++) panels.Add((At(i), At(i + 1)));
            }

            // A side run from z0 to z1 on side (+1 or -1): one straight line, no step at the benches or boxes.
            void Side(int side, float z0, float z1) => Line(new Vector2(side * (hw + offset), z0), new Vector2(side * (hw + offset), z1));

            const float quarter = Mathf.PI / 2f;
            Side(1, zn, ze);
            Arc(cx, ze, 0f, quarter);
            Line(new Vector2(cx, RinkBoards.EndZ + offset), new Vector2(-cx, RinkBoards.EndZ + offset));
            Arc(-cx, ze, quarter, 2f * quarter);
            Side(-1, ze, zn);
            Arc(-cx, zn, 2f * quarter, 3f * quarter);
            Line(new Vector2(-cx, RinkBoards.FarZ - offset), new Vector2(cx, RinkBoards.FarZ - offset));
            Arc(cx, zn, 3f * quarter, 4f * quarter);
            return panels;
        }
    }
}
