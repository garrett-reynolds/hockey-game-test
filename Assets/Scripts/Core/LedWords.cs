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

        /// <summary>Lights the block's cells (index y * cellsX + x; row 0 at the bottom).</summary>
        public static void Plot(bool[] cells, int cellsX, int cellsY, Block block)
        {
            for (int i = 0; i < block.Lines.Length; i++)
                LedFont.Plot(cells, cellsX, cellsY, block.Lines[i], block.Scale, block.Lefts[i], block.BaselineOf(i));
        }
    }
}
