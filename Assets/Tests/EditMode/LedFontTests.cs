using System;
using System.Collections.Generic;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The dot-matrix font of the LED boards and where it puts the words (#121). Tolerance 0: cells are whole.</summary>
    public class LedFontTests
    {
        /// <summary>Every string the Video Board and Centre Board can show: the Arena Show's words in every state, and the brands' ads.</summary>
        public static HashSet<string> BoardStrings()
        {
            var all = new HashSet<string>();
            void Add(string text)
            {
                if (!string.IsNullOrEmpty(text)) foreach (string line in text.Split('\n')) all.Add(line);
            }
            Add(ArenaShow.GameName);
            foreach (RoundPhase phase in Enum.GetValues(typeof(RoundPhase)))
            foreach (ArenaResult result in Enum.GetValues(typeof(ArenaResult)))
            foreach (bool newBest in new[] { false, true })
            foreach (bool hidden in new[] { false, true })
            foreach (double? speed in new double?[] { null, 7.4, 92.0, 128.6, 1234.0 })
            foreach (int goals in new[] { 0, 1, 6 })
            foreach (int shot in new[] { 1, 2, 6 })
            foreach (int best in new[] { 0, 3, 6 })
            {
                ArenaState state = ArenaShow.At(new ArenaShowInput
                {
                    Phase = phase,
                    PhaseSeconds = 1.0,
                    Result = result,
                    ResultHidden = hidden,
                    IsNewBest = newBest,
                    Goals = goals,
                    ShotNumber = shot,
                    BestScore = best,
                    LastShotSpeedKmh = speed
                });
                Add(state.VideoBoardWords.Headline);
                Add(state.VideoBoardWords.Details);
            }
            foreach (Brands.Brand b in Brands.All)
            {
                Add(b.Name);
                Add(b.AdWords);
            }
            return all;
        }

        static int CellsOf(double metres) => (int)Math.Round(metres / RinkLayout.VideoBoardLedCell);

        [Test]
        public void Every_character_the_boards_show_is_in_the_font()
        {
            HashSet<string> strings = BoardStrings();
            Assert.Greater(strings.Count, 20, "the collector found the game's words");
            var missing = new SortedSet<char>();
            foreach (string s in strings) foreach (char c in s) if (!LedFont.Knows(c)) missing.Add(c);
            Assert.IsEmpty(missing, "characters the boards show that the font does not have: " + string.Join(" ", missing) + " (in " + strings.Count + " strings)");
            foreach (Brands.Brand b in Brands.All)
                foreach (char c in b.Name + b.AdWords) Assert.IsTrue(LedFont.Knows(c), "'" + c + "' of " + b.Name);
            foreach (char c in "!.,:'-&/?· ") Assert.IsTrue(LedFont.Knows(c), "punctuation '" + c + "'");
        }

        [Test]
        public void Letters_and_digits_are_5_wide_a_space_is_3_and_an_unknown_character_is_a_blank_5()
        {
            foreach (char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
                Assert.AreEqual(5, LedFont.WidthOf(c), "'" + c + "'");
            Assert.AreEqual(3, LedFont.WidthOf(' '));
            Assert.AreEqual(3, LedFont.AdvanceOf(' '), "a space has no extra blank column");
            Assert.AreEqual(6, LedFont.AdvanceOf('A'), "a letter is followed by one blank column");
            Assert.AreEqual(5, LedFont.WidthOf('~'), "unknown: 5 columns");
            for (int column = 0; column < 5; column++)
                for (int row = -LedFont.Descender; row < LedFont.CapHeight; row++)
                    Assert.IsFalse(LedFont.IsLit('~', column, row), "unknown characters are blank");
            Assert.AreEqual(5 + 1 + 5, LedFont.LineWidth("AB"), "no blank column after the last letter");
            Assert.AreEqual(0, LedFont.LineWidth(""));
        }

        [Test]
        public void Only_g_j_p_q_y_and_the_comma_reach_below_the_baseline_and_only_b_d_f_h_k_l_t_i_j_and_capitals_reach_the_cap_row()
        {
            foreach (char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!.,:'-&/?·")
            {
                bool below = false, top = false, aboveXHeight = false;
                for (int column = 0; column < LedFont.WidthOf(c); column++)
                {
                    for (int row = -LedFont.Descender; row < 0; row++) below |= LedFont.IsLit(c, column, row);
                    top |= LedFont.IsLit(c, column, LedFont.CapHeight - 1);
                    for (int row = 5; row < LedFont.CapHeight; row++) aboveXHeight |= LedFont.IsLit(c, column, row);
                    Assert.IsFalse(LedFont.IsLit(c, column, LedFont.CapHeight), "'" + c + "' is not taller than 7 rows");
                    Assert.IsFalse(LedFont.IsLit(c, column, -LedFont.Descender - 1), "'" + c + "' is not deeper than 2 rows");
                }
                Assert.AreEqual("gjpqy,".IndexOf(c) >= 0, below, "'" + c + "' below the baseline");
                if (c >= 'a' && c <= 'z')
                    Assert.AreEqual("bdfhiklt".IndexOf(c) >= 0 || c == 'j', aboveXHeight, "'" + c + "' above the x-height (5 rows)");
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    Assert.IsTrue(top, "'" + c + "' reaches the cap row");
            }
        }

        [Test]
        public void An_E_at_scale_2_is_these_cells()
        {
            // E: ##### / #.... / #.... / ####. / #.... / #.... / #####, every dot 2 x 2 cells, left edge at column 1, baseline at row 1, in a 12 x 16 grid.
            const int w = 12, h = 16;
            var cells = new bool[w * h];
            LedFont.Plot(cells, w, h, "E", 2, 1, 1);
            string[] pattern = { "#####", "#....", "#....", "####.", "#....", "#....", "#####" };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int dotColumn = (int)Math.Floor((x - 1) / 2.0), dotLine = 6 - (int)Math.Floor((y - 1) / 2.0); // line 0 is the top row (row 6)
                    bool expected = dotColumn >= 0 && dotColumn < 5 && dotLine >= 0 && dotLine < 7 && pattern[dotLine][dotColumn] == '#';
                    Assert.AreEqual(expected, cells[y * w + x], "cell " + x + "," + y);
                }
            int lit = 0;
            foreach (bool c in cells) if (c) lit++;
            Assert.AreEqual((5 + 1 + 1 + 4 + 1 + 1 + 5) * 4, lit, "E has 18 dots of 4 cells");
        }

        [Test]
        public void Words_are_placed_in_whole_cells_with_the_first_baseline_on_a_row()
        {
            // Video Board 143 x 78 cells. Headline: capitals fit 24 % of 78 = 18.7 rows, so scale 2 (14 rows). Centre row 39 + 0.14 * 78 = 49.92;
            // the 14-row caps span 49.92 +- 7, top edge at row 57 (rounded), baseline 43; "E" is 5 columns = 10 cells wide, left edge (143 - 10) / 2 = 66.5 -> 66.
            LedWords.Block head = LedWords.Place("E", 143, 78, 0.24, 0.14);
            Assert.AreEqual(2, head.Scale);
            Assert.AreEqual(43, head.FirstBaseline);
            Assert.AreEqual(66, head.Lefts[0]);
            Assert.AreEqual(20, head.Pitch);
            // Details: 11 % of 78 = 8.6 rows -> scale 1. Two lines, pitch 10; centre row 39 - 0.22 * 78 = 21.84; span 10 + 7 = 17; top = 21.84 + 8.5 = 30.34 -> 30; first baseline 23.
            LedWords.Block details = LedWords.Place("AB\nC", 143, 78, 0.11, -0.22);
            Assert.AreEqual(1, details.Scale);
            Assert.AreEqual(23, details.FirstBaseline);
            Assert.AreEqual(13, details.BaselineOf(1));
            Assert.AreEqual((143 - 11) / 2, details.Lefts[0]);
            Assert.AreEqual((143 - 5) / 2, details.Lefts[1]);
            Assert.AreEqual(0, LedWords.Place("", 143, 78, 0.24, 0.14).Lines.Length);
        }

        [Test]
        public void Every_string_the_boards_show_fits_both_boards_between_the_side_margins()
        {
            int videoW = CellsOf(RinkLayout.VideoBoardWidth), videoH = CellsOf(RinkLayout.VideoBoardHeight);
            int centreW = CellsOf(RinkLayout.CentreBoardScreenWidth), centreH = CellsOf(RinkLayout.CentreBoardScreenHeight);
            var tooWide = new List<string>();
            foreach (string s in BoardStrings())
            {
                if (LedFont.LineWidth(s) > videoW - 2 * LedWords.SideMargin) tooWide.Add("Video Board: " + s);
                if (LedFont.LineWidth(s) > centreW - 2 * LedWords.SideMargin) tooWide.Add("Centre Board: " + s);
            }
            Assert.IsEmpty(tooWide, "lines wider than the screen at scale 1 (they would be clipped):\n" + string.Join("\n", tooWide));
            Assert.AreEqual(143, videoW);
            Assert.AreEqual(110, centreW);
            Assert.AreEqual(78, videoH);
            Assert.AreEqual(65, centreH);
        }

        [Test]
        public void A_line_wider_than_the_screen_is_drawn_at_scale_1_centred_and_clipped_at_both_edges()
        {
            const int w = 40, h = 12;
            string text = "HELLO WORLD"; // 62 columns
            LedWords.Block block = LedWords.Place(text, w, h, 0.9, 0);
            Assert.AreEqual(1, block.Scale, "no smaller scale exists");
            Assert.AreEqual((w - LedFont.LineWidth(text)) / 2, block.Lefts[0], "centred (negative: clipped on the left)");
            var cells = new bool[w * h];
            LedWords.Plot(cells, w, h, block); // must not throw
            var wide = new bool[LedFont.LineWidth(text) * h];
            LedFont.Plot(wide, LedFont.LineWidth(text), h, text, 1, 0, block.FirstBaseline);
            int shift = -block.Lefts[0];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Assert.AreEqual(wide[y * LedFont.LineWidth(text) + x + shift], cells[y * w + x], "cell " + x + "," + y);
        }

        [Test]
        public void A_scale_that_does_not_fit_the_width_is_stepped_down()
        {
            // Centre Board 110 x 65: "NEW BEST!" is 46 columns, 92 cells at scale 2, which fits between the margins (106); "PEAKSIDE ENERGY" is 86 columns, which does not.
            Assert.AreEqual(2, LedWords.Place("NEW BEST!", 110, 65, 0.24, 0.14).Scale);
            Assert.AreEqual(1, LedWords.Place("PEAKSIDE ENERGY", 110, 65, 0.24, 0.14).Scale);
        }
    }
}
