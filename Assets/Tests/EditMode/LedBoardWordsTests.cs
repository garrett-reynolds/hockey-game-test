using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The words on the Video Board and Centre Board are drawn cell by cell in the LED font, one texel per LED cell (#121). Tolerance 0.</summary>
    public class LedBoardWordsTests
    {
        GameObject _rink;

        [SetUp]
        public void MakeRink() => _rink = new GameObject("Rink");

        [TearDown]
        public void RemoveRink() => Object.DestroyImmediate(_rink);

        static ArenaState Show(RoundPhase phase, ArenaResult result = ArenaResult.None, bool newBest = false, double? kmh = null) =>
            ArenaShow.At(new ArenaShowInput
            {
                Phase = phase,
                PhaseSeconds = 1.0,
                Result = result,
                IsNewBest = newBest,
                Goals = 2,
                ShotNumber = 4,
                BestScore = 3,
                LastShotSpeedKmh = kmh
            });

        /// <summary>The cells the font table gives for these words on a screen of this many cells (row 0 at the bottom).</summary>
        public static bool[] Expected(int cellsX, int cellsY, string headline, string details)
        {
            var cells = new bool[cellsX * cellsY];
            LedWords.Plot(cells, cellsX, cellsY, LedWords.Place(headline, cellsX, cellsY, VideoBoards.HeadlineShare, VideoBoards.HeadlineY));
            LedWords.Plot(cells, cellsX, cellsY, LedWords.Place(details, cellsX, cellsY, VideoBoards.DetailsShare, -VideoBoards.DetailsY));
            return cells;
        }

        static string Differences(bool[] shown, bool[] expected, int cellsX)
        {
            int extra = 0, missing = 0;
            string first = "";
            for (int i = 0; i < expected.Length; i++)
            {
                if (shown[i] == expected[i]) continue;
                if (shown[i]) extra++; else missing++;
                if (first.Length == 0) first = " (first at cell " + i % cellsX + "," + i / cellsX + ")";
            }
            return extra + " lit cells not in the table, " + missing + " table cells not lit" + first;
        }

        void CheckScreens(VideoBoards boards, string headline, string details, string what)
        {
            for (int screen = 0; screen < boards.ScreenCount; screen++)
            {
                int w = boards.CellsAcross(screen), h = boards.CellsUp(screen);
                bool[] shown = boards.LitCellsOn(screen), expected = Expected(w, h, headline, details);
                Assert.AreEqual(expected.Length, shown.Length, what + ", screen " + screen + ": one texel per cell");
                string diff = Differences(shown, expected, w);
                Assert.IsTrue(diff.StartsWith("0 lit cells not in the table, 0 table cells not lit"), what + ", screen " + screen + ": " + diff);
            }
        }

        [Test]
        public void The_lit_cells_of_the_Arena_Shows_words_are_the_font_table_cell_for_cell_on_every_screen()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            var states = new Dictionary<string, ArenaState>
            {
                ["start card"] = Show(RoundPhase.Idle),
                ["score (Headline)"] = Show(RoundPhase.SkateIn),
                ["score and last shot (Details)"] = Show(RoundPhase.SlowDown, kmh: 92.4),
                ["goal"] = Show(RoundPhase.ResultHold, ArenaResult.Goal),
                ["new best"] = Show(RoundPhase.ResultHold, ArenaResult.Goal, newBest: true),
                ["round over (two Details lines)"] = Show(RoundPhase.RoundOver),
            };
            foreach (KeyValuePair<string, ArenaState> pair in states)
            {
                boards.Show(pair.Value, 0.0);
                VideoBoardWords words = pair.Value.VideoBoardWords;
                CheckScreens(boards, words.Headline, words.Details, pair.Key);
            }
        }

        // Hand-checked, no production layout code: "GO" is 5 + 1 + 5 = 11 font columns. On the Video Board (143 x 78) the Headline is
        // scale 2 (floor(0.24 x 78 / 7) = 2), 22 cells wide, left edge floor((143 - 22) / 2) = 60, so columns 60..81. Capitals span
        // 14 rows, top edge round(39 + 0.14 x 78 + 7) = 57, so rows 43..56. Each font dot is 2 x 2 cells. Written top row first.
        static readonly string[] GoOnTheVideoBoard =
        {
            "..######......######..", // row 56
            "..######......######..", // 55
            "##......##..##......##", // 54
            "##......##..##......##", // 53
            "##..........##......##", // 52
            "##..........##......##", // 51
            "##..######..##......##", // 50
            "##..######..##......##", // 49
            "##......##..##......##", // 48
            "##......##..##......##", // 47
            "##......##..##......##", // 46
            "##......##..##......##", // 45
            "..######......######..", // 44
            "..######......######..", // 43
        };

        [Test]
        public void The_word_GO_lights_exactly_these_hand_written_cells_on_the_Video_Board()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            boards.ShowWords("GO", "");
            Assert.AreEqual(143, boards.CellsAcross(0));
            Assert.AreEqual(78, boards.CellsUp(0));
            bool[] shown = boards.LitCellsOn(0);
            int wrong = 0;
            string first = "";
            for (int y = 0; y < 78; y++)
                for (int x = 0; x < 143; x++)
                {
                    bool expected = x >= 60 && x < 82 && y >= 43 && y < 57 && GoOnTheVideoBoard[56 - y][x - 60] == '#';
                    if (shown[y * 143 + x] == expected) continue;
                    wrong++;
                    if (first.Length == 0) first = " (first at cell " + x + "," + y + ")";
                }
            Assert.AreEqual(0, wrong, "cells differing from the hand-written GO" + first);
        }

        [Test]
        public void Mixed_case_brand_words_with_descenders_are_the_font_table_cell_for_cell()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            foreach (Brands.Brand brand in Brands.All)
            {
                boards.ShowWords(brand.Name, "Saves, goals & Y/N? gjpqy");
                CheckScreens(boards, brand.Name, "Saves, goals & Y/N? gjpqy", brand.Name);
            }
            // A descender row is lit under the g of "Big Timber Burgers" (rows 1 and 2 below the baseline), and nowhere else below it.
            boards.ShowWords("Big Timber Burgers", "");
            LedWords.Block block = LedWords.Place("Big Timber Burgers", boards.CellsAcross(1), boards.CellsUp(1), VideoBoards.HeadlineShare, VideoBoards.HeadlineY);
            bool[] shown = boards.LitCellsOn(1);
            int w = boards.CellsAcross(1), below = 0;
            for (int y = 0; y < block.FirstBaseline; y++)
                for (int x = 0; x < w; x++) if (shown[y * w + x]) below++;
            Assert.Greater(below, 0, "the g's of Big and Burgers hang below the baseline");
        }

        [Test]
        public void Words_wider_than_the_screen_are_clipped_not_wrapped_and_still_equal_the_table()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            string tooWide = "A very long sentence that cannot fit across the Centre Board";
            boards.ShowWords(tooWide, "");
            CheckScreens(boards, tooWide, "", "clipped line");
        }

        [Test]
        public void Each_board_has_one_R8_texel_per_cell_point_filtered_with_no_mips_and_the_Centre_Boards_four_screens_share_one()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            boards.Show(Show(RoundPhase.SkateIn), 0.0);
            Assert.AreEqual(2, boards.WordsTextureCount);
            long bytes = 0;
            for (int screen = 0; screen < boards.ScreenCount; screen++)
            {
                Texture shownTexture = boards.WordsTextureOn(screen);
                var texture = shownTexture as Texture2D;
                Assert.IsNotNull(texture, "screen " + screen + " words texture is a Texture2D");
                Assert.AreEqual(boards.CellsAcross(screen), texture.width, "screen " + screen);
                Assert.AreEqual(boards.CellsUp(screen), texture.height, "screen " + screen);
                Assert.AreEqual(TextureFormat.R8, texture.format);
                Assert.AreEqual(FilterMode.Point, texture.filterMode);
                Assert.AreEqual(1, texture.mipmapCount, "no mips");
                Assert.AreSame(texture, boards.PictureOn(screen).mainTexture, "the screen shows it");
                if (screen == 0 || screen == 1) bytes += texture.width * texture.height;
            }
            Assert.AreSame(boards.WordsTextureOn(1), boards.WordsTextureOn(4), "the Centre Board's four screens share one texture");
            Assert.AreEqual(143 * 78 + 110 * 65, bytes);
            Assert.AreEqual(bytes, boards.WordsTextureBytes);
        }

        [Test]
        public void The_scale_of_each_line_is_the_largest_whole_number_that_fits_its_height_and_the_screen_width()
        {
            // Headline: capitals 7 dots tall; a Headline gets 24 % of the screen's height, so at most scale 2 (14 rows of 78 or 65). Details get 11 %:
            // scale 1. A line must also fit between two blank cells at each side (143 - 4 = 139 across the Video Board, 110 - 4 = 106 across the Centre Board).
            var scales = new Dictionary<string, (int Video, int Centre)>
            {
                ["GOAL!"] = (2, 2),
                ["REPLAY"] = (2, 2),
                ["NEW BEST!"] = (2, 2),
                ["SHOOTOUT"] = (2, 2), // the start card's name (8 letters fit the Centre Board at scale 2)
                ["FINAL SCORE"] = (2, 1),
                ["GOALS 2 · SHOT 4 OF 6"] = (1, 1),
                ["BIG TIMBER BURGERS"] = (1, 1),
            };
            foreach (KeyValuePair<string, (int Video, int Centre)> pair in scales)
            {
                Assert.AreEqual(pair.Value.Video, LedWords.Place(pair.Key, 143, 78, VideoBoards.HeadlineShare, VideoBoards.HeadlineY).Scale, "Video Board: " + pair.Key);
                Assert.AreEqual(pair.Value.Centre, LedWords.Place(pair.Key, 110, 65, VideoBoards.HeadlineShare, VideoBoards.HeadlineY).Scale, "Centre Board: " + pair.Key);
            }
            Assert.AreEqual(1, LedWords.Place("BEST SCORE 3 OF 6", 143, 78, VideoBoards.DetailsShare, -VideoBoards.DetailsY).Scale, "Details, Video Board");
            Assert.AreEqual(1, LedWords.Place("BEST SCORE 3 OF 6", 110, 65, VideoBoards.DetailsShare, -VideoBoards.DetailsY).Scale, "Details, Centre Board");
        }

        [Test]
        public void The_Headline_and_the_Details_lines_stay_on_the_screen_and_apart_for_every_string()
        {
            foreach (int[] size in new[] { new[] { 143, 78 }, new[] { 110, 65 } })
            foreach (string headline in LedFontTests.BoardStrings())
            {
                LedWords.Block head = LedWords.Place(headline, size[0], size[1], VideoBoards.HeadlineShare, VideoBoards.HeadlineY);
                int headBottom = head.FirstBaseline - LedFont.Descender * head.Scale, headTop = head.FirstBaseline + LedFont.CapHeight * head.Scale;
                Assert.GreaterOrEqual(headBottom, 0, headline + " descenders on the screen");
                Assert.LessOrEqual(headTop, size[1], headline + " capitals on the screen");
                LedWords.Block details = LedWords.Place("BEST SCORE 3 OF 6\nSHOOT TO PLAY", size[0], size[1], VideoBoards.DetailsShare, -VideoBoards.DetailsY);
                int detailsBottom = details.BaselineOf(1) - LedFont.Descender * details.Scale, detailsTop = details.FirstBaseline + LedFont.CapHeight * details.Scale;
                Assert.GreaterOrEqual(detailsBottom, 0, "Details bottom line on the screen");
                Assert.Less(detailsTop, headBottom, headline + ": the Details' top row is below the Headline's lowest row");
            }
        }
    }
}
