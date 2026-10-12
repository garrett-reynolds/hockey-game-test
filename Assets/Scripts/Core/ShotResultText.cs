using System;
using System.Globalization;

namespace HockeyGame.Core
{
    /// <summary>Text shown after a Shot. Never shows the race (Reach Time, Flight Time) or a score chance.</summary>
    public static class ShotResultText
    {
        /// <summary>The Shot speed, e.g. "92 km/h", for a Shot with a puck; null for Too Late.</summary>
        public static string Speed(ShotResult result)
        {
            return result.HasShot ? Speed(result.SpeedKmh) : null;
        }

        /// <summary>A Shot speed to the nearest km/h, e.g. "92 km/h".</summary>
        public static string Speed(double speedKmh)
        {
            long kmh = (long)Math.Round(speedKmh, MidpointRounding.AwayFromZero);
            return kmh.ToString(CultureInfo.InvariantCulture) + " km/h";
        }
    }
}
