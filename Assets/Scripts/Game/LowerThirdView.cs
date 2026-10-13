using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Draws the lower third (#141) in look C "TV graphic" of #137: a red tab with the label, a navy bar with the number, slanted
    /// ends, a blue cap and a gold stripe. The art is made once on the CPU as a Texture2D (like the Shootout logo, #140); the words
    /// are IMGUI labels over it, like the rest of the HUD. What it says and when (LowerThird) is decided in the core; this only draws
    /// it, sliding in from the left.
    /// <para>
    /// The five colours are the League Skin's, the same values as StartLogoArt (branch feat/140, #140). They are defined here only
    /// so the two branches do not meet; after both are in main, make one list (Navy, Blue, Gold, Red, White) and use it in both.
    /// </para>
    /// </summary>
    public sealed class LowerThirdView
    {
        public static readonly Color32 Navy = new Color32(10, 16, 34, 255);
        public static readonly Color32 Blue = new Color32(20, 60, 150, 255);
        public static readonly Color32 Gold = new Color32(232, 190, 40, 255);
        public static readonly Color32 Red = new Color32(200, 30, 45, 255);
        public static readonly Color32 White = new Color32(255, 255, 255, 255);

        public const int Width = 1240, Height = 170;
        // Picture space (y down): everything leans right by Slant per unit of height above the middle.
        const float Slant = 0.25f, MidY = 85f, Left = 16f;
        // Where the words go in picture space: the label in the red tab, the number in the navy bar.
        const float TabX0 = 60f, TabX1 = 372f, BarX0 = 396f, BarX1 = 1128f, TextY0 = 30f, TextY1 = 130f;

        Texture2D _art;
        GUIStyle _kicker, _headline;

        struct Shape { public Vector2[] Points; public Color32 Colour; }

        static Vector2 Lean(float x, float y) => new Vector2(x + (MidY - y) * Slant + Left, y);

        static Shape Box(float x0, float y0, float x1, float y1, Color32 colour) => new Shape
        {
            Points = new[] { Lean(x0, y0), Lean(x1, y0), Lean(x1, y1), Lean(x0, y1) },
            Colour = colour
        };

        static Shape[] Shapes() => new[]
        {
            Box(40, 30, 380, 130, Red),       // the label's tab
            Box(0, 30, 46, 130, Blue),        // blue cap, with a gold bar, at the left (as the settings title)
            Box(40, 30, 52, 130, Gold),
            Box(380, 30, 1140, 130, Navy),    // the number's bar
            Box(1128, 30, 1190, 130, Blue),   // the blue end cap
            Box(0, 138, 1160, 154, Gold),     // the gold stripe under
            Box(110, 16, 1090, 24, White),    // the white streak over
        };

        /// <summary>The picture, made once: the shapes anti-aliased over a clear background.</summary>
        public static Texture2D MakeArt()
        {
            var r = new float[Width * Height];
            var g = new float[Width * Height];
            var b = new float[Width * Height];
            var a = new float[Width * Height];
            foreach (Shape shape in Shapes()) Fill(shape, r, g, b, a);
            var pixels = new Color32[Width * Height];
            for (int i = 0; i < pixels.Length; i++)
            {
                // Clear pixels carry navy, so the bilinear edge of the picture does not pick up black.
                pixels[i] = a[i] <= 0f
                    ? new Color32(Navy.r, Navy.g, Navy.b, 0)
                    : new Color32((byte)Mathf.RoundToInt(r[i]), (byte)Mathf.RoundToInt(g[i]), (byte)Mathf.RoundToInt(b[i]), (byte)Mathf.RoundToInt(a[i] * 255f));
            }
            var t = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(pixels);
            t.Apply();
            return t;
        }

        // Row 0 is the bottom of the texture, so the shape (y down) is turned over.
        static void Fill(Shape shape, float[] r, float[] g, float[] b, float[] a)
        {
            var p = new Vector2[4];
            for (int i = 0; i < 4; i++) p[i] = new Vector2(shape.Points[i].x, Height - shape.Points[i].y);
            float minX = Mathf.Min(Mathf.Min(p[0].x, p[1].x), Mathf.Min(p[2].x, p[3].x)), maxX = Mathf.Max(Mathf.Max(p[0].x, p[1].x), Mathf.Max(p[2].x, p[3].x));
            float minY = Mathf.Min(Mathf.Min(p[0].y, p[1].y), Mathf.Min(p[2].y, p[3].y)), maxY = Mathf.Max(Mathf.Max(p[0].y, p[1].y), Mathf.Max(p[2].y, p[3].y));
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX)), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(maxX));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float cover = Coverage(p, x, y);
                    if (cover <= 0f) continue;
                    int i = y * Width + x;
                    float outA = cover + a[i] * (1f - cover);
                    r[i] = (shape.Colour.r * cover + r[i] * a[i] * (1f - cover)) / outA;
                    g[i] = (shape.Colour.g * cover + g[i] * a[i] * (1f - cover)) / outA;
                    b[i] = (shape.Colour.b * cover + b[i] * a[i] * (1f - cover)) / outA;
                    a[i] = outA;
                }
        }

        const int Samples = 4;

        // Share of the pixel inside the convex quad (4 x 4 sample points; a pixel whose four corners are inside is whole).
        static float Coverage(Vector2[] p, int x, int y)
        {
            if (Inside(p, x, y) && Inside(p, x + 1, y) && Inside(p, x, y + 1) && Inside(p, x + 1, y + 1)) return 1f;
            int n = 0;
            for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                    if (Inside(p, x + (sx + 0.5f) / Samples, y + (sy + 0.5f) / Samples)) n++;
            return n / (float)(Samples * Samples);
        }

        // Same side of every edge (either winding).
        static bool Inside(Vector2[] p, float x, float y)
        {
            bool pos = false, neg = false;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % 4];
                float cross = (b.x - a.x) * (y - a.y) - (b.y - a.y) * (x - a.x);
                if (cross > 0) pos = true; else if (cross < 0) neg = true;
            }
            return !(pos && neg);
        }

        /// <summary>Draws the lower third on a screen of w by h pixels; nothing while it is off screen (show.Amount 0).</summary>
        public void Draw(float w, float h, LowerThirdShow show)
        {
            if (show.Amount <= 0) return;
            if (_art == null) Make();

            float barWidth = w * 0.8f, s = barWidth / Width, barHeight = Height * s;
            float x = Mathf.Lerp(-barWidth, w * 0.05f, (float)show.Amount); // slides in from the left edge
            float y = h - barHeight - h * 0.075f;                           // above the dev lines along the bottom
            GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), _art);

            Label(new Rect(x + TabX0 * s, y + TextY0 * s, (TabX1 - TabX0) * s, (TextY1 - TextY0) * s), show.Text.Kicker, _kicker, (int)(32 * s));
            Label(new Rect(x + BarX0 * s, y + TextY0 * s, (BarX1 - BarX0) * s, (TextY1 - TextY0) * s), show.Text.Headline, _headline, (int)(54 * s));
        }

        // The words at the biggest size up to maxSize that fits the rect's width.
        static void Label(Rect rect, string text, GUIStyle style, int maxSize)
        {
            var content = new GUIContent(text);
            int size = maxSize;
            for (; size > 10; size -= 2)
            {
                style.fontSize = size;
                if (style.CalcSize(content).x <= rect.width * 0.95f) break;
            }
            style.fontSize = size;
            GUI.Label(rect, content, style);
        }

        void Make()
        {
            _art = MakeArt();
            _kicker = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.BoldAndItalic, wordWrap = false, clipping = TextClipping.Overflow };
            _headline = new GUIStyle(_kicker);
            _kicker.normal.textColor = _headline.normal.textColor = Color.white;
        }

        public void Dispose()
        {
            if (_art != null) SceneObjects.DestroySafely(_art);
            _art = null;
        }
    }
}
