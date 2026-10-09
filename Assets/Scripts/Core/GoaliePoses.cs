using System;

namespace HockeyGame.Core
{
    /// <summary>Which side of the Net Face a Goalie Pose attacks from.</summary>
    public enum AttackSide
    {
        Left,
        Middle,
        Right
    }

    /// <summary>How a Zone is covered by a Goalie Pose.</summary>
    public enum ZoneCover
    {
        Open,
        Contested,
        Covered
    }

    /// <summary>The 6 Goalie Poses, in table order (pose 1 then pose 2 of each Attack Side).</summary>
    public enum GoaliePose
    {
        LeftPostHug,
        LeftButterfly,
        MiddleButterfly,
        MiddleStandUp,
        RightPostHug,
        RightButterfly
    }

    /// <summary>Pure lookups for Goalie Poses: Attack Side, cover per Zone, and the Contested chance.</summary>
    public static class GoaliePoses
    {
        public const int PosesPerSide = 2;

        // O = Open, C = Contested, X = Covered. Rows are written TOP ROW FIRST, shooter's view.
        // Only LEFT and MIDDLE are stored; RIGHT poses are the mirror of the LEFT poses.
        private static readonly string[][] LeftAndMiddleRows =
        {
            new[] { "XCO", "XXC", "XXX" }, // LeftPostHug
            new[] { "XOO", "XCC", "XXX" }, // LeftButterfly
            new[] { "OXO", "CXC", "XXX" }, // MiddleButterfly
            new[] { "XXX", "XXX", "COC" }, // MiddleStandUp
        };

        // [pose, column, row]; row 0 = bottom.
        private static readonly ZoneCover[,,] Table = BuildTable();

        public static AttackSide SideOf(GoaliePose pose) => (AttackSide)((int)pose / PosesPerSide);

        /// <summary>The 2 poses of an Attack Side, pose 1 first.</summary>
        public static GoaliePose[] PosesOf(AttackSide side)
        {
            int first = (int)side * PosesPerSide;
            return new[] { (GoaliePose)first, (GoaliePose)(first + 1) };
        }

        /// <summary>Cover of a Zone for a pose. Column 0 = left, row 0 = bottom.</summary>
        public static ZoneCover CoverAt(GoaliePose pose, int column, int row)
        {
            if (column < 0 || column >= Zones.Columns) throw new ArgumentOutOfRangeException(nameof(column));
            if (row < 0 || row >= Zones.Rows) throw new ArgumentOutOfRangeException(nameof(row));
            return Table[(int)pose, column, row];
        }

        /// <summary>Chance of a Goal for a Shot in a Contested Zone: low at 0 km/h up to top at top speed, linear, speed clamped.</summary>
        public static double ContestedChance(double speedKmh, RoundSettings settings)
        {
            double fraction = settings.TopSpeedKmh <= 0
                ? 1.0
                : Math.Min(Math.Max(speedKmh, 0.0), settings.TopSpeedKmh) / settings.TopSpeedKmh;
            return settings.ContestedLowChance + (settings.ContestedTopChance - settings.ContestedLowChance) * fraction;
        }

        private static ZoneCover[,,] BuildTable()
        {
            var table = new ZoneCover[6, Zones.Columns, Zones.Rows];
            for (int p = 0; p < LeftAndMiddleRows.Length; p++)
            {
                string[] rows = LeftAndMiddleRows[p];
                for (int column = 0; column < Zones.Columns; column++)
                {
                    for (int row = 0; row < Zones.Rows; row++)
                    {
                        ZoneCover cover = Parse(rows[Zones.Rows - 1 - row][column]);
                        table[p, column, row] = cover;
                        if (p < PosesPerSide) // LEFT poses also fill the RIGHT poses, mirrored.
                            table[(int)AttackSide.Right * PosesPerSide + p, Zones.Columns - 1 - column, row] = cover;
                    }
                }
            }
            return table;
        }

        private static ZoneCover Parse(char c)
        {
            switch (c)
            {
                case 'O': return ZoneCover.Open;
                case 'C': return ZoneCover.Contested;
                case 'X': return ZoneCover.Covered;
                default: throw new System.ArgumentException("Unknown pose table letter: " + c);
            }
        }
    }
}
