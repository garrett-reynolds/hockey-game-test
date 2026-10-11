namespace HockeyGame.Core
{
    /// <summary>Tunable values the Round core needs. Defaults are the spec values (the goalie's: starting numbers, tuned by feel).</summary>
    public sealed class RoundSettings
    {
        // The Goalie Pose's Contested chance: no longer decides a Shot (Reach in time does); goes with the Goalie Pose.
        public double ContestedLowChance = 0.50;
        public double ContestedTopChance = 0.85;
        public double TopSpeedKmh = 120;

        // Phase lengths, in real seconds.
        public double SkateInSeconds = 2.5;
        public double SlowDownSeconds = 1.2;
        public double ShootingWindowSeconds = 3.0;
        public double ResultHoldSeconds = 2.5;

        // Reach in time: the goalie's Reaction Time is ReactionSeconds, plus or minus up to ReactionWobbleSeconds at random per Shot.
        public double ReactionSeconds = 0.12;
        public double ReactionWobbleSeconds = 0.04;

        // Gear speeds (m/s): hands (glove, blocker), legs (pads, stick), and a push across with the whole body, up to MaxPushMetres.
        public double HandMetresPerSecond = 8;
        public double LegMetresPerSecond = 6;
        public double PushMetresPerSecond = 4;
        public double MaxPushMetres = 0.6;

        /// <summary>How far out from the Net Face (goal line) the goalie sets in a Clean Set-up (metres).</summary>
        public double NormalDepthMetres = 0.6;

        /// <summary>A Goal he missed by less than this (Late Margin, seconds) gets a Stick Slap; otherwise a Slump.</summary>
        public double LateMarginLimitSeconds = 0.05;
    }
}
