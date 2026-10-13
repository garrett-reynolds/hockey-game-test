using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Draws the lower third (#141) in look C "TV graphic" of #137: a red tab with the label, a navy bar with the number, slanted
    /// ends, a blue cap and a gold stripe. The art is made once on the CPU as a Texture2D (like the Shootout logo, #140); the words
    /// are IMGUI labels over it, like the rest of the HUD. What it says and when (LowerThird) is decided in the core; this only draws
    /// it, in place in the bottom-left corner (it does not slide).
    /// <para>
    /// The colours are look C's (LookC, one list). Where the words go is LowerThirdLayout (a test checks it at several screen sizes).
    /// The words' GUIContent and font sizes are made once per Shot (when the text changes), not on every OnGUI call.
    /// </para>
    /// </summary>
    public sealed class LowerThirdView
    {
        const int Width = LowerThirdLayout.Width, Height = LowerThirdLayout.Height;

        Texture2D _art;
        GUIStyle _kicker, _headline, _speed;
        Words _kickerWords, _headlineWords, _speedWords;

        // One line of words: its GUIContent, and the font size that fits (kept while the text and the box's width stay the same).
        sealed class Words
        {
            public string Text;
            public GUIContent Content;
            public float FittedWidth;
            public int Size;
        }

        struct Shape { public Vector2[] Points; public Color32 Colour; }

        static Vector2 Lean(float x, float y) => LowerThirdLayout.Lean(x, y);

        static Shape Box(float x0, float y0, float x1, float y1, Color32 colour) => new Shape
        {
            Points = new[] { Lean(x0, y0), Lean(x1, y0), Lean(x1, y1), Lean(x0, y1) },
            Colour = colour
        };

        static Shape[] Shapes() => new[]
        {
            Box(LowerThirdLayout.TabLeft, 30, LowerThirdLayout.TabRight, 130, LookC.Red), // the label's tab
            Box(0, 30, 46, 130, LookC.Blue),        // blue cap, with a gold bar, at the left (as the settings title)
            Box(40, 30, 52, 130, LookC.Gold),
            Box(380, 30, 1140, 130, LookC.Navy),    // the number's bar
            Box(1128, 30, 1190, 130, LookC.Blue),   // the blue end cap
            Box(0, 138, 1160, 154, LookC.Gold),     // the gold stripe under
            Box(110, 16, 1090, 24, LookC.White),    // the white streak over
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
                    ? new Color32(LookC.Navy.r, LookC.Navy.g, LookC.Navy.b, 0)
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

            float s = LowerThirdLayout.Scale(w);
            Rect bar = LowerThirdLayout.Bar(w, h); // always in place: no slide in or out
            GUI.DrawTexture(bar, _art);

            Label(LowerThirdLayout.Kicker(bar, s), show.Text.Kicker, _kicker, ref _kickerWords, (int)(32 * s));
            Label(LowerThirdLayout.Headline(bar, s), show.Text.Headline, _headline, ref _headlineWords, (int)(50 * s));
            Label(LowerThirdLayout.Speed(bar, s), show.Text.SpeedLine, _speed, ref _speedWords, (int)(26 * s));
        }

        // The words at the biggest size up to maxSize that fits the rect's width. The content and the size are kept while the text and
        // the width stay the same (a Shot's words do), so a frame only draws.
        static void Label(Rect rect, string text, GUIStyle style, ref Words words, int maxSize)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (words == null) words = new Words();
            if (words.Text != text || words.FittedWidth != rect.width)
            {
                words.Text = text;
                words.Content = new GUIContent(text);
                words.FittedWidth = rect.width;
                int size = maxSize;
                for (; size > 10; size -= 2)
                {
                    style.fontSize = size;
                    if (style.CalcSize(words.Content).x <= rect.width * LowerThirdLayout.TextFit) break;
                }
                words.Size = size;
            }
            style.fontSize = words.Size;
            GUI.Label(rect, words.Content, style);
        }

        void Make()
        {
            _art = MakeArt();
            _kicker = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.BoldAndItalic, wordWrap = false, clipping = TextClipping.Overflow };
            _headline = new GUIStyle(_kicker);
            _speed = new GUIStyle(_kicker);
            _kicker.normal.textColor = _headline.normal.textColor = Color.white;
            _speed.normal.textColor = LookC.Gold; // the speed line in look C's gold
        }

        public void Dispose()
        {
            if (_art != null) SceneObjects.DestroySafely(_art);
            _art = null;
        }
    }
}
