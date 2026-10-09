namespace HockeyGame.Core
{
    /// <summary>Tunable values the Round core needs. Defaults are the spec values.</summary>
    public sealed class RoundSettings
    {
        public double ContestedLowChance = 0.50;
        public double ContestedTopChance = 0.85;
        public double TopSpeedKmh = 120;

        // Phase lengths, in real seconds.
        public double SkateInSeconds = 2.5;
        public double SlowDownSeconds = 1.2;
        public double ShootingWindowSeconds = 3.0;
        public double ResultHoldSeconds = 2.5;
    }
}
