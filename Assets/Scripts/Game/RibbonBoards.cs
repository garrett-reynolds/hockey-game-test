using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The Ribbon Boards: LED panels all round the stands, on the arena model's ribbon ring between the tiers (it hides the
    /// model's own plain "Ribbon" part). They only follow the Arena Show (Show): brand ads that cycle, the result words
    /// (GOAL!, SAVE, OFF THE POST!, OFF THE BAR!), and in the Calm Area behind the near net one dim still colour from goalie
    /// set to the Shot. A panel is in the Calm Area when it stands behind the near end boards.
    /// <para>
    /// The ring is today's rink outline (RinkBoards, corner radius CornerRadius) pushed out by Offset, as Tools/make_arena.py
    /// builds the ribbon (FRONT + LOWER_ROWS * ROW_D out, FRONT_H + LOWER_ROWS * LOWER_RISE up, RIBBON_H tall): change both
    /// together. When the Rink Layout (#86) holds the full rink and the stands, these sizes should come from it.
    /// </para>
    /// </summary>
    public sealed class RibbonBoards : MonoBehaviour
    {
        /// <summary>How far the ribbon ring stands out from the inside of the boards (metres).</summary>
        public const float Offset = 18f;
        /// <summary>Height of the ribbon's bottom and the ribbon's height (metres).</summary>
        public const float Bottom = 10f, Height = 1.2f;
        /// <summary>The rink's corner radius (metres).</summary>
        public const float CornerRadius = 8.53f;
        /// <summary>About how long one panel is along the ring (metres).</summary>
        public const float PanelLength = 6f;
        /// <summary>Seconds each ad shows before the next.</summary>
        public const float AdSeconds = 5f;

        const float Inset = 0.05f;       // the panels stand just inside the model's ribbon
        const float TextLift = 0.02f;    // the words stand just in front of their panel
        const float BigText = 0.8f, SmallText = 0.45f, AdText = 0.6f; // word heights (metres)
        const float TextWidth = 0.9f;    // share of a panel the words may fill
        const int FontSize = 64;
        const float AdGlow = 0.85f;      // an ad's background glows this much of its colour
        static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");
        static readonly Color WordsBack = new Color32(14, 16, 22, 255);

        struct Brand
        {
            public readonly string Name;
            public readonly Color32 Back, Fore;
            public Brand(string name, Color32 back, Color32 fore) { Name = name; Back = back; Fore = fore; }
        }

        /// <summary>The fixed made-up brands (#84), with their colours from the #77 prototype.</summary>
        static readonly Brand[] Brands =
        {
            new Brand("NORTHVALE BANK", new Color32(12, 35, 80, 255), new Color32(255, 255, 255, 255)),
            new Brand("GlacierLink", new Color32(255, 255, 255, 255), new Color32(110, 40, 170, 255)),
            new Brand("BRAWNY'S LAGER", new Color32(200, 150, 30, 255), new Color32(60, 20, 10, 255)),
            new Brand("KESTREL MOTORS", new Color32(30, 30, 35, 255), new Color32(200, 205, 210, 255)),
            new Brand("Aurora Air", new Color32(255, 255, 255, 255), new Color32(0, 130, 140, 255)),
            new Brand("SHIELDCROSS", new Color32(0, 90, 60, 255), new Color32(255, 255, 255, 255)),
            new Brand("VOLT RUSH", new Color32(20, 20, 20, 255), new Color32(170, 230, 0, 255)),
            new Brand("BIG TIMBER BURGERS", new Color32(235, 110, 20, 255), new Color32(255, 255, 255, 255)),
            new Brand("PEAKSIDE ENERGY", new Color32(255, 255, 255, 255), new Color32(0, 80, 170, 255)),
            new Brand("FrostByte PCs", new Color32(0, 170, 220, 255), new Color32(255, 255, 255, 255)),
        };

        sealed class Panel
        {
            public Vector3 Centre;
            public float Length;
            public ArenaZone Zone;
            public Material Back;
            public TextMesh Words;
            public string Shown; // what it shows now, so Show changes only what changed
        }

        readonly List<Panel> _panels = new List<Panel>();
        Font _font;

        public int PanelCount => _panels.Count;
        public Vector3 CentreOf(int panel) => _panels[panel].Centre;
        public ArenaZone ZoneOf(int panel) => _panels[panel].Zone;
        public string TextOn(int panel) => _panels[panel].Words.text;
        public Color TextColourOn(int panel) => _panels[panel].Words.color;
        /// <summary>The panel's own light: its colour as seen (the panel is black and matte, so it shows only its glow).</summary>
        public Color BackOn(int panel) => _panels[panel].Back.GetColor(EmissionColour);
        public float TextScaleOn(int panel) => _panels[panel].Words.transform.localScale.x;

        public static Color ColourOf(ArenaColour c) => new Color32(c.R, c.G, c.B, 255);

        /// <summary>True for a brand's name as the ads show it.</summary>
        public static bool IsBrand(string text)
        {
            foreach (Brand b in Brands)
                if (b.Name == text) return true;
            return false;
        }

        /// <summary>Adds the Ribbon Boards under rink and hides the arena model's own ribbon. They show ads until told otherwise.</summary>
        public static RibbonBoards Build(Transform rink)
        {
            foreach (Renderer r in rink.GetComponentsInChildren<Renderer>())
                if (r.name == ArenaStands.GlowPart) r.enabled = false;

            var root = new GameObject("Ribbon Boards");
            root.transform.SetParent(rink, false);
            var ribbons = root.AddComponent<RibbonBoards>();
            ribbons._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            List<Vector2> ring = Ring(Offset - Inset);
            for (int i = 0; i < ring.Count; i++)
                ribbons.AddPanel(i, ring[i], ring[(i + 1) % ring.Count]);
            ribbons.Show(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.Idle }), 0.0);
            return ribbons;
        }

        /// <summary>Shows the Arena Show's answer. seconds: a running clock (real time) that cycles the ads.</summary>
        public void Show(ArenaState state, double seconds)
        {
            int adStep = (int)System.Math.Floor(seconds / AdSeconds);
            for (int i = 0; i < _panels.Count; i++)
            {
                Panel p = _panels[i];
                RibbonBoard board = state.RibbonIn(p.Zone);
                switch (board.Message)
                {
                    case RibbonMessage.Ads:
                        Brand b = Brands[(adStep + i) % Brands.Length];
                        Apply(p, b.Name, b.Fore, b.Back, AdGlow, AdText);
                        break;
                    case RibbonMessage.CalmStill:
                        Color still = ColourOf(board.Colour);
                        Apply(p, "", still, still, 1f, AdText);
                        break;
                    default:
                        Apply(p, board.Text, ColourOf(board.Colour), WordsBack, 1f, board.Small ? SmallText : BigText);
                        break;
                }
            }
        }

        void Apply(Panel p, string text, Color fore, Color back, float glow, float height)
        {
            string key = text + "|" + fore + "|" + back + "|" + height;
            if (p.Shown == key) return;
            p.Shown = key;
            p.Back.SetColor(EmissionColour, back * glow);
            p.Words.text = text;
            p.Words.color = fore;
            float scale = TextScale(text, height, p.Length * TextWidth);
            p.Words.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>The words' scale: height metres tall (one font size in TextMesh units is 0.1), narrowed to fit maxWidth.</summary>
        float TextScale(string text, float height, float maxWidth)
        {
            float scale = height / (FontSize * 0.1f);
            if (text.Length == 0) return scale;
            _font.RequestCharactersInTexture(text, FontSize, FontStyle.Bold);
            float advance = 0f;
            foreach (char c in text)
                if (_font.GetCharacterInfo(c, out CharacterInfo info, FontSize, FontStyle.Bold))
                    advance += info.advance;
            float width = advance * 0.1f * scale;
            return width > maxWidth ? scale * maxWidth / width : scale;
        }

        void AddPanel(int index, Vector2 from, Vector2 to)
        {
            Vector2 mid = (from + to) / 2f;
            float cx = RinkBoards.HalfWidth - CornerRadius;
            var core = new Vector2(Mathf.Clamp(mid.x, -cx, cx),
                                   Mathf.Clamp(mid.y, RinkBoards.NearZ + CornerRadius, RinkBoards.EndZ - CornerRadius));
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
            // Black and matte: no arena light or shine on it, so it shows only the colour it glows (the Calm Area's stays dim).
            Material back = ArenaLighting.Glow(Color.black, Color.black);
            back.SetFloat("_Smoothness", 0f);
            renderer.sharedMaterial = back;

            var words = new GameObject("Ribbon Words " + index);
            words.transform.SetParent(transform, false);
            words.transform.SetPositionAndRotation(centre - outward * TextLift, facing);
            var text = words.AddComponent<TextMesh>();
            text.font = _font;
            text.fontSize = FontSize;
            text.characterSize = 1f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            var wordsRenderer = words.GetComponent<MeshRenderer>();
            wordsRenderer.sharedMaterial = _font.material;
            wordsRenderer.shadowCastingMode = ShadowCastingMode.Off;
            wordsRenderer.receiveShadows = false;

            ArenaZone zone = centre.z > RinkBoards.EndZ ? ArenaZone.CalmArea
                : centre.z > (RinkBoards.EndZ + RinkBoards.NearZ) / 2f ? ArenaZone.Near
                : ArenaZone.Far;
            _panels.Add(new Panel { Centre = centre, Length = length, Zone = zone, Back = back, Words = text });
        }

        /// <summary>
        /// The panel ends round the rink outline pushed out by offset, as Tools/make_arena.py walks it: anticlockwise from
        /// above, up the +x side toward the net end. Straight runs and corners are cut into about PanelLength pieces.
        /// </summary>
        static List<Vector2> Ring(float offset)
        {
            var points = new List<Vector2>();
            float hw = RinkBoards.HalfWidth, r = CornerRadius, cx = hw - r;
            float ze = RinkBoards.EndZ - r, zn = RinkBoards.NearZ + r, radius = r + offset;

            void Line(Vector2 a, Vector2 b)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / PanelLength));
                for (int i = 0; i < n; i++) points.Add(Vector2.Lerp(a, b, i / (float)n));
            }

            void Arc(float x, float z, float a0, float a1)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(radius * (a1 - a0) / PanelLength));
                for (int i = 0; i < n; i++)
                {
                    float a = a0 + (a1 - a0) * i / n;
                    points.Add(new Vector2(x + radius * Mathf.Cos(a), z + radius * Mathf.Sin(a)));
                }
            }

            const float quarter = Mathf.PI / 2f;
            Line(new Vector2(hw + offset, zn), new Vector2(hw + offset, ze));
            Arc(cx, ze, 0f, quarter);
            Line(new Vector2(cx, RinkBoards.EndZ + offset), new Vector2(-cx, RinkBoards.EndZ + offset));
            Arc(-cx, ze, quarter, 2f * quarter);
            Line(new Vector2(-hw - offset, ze), new Vector2(-hw - offset, zn));
            Arc(-cx, zn, 2f * quarter, 3f * quarter);
            Line(new Vector2(-cx, RinkBoards.NearZ - offset), new Vector2(cx, RinkBoards.NearZ - offset));
            Arc(cx, zn, 3f * quarter, 4f * quarter);
            return points;
        }
    }
}
