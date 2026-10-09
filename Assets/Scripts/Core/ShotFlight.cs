using System;

namespace HockeyGame.Core
{
    /// <summary>How long the on-screen puck takes to fly to the Net Face. The Round does not use this.</summary>
    public static class ShotFlight
    {
        public const double MinSeconds = 0.3;

        /// <summary>Speeds below this (including 0 from bad hardware data) fly as if at this speed.</summary>
        public const double MinSpeedKmh = 10.0;

        /// <summary>Flight time = distance / speed, never less than MinSeconds.</summary>
        public static double Seconds(double distanceMetres, double speedKmh)
        {
            double metresPerSecond = Math.Max(speedKmh, MinSpeedKmh) / 3.6;
            return Math.Max(MinSeconds, distanceMetres / metresPerSecond);
        }
    }
}
