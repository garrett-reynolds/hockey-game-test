using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// The goalie-reach port: how the goalie's body, set in a Set-up, gets gear into the puck's line. Round asks it once per
    /// Shot on the Net Face, and for the Gaps Easy mode glows. The real port measures the goalie character (Game layer); tests
    /// use a fake with fixed answers.
    /// </summary>
    public interface IGoalieReach
    {
        /// <summary>
        /// For a puck flying from shotStart to netPoint (on the Net Face), with the goalie in this Set-up for this Attack Side,
        /// secondsSinceWindowOpened into the shooting window: is his gear already in the puck's line, which gear reaches it
        /// quickest, and how long that gear's move takes (from the end of the Reaction Time).
        /// </summary>
        GoalieReachAnswer Reach(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, RinkPoint netPoint);

        /// <summary>
        /// The Gaps a puck from shotStart can fit through, with the goalie in this Set-up for this Attack Side,
        /// secondsSinceWindowOpened into the shooting window: each Net Face cell whose centre Reach would find no gear already
        /// in the way of (for Easy mode; the same body and spot as Reach, so the glow and the race agree). Only the run of cells
        /// from firstCell (cellCount long, NetFaceGaps.Index order) is measured; the rest come back as not Gaps.
        /// </summary>
        NetFaceGaps Gaps(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, int firstCell, int cellCount);
    }

    /// <summary>The goalie-reach port's answer for one Shot.</summary>
    public readonly struct GoalieReachAnswer
    {
        /// <summary>His gear is already in the puck's line at the Shot: the Reach Time is 0.</summary>
        public bool InTheWay { get; }

        /// <summary>The gear that reaches the puck's line quickest, or null when none can.</summary>
        public SaveReaction? QuickestGear { get; }

        /// <summary>Seconds the quickest gear takes to move into the puck's line (0 in the way, infinity when none can).</summary>
        public double MoveSeconds { get; }

        private GoalieReachAnswer(bool inTheWay, SaveReaction? quickestGear, double moveSeconds)
        {
            InTheWay = inTheWay;
            QuickestGear = quickestGear;
            MoveSeconds = moveSeconds;
        }

        /// <summary>Gear already in the puck's line; quickestGear: the gear that would win a race, if one would.</summary>
        public static GoalieReachAnswer AlreadyInTheWay(SaveReaction? quickestGear = null) => new GoalieReachAnswer(true, quickestGear, 0);

        /// <summary>No gear in the way: this gear reaches the puck's line quickest, in moveSeconds.</summary>
        public static GoalieReachAnswer Reaches(SaveReaction gear, double moveSeconds) => new GoalieReachAnswer(false, gear, Math.Max(0, moveSeconds));

        /// <summary>No gear in the way, and none can reach the puck's line.</summary>
        public static GoalieReachAnswer NoGearReaches => new GoalieReachAnswer(false, null, double.PositiveInfinity);

        public override string ToString() => InTheWay ? "in the way" : QuickestGear.HasValue ? $"{QuickestGear} in {MoveSeconds:0.000} s" : "no gear reaches";
    }

    /// <summary>How fast the goalie's gear moves (RoundSettings): hands fastest, legs slower, a push across with the whole body slowest.</summary>
    public static class GearSpeeds
    {
        /// <summary>The speed (m/s) of the gear a Save Reaction moves: glove and blocker are hands, pads and stick legs, the body a push.</summary>
        public static double MetresPerSecond(SaveReaction gear, RoundSettings settings)
        {
            switch (gear)
            {
                case SaveReaction.Glove:
                case SaveReaction.Blocker: return settings.HandMetresPerSecond;
                case SaveReaction.Pads:
                case SaveReaction.Stick: return settings.LegMetresPerSecond;
                default: return settings.PushMetresPerSecond;
            }
        }

        /// <summary>
        /// Seconds a gear move takes: the push across (pushMetres) at the push speed, then the rest of the gear's travel
        /// (gearMetres, measured from the Ready Stance, the push included) at the gear's own speed. Infinity past the push limit.
        /// </summary>
        public static double MoveSeconds(SaveReaction gear, double gearMetres, double pushMetres, RoundSettings settings)
        {
            pushMetres = Math.Abs(pushMetres);
            if (pushMetres > settings.MaxPushMetres + 1e-6) return double.PositiveInfinity;
            return pushMetres / settings.PushMetresPerSecond + Math.Max(0, gearMetres - pushMetres) / MetresPerSecond(gear, settings);
        }
    }
}
