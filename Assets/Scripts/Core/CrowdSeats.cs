using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>One fan's seat: the floor under the fan's hips (on the row's tread), the way the fan faces (Yaw: radians
    /// round y from +z, so the fan faces (sin Yaw, 0, cos Yaw)), the row (0 = front) and the Arena Show zone.</summary>
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
    /// Where the 3D crowd's fans sit, from the Rink Layout's stands numbers (Tools/make_arena.py builds the stands to the
    /// same numbers): one fan per seat on each row's tread, near the back of it, facing the rink.
    /// </summary>
    public static class CrowdSeats
    {
        /// <summary>Seat width along a row (metres).</summary>
        public const double SeatWidth = 0.5;
        /// <summary>How far in front of the next row's riser a fan's hips sit (metres): the seat is at the back of the tread.</summary>
        public const double HipsFromRiser = 0.2;
        /// <summary>Seats in each lower row behind the near net: the end straight, between the corner arcs' centres.</summary>
        public const int SeatsPerRowBehindNearNet = (int)(2 * RinkLayout.CornerX / SeatWidth);

        /// <summary>The Arena Show zone a fan at z sits in: behind the near end boards is the Calm Area (as for the Ribbon
        /// Boards), then the rest of the near half, then the far half.</summary>
        public static ArenaZone ZoneAt(double z) =>
            z > RinkLayout.NearEndBoardsZ ? ArenaZone.CalmArea : z > RinkLayout.CentreLineZ ? ArenaZone.Near : ArenaZone.Far;

        /// <summary>Every seat of the lower stand behind the near net (the end straight), front row first.</summary>
        public static FanSeat[] BehindNearNet()
        {
            var seats = new List<FanSeat>();
            for (int row = 0; row < RinkLayout.LowerRows; row++)
            {
                double z = RinkLayout.NearEndBoardsZ + RinkLayout.StandsFront + (row + 1) * RinkLayout.RowDepth - HipsFromRiser;
                double y = RinkLayout.StandsFrontHeight + row * RinkLayout.LowerRise;
                for (int k = 0; k < SeatsPerRowBehindNearNet; k++)
                {
                    double x = (k - (SeatsPerRowBehindNearNet - 1) / 2.0) * SeatWidth;
                    seats.Add(new FanSeat(x, y, z, Math.PI, row, ZoneAt(z)));
                }
            }
            return seats.ToArray();
        }
    }
}
