namespace HockeyGame.Core
{
    /// <summary>Where the goalie is set for one Shot when the shooting window opens (see GLOSSARY: Set-up).</summary>
    public enum SetUp
    {
        /// <summary>On the line from the puck to the net centre, at his normal depth, settled in time.</summary>
        Clean
    }

    /// <summary>
    /// The goalie's Set-up spot: the one place that says where he stands for a Shot. The race (the goalie-reach port) and the
    /// goalie's motion both read it, so what he shows and what decides the Shot agree.
    /// </summary>
    public static class SetUps
    {
        /// <summary>
        /// Where the goalie stands (on the ice, Y = 0) in this Set-up, secondsSinceWindowOpened into the shooting window, for a
        /// puck at shotStart. Clean: on the angle (OnTheAngle).
        /// </summary>
        public static RinkPoint Spot(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, RoundSettings settings) =>
            OnTheAngle(shotStart, settings);

        /// <summary>
        /// Playing the angle: on the line (on the ice) from the puck to the net centre, NormalDepthMetres out from the Net Face.
        /// A Clean Set-up's spot.
        /// </summary>
        public static RinkPoint OnTheAngle(RinkPoint puck, RoundSettings settings)
        {
            double z = -settings.NormalDepthMetres;
            double x = puck.Z < -1e-6 ? puck.X * (z / puck.Z) : 0;
            return new RinkPoint(x, 0, z);
        }

        /// <summary>
        /// Where the goalie is headed on the approach to a Shot, phaseSeconds (real) into the phase: through the skate-in he plays
        /// the angle to the puck (OnTheAngle); through the slow-down he settles from there into his Set-up (eased, set as the
        /// window opens); from the window on he is on his Set-up spot (Spot), the spot the goalie-reach port measures.
        /// </summary>
        public static RinkPoint Approach(SetUp setUp, AttackSide side, RoundPhase phase, double phaseSeconds, RoundSettings settings)
        {
            RinkPoint puck = Breakaway.Puck(side, phase, phaseSeconds, settings);
            switch (phase)
            {
                case RoundPhase.SkateIn:
                    return OnTheAngle(puck, settings);
                case RoundPhase.SlowDown:
                    RinkPoint angle = OnTheAngle(puck, settings), set = Spot(setUp, side, 0, puck, settings);
                    double t = Numbers.Clamp01(phaseSeconds / settings.SlowDownSeconds);
                    t = t * t * (3 - 2 * t);
                    return new RinkPoint(angle.X + (set.X - angle.X) * t, 0, angle.Z + (set.Z - angle.Z) * t);
                default:
                    return Spot(setUp, side, phaseSeconds, puck, settings);
            }
        }

        /// <summary>The facing (GoalieTurn) that squares the goalie, standing at spot, up to the puck at shotStart.</summary>
        public static double Facing(RinkPoint spot, RinkPoint shotStart) => GoalieTurn.Facing(shotStart.X - spot.X, spot.Z - shotStart.Z);
    }
}
