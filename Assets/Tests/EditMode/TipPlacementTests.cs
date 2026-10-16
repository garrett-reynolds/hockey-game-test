using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class TipPlacementTests
    {
        const double PageW = 1920, PageH = 1080, BoxW = 600, BoxH = 90;

        static void AssertInsideAndClear(TipPlacement.Box box, double rx, double ry, double rw, double rh)
        {
            Assert.GreaterOrEqual(box.X, 0, "left edge");
            Assert.GreaterOrEqual(box.Y, 0, "top edge");
            Assert.LessOrEqual(box.X + BoxW, PageW, "right edge");
            Assert.LessOrEqual(box.Y + BoxH, PageH, "bottom edge");
            bool apart = box.X >= rx + rw || box.X + BoxW <= rx || box.Y >= ry + rh || box.Y + BoxH <= ry;
            Assert.IsTrue(apart, "the box must not cover the row");
        }

        [Test]
        public void A_top_row_keeps_the_box_inside_the_screen()
        {
            var box = TipPlacement.Place(100, 4, 800, 60, PageW, PageH, BoxW, BoxH);
            AssertInsideAndClear(box, 100, 4, 800, 60);
        }

        [Test]
        public void A_bottom_row_keeps_the_box_inside_the_screen()
        {
            var box = TipPlacement.Place(100, 1060, 800, 60, PageW, PageH, BoxW, BoxH);
            AssertInsideAndClear(box, 100, 1060, 800, 60);
        }

        [Test]
        public void A_wide_right_column_row_keeps_the_box_inside_and_off_the_row()
        {
            var box = TipPlacement.Place(1000, 1000, 880, 60, PageW, PageH, BoxW, BoxH);
            AssertInsideAndClear(box, 1000, 1000, 880, 60);
        }

        [Test]
        public void A_left_column_row_puts_the_box_beside_it()
        {
            var box = TipPlacement.Place(60, 400, 800, 60, PageW, PageH, BoxW, BoxH);
            Assert.GreaterOrEqual(box.X, 860);
            AssertInsideAndClear(box, 60, 400, 800, 60);
        }
    }
}
