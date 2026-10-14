using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The lower third's layout (look A): a thin full-width bar at the very bottom, its parts in order, at any screen size.</summary>
    public class LowerThirdViewTests
    {
        static readonly Vector2[] Screens = { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080), new Vector2(1024, 768) };

        [Test]
        public void At_720p_the_bar_is_40_px_high_and_full_width_at_the_very_bottom()
        {
            Rect bar = LowerThirdLayout.Bar(1280, 720);
            Assert.AreEqual(0f, bar.x, 0.001f);
            Assert.AreEqual(1280f, bar.width, 0.001f);
            Assert.AreEqual(40f, bar.height, 0.001f);
            Assert.AreEqual(720f, bar.yMax, 0.001f);
        }

        [Test]
        public void The_bar_and_its_stripe_stay_in_the_bottom_6_percent_clear_of_the_result_word_at_4_screen_sizes()
        {
            foreach (Vector2 screen in Screens)
            {
                string at = screen.x + "x" + screen.y;
                Rect bar = LowerThirdLayout.Bar(screen.x, screen.y), stripe = LowerThirdLayout.Stripe(screen.x, screen.y);
                Assert.AreEqual(0f, bar.xMin, 0.001f, at);
                Assert.AreEqual(screen.x, bar.xMax, 0.001f, at);
                Assert.AreEqual(screen.y, bar.yMax, 0.001f, at + ": at the very bottom");
                Assert.AreEqual(bar.yMin, stripe.yMax, 0.001f, at + ": the gold stripe sits on top of the bar");
                Assert.Greater(stripe.yMin, screen.y * 0.94f, at + ": low enough to keep off the goal and the Video Board");
                // The result word and its speed sit at the top: 0.2 h down, two lines of 60 px.
                Assert.Greater(stripe.yMin, screen.y * 0.2f + 120f, at + ": under the result word");
            }
        }

        [Test]
        public void Tab_tally_stat_and_cap_sit_left_to_right_inside_the_bar_without_overlap_at_4_screen_sizes()
        {
            foreach (Vector2 screen in Screens)
            {
                string at = screen.x + "x" + screen.y;
                float w = screen.x, h = screen.y;
                Rect bar = LowerThirdLayout.Bar(w, h);
                Rect[] parts = { LowerThirdLayout.Tab(w, h), LowerThirdLayout.Tally(w, h), LowerThirdLayout.Stat(w, h), LowerThirdLayout.Cap(w, h) };
                for (int i = 0; i < parts.Length; i++)
                {
                    Assert.GreaterOrEqual(parts[i].xMin, bar.xMin - 0.001f, at + " part " + i);
                    Assert.LessOrEqual(parts[i].xMax, bar.xMax + 0.001f, at + " part " + i);
                    Assert.AreEqual(bar.yMin, parts[i].yMin, 0.001f, at + " part " + i);
                    Assert.AreEqual(bar.height, parts[i].height, 0.001f, at + " part " + i);
                    if (i > 0) Assert.LessOrEqual(parts[i - 1].xMax, parts[i].xMin + 0.001f, at + ": part " + (i - 1) + " before part " + i);
                }
                Assert.AreEqual(bar.xMax, parts[3].xMax, 0.001f, at + ": the cap ends the bar");
                Assert.GreaterOrEqual(parts[2].width, 300f * LowerThirdLayout.Scale(w, h), at + ": room for the stat");
            }
        }
    }
}
