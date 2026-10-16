namespace HockeyGame.Core
{
    /// <summary>Where the Settings tip box goes (#202): under the hovered row, left edge lined up with the row; above the row when there is no room under it.</summary>
    public static class TipPlacement
    {
        public struct Box
        {
            public double X, Y;
        }

        public const double Gap = 8, Margin = 12;

        /// <summary>Top-left of the tip box. Under the row, else above it. bottomY is the lowest y the box may reach (the top of the bottom bar).</summary>
        public static Box Place(double rowX, double rowY, double rowW, double rowH,
            double pageW, double pageH, double boxW, double boxH, double bottomY = double.MaxValue)
        {
            double x = Clamp(rowX, Margin, pageW - Margin - boxW);
            double limit = System.Math.Min(pageH, bottomY) - Margin;
            double below = rowY + rowH + Gap;
            if (below + boxH <= limit) return new Box { X = x, Y = below };
            return new Box { X = x, Y = Clamp(rowY - Gap - boxH, Margin, limit - boxH) };
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);
    }
}
