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
    /// set to the Shot. A panel is in the Calm Area when it stands behind the near end boards. A Team Skin may give the
    /// panels edges in a team colour: a thin strip along the top and bottom of each panel, never in the Calm Area (the
    /// League Skin has none). The ads are the fixed brands (Brands): no skin changes them.
    /// <para>
    /// The ring is the Rink Layout's full rink outline pushed out by Offset (and a further BenchDepth beside the benches and
    /// penalty boxes, where the stands step back), as Tools/make_arena.py builds the ribbon from the same Rink Layout
    /// numbers: every size here comes from RinkLayout.
    /// </para>
    /// </summary>
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
        const float TextLift = 0.02f;    // the words stand just in front of their panel
        const float BigText = 0.8f, SmallText = 0.45f, AdText = 0.6f; // word heights (metres)
        const float TextWidth = 0.9f;    // share of a panel the words may fill
        /// <summary>The height of a team-coloured edge strip (metres), on the panel's top and bottom.</summary>
        public const float EdgeHeight = 0.08f;
        /// <summary>The name of the team-coloured edge strips.</summary>
        public const string EdgePart = "Ribbon Edge";
        const int FontSize = 64;
        const float AdGlow = 0.85f;      // an ad's background glows this much of its colour
        static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");
        static readonly Color WordsBack = new Color32(14, 16, 22, 255);

        sealed class Panel
        {
            public Vector3 Centre;
            public float Length;
            public ArenaZone Zone;
            public Material Back;
            public TextMesh Words;
            // What it shows now, so Show changes only what changed (it runs every frame).
            public string Text;
            public Color Fore, Glow;
            public float TextHeight = -1f;
        }

        readonly List<Panel> _panels = new List<Panel>();
        Font _font;
        Material _edge; // the skin's edge colour; null: no edges

        public int PanelCount => _panels.Count;
        public Vector3 CentreOf(int panel) => _panels[panel].Centre;
        public float LengthOf(int panel) => _panels[panel].Length;
        public ArenaZone ZoneOf(int panel) => _panels[panel].Zone;
        public string TextOn(int panel) => _panels[panel].Words.text;
        public Color TextColourOn(int panel) => _panels[panel].Words.color;
        /// <summary>The panel's own light: its colour as seen (the panel is black and matte, so it shows only its glow).</summary>
        public Color BackOn(int panel) => _panels[panel].Back.GetColor(EmissionColour);
        public float TextScaleOn(int panel) => _panels[panel].Words.transform.localScale.x;

        public static Color ColourOf(ArenaColour c) => new Color32(c.R, c.G, c.B, 255);

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
            ribbons._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (skin.RibbonEdgesOn)
            {
                ribbons._edge = ArenaLighting.Glow(Color.black, skin.RibbonEdge);
                ribbons._edge.SetFloat("_Smoothness", 0f);
            }
            List<(Vector2 From, Vector2 To)> ring = Ring(Offset - Inset);
            int index = 0;
            foreach ((Vector2 from, Vector2 to) in ring)
            {
                // A panel wholly behind the Video Board is never seen: leave it out.
                if (VideoBoardCover.HidesRibbonPanel(from.x, from.y, to.x, to.y)) continue;
                ribbons.AddPanel(index++, from, to);
            }
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
                        Brands.Brand b = Brands.All[(adStep + i) % Brands.All.Length];
                        Apply(p, b.AdWords, ColourOf(b.AdFore), ColourOf(b.AdBack), AdGlow, AdText);
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
            Color light = back * glow;
            if (p.Text == text && p.Fore == fore && p.Glow == light && p.TextHeight == height) return;
            p.Text = text;
            p.Fore = fore;
            p.Glow = light;
            p.TextHeight = height;
            p.Back.SetColor(EmissionColour, light);
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
                : centre.z > (RinkBoards.EndZ + RinkBoards.FarZ) / 2f ? ArenaZone.Near
                : ArenaZone.Far;
            _panels.Add(new Panel { Centre = centre, Length = length, Zone = zone, Back = back, Words = text });
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
        /// from above, up the +x side toward the net end. Straight runs and corners are cut into about PanelLength pieces; a
        /// side run is cut at each bench and penalty box, and the part beside one is pushed out a further BenchDepth.
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

            // A side run from z0 to z1 on side (+1 or -1), cut where the benches and boxes begin and end.
            void Side(int side, float z0, float z1)
            {
                var cuts = new List<float> { z0, z1 };
                foreach (RinkLayout.Opening[] group in new[] { RinkLayout.Benches, RinkLayout.PenaltyBoxes })
                    foreach (RinkLayout.Opening o in group)
                        if (o.Side == side)
                            foreach (float z in new[] { (float)o.FromZ, (float)o.ToZ })
                                if (z > Mathf.Min(z0, z1) && z < Mathf.Max(z0, z1)) cuts.Add(z);
                cuts.Sort((a, b) => (z1 > z0 ? a.CompareTo(b) : b.CompareTo(a)));
                for (int i = 0; i + 1 < cuts.Count; i++)
                {
                    float mid = (cuts[i] + cuts[i + 1]) / 2f, push = 0f;
                    foreach (RinkLayout.Opening[] group in new[] { RinkLayout.Benches, RinkLayout.PenaltyBoxes })
                        foreach (RinkLayout.Opening o in group)
                            if (o.Side == side && mid >= o.FromZ && mid <= o.ToZ) push = (float)o.Depth;
                    float x = side * (hw + offset + push);
                    Line(new Vector2(x, cuts[i]), new Vector2(x, cuts[i + 1]));
                }
            }

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
