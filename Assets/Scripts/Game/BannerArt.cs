using System;
using System.Collections.Generic;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The League Skin's roof banner art (#149), drawn in code: tall NHL-style banners with white trim, a star and the award for
    /// an award banner, a big number, a surname and years for a retired jersey. All names are made up. The text is our own
    /// 5 x 7 block letters (BannerFont), so no font file or licence is involved. Hockey Game/Banner Art/Make writes each banner to a PNG in
    /// Assets/Resources/Ads (the art the skin names); the game loads those PNGs like every other art.
    /// </summary>
    public static class BannerArt
    {
        public const int Width = 360, Height = 600;

        /// <summary>The League's colours: the rail's blue, the kick plate's gold, the home jersey's burgundy.</summary>
        public static readonly Color32 Blue = new Color32(20, 60, 150, 255), Gold = new Color32(232, 190, 40, 255),
            Burgundy = new Color32(112, 30, 46, 255), White = new Color32(246, 246, 240, 255), Shadow = new Color32(8, 12, 30, 255);

        /// <summary>One banner: its file name (no extension), the field colour, and the lines of type (text, block size, colour).</summary>
        public readonly struct Banner
        {
            public readonly string Name;
            public readonly Color32 Field;
            public readonly bool Star;
            public readonly Line[] Lines;
            public Banner(string name, Color32 field, bool star, params Line[] lines) { Name = name; Field = field; Star = star; Lines = lines; }
        }

        /// <summary>A line of type: its text, the size of one block of a letter, the top edge, and the colour.</summary>
        public readonly struct Line
        {
            public readonly string Text;
            public readonly int Scale, Top;
            public readonly Color32 Colour;
            public Line(string text, int scale, int top, Color32 colour) { Text = text; Scale = scale; Top = top; Colour = colour; }
        }

        /// <summary>The League Skin's banners, left to right as they hang: awards and retired jerseys.</summary>
        public static readonly Banner[] League =
        {
            new Banner("banner-award-shootout", Blue, true,
                new Line("SHOOTOUT", 5, 215, White), new Line("CHAMPIONS", 5, 262, White), new Line("2019", 11, 335, Gold)),
            new Banner("banner-jersey-7-korvak", Burgundy, false,
                new Line("7", 22, 120, White), new Line("KORVAK", 8, 300, Gold), new Line("1998-2011", 5, 385, White), new Line("RETIRED", 5, 430, Gold)),
            new Banner("banner-award-mvp", Blue, true,
                new Line("LEAGUE", 7, 215, White), new Line("MVP", 14, 285, Gold), new Line("2022", 8, 400, White)),
            new Banner("banner-jersey-12-delmar", Blue, false,
                new Line("12", 22, 120, White), new Line("DELMAR", 8, 300, Gold), new Line("2003-2016", 5, 385, White), new Line("RETIRED", 5, 430, Gold)),
            new Banner("banner-award-goalie", Burgundy, true,
                new Line("TOP", 8, 215, White), new Line("GOALIE", 8, 280, Gold), new Line("2024", 11, 360, White)),
            new Banner("banner-jersey-30-halvar", Burgundy, false,
                new Line("30", 22, 120, White), new Line("HALVAR", 8, 300, Gold), new Line("1995-2009", 5, 385, White), new Line("RETIRED", 5, 430, Gold)),
        };

        /// <summary>The names of the League's banners, left to right (the League Skin's RoofBanners list).</summary>
        public static string[] LeagueNames()
        {
            var names = new string[League.Length];
            for (int i = 0; i < names.Length; i++) names[i] = League[i].Name;
            return names;
        }

        /// <summary>Draws one banner: pixels from the bottom left, as a texture holds them.</summary>
        public static Color32[] Draw(Banner banner)
        {
            var top = new Color32[Width * Height]; // row 0 is the top while drawing
            Rect(top, 0, 0, Width, Height, banner.Field);
            // The white trim all round, then a thin gold line inside it, then a hem bar across the top.
            Frame(top, 0, 0, Width, Height, 12, White);
            Frame(top, 20, 20, Width - 40, Height - 40, 4, Gold);
            Rect(top, 0, 0, Width, 26, White);
            Rect(top, 0, 26, Width, 5, Gold);
            // The V stripes near the foot: the banner's swallow-tail look.
            for (int x = 12; x < Width - 12; x++)
            {
                int y = Height - 120 + (int)(Math.Abs(x - Width / 2) * 0.32f);
                Rect(top, x, y, 1, 12, White);
                Rect(top, x, y + 18, 1, 6, Gold);
            }
            if (banner.Star) Star(top, Width / 2, 120, 62, Gold);
            foreach (Line line in banner.Lines)
            {
                int shadow = Math.Max(2, line.Scale / 4);
                Text(top, line.Text, line.Scale, line.Top + shadow, shadow, Shadow);
                Text(top, line.Text, line.Scale, line.Top, 0, line.Colour);
            }

            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
                Array.Copy(top, (Height - 1 - y) * Width, pixels, y * Width, Width);
            return pixels;
        }

        public static Texture2D Make(Banner banner)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { name = banner.Name };
            texture.SetPixels32(Draw(banner));
            texture.Apply();
            return texture;
        }

        static void Rect(Color32[] p, int x, int y, int w, int h, Color32 c)
        {
            for (int j = Math.Max(0, y); j < Math.Min(Height, y + h); j++)
                for (int i = Math.Max(0, x); i < Math.Min(Width, x + w); i++)
                    p[j * Width + i] = c;
        }

        static void Frame(Color32[] p, int x, int y, int w, int h, int thick, Color32 c)
        {
            Rect(p, x, y, w, thick, c);
            Rect(p, x, y + h - thick, w, thick, c);
            Rect(p, x, y, thick, h, c);
            Rect(p, x + w - thick, y, thick, h, c);
        }

        /// <summary>A five-pointed star about (cx, cy) with outer radius r.</summary>
        static void Star(Color32[] p, int cx, int cy, int r, Color32 c)
        {
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f, rad = i % 2 == 0 ? r : r * 0.42f;
                poly[i] = new Vector2(cx + rad * Mathf.Cos(a), cy - rad * Mathf.Sin(a));
            }
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if (Inside(poly, x + 0.5f, y + 0.5f) && x >= 0 && x < Width && y >= 0 && y < Height) p[y * Width + x] = c;
        }

        static bool Inside(Vector2[] poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > y) != (poly[j].y > y) && x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        /// <summary>Text centred across the banner, its top edge at top, shifted right by dx (for a shadow).</summary>
        static void Text(Color32[] p, string text, int scale, int top, int dx, Color32 c)
        {
            int width = text.Length * 6 * scale - scale;
            int x = (Width - width) / 2 + dx;
            foreach (char ch in text)
            {
                string glyph = BannerFont.Glyph(ch);
                for (int row = 0; row < BannerFont.Rows; row++)
                    for (int col = 0; col < BannerFont.Columns; col++)
                        if (glyph[row * BannerFont.Columns + col] == '#') Rect(p, x + col * scale, top + row * scale, scale, scale, c);
                x += 6 * scale;
            }
        }
    }

    /// <summary>Our own 5 x 7 block letters for the banners: capitals, digits and a hyphen. No font file.</summary>
    public static class BannerFont
    {
        public const int Columns = 5, Rows = 7;

        // Each glyph is 7 rows of 5 cells, '#' is ink (BannerFont.Glyph joins them; the tests check every row is 5 wide).
        static readonly Dictionary<char, string[]> Rows7 = new Dictionary<char, string[]>
        {
            { 'A', new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" } },
            { 'B', new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." } },
            { 'C', new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" } },
            { 'D', new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." } },
            { 'E', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" } },
            { 'F', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." } },
            { 'G', new[] { ".####", "#....", "#....", "#.###", "#...#", "#...#", ".###." } },
            { 'H', new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" } },
            { 'I', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" } },
            { 'J', new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." } },
            { 'K', new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" } },
            { 'L', new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" } },
            { 'M', new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" } },
            { 'N', new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" } },
            { 'O', new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." } },
            { 'P', new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." } },
            { 'Q', new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" } },
            { 'R', new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" } },
            { 'S', new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." } },
            { 'T', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." } },
            { 'U', new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." } },
            { 'V', new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." } },
            { 'W', new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" } },
            { 'X', new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" } },
            { 'Y', new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." } },
            { 'Z', new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" } },
            { '0', new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." } },
            { '1', new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." } },
            { '2', new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" } },
            { '3', new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." } },
            { '4', new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." } },
            { '5', new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." } },
            { '6', new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." } },
            { '7', new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." } },
            { '8', new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." } },
            { '9', new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." } },
            { '-', new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." } },
            { ' ', new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." } },
        };

        static readonly Dictionary<char, string> Glyphs = BuildGlyphs();

        static Dictionary<char, string> BuildGlyphs()
        {
            var glyphs = new Dictionary<char, string>();
            foreach (KeyValuePair<char, string[]> g in Rows7) glyphs[g.Key] = string.Concat(g.Value);
            return glyphs;
        }

        /// <summary>The rows of a glyph, for checking that each is 5 cells wide.</summary>
        public static string[] RowsOf(char c) => Rows7[char.ToUpperInvariant(c)];

        /// <summary>The glyph's 35 cells, row by row ('#' is ink); a missing character is a blank.</summary>
        public static string Glyph(char c) => Glyphs.TryGetValue(char.ToUpperInvariant(c), out string g) ? g : Glyphs[' '];

        public static bool Has(char c) => Glyphs.ContainsKey(char.ToUpperInvariant(c));
    }
}
