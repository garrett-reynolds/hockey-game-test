namespace HockeyGame.Core
{
    /// <summary>The 9 equal Zones of the Net Face: 3 columns x 3 rows, counted from the bottom-left, shooter's view.</summary>
    public static class Zones
    {
        public const int Columns = 3;
        public const int Rows = 3;

        /// <summary>Zone column 0 (left) to 2 (right) for a Net Face x. A Shot exactly on a line goes to the higher Zone.</summary>
        public static int Column(double x) => Index(x);

        /// <summary>Zone row 0 (bottom) to 2 (top) for a Net Face y. A Shot exactly on a line goes to the higher Zone.</summary>
        public static int Row(double y) => Index(y);

        private static int Index(double value)
        {
            if (value < 100.0 / 3) return 0;
            if (value < 200.0 / 3) return 1;
            return 2;
        }
    }
}
