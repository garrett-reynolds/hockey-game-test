using System;

namespace HockeyGame.Core
{
    /// <summary>The kind of a Set-up: a Clean Set-up or one of the three Set-up Mistakes (see GLOSSARY: Set-up Mistake).</summary>
    public enum SetUpKind
    {
        /// <summary>On the line from the puck to the net centre, at his normal depth, settled in time.</summary>
        Clean,

        /// <summary>Set off the angle line toward one post (CheatMetres), opening the other side. Holds the whole window.</summary>
        Cheat,

        /// <summary>Set too deep, back toward the goal line (DepthMetres), showing more net high and in the corners. Holds the whole window.</summary>
        Depth,

        /// <summary>Still sliding across when the window opens: LateSettleMetres short of his spot, closing over LateSettleSeconds.</summary>
        LateSettle
    }

    /// <summary>A post of the net, from the shooter's view (left is -X on the rink).</summary>
    public enum Post
    {
        Left,
        Right
    }

    /// <summary>
    /// Where the goalie is set for one Shot when the shooting window opens (see GLOSSARY: Set-up): its kind, and for a Cheat
    /// or a Late Settle the post he is off toward (a Cheat sets toward it; a Late Settle is still coming across from it).
    /// </summary>
    public readonly struct SetUp : IEquatable<SetUp>
    {
        /// <summary>A Clean Set-up or the Set-up Mistake it has.</summary>
        public SetUpKind Kind { get; }

        /// <summary>The post a Cheat or a Late Settle is off toward; null for a Clean Set-up and a Depth.</summary>
        public Post? Toward { get; }

        private SetUp(SetUpKind kind, Post? toward)
        {
            Kind = kind;
            Toward = toward;
        }

        public static SetUp Clean => new SetUp(SetUpKind.Clean, null);
        public static SetUp Depth => new SetUp(SetUpKind.Depth, null);
        public static SetUp Cheat(Post toward) => new SetUp(SetUpKind.Cheat, toward);
        public static SetUp LateSettle(Post toward) => new SetUp(SetUpKind.LateSettle, toward);

        /// <summary>The Set-up of this kind; the post is kept only where the kind uses one (Cheat, Late Settle).</summary>
        public static SetUp Of(SetUpKind kind, Post post) =>
            kind == SetUpKind.Cheat || kind == SetUpKind.LateSettle ? new SetUp(kind, post) : new SetUp(kind, null);

        /// <summary>True for a Cheat, a Depth or a Late Settle.</summary>
        public bool IsMistake => Kind != SetUpKind.Clean;

        public bool Equals(SetUp other) => Kind == other.Kind && Toward == other.Toward;
        public override bool Equals(object obj) => obj is SetUp other && Equals(other);
        public override int GetHashCode() => (int)Kind * 3 + (Toward.HasValue ? 1 + (int)Toward.Value : 0);
        public static bool operator ==(SetUp a, SetUp b) => a.Equals(b);
        public static bool operator !=(SetUp a, SetUp b) => !a.Equals(b);

        public override string ToString()
        {
            string name = Kind == SetUpKind.LateSettle ? "Late Settle" : Kind.ToString();
            return Toward.HasValue ? $"{name} ({Toward.Value.ToString().ToLowerInvariant()} post)" : name;
        }
    }

    /// <summary>
    /// The goalie's Set-up spot: the one place that says where he stands for a Shot. The race (the goalie-reach port) and the
    /// goalie's motion both read it, so what he shows and what decides the Shot agree.
    /// </summary>
    public static class SetUps
    {
        /// <summary>
        /// Where the goalie stands (on the ice, Y = 0) in this Set-up, secondsSinceWindowOpened into the shooting window, for a
        /// puck at shotStart. Clean: on the angle (OnTheAngle). Depth: on the angle, DepthMetres deeper (back toward the goal
        /// line). Cheat: CheatMetres across from the angle toward its post. Late Settle: LateSettleMetres across toward its post
        /// as the window opens, closing linearly onto the angle over LateSettleSeconds. Mistakes mirror left and right: the
        /// angle mirrors with the puck, and the post side is the Set-up's own.
        /// </summary>
        public static RinkPoint Spot(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, RoundSettings settings)
        {
            double depth = settings.NormalDepthMetres - (setUp.Kind == SetUpKind.Depth ? settings.DepthMetres : 0);
            RinkPoint angle = OnTheAngle(shotStart, depth);
            double across = 0;
            if (setUp.Kind == SetUpKind.Cheat)
                across = settings.CheatMetres;
            else if (setUp.Kind == SetUpKind.LateSettle)
                across = settings.LateSettleMetres * (1 - Numbers.Clamp01(secondsSinceWindowOpened / Math.Max(1e-6, settings.LateSettleSeconds)));
            if (setUp.Toward == Post.Left) across = -across;
            return new RinkPoint(angle.X + across, 0, angle.Z);
        }

        /// <summary>
        /// Playing the angle: on the line (on the ice) from the puck to the net centre, NormalDepthMetres out from the Net Face.
        /// A Clean Set-up's spot.
        /// </summary>
        public static RinkPoint OnTheAngle(RinkPoint puck, RoundSettings settings) => OnTheAngle(puck, settings.NormalDepthMetres);

        private static RinkPoint OnTheAngle(RinkPoint puck, double depthMetres)
        {
            double z = -depthMetres;
            double x = puck.Z < -1e-6 ? puck.X * (z / puck.Z) : 0;
            return new RinkPoint(x, 0, z);
        }

        /// <summary>
        /// Where the goalie is headed on the approach to a Shot, phaseSeconds (real) into the phase: through the skate-in he plays
        /// the angle to the puck (OnTheAngle); through the slow-down he settles from there into his Set-up (eased, set as the
        /// window opens; a Late Settle only as far as its short spot); from the window on he is on his Set-up spot (Spot), the
        /// spot the goalie-reach port measures (a Late Settle slides on to the angle there).
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
