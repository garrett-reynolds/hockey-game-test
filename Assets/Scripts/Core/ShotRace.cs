namespace HockeyGame.Core
{
    /// <summary>
    /// The race that decided one Shot on the Net Face (Reach in time): a Save when the Reach Time is no longer than the
    /// Flight Time, otherwise a Goal. Published by Round for the view.
    /// </summary>
    public readonly struct ShotRace
    {
        /// <summary>
        /// The race for a Shot at Net Face point (x, y), secondsSinceWindowOpened into the shooting window, with this Reaction
        /// Time. The puck starts where the shooter has skated to (Breakaway.ShotStart); its Flight Time runs to where it passes
        /// the goalie, at the depth of his Set-up spot (SetUps.Spot). The goalie-reach port is asked once. Rolls nothing: Round
        /// passes the Shot's rolled Reaction Time, the Goal-rate tool the nominal one (RoundSettings.ReactionSeconds).
        /// </summary>
        public static ShotRace At(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, double x, double y, double speedKmh,
            double reactionSeconds, IGoalieReach reach, RoundSettings settings)
        {
            RinkPoint from = Breakaway.ShotStart(side, secondsSinceWindowOpened, settings);
            RinkPoint netPoint = RinkPoint.OnNetFace(x, y);
            RinkPoint spot = SetUps.Spot(setUp, side, secondsSinceWindowOpened, from, settings);
            double flight = ShotFlight.Seconds(RinkPoint.Distance(from, RinkPoint.AtDepth(from, netPoint, spot.Z)), speedKmh);

            GoalieReachAnswer answer = reach.Reach(setUp, side, secondsSinceWindowOpened, from, netPoint);
            double move = answer.InTheWay ? 0 : answer.MoveSeconds;
            double reachSeconds = answer.InTheWay ? 0 : reactionSeconds + move;
            SaveReaction shown = answer.InTheWay || !answer.QuickestGear.HasValue ? SaveReactions.For(x, y) : answer.QuickestGear.Value;
            return new ShotRace(setUp, side, reactionSeconds, move, reachSeconds, flight, answer.InTheWay, shown, from, spot);
        }

        public SetUp SetUp { get; }
        public AttackSide AttackSide { get; }
        /// <summary>How long the goalie waited after the Shot before he started to move (seconds).</summary>
        public double ReactionSeconds { get; }
        /// <summary>How long his quickest gear took to move into the puck's line (0 in the way, infinity when none can).</summary>
        public double MoveSeconds { get; }
        /// <summary>Reaction Time plus the move; 0 when his gear was already in the puck's line.</summary>
        public double ReachSeconds { get; }
        /// <summary>How long the puck took from the Shot to where it passes the goalie (seconds).</summary>
        public double FlightSeconds { get; }
        /// <summary>His gear was already in the puck's line at the Shot.</summary>
        public bool InTheWay { get; }
        /// <summary>The Save Reaction to show: the Shot point's when his gear was in the way, else the quickest gear in the race.</summary>
        public SaveReaction SaveReaction { get; }
        /// <summary>Where the puck started (Breakaway.ShotStart).</summary>
        public RinkPoint ShotStart { get; }
        /// <summary>The goalie's Set-up spot at the Shot (SetUps.Spot): where the race measured him, so where he reacts from.</summary>
        public RinkPoint Spot { get; }

        /// <summary>How late he was: Reach Time minus Flight Time (seconds; 0 or less on a Save).</summary>
        public double LateMarginSeconds => ReachSeconds - FlightSeconds;

        /// <summary>Reach in time: a Save when the Reach Time is no longer than the Flight Time.</summary>
        public bool IsSave => ReachSeconds <= FlightSeconds;

        public ShotRace(SetUp setUp, AttackSide attackSide, double reactionSeconds, double moveSeconds, double reachSeconds,
            double flightSeconds, bool inTheWay, SaveReaction saveReaction, RinkPoint shotStart, RinkPoint spot)
        {
            SetUp = setUp;
            AttackSide = attackSide;
            ReactionSeconds = reactionSeconds;
            MoveSeconds = moveSeconds;
            ReachSeconds = reachSeconds;
            FlightSeconds = flightSeconds;
            InTheWay = inTheWay;
            SaveReaction = saveReaction;
            ShotStart = shotStart;
            Spot = spot;
        }
    }
}
