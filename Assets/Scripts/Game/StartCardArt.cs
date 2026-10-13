using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The Video Board's start card as LED dots (#161, was a sharp picture in #153): the real Shootout logo (StartLogoArt: blue caps,
    /// gold stripe, slant, shadow) on a navy board, with the small words under it ("BEST SCORE 6 OF 6", "SHOOT TO PLAY") in LED-font
    /// cells. One texel per LED cell, point filtered, so the LED Board shader draws it on the same dot grid (same dot size and gap) as
    /// the other cards. The picture has the board's shape (cellsX by cellsY LED cells), so nothing is stretched. Made again only when
    /// the small words change.
    /// </summary>
    public static class StartCardArt
    {
        public const int PixelsPerCell = 1;
        /// <summary>The logo's width as a share of the board's, and the most of the board's height it may take.</summary>
        public const float LogoWidthShare = 0.88f, LogoHeightShare = 0.55f;
        /// <summary>The logo's middle, as a share of the board's height up from the bottom.</summary>
        public const float LogoCentreShare = 0.70f;

        /// <summary>The logo's rectangle on the picture, in texels (cells) from its lower-left corner.</summary>
        public static RectInt LogoRect(int cellsX, int cellsY)
        {
            int w = cellsX * PixelsPerCell, h = cellsY * PixelsPerCell;
            int logoW = Mathf.Min(Mathf.RoundToInt(LogoWidthShare * w), Mathf.RoundToInt(LogoHeightShare * h * StartLogoArt.Aspect));
            int logoH = Mathf.RoundToInt(logoW / StartLogoArt.Aspect);
            return new RectInt((w - logoW) / 2, Mathf.RoundToInt(LogoCentreShare * h - logoH / 2f), logoW, logoH);
        }

        public static Texture2D Make(int cellsX, int cellsY, string details)
        {
            int w = cellsX * PixelsPerCell, h = cellsY * PixelsPerCell;
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = LookC.Navy;

            // The logo: each LED cell takes the average colour of the logo's pixels under it (alpha weighted), laid over the navy.
            RectInt logo = LogoRect(cellsX, cellsY);
            Color32[] art = StartLogoArt.Pixels();
            int aw = StartLogoArt.Width, ah = StartLogoArt.Height;
            for (int y = 0; y < logo.height; y++)
            {
                int py = logo.y + y;
                if (py < 0 || py >= h) continue;
                int v0 = y * ah / logo.height, v1 = Mathf.Max(v0 + 1, (y + 1) * ah / logo.height);
                for (int x = 0; x < logo.width; x++)
                {
                    int px = logo.x + x;
                    if (px < 0 || px >= w) continue;
                    int u0 = x * aw / logo.width, u1 = Mathf.Max(u0 + 1, (x + 1) * aw / logo.width);
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int v = v0; v < v1; v++)
                        for (int u = u0; u < u1; u++)
                        {
                            Color32 c = art[v * aw + u];
                            float ca = c.a / 255f;
                            r += c.r * ca;
                            g += c.g * ca;
                            b += c.b * ca;
                            a += ca;
                        }
                    if (a <= 0f) continue;
                    float coverage = a / ((v1 - v0) * (u1 - u0));
                    Color32 back = pixels[py * w + px];
                    pixels[py * w + px] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(back.r, r / a, coverage)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(back.g, g / a, coverage)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(back.b, b / a, coverage)), 255);
                }
            }

            // The small words: LED-font cells; a lit cell is a white texel.
            var cells = new bool[cellsX * cellsY];
            LedWords.Plot(cells, cellsX, cellsY, LedWords.Place(details, cellsX, cellsY, VideoBoards.DetailsShare, -VideoBoards.DetailsY)); // the small words sit where the other cards' do
            for (int i = 0; i < cells.Length; i++)
                if (cells[i]) pixels[i] = LookC.White;

            // One texel per cell, read point filtered by the LED Board shader on the board's own dot grid.
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Video Board Start Card", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(pixels);
            t.Apply(false);
            return t;
        }
    }
}
