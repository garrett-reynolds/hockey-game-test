using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>One sound to start this frame. Loops (crowd, heartbeat) are levels, not cues.</summary>
    public enum SoundCue
    {
        Tick,
        FastTick,
        Buzzer,
        GoalImpact,   // puck hits the net + goal horn
        SaveImpact,   // puck thuds on the goalie pad
        MissImpact,   // puck clangs on the boards
        Cheer,
        Groan,
        NewBestCheer
    }

    /// <summary>Slow-down audio for one frame: low-pass cutoff, pitch, heartbeat on/off.</summary>
    public readonly struct SlowMoSound
    {
        public double LowPassHz { get; }
        public double Pitch { get; }
        public bool Heartbeat { get; }

        public SlowMoSound(double lowPassHz, double pitch, bool heartbeat)
        {
            LowPassHz = lowPassHz;
            Pitch = pitch;
            Heartbeat = heartbeat;
        }
    }

    /// <summary>
    /// Turns what the Round reports (phase, phase time, results, new best) into sound cues, frame by frame.
    /// Holds no rules of its own: call Update once per frame after the Round ticks.
    /// </summary>
    public sealed class RoundSoundCues
    {
        /// <summary>Low-pass cutoff when nothing is muffled (Hz).</summary>
        public const double OpenLowPassHz = 22000;
        /// <summary>Low-pass cutoff at full slow-down (Hz).</summary>
        public const double SlowLowPassHz = 900;
        public const double SlowPitch = 0.8;
        /// <summary>Spacing of the ticks in the last second of the shooting window.</summary>
        public const double FastTickSeconds = 0.25;
        /// <summary>If the puck has not arrived this long into the result hold, play the result sound anyway.</summary>
        public const double ImpactFallbackSeconds = 1.5;

        private readonly SoundSettings _settings;
        private readonly List<SoundCue> _cues = new List<SoundCue>();
        private RoundPhase _lastPhase = RoundPhase.Idle;
        private int _ticksPlayed;
        private int _resultsSeen;
        private ShotResultKind? _pendingImpact;

        public RoundSoundCues(SoundSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Cues to start this frame. puckArrived is true on the frame the on-screen puck reaches the net
        /// (or passes it on a Miss). The list is reused; read it before the next call.
        /// </summary>
        public IReadOnlyList<SoundCue> Update(Round round, bool puckArrived)
        {
            _cues.Clear();
            RoundPhase phase = round.Phase;

            if (phase != _lastPhase)
            {
                if (_lastPhase == RoundPhase.ResultHold) PlayPendingImpact();
                if (phase == RoundPhase.RoundOver && round.IsNewBest) _cues.Add(SoundCue.NewBestCheer);
                _ticksPlayed = 0;
                _lastPhase = phase;
            }

            int results = round.Results.Count;
            if (results < _resultsSeen)
            {
                _resultsSeen = results; // a new Round started
                _pendingImpact = null;
            }
            else if (results > _resultsSeen)
            {
                _resultsSeen = results;
                if (round.LastResult.HasValue)
                {
                    if (round.LastResult.Value.Kind == ShotResultKind.TooLate)
                    {
                        _cues.Add(SoundCue.Buzzer);
                        _cues.Add(SoundCue.Groan);
                    }
                    else
                    {
                        _pendingImpact = round.LastResult.Value.Kind;
                    }
                }
            }

            if (_pendingImpact.HasValue
                && (puckArrived || (phase == RoundPhase.ResultHold && round.PhaseSecondsElapsed >= ImpactFallbackSeconds)))
                PlayPendingImpact();

            if (phase == RoundPhase.ShootingWindow)
            {
                double windowSeconds = round.PhaseSecondsElapsed + round.WindowSecondsLeft;
                int due = TicksDue(round.PhaseSecondsElapsed, windowSeconds);
                if (due > _ticksPlayed)
                {
                    if (_settings.TickOn)
                        _cues.Add(IsFastTick(due - 1, windowSeconds) ? SoundCue.FastTick : SoundCue.Tick);
                    _ticksPlayed = due; // at most one tick per frame
                }
            }

            return _cues;
        }

        private void PlayPendingImpact()
        {
            if (!_pendingImpact.HasValue) return;
            switch (_pendingImpact.Value)
            {
                case ShotResultKind.Goal:
                    _cues.Add(SoundCue.GoalImpact);
                    _cues.Add(SoundCue.Cheer);
                    break;
                case ShotResultKind.Save:
                    _cues.Add(SoundCue.SaveImpact);
                    _cues.Add(SoundCue.Groan);
                    break;
                default:
                    _cues.Add(SoundCue.MissImpact);
                    _cues.Add(SoundCue.Groan);
                    break;
            }
            _pendingImpact = null;
        }

        /// <summary>Whole-second ticks before the last second of the window (tick 0 when it opens).</summary>
        private static int SlowTickCount(double windowSeconds) => (int)Math.Ceiling(windowSeconds - 1.0 - 1e-9);

        /// <summary>Ticks due by this many real seconds into the window: one per second, then every FastTickSeconds in the last second.</summary>
        private static int TicksDue(double elapsed, double windowSeconds)
        {
            int slow = SlowTickCount(windowSeconds);
            double lastSecond = windowSeconds - 1.0;
            if (elapsed < lastSecond)
                return Math.Min(slow, (int)Math.Floor(elapsed + 1e-9) + 1);
            int fastTotal = (int)Math.Round(1.0 / FastTickSeconds);
            int fast = Math.Min(fastTotal, (int)Math.Floor((elapsed - lastSecond) / FastTickSeconds + 1e-9) + 1);
            return slow + fast;
        }

        private static bool IsFastTick(int index, double windowSeconds) => index >= SlowTickCount(windowSeconds);

        /// <summary>
        /// Target crowd loudness (0..1): soft on the start screen, rising through the skate-in, quiet and tense
        /// in the slow-down and shooting window. Result reactions play on top as cues.
        /// </summary>
        public static double CrowdLevel(RoundPhase phase, double phaseSeconds)
        {
            switch (phase)
            {
                case RoundPhase.SkateIn:
                    double t = Clamp01(phaseSeconds / Breakaway.SkateInSeconds);
                    return 0.4 + 0.5 * t;
                case RoundPhase.SlowDown:
                case RoundPhase.ShootingWindow:
                    return 0.12;
                case RoundPhase.ResultHold:
                    return 0.45;
                default:
                    return 0.25; // start and end screens
            }
        }

        /// <summary>
        /// Slow-down audio, following the world time scale: muffled and lower as time slows, heartbeat while slowed.
        /// Snaps back with the time scale on the Shot.
        /// </summary>
        public static SlowMoSound SlowMo(RoundPhase phase, double phaseSeconds)
        {
            double amount = Breakaway.LookAmount(Breakaway.TimeScale(phase, phaseSeconds));
            double lowPass = OpenLowPassHz * Math.Pow(SlowLowPassHz / OpenLowPassHz, amount); // even on a log scale
            double pitch = 1.0 + (SlowPitch - 1.0) * amount;
            bool heartbeat = phase == RoundPhase.SlowDown || phase == RoundPhase.ShootingWindow;
            return new SlowMoSound(lowPass, pitch, heartbeat);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
