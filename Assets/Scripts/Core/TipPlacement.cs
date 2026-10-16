namespace HockeyGame.Core
{
    /// <summary>Where the Settings tip box goes (#199): beside the hovered row, inside the page, never over the row.</summary>
    public static class TipPlacement
    {
        public struct Box
        {
            public double X, Y;
        }

        public const double Gap = 20, Margin = 12;

        /// <summary>Top-left of the tip box. Tries the right of the row, then under it, then over it.</summary>
        public static Box Place(double rowX, double rowY, double rowW, double rowH,
            double pageW, double pageH, double boxW, double boxH)
        {
            double top = Clamp(rowY, Margin, pageH - Margin - boxH);
            double right = rowX + rowW + Gap;
            if (right + boxW <= pageW - Margin) return new Box { X = right, Y = top };

            // No room beside the row: under it, else over it, lined up with the row's left edge.
            double x = Clamp(rowX, Margin, pageW - Margin - boxW);
            double below = rowY + rowH + Gap;
            if (below + boxH <= pageH - Margin) return new Box { X = x, Y = below };
            return new Box { X = x, Y = Clamp(rowY - Gap - boxH, Margin, pageH - Margin - boxH) };
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);
    }
}
