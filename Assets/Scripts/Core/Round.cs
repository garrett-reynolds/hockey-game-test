using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>
    /// A Round of 6 Shots. Pure logic: time only enters through Tick(realSeconds).
    /// <para>
    /// A Shot on the Net Face is decided by Reach in time: a Save when the goalie's Reach Time is no longer than the puck's
    /// Flight Time, otherwise a Goal (see ShotRace). The goalie-reach port says how his body, in the Shot's Set-up, gets gear
    /// into the puck's line. A Shot off the Net Face (a Frame Hit too) is a Miss: the goalie does not decide it.
    /// </para>
    /// <para>
    /// Random call order (public contract): when a Shot starts, per pose pick try, Next() for the Attack Side
    /// (side = (int)(Next() * 3)), then Next() for the pose (index = (int)(Next() * 2)). A try that repeats the
    /// previous Shot's pose is retried, up to 10 tries; if all 10 repeat it, the next pose in GoaliePose enum
    /// order (wrapping) is used. Then one Next() for the Reaction Time wobble: ReactionSeconds + ReactionWobbleSeconds *
    /// (2 * Next() - 1). Nothing else rolls: not the Shot's result, a Miss or TooLate.
    /// </para>
    /// </summary>
    public sealed class Round
    {
        public const int ShotsPerRound = 6;

        private readonly IRandomSource _random;
        private readonly RoundSettings _settings;
        private readonly IGoalieReach _reach;
        private readonly List<ShotResultKind> _results = new List<ShotResultKind>();
        private const int MaxPickTries = 10;
        private double _phaseElapsed;
        private double _resultDelay;
        private GoaliePose? _previousPose;

        public Round(IRandomSource random, RoundSettings settings, IGoalieReach reach, int bestScore)
        {
            _random = random;
            _settings = settings;
            _reach = reach;
            BestScore = bestScore;
            Phase = RoundPhase.Idle;
        }

        /// <summary>The tunable values this Round runs on (phase lengths, the goalie's Reaction Time and gear speeds).</summary>
        public RoundSettings Settings => _settings;

        public RoundPhase Phase { get; private set; }

        /// <summary>Current Shot number, 1 to 6. 0 before the Round starts.</summary>
        public int ShotNumber { get; private set; }

        /// <summary>Real seconds left in the shooting window; 0 outside the window.</summary>
        public double WindowSecondsLeft =>
            Phase == RoundPhase.ShootingWindow ? _settings.ShootingWindowSeconds - _phaseElapsed : 0;

        /// <summary>Real seconds spent in the current phase.</summary>
        public double PhaseSecondsElapsed => _phaseElapsed;

        /// <summary>Result of the current Shot; null until it has one.</summary>
        public ShotResult? LastResult { get; private set; }

        /// <summary>Result per finished Shot, in order (TooLate counts as a Miss in the tally).</summary>
        public IReadOnlyList<ShotResultKind> Results => _results;

        public int Goals { get; private set; }

        /// <summary>True only at RoundOver, when Goals beat the current BestScore (which updates when beaten, so a reused Round compares each round against the latest best).</summary>
        public bool IsNewBest { get; private set; }

        /// <summary>Best score so far; changes only when beaten at RoundOver.</summary>
        public int BestScore { get; private set; }

        /// <summary>Attack Side of the current Shot (from its Goalie Pose until that goes).</summary>
        public AttackSide AttackSide => GoaliePoses.SideOf(GoaliePose);

        /// <summary>Goalie Pose for the current Shot; chosen when the Shot starts. It no longer decides the Shot's result.</summary>
        public GoaliePose GoaliePose { get; private set; }

        /// <summary>The goalie's Set-up for the current Shot.</summary>
        public SetUp SetUp { get; private set; }

        /// <summary>The goalie's Reaction Time for the current Shot (seconds), rolled when the Shot starts.</summary>
        public double ReactionSeconds { get; private set; }

        /// <summary>The race that decided the current Shot (Reach in time); null until a Shot on the Net Face.</summary>
        public ShotRace? LastRace { get; private set; }

        /// <summary>
        /// False while a delayed result is still hidden (see DelayResult); true otherwise. The scene shows the
        /// result text and plays the result sound only once this is true.
        /// </summary>
        public bool ResultShowing => Phase != RoundPhase.ResultHold || _phaseElapsed >= _resultDelay;

        /// <summary>Real seconds the current result is held back (see DelayResult): the on-screen puck flight, 0 if none.</summary>
        public double ResultDelaySeconds => _resultDelay;

        /// <summary>
        /// Holds back the current result for this many real seconds (the on-screen puck flight), then holds it
        /// for the normal ResultHoldSeconds. Only in ResultHold; later calls replace the delay.
        /// </summary>
        public void DelayResult(double seconds)
        {
            if (Phase == RoundPhase.ResultHold)
                _resultDelay = Math.Max(0, seconds);
        }

        /// <summary>Starts a Round against the given best score (for example after a reset on the settings screen).</summary>
        public void StartRound(int bestScore)
        {
            BestScore = bestScore;
            StartRound();
        }

        public void StartRound()
        {
            _previousPose = null;
            _results.Clear();
            Goals = 0;
            IsNewBest = false;
            LastResult = null;
            ShotNumber = 1;
            StartShot();
            EnterPhase(RoundPhase.SkateIn);
        }

        /// <summary>Submit a Shot. Counts only as the first one in the shooting window.</summary>
        public void SubmitShot(double x, double y, double speedKmh, bool isTestShot)
        {
            if (Phase != RoundPhase.ShootingWindow)
                return;

            bool onNetFace = x >= 0 && x <= 100 && y >= 0 && y <= 100;
            ShotResultKind kind = ShotResultKind.Miss;
            if (onNetFace)
            {
                ShotRace race = Race(x, y, speedKmh);
                LastRace = race;
                kind = race.IsSave ? ShotResultKind.Save : ShotResultKind.Goal;
            }
            Finish(new ShotResult(kind, true, x, y, speedKmh, isTestShot));
        }

        /// <summary>Advance by real seconds. Large steps carry across phase boundaries.</summary>
        public void Tick(double realSeconds)
        {
            double remaining = realSeconds;
            while (remaining > 0 && Phase != RoundPhase.Idle && Phase != RoundPhase.RoundOver)
            {
                double left = PhaseLength(Phase) - _phaseElapsed;
                if (remaining < left)
                {
                    _phaseElapsed += remaining;
                    return;
                }
                remaining -= left;
                Advance();
            }
        }

        private void Advance()
        {
            switch (Phase)
            {
                case RoundPhase.SkateIn:
                    EnterPhase(RoundPhase.SlowDown);
                    break;
                case RoundPhase.SlowDown:
                    EnterPhase(RoundPhase.ShootingWindow);
                    break;
                case RoundPhase.ShootingWindow:
                    Finish(new ShotResult(ShotResultKind.TooLate, false, 0, 0, 0, false));
                    break;
                case RoundPhase.ResultHold:
                    if (ShotNumber >= ShotsPerRound)
                    {
                        EndRound();
                    }
                    else
                    {
                        ShotNumber++;
                        StartShot();
                        LastResult = null;
                        EnterPhase(RoundPhase.SkateIn);
                    }
                    break;
            }
        }

        /// <summary>
        /// Reach in time for a Shot at Net Face point (x, y), now in the shooting window. The puck starts where the shooter has
        /// skated to (Breakaway.ShotStart); its Flight Time runs to where it passes the goalie, at the depth of his Set-up spot.
        /// </summary>
        private ShotRace Race(double x, double y, double speedKmh)
        {
            double sinceWindowOpened = _phaseElapsed;
            RinkPoint from = Breakaway.ShotStart(AttackSide, sinceWindowOpened, _settings);
            RinkPoint netPoint = RinkPoint.OnNetFace(x, y);
            RinkPoint spot = SetUps.Spot(SetUp, AttackSide, sinceWindowOpened, from, _settings);
            double flight = ShotFlight.Seconds(RinkPoint.Distance(from, RinkPoint.AtDepth(from, netPoint, spot.Z)), speedKmh);

            GoalieReachAnswer answer = _reach.Reach(SetUp, AttackSide, sinceWindowOpened, from, netPoint);
            double move = answer.InTheWay ? 0 : answer.MoveSeconds;
            double reach = answer.InTheWay ? 0 : ReactionSeconds + move;
            SaveReaction shown = answer.InTheWay || !answer.QuickestGear.HasValue ? SaveReactions.For(x, y) : answer.QuickestGear.Value;
            return new ShotRace(SetUp, AttackSide, ReactionSeconds, move, reach, flight, answer.InTheWay, shown);
        }

        /// <summary>A new Shot: its Goalie Pose (and so Attack Side), its Set-up and its Reaction Time, in the roll order.</summary>
        private void StartShot()
        {
            PickGoaliePose();
            SetUp = SetUp.Clean;
            ReactionSeconds = _settings.ReactionSeconds + _settings.ReactionWobbleSeconds * (2 * _random.Next() - 1);
            LastRace = null;
        }

        private void PickGoaliePose()
        {
            for (int attempt = 0; attempt < MaxPickTries; attempt++)
            {
                int side = Math.Min((int)(_random.Next() * 3), 2);
                int index = Math.Min((int)(_random.Next() * GoaliePoses.PosesPerSide), GoaliePoses.PosesPerSide - 1);
                GoaliePose candidate = GoaliePoses.PosesOf((AttackSide)side)[index];
                if (_previousPose == null || candidate != _previousPose.Value)
                {
                    SetPose(candidate);
                    return;
                }
            }
            int poseCount = Enum.GetValues(typeof(GoaliePose)).Length;
            SetPose((GoaliePose)(((int)_previousPose.Value + 1) % poseCount));
        }

        private void SetPose(GoaliePose pose)
        {
            GoaliePose = pose;
            _previousPose = pose;
        }

        private void Finish(ShotResult result)
        {
            LastResult = result;
            _results.Add(result.Kind);
            if (result.Kind == ShotResultKind.Goal)
                Goals++;
            EnterPhase(RoundPhase.ResultHold);
        }

        private void EndRound()
        {
            if (Goals > BestScore)
            {
                BestScore = Goals;
                IsNewBest = true;
            }
            EnterPhase(RoundPhase.RoundOver);
        }

        private void EnterPhase(RoundPhase phase)
        {
            Phase = phase;
            _phaseElapsed = 0;
            _resultDelay = 0;
        }

        private double PhaseLength(RoundPhase phase)
        {
            switch (phase)
            {
                case RoundPhase.SkateIn: return _settings.SkateInSeconds;
                case RoundPhase.SlowDown: return _settings.SlowDownSeconds;
                case RoundPhase.ShootingWindow: return _settings.ShootingWindowSeconds;
                default: return _resultDelay + _settings.ResultHoldSeconds;
            }
        }
    }
}
