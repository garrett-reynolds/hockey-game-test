using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>
    /// A Round of 6 Shots. Pure logic: time only enters through Tick(realSeconds).
    /// <para>
    /// Random call order (public contract): when a Shot starts, per pose pick try, Next() for the Attack Side
    /// (side = (int)(Next() * 3)), then Next() for the pose (index = (int)(Next() * 2)). A try that repeats the
    /// previous Shot's pose is retried, up to 10 tries; if all 10 repeat it, the next pose in GoaliePose enum
    /// order (wrapping) is used. Then one Next() per Contested Shot (Goal when roll is below the chance); none for
    /// Open, Covered, Miss or TooLate.
    /// </para>
    /// </summary>
    public sealed class Round
    {
        public const int ShotsPerRound = 6;

        private readonly IRandomSource _random;
        private readonly RoundSettings _settings;
        private readonly List<ShotResultKind> _results = new List<ShotResultKind>();
        private const int MaxPickTries = 10;
        private double _phaseElapsed;
        private GoaliePose? _previousPose;

        public Round(IRandomSource random, RoundSettings settings, int bestScore)
        {
            _random = random;
            _settings = settings;
            BestScore = bestScore;
            Phase = RoundPhase.Idle;
        }

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

        /// <summary>Attack Side of the current Shot's Goalie Pose.</summary>
        public AttackSide AttackSide => GoaliePoses.SideOf(GoaliePose);

        /// <summary>Goalie Pose for the current Shot; chosen when the Shot starts.</summary>
        public GoaliePose GoaliePose { get; private set; }

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
            PickGoaliePose();
            EnterPhase(RoundPhase.SkateIn);
        }

        /// <summary>Submit a Shot. Counts only as the first one in the shooting window.</summary>
        public void SubmitShot(double x, double y, double speedKmh, bool isTestShot)
        {
            if (Phase != RoundPhase.ShootingWindow)
                return;

            bool onNetFace = x >= 0 && x <= 100 && y >= 0 && y <= 100;
            var kind = onNetFace ? ResolveOnNetFace(x, y, speedKmh) : ShotResultKind.Miss;
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
                        PickGoaliePose();
                        LastResult = null;
                        EnterPhase(RoundPhase.SkateIn);
                    }
                    break;
            }
        }

        private ShotResultKind ResolveOnNetFace(double x, double y, double speedKmh)
        {
            switch (GoaliePoses.CoverAt(GoaliePose, Zones.Column(x), Zones.Row(y)))
            {
                case ZoneCover.Open:
                    return ShotResultKind.Goal;
                case ZoneCover.Covered:
                    return ShotResultKind.Save;
                default:
                    double chance = GoaliePoses.ContestedChance(speedKmh, _settings);
                    return _random.Next() < chance ? ShotResultKind.Goal : ShotResultKind.Save;
            }
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
        }

        private double PhaseLength(RoundPhase phase)
        {
            switch (phase)
            {
                case RoundPhase.SkateIn: return _settings.SkateInSeconds;
                case RoundPhase.SlowDown: return _settings.SlowDownSeconds;
                case RoundPhase.ShootingWindow: return _settings.ShootingWindowSeconds;
                default: return _settings.ResultHoldSeconds;
            }
        }
    }
}
