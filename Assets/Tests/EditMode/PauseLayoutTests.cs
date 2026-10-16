using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class PauseLayoutTests
    {
        static readonly Vector2[] Sizes = { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(800, 600), new Vector2(640, 360) };

        [Test]
        public void The_box_is_centred_and_the_buttons_sit_inside_it_without_touching()
        {
            foreach (var size in Sizes)
            {
                Rect box = PauseLayout.Box(size.x, size.y);
                Assert.AreEqual(size.x / 2f, box.center.x, 0.01f, "centred across at " + size);
                Assert.AreEqual(size.y / 2f, box.center.y, 0.01f, "centred down at " + size);
                Rect a = PauseLayout.Button(size.x, size.y, 0), b = PauseLayout.Button(size.x, size.y, 1);
                Assert.IsTrue(box.Contains(a.min) && box.Contains(a.max - Vector2.one), "Resume inside at " + size);
                Assert.IsTrue(box.Contains(b.min) && box.Contains(b.max - Vector2.one), "Quit inside at " + size);
                Assert.Less(a.yMax, b.y, "the buttons do not touch at " + size);
                Assert.GreaterOrEqual(a.y, PauseLayout.Header(size.x, size.y).yMax, "buttons under the header at " + size);
            }
        }

        [Test]
        public void The_box_hides_neither_behind_the_Score_Box_nor_the_lower_third()
        {
            foreach (var size in Sizes)
            {
                Rect box = PauseLayout.Box(size.x, size.y);
                Rect score = HockeyGame.RoundDriver.ScoreBoxArea(size.x);
                Assert.IsFalse(box.Overlaps(score), "Score Box at " + size);
                Assert.IsFalse(box.Overlaps(LowerThirdLayout.Bar(size.x, size.y)), "lower third at " + size);
            }
        }

        [Test]
        public void A_point_finds_its_button()
        {
            Rect a = PauseLayout.Button(1280, 720, 0), b = PauseLayout.Button(1280, 720, 1);
            Assert.AreEqual(0, PauseLayout.ButtonAt(1280, 720, a.center));
            Assert.AreEqual(1, PauseLayout.ButtonAt(1280, 720, b.center));
            Assert.AreEqual(-1, PauseLayout.ButtonAt(1280, 720, new Vector2(2, 2)));
        }
    }
}
