using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// Where a board's words go on its LED cells (#121): a block of centred lines in the LedFont, at a whole-number scale (each font dot is
    /// scale x scale cells), the first baseline on a cell row and the lines a whole number of cells apart. Pure arithmetic, no engine.
    /// </summary>
    public static class LedWords
    {
        /// <summary>Blank cells kept on each side of a line (a line wider than the screen less this is shrunk, then clipped).</summary>
        public const int SideMargin = 2;

        public readonly struct Block
        {
            public readonly string[] Lines;
            public readonly int Scale;         // cells per font dot
            public readonly int FirstBaseline; // the row (0 = the screen's bottom row) of the first line's lowest capital row
            public readonly int Pitch;         // rows between two lines' baselines
            public readonly int[] Lefts;       // the column of each line's left edge (negative: clipped on the left)

            public Block(string[] lines, int scale, int firstBaseline, int pitch, int[] lefts)
            {
                Lines = lines;
                Scale = scale;
                FirstBaseline = firstBaseline;
                Pitch = pitch;
                Lefts = lefts;
            }

            public int BaselineOf(int line) => FirstBaseline - line * Pitch;
        }

        /// <summary>
        /// Places text (lines split by '\n') on a screen of cellsX x cellsY cells, centred on the screen's middle moved up by
        /// centreOffsetShare of its height. The scale is the largest whole number whose capitals fit heightShare of the screen's height
        /// and whose widest line fits between the side margins; if even scale 1 is too wide the line is centred and clipped.
        /// </summary>
        public static Block Place(string text, int cellsX, int cellsY, double heightShare, double centreOffsetShare)
        {
            string[] lines = string.IsNullOrEmpty(text) ? new string[0] : text.Split('\n');
            int widest = 0;
            foreach (string line in lines) widest = Math.Max(widest, LedFont.LineWidth(line));
            int scale = Math.Max(1, (int)Math.Floor(heightShare * cellsY / LedFont.CapHeight));
            while (scale > 1 && widest * scale > cellsX - 2 * SideMargin) scale--;
            int pitch = LedFont.LinePitch * scale;
            double centre = cellsY / 2.0 + centreOffsetShare * cellsY;
            int span = Math.Max(0, lines.Length - 1) * pitch + LedFont.CapHeight * scale;
            int top = (int)Math.Floor(centre + span / 2.0 + 0.5);
            var lefts = new int[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                lefts[i] = (int)Math.Floor((cellsX - LedFont.LineWidth(lines[i]) * scale) / 2.0);
            return new Block(lines, scale, top - LedFont.CapHeight * scale, pitch, lefts);
        }

        /// <summary>One line placed by PlaceWhole: the letters kept (a prefix of the text), their scale, left column and baseline row.</summary>
        public readonly struct Fitted
        {
            public readonly string Text;
            public readonly int Scale;     // cells per font dot (0: no words)
            public readonly int Left;      // the column of the line's left edge
            public readonly int Baseline;  // the row (0 = the bottom row) of the capitals' lowest row
            public readonly bool Clipped;  // true when letters were dropped to fit

            public Fitted(string text, int scale, int left, int baseline, bool clipped)
            {
                Text = text;
                Scale = scale;
                Left = left;
                Baseline = baseline;
                Clipped = clipped;
            }
        }

        /// <summary>Rows the line's lit dots reach below the baseline at scale 1 (0 for capitals and digits; up to LedFont.Descender).</summary>
        public static int DepthBelow(string line)
        {
            int depth = 0;
            foreach (char c in line)
                for (int row = -LedFont.Descender; row < 0; row++)
                    for (int column = 0; column < LedFont.WidthOf(c); column++)
                        if (LedFont.IsLit(c, column, row)) depth = Math.Max(depth, -row);
            return depth;
        }

        /// <summary>
        /// Places one line on a screen of cellsX x cellsY cells for a board whose cells are few (the Ribbon Boards). The scale is the largest
        /// whole number up to maxScale whose ink, the capitals plus the rows the line's descenders reach, fits the height and whose width fits
        /// between the side margins. If even scale 1 is too wide, whole letters are dropped from the end (never half a letter) and the
        /// rest is centred. The ink is centred up and down.
        /// </summary>
        public static Fitted PlaceWhole(string text, int cellsX, int cellsY, int maxScale)
        {
            text = text ?? "";
            if (text.Length == 0) return new Fitted("", 0, 0, 0, false);
            int room = cellsX - 2 * SideMargin;
            int scale = Math.Max(1, maxScale);
            while (scale > 1 && (LedFont.LineWidth(text) * scale > room || (LedFont.CapHeight + DepthBelow(text)) * scale > cellsY)) scale--;
            string kept = text;
            while (kept.Length > 0 && LedFont.LineWidth(kept) * scale > room) kept = kept.Substring(0, kept.Length - 1).TrimEnd();
            int depth = DepthBelow(kept);
            int total = (LedFont.CapHeight + depth) * scale;
            int left = (int)Math.Floor((cellsX - LedFont.LineWidth(kept) * scale) / 2.0);
            int baseline = (int)Math.Floor((cellsY - total) / 2.0) + depth * scale;
            return new Fitted(kept, scale, left, baseline, kept.Length != text.Length);
        }

        /// <summary>Lights the block's cells (index y * cellsX + x; row 0 at the bottom).</summary>
        public static void Plot(bool[] cells, int cellsX, int cellsY, Block block)
        {
            for (int i = 0; i < block.Lines.Length; i++)
                LedFont.Plot(cells, cellsX, cellsY, block.Lines[i], block.Scale, block.Lefts[i], block.BaselineOf(i));
        }
    }
}
