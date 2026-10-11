using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// A point on the rink in metres, in the scene's frame: X right of the Net Face centre (shooter's view), Y up from the ice,
    /// Z toward the net (the Net Face is at Z = 0 and the shooter stands at -Z).
    /// </summary>
    public readonly struct RinkPoint
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public RinkPoint(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>The point on the Net Face (Z = 0) at Net Face coordinates x, y (0-100 from the bottom-left, shooter's view).</summary>
        public static RinkPoint OnNetFace(double x, double y) =>
            new RinkPoint((x / 100.0 - 0.5) * NetFrame.FaceWidthMetres, y / 100.0 * NetFrame.FaceHeightMetres, 0);

        public static double Distance(RinkPoint a, RinkPoint b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>The point on the line from `from` to `to` at depth z, kept between the two ends.</summary>
        public static RinkPoint AtDepth(RinkPoint from, RinkPoint to, double z)
        {
            double t = Math.Abs(to.Z - from.Z) < 1e-9 ? 1 : Numbers.Clamp01((z - from.Z) / (to.Z - from.Z));
            return new RinkPoint(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t, from.Z + (to.Z - from.Z) * t);
        }

        public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
    }
}
