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
        NewBestCheer,
        PostPing,     // a Miss rings off a post or the crossbar
        GloveCatch,   // a Save in the catching glove
        BlockerHit    // a Save off the blocker
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

        private readonly SaveData _save;
        private readonly List<SoundCue> _cues = new List<SoundCue>();
        private RoundPhase _lastPhase = RoundPhase.Idle;
        private int _ticksPlayed;
        private readonly ShotResultWatch _newResult = new ShotResultWatch();
        private ShotResult? _pendingImpact;

        /// <summary>Reads the tick on/off setting from save each frame, so a settings change applies at once.</summary>
        public RoundSoundCues(SaveData save)
        {
            _save = save;
        }

        /// <summary>Volume for everything (0..1): 0 when muted, else the master volume, clamped.</summary>
        public static double ListenerVolume(SaveData save) => save.Mute ? 0 : Numbers.Clamp01(save.MasterVolume);

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

            // A pending impact never outlives its result hold: leaving ResultHold plays it (above).
            if (_newResult.TakeNew(round))
            {
                if (round.LastResult.Value.Kind == ShotResultKind.TooLate)
                {
                    _cues.Add(SoundCue.Buzzer);
                    _cues.Add(SoundCue.Groan);
                }
                else
                {
                    _pendingImpact = round.LastResult.Value;
                }
            }

            // The result shows when the puck lands (Round.DelayResult), so a missed arrival frame still plays it then.
            if (_pendingImpact.HasValue && (puckArrived || (phase == RoundPhase.ResultHold && round.ResultShowing)))
                PlayPendingImpact();

            if (phase == RoundPhase.ShootingWindow)
            {
                double windowSeconds = round.PhaseSecondsElapsed + round.WindowSecondsLeft;
                int due = TicksDue(round.PhaseSecondsElapsed, windowSeconds);
                if (due > _ticksPlayed)
                {
                    if (_save.TickOn)
                        _cues.Add(IsFastTick(due - 1, windowSeconds) ? SoundCue.FastTick : SoundCue.Tick);
                    _ticksPlayed = due; // at most one tick per frame
                }
            }

            return _cues;
        }

        private void PlayPendingImpact()
        {
            if (!_pendingImpact.HasValue) return;
            ShotResult result = _pendingImpact.Value;
            switch (result.Kind)
            {
                case ShotResultKind.Goal:
                    _cues.Add(SoundCue.GoalImpact);
                    _cues.Add(SoundCue.Cheer);
                    break;
                case ShotResultKind.Save:
                    _cues.Add(SaveContact(result.X, result.Y));
                    _cues.Add(SoundCue.Groan);
                    break;
                default:
                    _cues.Add(NetFrame.At(result.X, result.Y) == FrameHit.None ? SoundCue.MissImpact : SoundCue.PostPing);
                    _cues.Add(SoundCue.Groan);
                    break;
            }
            _pendingImpact = null;
        }

        /// <summary>
        /// What stopped a Save at this Net Face position. Bottom row: the leg pads. Top row: the catching glove on the
        /// shooter's right (the goalie's left hand faces the shooter on that side), the blocker on the shooter's left.
        /// Middle Zones pick the nearer one: the middle row is pad below half height, else a hand; the middle column is
        /// the blocker left of half width, else the glove.
        /// </summary>
        private static SoundCue SaveContact(double x, double y)
        {
            int row = Zones.Row(y);
            if (row == 0 || (row == 1 && y < 50.0)) return SoundCue.SaveImpact;
            int column = Zones.Column(x);
            bool gloveSide = column == 2 || (column == 1 && x >= 50.0);
            return gloveSide ? SoundCue.GloveCatch : SoundCue.BlockerHit;
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
        public static double CrowdLevel(RoundPhase phase, double phaseSeconds, RoundSettings settings)
        {
            switch (phase)
            {
                case RoundPhase.SkateIn:
                    double t = Numbers.Clamp01(phaseSeconds / settings.SkateInSeconds);
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
    }
}
