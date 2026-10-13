using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>One fan's seat: the floor under the fan's hips (on the row's tread), the way the fan faces (Yaw: radians
    /// round y from +z, so the fan faces (sin Yaw, 0, cos Yaw)), the row (0 = the lower tier's front row; the upper tier's
    /// rows follow on from RinkLayout.LowerRows) and the Arena Show zone.</summary>
    public readonly struct FanSeat
    {
        public readonly double X, Y, Z, Yaw;
        public readonly int Row;
        public readonly ArenaZone Zone;

        public FanSeat(double x, double y, double z, double yaw, int row, ArenaZone zone)
        {
            X = x; Y = y; Z = z; Yaw = yaw; Row = row; Zone = zone;
        }

        public override string ToString() => $"seat row {Row} at ({X:0.##}, {Y:0.##}, {Z:0.##}), {Zone}";
    }

    /// <summary>
    /// Where the 3D crowd's fans sit: every seat of both tiers all round the bowl, from the Rink Layout's stands numbers
    /// (Tools/make_arena.py builds the stands to the same numbers). One fan per seat on each row's tread, near the back
    /// of it, facing straight in. Beside a bench or penalty box the stands step back (RinkLayout.BenchDepth), and so do
    /// the fans; nobody sits on the step itself.
    /// </summary>
    public static class CrowdSeats
    {
        /// <summary>Least seat width along a row (metres): each straight run or corner holds as many as fit.</summary>
        public const double SeatWidth = 0.5;
        /// <summary>How far in front of the next row's riser a fan's hips sit (metres): the seat is at the back of the tread.</summary>
        public const double HipsFromRiser = 0.2;
        /// <summary>The stands step back over this length at each end of a bench or penalty box (make_arena.py's STEP).</summary>
        public const double OpeningStep = 0.25;
        /// <summary>Fans this close across the floor (metres), in the same or the next row, are neighbours.</summary>
        public const double NeighbourReach = 0.98;
        /// <summary>How many of the lower tier's top rows have no fans (#165; the seats stay as empty steps): a 1.3 m fan in a row 3 or fewer
        /// rows under the top reaches the Ribbon Board's bottom edge.</summary>
        public const int LowerRowsLeftEmpty = 3;

        /// <summary>The Arena Show zone a fan at z sits in: behind the near end boards is the Calm Area (as for the Ribbon
        /// Boards), then the rest of the near half, then the far half.</summary>
        public static ArenaZone ZoneAt(double z) =>
            z > RinkLayout.NearEndBoardsZ ? ArenaZone.CalmArea : z > RinkLayout.CentreLineZ ? ArenaZone.Near : ArenaZone.Far;

        /// <summary>Near fans (the lower tier in the near half) get the full body; the upper tier and the far half are
        /// far fans (the lighter body, or flat cards).</summary>
        public static bool IsNearFan(FanSeat seat) => seat.Row < RinkLayout.LowerRows && seat.Z > RinkLayout.CentreLineZ;

        /// <summary>Every seat a player can see (the fans wholly behind the Video Board are left out; AllIncludingHidden has every
        /// seat), row by row from the lower tier's front row to the upper tier's back row; along each row from behind the near
        /// net's left end round past +x, the far end and -x, so seats next in the list sit side by side. Worked out once.</summary>
        public static FanSeat[] All() => _visible ??= Array.FindAll(AllIncludingHidden(), s => !VideoBoardCover.HidesFan(s));
        static FanSeat[] _visible;

        /// <summary>As All, with the fans the Video Board hides (VideoBoardCover) too.</summary>
        public static FanSeat[] AllIncludingHidden()
        {
            var seats = new List<FanSeat>();
            for (int row = 0; row < RinkLayout.LowerRows + RinkLayout.UpperRows; row++)
            {
                bool upper = row >= RinkLayout.LowerRows;
                int r = upper ? row - RinkLayout.LowerRows : row;
                double offset = (upper ? RinkLayout.RibbonOffset : RinkLayout.StandsFront) + (r + 1) * RinkLayout.RowDepth - HipsFromRiser;
                double y = upper ? RinkLayout.RibbonBottom + RinkLayout.RibbonHeight + r * RinkLayout.UpperRise
                                 : RinkLayout.StandsFrontHeight + r * RinkLayout.LowerRise;
                if (!upper && r >= RinkLayout.LowerRows - LowerRowsLeftEmpty) continue; // empty steps under the Ribbon, no fans
                Row(seats, row, offset, y);
                // No fans in the upper tier right behind the Video Board and its banners (the seats stay, dark and empty).
                if (upper) seats.RemoveAll(s => s.Row == row && s.Z > RinkLayout.NearEndBoardsZ && Math.Abs(s.X) < RinkLayout.UpperCutOutHalfWidth);
            }
            return seats.ToArray();
        }

        static void Row(List<FanSeat> seats, int row, double o, double y)
        {
            double cx = RinkLayout.CornerX, zn = RinkLayout.NearCornerZ, zf = RinkLayout.FarCornerZ;
            double hw = RinkLayout.HalfWidth, radius = RinkLayout.CornerRadius + o;
            Line(seats, row, y, -cx, RinkLayout.NearEndBoardsZ + o, cx, RinkLayout.NearEndBoardsZ + o, 0, -1);
            Arc(seats, row, y, cx, zn, radius, Math.PI / 2, 0);
            Side(seats, row, y, +1, hw + o, zn, zf);
            Arc(seats, row, y, cx, zf, radius, 2 * Math.PI, 1.5 * Math.PI);
            Line(seats, row, y, cx, RinkLayout.FarEndBoardsZ - o, -cx, RinkLayout.FarEndBoardsZ - o, 0, 1);
            Arc(seats, row, y, -cx, zf, radius, 1.5 * Math.PI, Math.PI);
            Side(seats, row, y, -1, -(hw + o), zf, zn);
            Arc(seats, row, y, -cx, zn, radius, Math.PI, Math.PI / 2);
        }

        /// <summary>A side straight from z0 to z1 at x, split round the openings on that side: the stands beside an opening
        /// are BenchDepth further back, and no seat is on the step at either end of it.</summary>
        static void Side(List<FanSeat> seats, int row, double y, int side, double x, double z0, double z1)
        {
            var openings = new List<RinkLayout.Opening>();
            foreach (RinkLayout.Opening op in RinkLayout.Benches) if (op.Side == side) openings.Add(op);
            foreach (RinkLayout.Opening op in RinkLayout.PenaltyBoxes) if (op.Side == side) openings.Add(op);
            openings.Sort((a, b) => a.FromZ.CompareTo(b.FromZ));
            var runs = new List<(double from, double to, bool back)>();
            double at = Math.Min(z0, z1);
            foreach (RinkLayout.Opening op in openings)
            {
                runs.Add((at, op.FromZ - OpeningStep, false));
                runs.Add((op.FromZ, op.ToZ, true));
                at = op.ToZ + OpeningStep;
            }
            runs.Add((at, Math.Max(z0, z1), false));
            bool down = z1 < z0;
            if (down) runs.Reverse();
            foreach ((double from, double to, bool back) in runs)
            {
                double sx = back ? x + side * RinkLayout.BenchDepth : x;
                if (down) Line(seats, row, y, sx, to, sx, from, -side, 0);
                else Line(seats, row, y, sx, from, sx, to, -side, 0);
            }
        }

        /// <summary>As many seats as fit, evenly spread and centred, on the straight from (x0, z0) to (x1, z1), facing (ix, iz).</summary>
        static void Line(List<FanSeat> seats, int row, double y, double x0, double z0, double x1, double z1, double ix, double iz)
        {
            double length = Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            int n = (int)Math.Floor(length / SeatWidth + 1e-9);
            double yaw = Math.Atan2(ix, iz);
            for (int k = 0; k < n; k++)
            {
                double t = (k + 0.5) / n;
                double x = x0 + (x1 - x0) * t, z = z0 + (z1 - z0) * t;
                seats.Add(new FanSeat(x, y, z, yaw, row, ZoneAt(z)));
            }
        }

        /// <summary>As many seats as fit on the corner arc round (cx, cz) from angle a0 to a1 (angle 0 = +x, pi/2 = +z).</summary>
        static void Arc(List<FanSeat> seats, int row, double y, double cx, double cz, double radius, double a0, double a1)
        {
            int n = (int)Math.Floor(Math.Abs(a1 - a0) * radius / SeatWidth + 1e-9);
            for (int k = 0; k < n; k++)
            {
                double a = a0 + (a1 - a0) * (k + 0.5) / n;
                double x = cx + radius * Math.Cos(a), z = cz + radius * Math.Sin(a);
                seats.Add(new FanSeat(x, y, z, Math.Atan2(-Math.Cos(a), -Math.Sin(a)), row, ZoneAt(z)));
            }
        }

        /// <summary>Each seat's neighbours (indices into seats): the fans within NeighbourReach across the floor in the same
        /// row or the rows in front and behind.</summary>
        public static int[][] Neighbours(FanSeat[] seats)
        {
            var cells = new Dictionary<(int, int), List<int>>();
            (int, int) Cell(FanSeat s) => ((int)Math.Floor(s.X / NeighbourReach), (int)Math.Floor(s.Z / NeighbourReach));
            for (int i = 0; i < seats.Length; i++)
            {
                if (!cells.TryGetValue(Cell(seats[i]), out List<int> list)) cells[Cell(seats[i])] = list = new List<int>();
                list.Add(i);
            }
            var result = new int[seats.Length][];
            var mine = new List<int>();
            for (int i = 0; i < seats.Length; i++)
            {
                mine.Clear();
                FanSeat s = seats[i];
                (int cx, int cz) = Cell(s);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!cells.TryGetValue((cx + dx, cz + dz), out List<int> list)) continue;
                        foreach (int j in list)
                        {
                            if (j == i || Math.Abs(seats[j].Row - s.Row) > 1) continue;
                            double ex = seats[j].X - s.X, ez = seats[j].Z - s.Z;
                            if (ex * ex + ez * ez <= NeighbourReach * NeighbourReach) mine.Add(j);
                        }
                    }
                result[i] = mine.ToArray();
            }
            return result;
        }
    }
}
