using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// The reserved colours of the Calm Area rules: never used behind the near net from goalie set to the Shot, so the
    /// goalie, the Easy mode glow, the puck and the posts stay easy to read. Values as the game draws them: the goalie's
    /// blue (GoalieView) and its white and grey gear, the Easy mode glow green (EasyGlow), puck black (PhysicsPuck) and the
    /// red posts (NetBuilder).
    /// </summary>
    public static class ReservedColours
    {
        public static readonly ArenaColour GoalieBlue = new ArenaColour(38, 64, 153);
        public static readonly ArenaColour GoalieWhite = new ArenaColour(230, 230, 230);
        public static readonly ArenaColour GoalieGrey = new ArenaColour(128, 128, 128);
        public static readonly ArenaColour GlowGreen = new ArenaColour(77, 255, 77);
        public static readonly ArenaColour PuckBlack = new ArenaColour(13, 13, 13);
        public static readonly ArenaColour PostRed = new ArenaColour(255, 0, 0);

        public static readonly ArenaColour[] All = { GoalieBlue, GoalieWhite, GoalieGrey, GlowGreen, PuckBlack, PostRed };

        /// <summary>The least distance (Distance) a Calm Area colour keeps from every reserved colour.</summary>
        public const double MinDistance = 40;

        /// <summary>True when colour keeps at least MinDistance from every reserved colour, so it may show in the Calm Area.</summary>
        public static bool IsSafe(ArenaColour colour)
        {
            foreach (ArenaColour reserved in All)
                if (Distance(colour, reserved) < MinDistance) return false;
            return true;
        }

        /// <summary>Straight-line distance between two colours in 0..255 RGB.</summary>
        public static double Distance(ArenaColour a, ArenaColour b)
        {
            double r = a.R - b.R, g = a.G - b.G, bl = a.B - b.B;
            return Math.Sqrt(r * r + g * g + bl * bl);
        }
    }
}
