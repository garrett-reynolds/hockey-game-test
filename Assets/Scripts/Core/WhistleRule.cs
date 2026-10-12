namespace HockeyGame.Core
{
    /// <summary>What the puck is doing on one physics step of a Shot, as plain facts (no Unity types).</summary>
    public readonly struct PuckFacts
    {
        /// <summary>Seconds since the Shot.</summary>
        public double SecondsSinceShot { get; }
        /// <summary>The whole puck is past the goal line, inside the net.</summary>
        public bool WholePuckInNet { get; }
        /// <summary>The puck has touched the goalie's gear, at any time this Shot.</summary>
        public bool GearTouched { get; }
        /// <summary>Puck speed (m/s).</summary>
        public double SpeedMetresPerSecond { get; }
        /// <summary>The puck is gone: behind the goal line outside the net, or out of the camera's view.</summary>
        public bool Gone { get; }

        public PuckFacts(double secondsSinceShot, bool wholePuckInNet, bool gearTouched, double speedMetresPerSecond, bool gone)
        {
            SecondsSinceShot = secondsSinceShot;
            WholePuckInNet = wholePuckInNet;
            GearTouched = gearTouched;
            SpeedMetresPerSecond = speedMetresPerSecond;
            Gone = gone;
        }
    }

    /// <summary>
    /// When the Whistle goes for a Shot, and its result. Pure logic: the game gives it the puck's facts each step.
    /// <para>
    /// A Goal at once, on the first step the whole puck is past the goal line inside the net. Otherwise the first of: the puck
    /// dead (very slow for WhistleDeadSeconds in a row), gone, or the cap (WhistleCapSeconds after the Shot). Then a Save if
    /// the puck touched the goalie's gear at any time, else a Miss (a post alone is a Miss). Once the Whistle has gone,
    /// nothing changes the result. One rule per Shot.
    /// </para>
    /// </summary>
    public sealed class WhistleRule
    {
        private readonly RoundSettings _settings;
        private double _slowSince = -1;

        public WhistleRule(RoundSettings settings)
        {
            _settings = settings;
        }

        /// <summary>True once the Whistle has gone.</summary>
        public bool HasWhistled { get; private set; }

        /// <summary>The Shot's result: Goal, Save or Miss once the Whistle has gone; null before it.</summary>
        public ShotResultKind? Result { get; private set; }

        /// <summary>Takes one step's facts. Returns true if the Whistle has gone (on this step or before).</summary>
        public bool Step(PuckFacts facts)
        {
            if (HasWhistled)
                return true;

            if (facts.WholePuckInNet)
                return Blow(ShotResultKind.Goal);

            if (facts.SpeedMetresPerSecond < _settings.WhistleVerySlowMetresPerSecond)
            {
                if (_slowSince < 0) _slowSince = facts.SecondsSinceShot;
            }
            else
            {
                _slowSince = -1;
            }

            bool dead = _slowSince >= 0 && facts.SecondsSinceShot - _slowSince >= _settings.WhistleDeadSeconds;
            bool capped = facts.SecondsSinceShot >= _settings.WhistleCapSeconds;
            if (dead || facts.Gone || capped)
                return Blow(facts.GearTouched ? ShotResultKind.Save : ShotResultKind.Miss);
            return false;
        }

        private bool Blow(ShotResultKind kind)
        {
            HasWhistled = true;
            Result = kind;
            return true;
        }
    }
}
