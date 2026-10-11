using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// The Gaps a puck can fit through, for one Set-up at one moment: the Net Face cut into cells about a puck across, each a
    /// Gap when a puck shot at its centre meets none of the goalie's gear already in the way (its edges count, so the puck
    /// fits). The goalie-reach port answers them (IGoalieReach.Gaps) from the same body the race measures; Easy mode glows them.
    /// Cells are numbered row by row from the bottom-left (Index), so a run of cells can be measured on its own.
    /// </summary>
    public sealed class NetFaceGaps
    {
        /// <summary>Cells across and up the Net Face: 1.83 m / 12 and 1.22 m / 8 are both about 0.15 m, a puck across.</summary>
        public const int Columns = 12, Rows = 8;

        /// <summary>How many cells the Net Face has.</summary>
        public const int Cells = Columns * Rows;

        private readonly bool[] _isGap;

        /// <summary>isGap: one per cell, at Index(column, row).</summary>
        public NetFaceGaps(bool[] isGap)
        {
            if (isGap == null || isGap.Length != Cells)
                throw new ArgumentException("one value per cell: " + Cells, nameof(isGap));
            _isGap = (bool[])isGap.Clone();
            foreach (bool gap in _isGap) if (gap) Count++;
        }

        /// <summary>
        /// A Gap at each cell, of the run of cells from firstCell (cellCount long), whose centre (Net Face x, y, 0-100) isGap
        /// says; cells outside the run are not Gaps. By default the whole Net Face.
        /// </summary>
        public static NetFaceGaps Where(Func<double, double, bool> isGap, int firstCell = 0, int cellCount = Cells)
        {
            var cells = new bool[Cells];
            for (int cell = Math.Max(0, firstCell); cell < Math.Min(Cells, firstCell + cellCount); cell++)
            {
                CellCentre(cell % Columns, cell / Columns, out double x, out double y);
                cells[cell] = isGap(x, y);
            }
            return new NetFaceGaps(cells);
        }

        /// <summary>No Gaps.</summary>
        public static readonly NetFaceGaps None = new NetFaceGaps(new bool[Cells]);

        /// <summary>How many cells are Gaps.</summary>
        public int Count { get; }

        /// <summary>Column 0 (left) to Columns - 1, row 0 (bottom) to Rows - 1, shooter's view.</summary>
        public bool IsGap(int column, int row) => _isGap[Index(column, row)];

        /// <summary>These Gaps with the run of cells from firstCell (cellCount long) taken from measured instead.</summary>
        public NetFaceGaps With(NetFaceGaps measured, int firstCell, int cellCount)
        {
            var cells = (bool[])_isGap.Clone();
            for (int cell = Math.Max(0, firstCell); cell < Math.Min(Cells, firstCell + cellCount); cell++)
                cells[cell] = measured._isGap[cell];
            return new NetFaceGaps(cells);
        }

        /// <summary>A cell's number: row by row from the bottom-left.</summary>
        public static int Index(int column, int row) => row * Columns + column;

        /// <summary>A cell's centre in Net Face coordinates (0-100 from the bottom-left).</summary>
        public static void CellCentre(int column, int row, out double x, out double y)
        {
            x = (column + 0.5) * 100.0 / Columns;
            y = (row + 0.5) * 100.0 / Rows;
        }

        /// <summary>A cell's corners in Net Face coordinates (0-100 from the bottom-left).</summary>
        public static void Cell(int column, int row, out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = column * 100.0 / Columns;
            maxX = (column + 1) * 100.0 / Columns;
            minY = row * 100.0 / Rows;
            maxY = (row + 1) * 100.0 / Rows;
        }
    }
}
