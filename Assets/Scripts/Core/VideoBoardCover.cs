using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>
    /// What the Video Board hides. It hangs in front of the near end's crowd and Ribbon Board, so a fan or Ribbon Board panel
    /// that is behind it from every eye the player has (the skate-in and the shooting window, and the Replay's look-up, which
    /// is the same eye turned up) is never seen: the game leaves it out. A fan or panel only partly hidden stays.
    /// </summary>
    public static class VideoBoardCover
    {
        /// <summary>A fan's body above the seat's floor and its half width (metres), for the fan's box.</summary>
        public const double FanHeight = 1.3, FanHalfWidth = 0.25;
        /// <summary>How far behind the Video Board's frame the nearest fan or seat may be (metres): a small clear gap.</summary>
        public const double ClearGap = 0.3;

        // The board's unit normal (toward the player) and its up along the face.
        static readonly double Tilt = RinkLayout.VideoBoardTiltDegrees * Math.PI / 180;
        static readonly double NY = -Math.Sin(Tilt), NZ = -Math.Cos(Tilt), UY = Math.Cos(Tilt), UZ = -Math.Sin(Tilt);

        /// <summary>The point in the board's own frame: right along x, up along the face, and distance in front of the face (negative behind it).</summary>
        public static void ToBoard(double x, double y, double z, out double right, out double up, out double front)
        {
            double dy = y - RinkLayout.VideoBoard.Y, dz = z - RinkLayout.VideoBoard.Z;
            right = x - RinkLayout.VideoBoard.X;
            up = dy * UY + dz * UZ;
            front = dy * NY + dz * NZ;
        }

        /// <summary>Whether the point is inside the frame's outline seen straight on and not more than behind metres behind the
        /// face (in front of the face counts): the space the board fills, or stands in front of, that nothing may share.</summary>
        public static bool InSlab(double x, double y, double z, double behind)
        {
            ToBoard(x, y, z, out double r, out double u, out double f);
            return Math.Abs(r) <= RinkLayout.VideoBoardWidth / 2 + RinkLayout.VideoBoardFrameMargin
                && Math.Abs(u) <= RinkLayout.VideoBoardHeight / 2 + RinkLayout.VideoBoardFrameMargin
                && f > -behind;
        }

        /// <summary>The eyes the player has: every Attack Side, along the skate-in and through the shooting window, with the stride bob and a little sway.</summary>
        public static readonly (double X, double Y, double Z)[] Eyes = MakeEyes();

        static (double, double, double)[] MakeEyes()
        {
            var eyes = new List<(double, double, double)>();
            var settings = new RoundSettings();
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            {
                var grounds = new List<(double X, double Z)>();
                for (int i = 0; i <= 10; i++)
                {
                    Breakaway.SkatePoint(side, i / 10.0, out double x, out double d);
                    grounds.Add((x, -d));
                }
                Breakaway.SkatePoint(side, 1.0, out double sx, out double sd);
                for (double w = 0; w <= settings.ShootingWindowSeconds + 1e-9; w += 0.25)
                {
                    double drift = Breakaway.Drift(side, settings.SlowDownSeconds, w, settings);
                    double toNet = Math.Sqrt(sx * sx + sd * sd), along = Breakaway.CappedDrift(drift, toNet);
                    grounds.Add((sx - sx / toNet * along, -sd + sd / toNet * along));
                }
                foreach ((double X, double Z) g in grounds)
                    foreach (double dy in new[] { -0.1, 0, 0.1 })
                        foreach (double dx in new[] { -0.25, 0, 0.25 })
                            eyes.Add((g.X + dx, Breakaway.EyeHeight + dy, g.Z));
            }
            return eyes.ToArray();
        }

        // Whether the board's face covers the point from the eye: the point is behind the face and the line from the eye to it
        // crosses the face inside the frame's outline.
        static bool Covers((double X, double Y, double Z) eye, double x, double y, double z)
        {
            ToBoard(eye.X, eye.Y, eye.Z, out double er, out double eu, out double ef);
            ToBoard(x, y, z, out double pr, out double pu, out double pf);
            if (ef <= 0 || pf >= 0) return false;
            double t = ef / (ef - pf);
            double r = er + (pr - er) * t, u = eu + (pu - eu) * t;
            return Math.Abs(r) <= RinkLayout.VideoBoardWidth / 2 + RinkLayout.VideoBoardFrameMargin
                && Math.Abs(u) <= RinkLayout.VideoBoardHeight / 2 + RinkLayout.VideoBoardFrameMargin;
        }

        /// <summary>Whether every corner of the box is covered from every eye.</summary>
        public static bool HidesBox(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            foreach (var eye in Eyes)
                for (int i = 0; i < 8; i++)
                    if (!Covers(eye, (i & 1) == 0 ? x0 : x1, (i & 2) == 0 ? y0 : y1, (i & 4) == 0 ? z0 : z1)) return false;
            return true;
        }

        /// <summary>Whether the fan is wholly hidden behind the Video Board (the game leaves such a fan out).</summary>
        public static bool HidesFan(FanSeat seat) =>
            HidesBox(seat.X - FanHalfWidth, seat.Y, seat.Z - FanHalfWidth, seat.X + FanHalfWidth, seat.Y + FanHeight, seat.Z + FanHalfWidth);

        /// <summary>Whether a Ribbon Board panel from (x0, z0) to (x1, z1), RinkLayout.RibbonHeight tall from RinkLayout.RibbonBottom, is wholly hidden.</summary>
        public static bool HidesRibbonPanel(double x0, double z0, double x1, double z1) =>
            HidesBox(Math.Min(x0, x1), RinkLayout.RibbonBottom, Math.Min(z0, z1), Math.Max(x0, x1), RinkLayout.RibbonBottom + RinkLayout.RibbonHeight, Math.Max(z0, z1));
    }
}
