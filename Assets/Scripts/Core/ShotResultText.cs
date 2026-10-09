using System;
using System.Globalization;

namespace HockeyGame.Core
{
    /// <summary>Text shown after a Shot. Never shows the Zone or the score chance.</summary>
    public static class ShotResultText
    {
        /// <summary>The Shot speed, e.g. "92 km/h", for a Shot with a puck; null for Too Late.</summary>
        public static string Speed(ShotResult result)
        {
            if (!result.HasShot) return null;
            long kmh = (long)Math.Round(result.SpeedKmh, MidpointRounding.AwayFromZero);
            return kmh.ToString(CultureInfo.InvariantCulture) + " km/h";
        }
    }
}
