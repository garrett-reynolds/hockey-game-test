using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>LedWords.PlaceWhole (#122): one line on a panel of few cells, descenders counted, whole letters only.</summary>
    public class LedWordsTests
    {
        [Test]
        public void A_short_word_takes_the_largest_scale_up_to_the_cap_and_is_centred()
        {
            LedWords.Fitted fit = LedWords.PlaceWhole("GOAL!", 120, 24, 3);
            Assert.AreEqual(3, fit.Scale);
            Assert.IsFalse(fit.Clipped);
            Assert.AreEqual("GOAL!", fit.Text);
            Assert.AreEqual((120 - LedFont.LineWidth("GOAL!") * 3) / 2, fit.Left);
            Assert.AreEqual((24 - 7 * 3) / 2, fit.Baseline, "capitals centred up and down (no descenders)");
            Assert.AreEqual(2, LedWords.PlaceWhole("GOAL!", 120, 24, 2).Scale, "the cap holds");
        }

        [Test]
        public void Descender_rows_count_in_the_height()
        {
            Assert.AreEqual(0, LedWords.DepthBelow("GOAL!"));
            Assert.AreEqual(LedFont.Descender, LedWords.DepthBelow("Spy"));
            // 24 rows: capitals alone fit scale 3 (21 rows); with descenders (9 rows each) only scale 2 (18).
            Assert.AreEqual(3, LedWords.PlaceWhole("Sun", 120, 24, 3).Scale);
            LedWords.Fitted low = LedWords.PlaceWhole("Spy", 120, 24, 3);
            Assert.AreEqual(2, low.Scale);
            int top = low.Baseline + 7 * low.Scale, bottom = low.Baseline - LedFont.Descender * low.Scale;
            Assert.GreaterOrEqual(bottom, 0, "the descenders stay on the panel");
            Assert.LessOrEqual(top, 24, "the capitals stay on the panel");
        }

        [Test]
        public void A_line_too_wide_at_scale_1_drops_whole_letters_from_the_end()
        {
            const string ad = "NORTHVALE BANK";
            LedWords.Fitted fit = LedWords.PlaceWhole(ad, 40, 24, 2);
            Assert.AreEqual(1, fit.Scale);
            Assert.IsTrue(fit.Clipped);
            Assert.IsTrue(ad.StartsWith(fit.Text) && fit.Text.Length > 0 && fit.Text.Length < ad.Length);
            Assert.LessOrEqual(LedFont.LineWidth(fit.Text), 40 - 2 * LedWords.SideMargin, "what is kept fits between the margins");
            string next = ad.Substring(0, fit.Text.Length + 1).TrimEnd();
            Assert.Greater(LedFont.LineWidth(next), 40 - 2 * LedWords.SideMargin, "the next letter would not fit");
            Assert.GreaterOrEqual(fit.Left, LedWords.SideMargin);
        }

        [TestCase("OFF THE POST!", 48)]
        [TestCase("OFF THE BAR!", 48)]
        [TestCase("KESTREL MOTORS", 73)]
        [TestCase("Aurora Air", 48)] // fits one line on 73 cells (56 of 69), so the 48-cell bench cut is where it splits
        public void Words_too_wide_for_one_line_split_in_two_at_a_space_before_any_letter_is_dropped(string text, int cells)
        {
            Assert.Greater(LedFont.LineWidth(text), cells - 2 * LedWords.SideMargin, "too wide for one line at scale 1");
            LedWords.Fitted fit = LedWords.PlaceWhole(text, cells, 24, 2);
            Assert.IsFalse(fit.Clipped, "no letter dropped");
            Assert.AreEqual(1, fit.Scale);
            Assert.AreEqual(text, fit.Shown);
            Assert.AreNotEqual("", fit.Second, "two lines");
            int room = cells - 2 * LedWords.SideMargin;
            Assert.LessOrEqual(LedFont.LineWidth(fit.Text), room);
            Assert.LessOrEqual(LedFont.LineWidth(fit.Second), room);
            Assert.GreaterOrEqual(fit.Left, LedWords.SideMargin);
            Assert.GreaterOrEqual(fit.SecondLeft, LedWords.SideMargin);
            Assert.Greater(fit.Baseline, fit.SecondBaseline + LedFont.CapHeight - 1, "the first line sits above the second, not touching");
            Assert.GreaterOrEqual(fit.SecondBaseline - LedWords.DepthBelow(fit.Second), 0, "the second line stays on the panel");
            Assert.LessOrEqual(fit.Baseline + LedFont.CapHeight, 24, "the first line stays on the panel");
            // balanced: moving the split one word either way does not make the wider line narrower
            int widest = System.Math.Max(LedFont.LineWidth(fit.Text), LedFont.LineWidth(fit.Second));
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != ' ') continue;
                int other = System.Math.Max(LedFont.LineWidth(text.Substring(0, i)), LedFont.LineWidth(text.Substring(i + 1)));
                Assert.LessOrEqual(widest, other, "split at " + i);
            }
        }

        [Test]
        public void A_two_line_split_that_still_does_not_fit_drops_letters_at_a_word_boundary_when_it_can()
        {
            LedWords.Fitted fit = LedWords.PlaceWhole("AB CDEFGHIJKLMNOPQRSTUV", 30, 24, 1);
            Assert.IsTrue(fit.Clipped);
            Assert.AreEqual("AB", fit.Text, "cut back to the word boundary");
            Assert.AreEqual("", fit.Second);
        }

        [Test]
        public void No_text_places_nothing()
        {
            Assert.AreEqual(0, LedWords.PlaceWhole("", 120, 24, 3).Scale);
            Assert.AreEqual(0, LedWords.PlaceWhole(null, 120, 24, 3).Scale);
        }
    }
}
