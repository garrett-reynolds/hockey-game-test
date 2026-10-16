using System;
using System.Collections.Generic;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The Shootout logo's picture (#140), look C "TV graphic" of #137: a red band with slanted ends, a blue end cap and a gold
    /// stripe, and SHOOTOUT in white block italic letters (built from boxes here, so no font and no licence). Made once on the
    /// CPU, anti-aliased, as a Texture2D; the start screen draws it. The colours are look C's (LookC, one list; a test checks them against the League Skin).
    /// </summary>
    public static class StartLogoArt
    {
        public const int Width = 1200, Height = 330;
        public const float Aspect = Width / (float)Height;

        /// <summary>The text strip under the logo: see-through navy with a gold top edge (look C).</summary>
        public static readonly Color StripBack = new Color(10 / 255f, 16 / 255f, 34 / 255f, 0.8f);
        public const float StripEdgeHeight = 5f;

        // Logo space: 1000 x 300, y down. Everything leans right by Slant per unit of height above the middle.
        const float Space = 1.05f, Slant = 0.28f, MidY = 150f, Left = 84f; // Left: keeps the leaned left end on the picture
        const float LetterW = 84f, LetterH = 150f, Stroke = 26f, Pitch = 102f, LettersLeft = 100f, LettersTop = 75f;

        struct Shape { public Vector2[] Points; public Color32 Colour; }

        static Vector2 Lean(float x, float y) => new Vector2((x + (MidY - y) * Slant) * Space + Left, y * Space);

        static Shape Box(float x0, float y0, float x1, float y1, Color32 colour) => new Shape
        {
            Points = new[] { Lean(x0, y0), Lean(x1, y0), Lean(x1, y1), Lean(x0, y1) },
            Colour = colour
        };

        // Each letter as boxes in its own 84 x 150 cell: (x, y, width, height).
        static float[][] LetterBoxes(char c)
        {
            float t = Stroke, w = LetterW, h = LetterH, m = (h - t) / 2f;
            float[] top = { 0, 0, w, t }, bottom = { 0, h - t, w, t }, mid = { 0, m, w, t };
            float[] left = { 0, 0, t, h }, right = { w - t, 0, t, h };
            switch (c)
            {
                case 'S': return new[] { top, new float[] { 0, 0, t, m + t }, mid, new float[] { w - t, m, t, h - m }, bottom };
                case 'H': return new[] { left, right, mid };
                case 'O': return new[] { top, bottom, left, right };
                case 'T': return new[] { top, new float[] { (w - t) / 2f, 0, t, h } };
                case 'U': return new[] { left, right, bottom };
                default: throw new ArgumentException("No letter " + c);
            }
        }

        static List<Shape> Shapes()
        {
            var s = new List<Shape>();
            // The band: red, slanted ends; blue cap with a gold bar at the left (as the settings title); gold stripe under; white streak over.
            s.Add(Box(30, 40, 960, 262, LookC.Red));
            s.Add(Box(-30, 40, 36, 262, LookC.Blue));
            s.Add(Box(30, 40, 42, 262, LookC.Gold));
            s.Add(Box(954, 40, 1010, 262, LookC.Blue));
            s.Add(Box(-10, 270, 1000, 292, LookC.Gold));
            s.Add(Box(110, 18, 880, 30, LookC.White));
            // Letter shadows (navy), then the letters (white).
            const string word = "SHOOTOUT";
            for (int pass = 0; pass < 2; pass++)
            {
                float shift = pass == 0 ? 7f : 0f;
                Color32 colour = pass == 0 ? LookC.Navy : LookC.White;
                for (int i = 0; i < word.Length; i++)
                    foreach (float[] b in LetterBoxes(word[i]))
                    {
                        float x = LettersLeft + i * Pitch + b[0] + shift, y = LettersTop + b[1] + shift;
                        s.Add(Box(x, y, x + b[2], y + b[3], colour));
                    }
            }
            return s;
        }

        static Color32[] _pixels;

        /// <summary>
        /// The logo's pixels (row 0 at the bottom), worked out once and kept (about 0.35 s): the start screen's picture and the Video
        /// Board's start card (#153) both read them. Do not change the array.
        /// </summary>
        public static Color32[] Pixels()
        {
            if (_pixels != null) return _pixels;
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
            _pixels = pixels;
            return _pixels;
        }

        public static Texture2D Make()
        {
            var t = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset }; // #201: only managed code holds it, so Unity would unload it as unused and the draw would get null
            t.SetPixels32(Pixels());
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
                if (pos && neg) return false;
            }
            return true;
        }
    }
}
