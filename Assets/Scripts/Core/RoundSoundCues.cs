using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>One sound to start (or, for HockeyStopEnd, to stop) this frame. Loops (crowd, heartbeat) are levels, not cues.</summary>
    public enum SoundCue
    {
        Tick,
        FastTick,
        Buzzer,
        GoalImpact,   // puck hits the net + goal horn
        SaveImpact,   // puck thuds on the goalie pad (or body)
        MissImpact,   // puck clangs on the boards
        Cheer,
        Groan,
        NewBestCheer,
        PostPing,     // a Miss rings off a post or the crossbar
        GloveCatch,   // a Save in the catching glove
        BlockerHit,   // a Save off the blocker (or the stick or a skate)
        HockeyStop,   // the skate-in begins: start the hockey stop, placed so it ends with the skate-in
        HockeyStopEnd, // the skate-in ends: stop the hockey stop
        StrideLeft,   // a push of the camera's stride while it leans left: one stride sound, panned left
        StrideRight   // a push of the camera's stride while it leans right: one stride sound, panned right
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
        /// <summary>
        /// Length of the hockey stop that ends the skate-in (seconds; the hockey-stop sound file is cut to it).
        /// No stride plays once it has begun.
        /// </summary>
        public const double HockeyStopSeconds = 0.6;

        private readonly SaveData _save;
        private readonly List<SoundCue> _cues = new List<SoundCue>();
        private RoundPhase _lastPhase = RoundPhase.Idle;
        private int _ticksPlayed;
        private int _stridesPlayed;
        private readonly ShotResultWatch _newResult = new ShotResultWatch();
        private bool _hitThisShot; // a hit sound (a post or the crossbar) has played since the Shot

        /// <summary>Reads the tick on/off setting from save each frame, so a settings change applies at once.</summary>
        public RoundSoundCues(SaveData save)
        {
            _save = save;
        }

        /// <summary>Volume for everything (0..1): 0 when muted, else the master volume, clamped.</summary>
        public static double ListenerVolume(SaveData save) => save.Mute ? 0 : Numbers.Clamp01(save.MasterVolume);

        /// <summary>
        /// Cues to start this frame. A Shot's result sounds at its Whistle, from the Round's result (Round.Whistle): a GOAL!'s
        /// net hit, horn and cheer at the line crossing. The list is reused; read it before the next call.
        /// </summary>
        public IReadOnlyList<SoundCue> Update(Round round) => Update(round, null);

        /// <summary>
        /// As Update(round), with the sound of a hit the physics puck made since the last frame (PhysicsShotEffects.HitSound:
        /// a post or the crossbar), played at once. null: no hit.
        /// </summary>
        public IReadOnlyList<SoundCue> Update(Round round, SoundCue? hit)
        {
            _cues.Clear();
            RoundPhase phase = round.Phase;

            if (phase != _lastPhase)
            {
                if (_lastPhase == RoundPhase.SkateIn) _cues.Add(SoundCue.HockeyStopEnd);
                if (phase == RoundPhase.SkateIn) _cues.Add(SoundCue.HockeyStop);
                if (phase == RoundPhase.RoundOver && round.IsNewBest) _cues.Add(SoundCue.NewBestCheer);
                _ticksPlayed = 0;
                _stridesPlayed = 0;
                _lastPhase = phase;
            }

            if (_newResult.TakeNewShot(round)) _hitThisShot = false;
            if (hit.HasValue)
            {
                _cues.Add(hit.Value);
                _hitThisShot = true;
            }
            // A new result is a Too Late (at once) or a Shot's Whistle.
            if (_newResult.TakeNew(round)) PlayResult(round);

            if (phase == RoundPhase.SkateIn)
            {
                int due = StridesDue(round.PhaseSecondsElapsed, round.Settings.SkateInSeconds);
                if (due > _stridesPlayed)
                {
                    _cues.Add(StrideCue(due - 1));
                    _stridesPlayed = due; // at most one stride per frame
                }
            }

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

        private void PlayResult(Round round)
        {
            ShotResult result = round.LastResult.Value;
            switch (result.Kind)
            {
                case ShotResultKind.TooLate:
                    _cues.Add(SoundCue.Buzzer);
                    break;
                case ShotResultKind.Goal:
                    _cues.Add(SoundCue.GoalImpact);
                    _cues.Add(SoundCue.Cheer);
                    return;
                case ShotResultKind.Save:
                    // Until #111 (the save sound at each contact with the gear): the race's Save Reaction's sound, at the Whistle.
                    _cues.Add(SaveReactions.Sound(round.LastRace?.SaveReaction ?? SaveReactions.For(result.X, result.Y)));
                    break;
                default:
                    // Until #110 (boards from physics contacts): a Miss that rang off nothing clangs on the boards at its Whistle.
                    if (!_hitThisShot) _cues.Add(SoundCue.MissImpact);
                    break;
            }
            _cues.Add(SoundCue.Groan);
        }

        /// <summary>What stops a Save at this Net Face position: the gear of its Save Reaction (SaveReactions).</summary>
        public static SoundCue SaveContact(double x, double y) => SaveReactions.Sound(SaveReactions.For(x, y));

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

        /// <summary>
        /// Where the hockey stop clip should be now (seconds into the clip), so its end (the stop itself) lands on the end
        /// of the skate-in. skateSeconds: how far into the skate-in we are. Negative: wait that long before playing it all.
        /// </summary>
        public static double HockeyStopClipTime(double clipSeconds, double skateInSeconds, double skateSeconds) =>
            clipSeconds - skateInSeconds + skateSeconds;

        /// <summary>
        /// Pushes of the camera's stride (Breakaway.StridePushSeconds) due by skateSeconds into the skate-in (game time
        /// is real time in the skate-in), leaving out those inside the hockey stop.
        /// </summary>
        private static int StridesDue(double skateSeconds, double skateInSeconds)
        {
            int due = 0;
            while (Breakaway.StridePushSeconds(due) <= skateSeconds + 1e-9
                   && Breakaway.StridePushSeconds(due) < skateInSeconds - HockeyStopSeconds)
                due++;
            return due;
        }

        /// <summary>The k-th stride sounds on the side the camera leans to at that push (Breakaway.Roll: positive leans left).</summary>
        private static SoundCue StrideCue(int k) =>
            Breakaway.Roll(Breakaway.StridePushSeconds(k)) > 0 ? SoundCue.StrideLeft : SoundCue.StrideRight;

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
