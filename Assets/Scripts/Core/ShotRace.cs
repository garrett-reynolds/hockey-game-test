namespace HockeyGame.Core
{
    /// <summary>
    /// The race that decided one Shot on the Net Face (Reach in time): a Save when the Reach Time is no longer than the
    /// Flight Time, otherwise a Goal. Published by Round for the view.
    /// </summary>
    public readonly struct ShotRace
    {
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

        /// <summary>How late he was: Reach Time minus Flight Time (seconds; 0 or less on a Save).</summary>
        public double LateMarginSeconds => ReachSeconds - FlightSeconds;

        /// <summary>Reach in time: a Save when the Reach Time is no longer than the Flight Time.</summary>
        public bool IsSave => ReachSeconds <= FlightSeconds;

        public ShotRace(SetUp setUp, AttackSide attackSide, double reactionSeconds, double moveSeconds, double reachSeconds,
            double flightSeconds, bool inTheWay, SaveReaction saveReaction)
        {
            SetUp = setUp;
            AttackSide = attackSide;
            ReactionSeconds = reactionSeconds;
            MoveSeconds = moveSeconds;
            ReachSeconds = reachSeconds;
            FlightSeconds = flightSeconds;
            InTheWay = inTheWay;
            SaveReaction = saveReaction;
        }
    }
}
