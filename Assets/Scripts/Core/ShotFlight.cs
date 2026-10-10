using System;

namespace HockeyGame.Core
{
    /// <summary>How long the on-screen puck takes to fly to the Net Face. The Round does not use this.</summary>
    public static class ShotFlight
    {
        /// <summary>
        /// One frame at 60 fps: only guards a zero distance (a 0 s flight would divide by zero). Never reached in play: the
        /// closest start, about 2 m out, takes 0.036 s even at 200 km/h.
        /// </summary>
        public const double MinSeconds = 1.0 / 60.0;

        /// <summary>Speeds below this (including 0 from bad hardware data) fly as if at this speed.</summary>
        public const double MinSpeedKmh = 10.0;

        /// <summary>Flight time = distance / speed (#46: no floor flattens fast Shots), at least one frame.</summary>
        public static double Seconds(double distanceMetres, double speedKmh)
        {
            double metresPerSecond = Math.Max(speedKmh, MinSpeedKmh) / 3.6;
            return Math.Max(MinSeconds, distanceMetres / metresPerSecond);
        }
    }
}
