using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The lower third's picture and layout (#141, #151): look C's colours, and the words clear of the tab's edges at any screen size.</summary>
    public class LowerThirdViewTests
    {
        static readonly Vector2[] Screens = { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080), new Vector2(1024, 768) };

        static void AssertColour(Color32 want, Color32 got, string where)
        {
            Assert.AreEqual(want.r, got.r, where + " r");
            Assert.AreEqual(want.g, got.g, where + " g");
            Assert.AreEqual(want.b, got.b, where + " b");
            Assert.AreEqual(255, got.a, where + " opaque");
        }

        [Test]
        public void The_picture_has_a_red_tab_a_navy_bar_a_gold_stripe_and_a_clear_corner()
        {
            Texture2D art = LowerThirdView.MakeArt();
            try
            {
                int top = LowerThirdLayout.Height; // texture rows count from the bottom; picture y counts from the top
                AssertColour(LookC.Red, art.GetPixel(230, top - 80), "tab");
                AssertColour(LookC.Navy, art.GetPixel(760, top - 80), "bar");
                AssertColour(LookC.Gold, art.GetPixel(600, top - 146), "stripe");
                AssertColour(LookC.White, art.GetPixel(600, top - 20), "streak");
                Assert.AreEqual(0, art.GetPixel(1220, top - 80).a, 0.001f, "outside the bar's slanted end");
            }
            finally
            {
                Object.DestroyImmediate(art);
            }
        }

        [Test]
        public void The_kicker_words_stay_inside_the_red_tab_with_a_margin_at_4_screen_sizes()
        {
            // The widest the words can get is TextFit of the kicker's box, centred in it (the view shrinks the font until they fit).
            foreach (Vector2 screen in Screens)
            {
                string at = screen.x + "x" + screen.y;
                float s = LowerThirdLayout.Scale(screen.x);
                Rect bar = LowerThirdLayout.Bar(screen.x, screen.y);
                Rect box = LowerThirdLayout.Kicker(bar, s);
                float textWidth = box.width * LowerThirdLayout.TextFit;
                float textLeft = box.center.x - textWidth / 2f - bar.x, textRight = box.center.x + textWidth / 2f - bar.x; // in the bar's own pixels
                for (float y = LowerThirdLayout.KickerBandY0; y <= LowerThirdLayout.KickerBandY1; y += 2f)
                {
                    float tabLeft = LowerThirdLayout.TabLeftAt(y) * s, tabRight = LowerThirdLayout.TabRightAt(y) * s;
                    float margin = LowerThirdLayout.KickerMargin * s;
                    Assert.GreaterOrEqual(textLeft - tabLeft, margin, at + ": left gap at row " + y);
                    Assert.GreaterOrEqual(tabRight - textRight, margin, at + ": right gap at row " + y);
                }
            }
        }

        [Test]
        public void The_bar_fits_the_screen_and_stays_clear_of_the_result_word_at_4_screen_sizes()
        {
            foreach (Vector2 screen in Screens)
            {
                string at = screen.x + "x" + screen.y;
                Rect bar = LowerThirdLayout.Bar(screen.x, screen.y);
                Assert.GreaterOrEqual(bar.xMin, 0f, at);
                Assert.LessOrEqual(bar.xMax, screen.x, at);
                Assert.GreaterOrEqual(bar.yMin, 0f, at);
                Assert.LessOrEqual(bar.yMax, screen.y, at);
                // The result word and its speed sit at the top: 0.2 h down, two lines of 60 px.
                Assert.Greater(bar.yMin, screen.y * 0.2f + 120f, at + ": under the result word");
            }
        }

        [Test]
        public void The_headline_and_the_speed_line_sit_one_over_the_other_inside_the_navy_bar()
        {
            foreach (Vector2 screen in Screens)
            {
                string at = screen.x + "x" + screen.y;
                float s = LowerThirdLayout.Scale(screen.x);
                Rect bar = LowerThirdLayout.Bar(screen.x, screen.y);
                Rect headline = LowerThirdLayout.Headline(bar, s), speed = LowerThirdLayout.Speed(bar, s);
                Assert.LessOrEqual(headline.yMax, speed.yMin + 0.001f, at + ": the speed line is under the headline");
                Assert.GreaterOrEqual(headline.yMin, bar.yMin + LowerThirdLayout.TextY0 * s - 0.001f, at);
                Assert.LessOrEqual(speed.yMax, bar.yMin + LowerThirdLayout.TextY1 * s + 0.001f, at + ": inside the bar's rows");
                Assert.AreEqual(headline.x, speed.x, 0.001f, at);
                Assert.AreEqual(headline.width, speed.width, 0.001f, at);
            }
        }
    }
}
