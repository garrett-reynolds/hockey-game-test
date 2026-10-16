using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    // #202: the tip box sits under the hovered row, left edge lined up with the row, inside the row's column.
    // With no room under the row (the bottom bar or the screen edge is in the way) it sits above the row.
    public class TipPlacementTests
    {
        const double PageW = 1920, PageH = 1080, BoxW = 600, BoxH = 90, BarTop = 980;

        static TipPlacement.Box Place(double rx, double ry, double rw, double rh)
            => TipPlacement.Place(rx, ry, rw, rh, PageW, PageH, BoxW, BoxH, BarTop);

        static void AssertInsideColumnAndClear(TipPlacement.Box box, double rx, double ry, double rw, double rh)
        {
            Assert.GreaterOrEqual(box.X, rx, "left edge: not left of the row's column");
            Assert.LessOrEqual(box.X + BoxW, rx + rw, "right edge: not into the other column");
            Assert.GreaterOrEqual(box.Y, 0, "top edge");
            Assert.LessOrEqual(box.Y + BoxH, BarTop, "bottom edge: not over the bottom bar");
            bool apart = box.Y >= ry + rh || box.Y + BoxH <= ry;
            Assert.IsTrue(apart, "the box must not cover the row");
        }

        [Test]
        public void A_top_left_row_puts_the_box_under_the_row_lined_up_with_its_left_edge()
        {
            var box = Place(60, 100, 800, 60);
            Assert.AreEqual(60, box.X, 0.01, "left edge lines up with the row");
            Assert.GreaterOrEqual(box.Y, 160, "under the row");
            Assert.LessOrEqual(box.Y, 160 + TipPlacement.Gap + 0.01, "close under the row");
            AssertInsideColumnAndClear(box, 60, 100, 800, 60);
        }

        [Test]
        public void A_right_column_row_puts_the_box_under_the_row_inside_its_column()
        {
            var box = Place(1000, 300, 880, 60);
            Assert.AreEqual(1000, box.X, 0.01, "left edge lines up with the row");
            Assert.GreaterOrEqual(box.Y, 360, "under the row");
            AssertInsideColumnAndClear(box, 1000, 300, 880, 60);
        }

        [Test]
        public void A_left_column_row_never_puts_the_box_over_the_right_column()
        {
            var box = Place(60, 400, 800, 60);
            Assert.LessOrEqual(box.X + BoxW, 60 + 800, "stays inside the left column");
        }

        [Test]
        public void A_row_near_the_bottom_bar_puts_the_box_above_the_row()
        {
            var box = Place(60, 900, 800, 60);
            Assert.AreEqual(60, box.X, 0.01, "left edge lines up with the row");
            Assert.LessOrEqual(box.Y + BoxH, 900, "above the row");
            AssertInsideColumnAndClear(box, 60, 900, 800, 60);
        }

        [Test]
        public void A_row_whose_box_would_cross_the_bottom_bar_goes_above()
        {
            var box = Place(60, 850, 800, 60);
            Assert.LessOrEqual(box.Y + BoxH, 850, "above the row");
            AssertInsideColumnAndClear(box, 60, 850, 800, 60);
        }
    }
}
