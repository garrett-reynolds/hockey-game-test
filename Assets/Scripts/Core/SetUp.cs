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
        /// puck at shotStart. Clean: on the line from the puck to the net centre, NormalDepthMetres out from the Net Face.
        /// </summary>
        public static RinkPoint Spot(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, RoundSettings settings)
        {
            double z = -settings.NormalDepthMetres;
            double x = shotStart.Z < -1e-6 ? shotStart.X * (z / shotStart.Z) : 0;
            return new RinkPoint(x, 0, z);
        }

        /// <summary>The facing (GoalieTurn) that squares the goalie, standing at spot, up to the puck at shotStart.</summary>
        public static double Facing(RinkPoint spot, RinkPoint shotStart) => GoalieTurn.Facing(shotStart.X - spot.X, spot.Z - shotStart.Z);
    }
}
