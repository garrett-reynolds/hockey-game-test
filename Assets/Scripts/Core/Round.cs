using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>
    /// A Round of 6 Shots. Pure logic: time only enters through Tick(realSeconds).
    /// </summary>
    public sealed class Round
    {
        public const int ShotsPerRound = 6;

        private readonly IRandomSource _random; // unused until Save chances arrive (#12)
        private readonly RoundSettings _settings;
        private readonly List<ShotResultKind> _results = new List<ShotResultKind>();
        private double _phaseElapsed;

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

        /// <summary>Result of the current Shot; null until it has one.</summary>
        public ShotResult? LastResult { get; private set; }

        /// <summary>Result per finished Shot, in order (TooLate counts as a Miss in the tally).</summary>
        public IReadOnlyList<ShotResultKind> Results => _results;

        public int Goals { get; private set; }

        /// <summary>True only at RoundOver, when Goals beat the best score given at construction.</summary>
        public bool IsNewBest { get; private set; }

        /// <summary>Best score so far; changes only when beaten at RoundOver.</summary>
        public int BestScore { get; private set; }

        public void StartRound()
        {
            _results.Clear();
            Goals = 0;
            IsNewBest = false;
            LastResult = null;
            ShotNumber = 1;
            EnterPhase(RoundPhase.SkateIn);
        }

        /// <summary>Submit a Shot. Counts only as the first one in the shooting window.</summary>
        public void SubmitShot(double x, double y, double speedKmh, bool isTestShot)
        {
            if (Phase != RoundPhase.ShootingWindow)
                return;

            bool onNetFace = x >= 0 && x <= 100 && y >= 0 && y <= 100;
            var kind = onNetFace ? ShotResultKind.Goal : ShotResultKind.Miss;
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
                        LastResult = null;
                        EnterPhase(RoundPhase.SkateIn);
                    }
                    break;
            }
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
