using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>A picture as plain pixels: RGBA bytes, row 0 at the top.</summary>
    public sealed class RgbaPicture
    {
        public readonly int Width, Height;
        public readonly byte[] Rgba;

        public RgbaPicture(int width, int height, byte[] rgba)
        {
            Width = width;
            Height = height;
            Rgba = rgba;
        }
    }

    /// <summary>
    /// Makes a brand's colour board panel from one picked logo picture (#226), the way Tools/make_ads.py does: crop to the logo,
    /// clear a white box joined to the picture's edge, then lay the logo on a 3:1 board-white panel, fitted 84% x 70%, clear
    /// round the logo. Keep the numbers here in step with the PANEL, PANEL_FIT and BOARD_WHITE numbers in Tools/make_ads.py
    /// (and the crop and white-box rules with its crop and clear_white_box).
    /// Plain pixel arrays, no Unity types, so EditMode tests run it with no scene.
    /// </summary>
    public static class LogoArt
    {
        public const int PanelWidth = 768, PanelHeight = 256;
        public const double FitWidth = 0.84, FitHeight = 0.70;
        public const byte BoardWhiteR = 236, BoardWhiteG = 238, BoardWhiteB = 240;

        const int VisibleAlpha = 8;   // the art tool's crop: alpha above this is part of the logo
        const int WhiteBoxClear = 30; // clear_white_box: alpha below this counts as white
        const int WhiteBoxLevel = 225; // ... and so does a pixel brighter than this in every colour

        /// <summary>Crop, clear the joined white box, crop again. Null: no visible logo (all clear, or one plain colour box).</summary>
        public static RgbaPicture MakeLogo(RgbaPicture picked)
        {
            RgbaPicture logo = Crop(picked);
            if (logo == null) return null;
            logo = Crop(ClearWhiteBox(logo));
            if (logo == null) return null;

            bool anyClear = false, anyDifferent = false;
            byte[] p = logo.Rgba;
            for (int i = 0; i < p.Length; i += 4)
            {
                if (p[i + 3] <= VisibleAlpha) anyClear = true;
                if (p[i] != p[0] || p[i + 1] != p[1] || p[i + 2] != p[2]) anyDifferent = true;
            }
            return anyClear || anyDifferent ? logo : null; // a plain one-colour box has no shape to show
        }

        /// <summary>The 768 x 256 colour panel for a made logo: board white under the logo, alpha 0 round it.</summary>
        public static RgbaPicture MakePanel(RgbaPicture logo)
        {
            double k = Math.Min(PanelWidth * FitWidth / logo.Width, PanelHeight * FitHeight / logo.Height);
            int w = Math.Max(1, (int)Math.Round(logo.Width * k)), h = Math.Max(1, (int)Math.Round(logo.Height * k));

            // The logo laid on board white (colour), with its own alpha kept apart, as the art tool's on_white does.
            var onWhite = new byte[logo.Rgba.Length];
            for (int i = 0; i < onWhite.Length; i += 4)
            {
                int a = logo.Rgba[i + 3];
                onWhite[i] = (byte)((logo.Rgba[i] * a + BoardWhiteR * (255 - a)) / 255);
                onWhite[i + 1] = (byte)((logo.Rgba[i + 1] * a + BoardWhiteG * (255 - a)) / 255);
                onWhite[i + 2] = (byte)((logo.Rgba[i + 2] * a + BoardWhiteB * (255 - a)) / 255);
                onWhite[i + 3] = (byte)a;
            }
            byte[] small = Resize(onWhite, logo.Width, logo.Height, w, h);

            var panel = new byte[PanelWidth * PanelHeight * 4];
            for (int i = 0; i < panel.Length; i += 4)
            {
                panel[i] = BoardWhiteR; panel[i + 1] = BoardWhiteG; panel[i + 2] = BoardWhiteB; // alpha 0
            }
            int x0 = (PanelWidth - w) / 2, y0 = (PanelHeight - h) / 2;
            for (int y = 0; y < h; y++)
                Array.Copy(small, y * w * 4, panel, ((y0 + y) * PanelWidth + x0) * 4, w * 4);
            return new RgbaPicture(PanelWidth, PanelHeight, panel);
        }

        /// <summary>The picture shrunk so its long side is at most maxSide (the same picture back when it already is).</summary>
        public static RgbaPicture Shrink(RgbaPicture picture, int maxSide)
        {
            int longSide = Math.Max(picture.Width, picture.Height);
            if (longSide <= maxSide) return picture;
            double k = (double)maxSide / longSide;
            int w = Math.Max(1, (int)Math.Round(picture.Width * k)), h = Math.Max(1, (int)Math.Round(picture.Height * k));

            var premultiplied = new byte[picture.Rgba.Length]; // so clear pixels do not bleed their colour into the edge
            for (int i = 0; i < premultiplied.Length; i += 4)
            {
                int a = picture.Rgba[i + 3];
                premultiplied[i] = (byte)(picture.Rgba[i] * a / 255);
                premultiplied[i + 1] = (byte)(picture.Rgba[i + 1] * a / 255);
                premultiplied[i + 2] = (byte)(picture.Rgba[i + 2] * a / 255);
                premultiplied[i + 3] = (byte)a;
            }
            byte[] small = Resize(premultiplied, picture.Width, picture.Height, w, h);
            for (int i = 0; i < small.Length; i += 4)
            {
                int a = small[i + 3];
                if (a == 0) continue;
                small[i] = (byte)Math.Min(255, small[i] * 255 / a);
                small[i + 1] = (byte)Math.Min(255, small[i + 1] * 255 / a);
                small[i + 2] = (byte)Math.Min(255, small[i + 2] * 255 / a);
            }
            return new RgbaPicture(w, h, small);
        }

        /// <summary>The picture cut to the box of its visible pixels (alpha above 8). Null when nothing is visible.</summary>
        static RgbaPicture Crop(RgbaPicture p)
        {
            int x0 = p.Width, y0 = p.Height, x1 = -1, y1 = -1;
            for (int y = 0; y < p.Height; y++)
                for (int x = 0; x < p.Width; x++)
                    if (p.Rgba[(y * p.Width + x) * 4 + 3] > VisibleAlpha)
                    {
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
            if (x1 < 0) return null;
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                Array.Copy(p.Rgba, ((y0 + y) * p.Width + x0) * 4, rgba, y * w * 4, w * 4);
            return new RgbaPicture(w, h, rgba);
        }

        /// <summary>Clears the white (and clear) area joined to the picture's edge: a full-size white box behind the logo.</summary>
        static RgbaPicture ClearWhiteBox(RgbaPicture p)
        {
            int w = p.Width, h = p.Height;
            byte[] src = p.Rgba;
            var rgba = (byte[])src.Clone();
            var joined = new bool[w * h];
            var stack = new Stack<int>();
            Func<int, bool> white = n =>
            {
                int i = n * 4;
                return src[i + 3] < WhiteBoxClear || (src[i] > WhiteBoxLevel && src[i + 1] > WhiteBoxLevel && src[i + 2] > WhiteBoxLevel);
            };
            Action<int> seed = n => { if (!joined[n] && white(n)) { joined[n] = true; stack.Push(n); } };
            for (int x = 0; x < w; x++) { seed(x); seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { seed(y * w); seed(y * w + w - 1); }
            while (stack.Count > 0)
            {
                int n = stack.Pop(), x = n % w, y = n / w;
                if (x > 0) seed(n - 1);
                if (x < w - 1) seed(n + 1);
                if (y > 0) seed(n - w);
                if (y < h - 1) seed(n + w);
            }
            for (int n = 0; n < joined.Length; n++)
                if (joined[n]) rgba[n * 4 + 3] = 0;
            return new RgbaPicture(w, h, rgba);
        }

        /// <summary>Four-channel resize: a box average when shrinking, a straight-line blend when growing. Two passes, across then down.</summary>
        static byte[] Resize(byte[] src, int sw, int sh, int dw, int dh)
        {
            var across = new byte[dw * sh * 4];
            Weights(sw, dw, out int[] firstX, out float[][] wx);
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < dw; x++)
                    for (int c = 0; c < 4; c++)
                    {
                        float sum = 0;
                        for (int j = 0; j < wx[x].Length; j++) sum += src[(y * sw + firstX[x] + j) * 4 + c] * wx[x][j];
                        across[(y * dw + x) * 4 + c] = (byte)Math.Min(255, Math.Max(0, (int)(sum + 0.5f)));
                    }

            var down = new byte[dw * dh * 4];
            Weights(sh, dh, out int[] firstY, out float[][] wy);
            for (int y = 0; y < dh; y++)
                for (int x = 0; x < dw; x++)
                    for (int c = 0; c < 4; c++)
                    {
                        float sum = 0;
                        for (int j = 0; j < wy[y].Length; j++) sum += across[((firstY[y] + j) * dw + x) * 4 + c] * wy[y][j];
                        down[(y * dw + x) * 4 + c] = (byte)Math.Min(255, Math.Max(0, (int)(sum + 0.5f)));
                    }
            return down;
        }

        /// <summary>For each of dstN output places, the first source place it reads and the weights over the places from there (they add to 1).</summary>
        static void Weights(int srcN, int dstN, out int[] first, out float[][] weights)
        {
            first = new int[dstN];
            weights = new float[dstN][];
            double scale = (double)srcN / dstN;
            for (int i = 0; i < dstN; i++)
            {
                if (scale > 1) // shrinking: every source place under this output place counts by how much of it is under
                {
                    double from = i * scale, to = (i + 1) * scale;
                    int a = (int)Math.Floor(from), b = Math.Min(srcN - 1, (int)Math.Ceiling(to) - 1);
                    var w = new float[b - a + 1];
                    float total = 0;
                    for (int s = a; s <= b; s++)
                    {
                        w[s - a] = (float)(Math.Min(to, s + 1) - Math.Max(from, s));
                        total += w[s - a];
                    }
                    for (int s = 0; s < w.Length; s++) w[s] /= total;
                    first[i] = a;
                    weights[i] = w;
                }
                else // growing: blend the two source places either side of the output place's middle
                {
                    double centre = (i + 0.5) * scale - 0.5;
                    int a = (int)Math.Floor(centre);
                    float t = (float)(centre - a);
                    int lo = Math.Max(0, Math.Min(srcN - 1, a)), hi = Math.Max(0, Math.Min(srcN - 1, a + 1));
                    if (hi == lo) { first[i] = lo; weights[i] = new[] { 1f }; }
                    else { first[i] = lo; weights[i] = new[] { 1f - t, t }; }
                }
            }
        }
    }
}
