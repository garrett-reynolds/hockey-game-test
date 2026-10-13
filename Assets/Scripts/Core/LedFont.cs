using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>
    /// The dot-matrix font of the LED boards (#121): every letter is drawn cell by cell, as on a real LED board, from the pattern table
    /// below. Capitals and digits are 5 columns wide and 7 rows tall; lowercase letters are 5 wide with an x-height of 5 rows (b d f h k l t
    /// and the i and j dots rise to the capital height); g j p q y reach 2 rows below the baseline. Punctuation is 1 to 3 columns wide.
    /// A letter is followed by one blank column; a space is 3 columns wide. A character that is not in the table draws as a blank
    /// 5-column space. Rows count up from the baseline (row 0 is the lowest row of a capital); descenders are rows -1 and -2.
    /// </summary>
    public static class LedFont
    {
        public const int CapHeight = 7, Descender = 2, LetterGap = 1, SpaceWidth = 3, UnknownWidth = 5;
        /// <summary>Rows from one line's baseline to the next at scale 1: capitals, descenders and one blank row.</summary>
        public const int LinePitch = CapHeight + Descender + 1;

        sealed class Glyph
        {
            public int Width, Top; // Top: the row (above the baseline) of the first pattern line
            public string[] Rows;  // '#' lit, '.' not; the lines run downward from Top
        }

        static readonly Dictionary<char, Glyph> Table = Build();

        static Glyph G(int top, string rows)
        {
            string[] lines = rows.Split('/');
            return new Glyph { Top = top, Rows = lines, Width = lines[0].Length };
        }

        static Dictionary<char, Glyph> Build()
        {
            var t = new Dictionary<char, Glyph>
            {
                ['A'] = G(6, ".###./#...#/#...#/#####/#...#/#...#/#...#"),
                ['B'] = G(6, "####./#...#/#...#/####./#...#/#...#/####."),
                ['C'] = G(6, ".###./#...#/#..../#..../#..../#...#/.###."),
                ['D'] = G(6, "####./#...#/#...#/#...#/#...#/#...#/####."),
                ['E'] = G(6, "#####/#..../#..../####./#..../#..../#####"),
                ['F'] = G(6, "#####/#..../#..../####./#..../#..../#...."),
                ['G'] = G(6, ".###./#...#/#..../#.###/#...#/#...#/.###."),
                ['H'] = G(6, "#...#/#...#/#...#/#####/#...#/#...#/#...#"),
                ['I'] = G(6, ".###./..#../..#../..#../..#../..#../.###."),
                ['J'] = G(6, "..###/....#/....#/....#/....#/#...#/.###."),
                ['K'] = G(6, "#...#/#..#./#.#../##.../#.#../#..#./#...#"),
                ['L'] = G(6, "#..../#..../#..../#..../#..../#..../#####"),
                ['M'] = G(6, "#...#/##.##/#.#.#/#.#.#/#...#/#...#/#...#"),
                ['N'] = G(6, "#...#/##..#/##..#/#.#.#/#..##/#..##/#...#"),
                ['O'] = G(6, ".###./#...#/#...#/#...#/#...#/#...#/.###."),
                ['P'] = G(6, "####./#...#/#...#/####./#..../#..../#...."),
                ['Q'] = G(6, ".###./#...#/#...#/#...#/#.#.#/#..#./.##.#"),
                ['R'] = G(6, "####./#...#/#...#/####./#.#../#..#./#...#"),
                ['S'] = G(6, ".####/#..../#..../.###./....#/....#/####."),
                ['T'] = G(6, "#####/..#../..#../..#../..#../..#../..#.."),
                ['U'] = G(6, "#...#/#...#/#...#/#...#/#...#/#...#/.###."),
                ['V'] = G(6, "#...#/#...#/#...#/#...#/#...#/.#.#./..#.."),
                ['W'] = G(6, "#...#/#...#/#...#/#.#.#/#.#.#/##.##/#...#"),
                ['X'] = G(6, "#...#/#...#/.#.#./..#../.#.#./#...#/#...#"),
                ['Y'] = G(6, "#...#/#...#/.#.#./..#../..#../..#../..#.."),
                ['Z'] = G(6, "#####/....#/...#./..#../.#.../#..../#####"),

                ['0'] = G(6, ".###./#...#/#..##/#.#.#/##..#/#...#/.###."),
                ['1'] = G(6, "..#../.##../..#../..#../..#../..#../.###."),
                ['2'] = G(6, ".###./#...#/....#/...#./..#../.#.../#####"),
                ['3'] = G(6, "####./....#/....#/.###./....#/....#/####."),
                ['4'] = G(6, "...#./..##./.#.#./#..#./#####/...#./...#."),
                ['5'] = G(6, "#####/#..../####./....#/....#/#...#/.###."),
                ['6'] = G(6, ".###./#..../#..../####./#...#/#...#/.###."),
                ['7'] = G(6, "#####/....#/...#./..#../.#.../.#.../.#..."),
                ['8'] = G(6, ".###./#...#/#...#/.###./#...#/#...#/.###."),
                ['9'] = G(6, ".###./#...#/#...#/.####/....#/....#/.###."),

                ['a'] = G(4, ".###./....#/.####/#...#/.####"),
                ['b'] = G(6, "#..../#..../####./#...#/#...#/#...#/####."),
                ['c'] = G(4, ".###./#...#/#..../#...#/.###."),
                ['d'] = G(6, "....#/....#/.####/#...#/#...#/#...#/.####"),
                ['e'] = G(4, ".###./#...#/#####/#..../.###."),
                ['f'] = G(6, "..##./.#..#/.#.../###../.#.../.#.../.#..."),
                ['g'] = G(4, ".####/#...#/#...#/.####/....#/#...#/.###."),
                ['h'] = G(6, "#..../#..../####./#...#/#...#/#...#/#...#"),
                ['i'] = G(6, "..#../...../.##../..#../..#../..#../.###."),
                ['j'] = G(6, "...#./...../..##./...#./...#./...#./...#./#..#./.##.."),
                ['k'] = G(6, "#..../#..../#..#./#.#../##.../#.#../#..#."),
                ['l'] = G(6, ".##../..#../..#../..#../..#../..#../.###."),
                ['m'] = G(4, "##.#./#.#.#/#.#.#/#.#.#/#.#.#"),
                ['n'] = G(4, "####./#...#/#...#/#...#/#...#"),
                ['o'] = G(4, ".###./#...#/#...#/#...#/.###."),
                ['p'] = G(4, "####./#...#/#...#/####./#..../#..../#...."),
                ['q'] = G(4, ".####/#...#/#...#/.####/....#/....#/....#"),
                ['r'] = G(4, "#.##./##..#/#..../#..../#...."),
                ['s'] = G(4, ".####/#..../.###./....#/####."),
                ['t'] = G(6, ".#.../.#.../###../.#.../.#.../.#..#/..##."),
                ['u'] = G(4, "#...#/#...#/#...#/#..##/.##.#"),
                ['v'] = G(4, "#...#/#...#/#...#/.#.#./..#.."),
                ['w'] = G(4, "#...#/#...#/#.#.#/#.#.#/.#.#."),
                ['x'] = G(4, "#...#/.#.#./..#../.#.#./#...#"),
                ['y'] = G(4, "#...#/#...#/#...#/.####/....#/#...#/.###."),
                ['z'] = G(4, "#####/...#./..#../.#.../#####"),

                ['!'] = G(6, "#/#/#/#/#/./#"),
                ['.'] = G(0, "#"),
                [','] = G(0, ".#/.#/#."),
                [':'] = G(5, "#/./././#"),
                ['\''] = G(6, "#/#/."),
                ['·'] = G(3, "#"), // middle dot: the separator in the score words
                ['-'] = G(3, "###"),
                ['&'] = G(6, ".##../#..#./#.#../.#.../#.#.#/#..#./.##.#"),
                ['/'] = G(6, "....#/....#/...#./..#../.#.../#..../#...."),
                ['?'] = G(6, ".###./#...#/....#/...#./..#../...../..#.."),
            };
            return t;
        }

        /// <summary>True when the character has a pattern (a space and unknown characters have none; a space is still blank space).</summary>
        public static bool Has(char c) => Table.ContainsKey(c);

        /// <summary>True when the font draws the character on purpose: it has a pattern, or it is a space.</summary>
        public static bool Knows(char c) => c == ' ' || Table.ContainsKey(c);

        /// <summary>Columns the character's pattern takes (a space: 3; an unknown character: 5, blank).</summary>
        public static int WidthOf(char c)
        {
            if (c == ' ') return SpaceWidth;
            return Table.TryGetValue(c, out Glyph g) ? g.Width : UnknownWidth;
        }

        /// <summary>Columns from this character's left edge to the next character's: the pattern plus its blank column (a space: just 3).</summary>
        public static int AdvanceOf(char c) => c == ' ' ? SpaceWidth : WidthOf(c) + LetterGap;

        /// <summary>Columns a line takes at scale 1, with no blank column after its last character.</summary>
        public static int LineWidth(string line)
        {
            if (string.IsNullOrEmpty(line)) return 0;
            int width = 0;
            foreach (char c in line) width += AdvanceOf(c);
            return line[line.Length - 1] == ' ' ? width : width - LetterGap;
        }

        /// <summary>True when the pattern lights the dot at this column (0 at the left edge) and row (0 on the baseline, up positive).</summary>
        public static bool IsLit(char c, int column, int row)
        {
            if (!Table.TryGetValue(c, out Glyph g) || column < 0 || column >= g.Width) return false;
            int line = g.Top - row;
            return line >= 0 && line < g.Rows.Length && g.Rows[line][column] == '#';
        }

        /// <summary>
        /// Draws a line into cells (index y * cellsX + x, row 0 at the bottom): each font dot becomes scale x scale cells, the line's left
        /// edge is at column left and its baseline at row baseline. Cells outside the grid are clipped. Lit cells are set to true.
        /// </summary>
        public static void Plot(bool[] cells, int cellsX, int cellsY, string line, int scale, int left, int baseline)
        {
            int x0 = left;
            foreach (char c in line)
            {
                int width = WidthOf(c);
                for (int column = 0; column < width; column++)
                    for (int row = -Descender; row < CapHeight; row++)
                    {
                        if (!IsLit(c, column, row)) continue;
                        for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                        {
                            int x = x0 + column * scale + dx, y = baseline + row * scale + dy;
                            if (x >= 0 && x < cellsX && y >= 0 && y < cellsY) cells[y * cellsX + x] = true;
                        }
                    }
                x0 += AdvanceOf(c) * scale;
            }
        }
    }
}
