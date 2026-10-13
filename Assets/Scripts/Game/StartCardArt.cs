using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The Video Board's start card as one picture (#153): the real Shootout logo (StartLogoArt: blue caps, gold stripe, slant, shadow)
    /// on a navy board, with the small words under it ("BEST SCORE 6 OF 6", "SHOOT TO PLAY") in LED-font cells, each cell a block of
    /// PixelsPerCell pixels. The picture has the board's shape (cellsX by cellsY LED cells), so nothing is stretched. Made again only
    /// when the small words change.
    /// </summary>
    public static class StartCardArt
    {
        public const int PixelsPerCell = 6;
        /// <summary>The logo's width as a share of the board's, and the most of the board's height it may take.</summary>
        public const float LogoWidthShare = 0.88f, LogoHeightShare = 0.55f;
        /// <summary>The logo's middle, as a share of the board's height up from the bottom.</summary>
        public const float LogoCentreShare = 0.70f;

        /// <summary>The logo's rectangle on the picture, in pixels from its lower-left corner.</summary>
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

            RectInt logo = LogoRect(cellsX, cellsY);
            Color32[] art = StartLogoArt.Pixels();
            int aw = StartLogoArt.Width, ah = StartLogoArt.Height;
            for (int y = 0; y < logo.height; y++)
            {
                int py = logo.y + y;
                if (py < 0 || py >= h) continue;
                float v = Mathf.Clamp((y + 0.5f) / logo.height * ah - 0.5f, 0f, ah - 1f);
                int v0 = Mathf.FloorToInt(v), v1 = Mathf.Min(v0 + 1, ah - 1);
                float fv = v - v0;
                for (int x = 0; x < logo.width; x++)
                {
                    int px = logo.x + x;
                    if (px < 0 || px >= w) continue;
                    float u = Mathf.Clamp((x + 0.5f) / logo.width * aw - 0.5f, 0f, aw - 1f);
                    int u0 = Mathf.FloorToInt(u), u1 = Mathf.Min(u0 + 1, aw - 1);
                    float fu = u - u0;
                    Color c = Mix(Mix(art[v0 * aw + u0], art[v0 * aw + u1], fu), Mix(art[v1 * aw + u0], art[v1 * aw + u1], fu), fv);
                    Color32 back = pixels[py * w + px];
                    pixels[py * w + px] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(back.r, c.r * 255f, c.a)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(back.g, c.g * 255f, c.a)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(back.b, c.b * 255f, c.a)), 255);
                }
            }

            // The small words: LED-font cells, each a block with a one-pixel gap.
            var cells = new bool[cellsX * cellsY];
            LedWords.Plot(cells, cellsX, cellsY, LedWords.Place(details, cellsX, cellsY, VideoBoards.DetailsShare, -VideoBoards.DetailsY)); // the small words sit where the other cards' do
            for (int cy = 0; cy < cellsY; cy++)
                for (int cx = 0; cx < cellsX; cx++)
                {
                    if (!cells[cy * cellsX + cx]) continue;
                    for (int dy = 0; dy < PixelsPerCell - 1; dy++)
                        for (int dx = 0; dx < PixelsPerCell - 1; dx++)
                            pixels[(cy * PixelsPerCell + dy) * w + cx * PixelsPerCell + dx] = LookC.White;
                }

            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Video Board Start Card", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(pixels);
            t.Apply(false);
            return t;
        }

        static Color Mix(Color32 a, Color32 b, float t) => Color.Lerp(a, b, t);
        static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);
    }
}
