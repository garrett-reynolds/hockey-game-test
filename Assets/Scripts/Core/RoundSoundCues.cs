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
        MissImpact,   // puck clangs on the boards (a physics contact)
        Cheer,
        Groan,        // a Save: the home crowd groans
        Ooh,          // a Frame Hit (post or crossbar): the crowd's "ooh"
        SmallGroan,   // a Wide Miss or no Shot in time: a small groan
        NewBestCheer,
        PostPing,     // a Miss rings off a post or the crossbar
        GloveCatch,   // a Save in the catching glove
        NetRustle,    // the puck meets the netting (a GOAL!): the cloth gives
        BlockerHit,   // a Save off the blocker (or the stick or a skate)
        HockeyStop,   // the skate-in begins: start the hockey stop, placed so it ends with the skate-in
        HockeyStopEnd, // the skate-in ends: stop the hockey stop
        StrideLeft,   // a push of the camera's stride while it leans left: one stride sound, panned left
        StrideRight,  // a push of the camera's stride while it leans right: one stride sound, panned right
        Chant,        // start the Clap Chant ("Let's go!", clap-clap, clap-clap-clap) from its beginning
        ChantEnd,     // a Round starts while the chant plays: stop it (fade out, no click)
        GlassImpact   // puck bangs on the glass (a physics contact)
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
    /// <para>
    /// The Clap Chant needs the screen flow and real time (Update(ScreenFlow, ...)); it follows the Arena Show's chant on/off.
    /// On the start screen it repeats with a random quiet gap of ChantGapMinSeconds to ChantGapMaxSeconds from when one
    /// ends to when the next starts. At Round over it plays once, as soon as the last cheer, goal horn and new-best cheer
    /// have ended (at once when none plays). Never during a Round: a chant still playing when the skate-in starts is stopped.
    /// The Round-only Update overloads never chant.
    /// </para>
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

        /// <summary>Quiet gap between two chants on the start screen (seconds, rolled evenly between these).</summary>
        public const double ChantGapMinSeconds = 4;
        public const double ChantGapMaxSeconds = 8;

        /// <summary>Loudness order: goal horn > cheer > chant > crowd loop (CrowdLevel on the start and end screens).</summary>
        public const double HornVolume = 1.0;
        public const double CheerVolume = 0.8;
        /// <summary>How far the chant sits under the cheer (dB).</summary>
        public const double ChantUnderCheerDb = 6;
        public static readonly double ChantVolume = CheerVolume * Math.Pow(10, -ChantUnderCheerDb / 20);
        /// <summary>The Save groan and the Frame Hit "ooh": as loud as a cheer. The small groan is softer.</summary>
        public const double GroanVolume = 0.8;
        public const double SmallGroanVolume = 0.45;

        private readonly SaveData _save;
        private readonly List<SoundCue> _cues = new List<SoundCue>();
        private RoundPhase _lastPhase = RoundPhase.Idle;
        private int _ticksPlayed;
        private int _stridesPlayed;
        private readonly ShotResultWatch _newResult = new ShotResultWatch();
        private bool _hitThisShot; // a hit sound (a post or the crossbar) has played since the Shot

        private enum ChantMode { Off, Repeat, Once }
        private readonly IRandomSource _chantRandom;
        private readonly Func<SoundCue, double> _cueSeconds;
        private double _clock;              // real seconds, counted by Update(ScreenFlow, ...)
        private double _cheerEndsAt;        // when the last cheer, goal horn or new-best cheer ends
        private double _chantEndsAt;
        private double _nextChantAt;
        private bool _roundOverChanted;
        private ChantMode _chantMode = ChantMode.Off;

        /// <summary>Reads the tick on/off setting from save each frame, so a settings change applies at once. No chant.</summary>
        public RoundSoundCues(SaveData save)
        {
            _save = save;
        }

        /// <summary>
        /// With the Clap Chant (Update(ScreenFlow, ...)). chantRandom rolls the start-screen gaps (its own source, so the
        /// Round's rolls do not move); cueSeconds gives how long each cue's sound plays (the clip lengths), so the chant
        /// can wait for the cheer and horn and time its gaps from its own end.
        /// </summary>
        public RoundSoundCues(SaveData save, IRandomSource chantRandom, Func<SoundCue, double> cueSeconds) : this(save)
        {
            _chantRandom = chantRandom ?? throw new ArgumentNullException(nameof(chantRandom));
            _cueSeconds = cueSeconds ?? throw new ArgumentNullException(nameof(cueSeconds));
        }

        /// <summary>Volume for everything (0..1): 0 when muted, else the master volume, clamped.</summary>
        public static double ListenerVolume(SaveData save) => save.Mute ? 0 : Numbers.Clamp01(save.MasterVolume);

        /// <summary>How loud a cue plays (0..1, before the master volume): GoalImpact is the goal horn (and the net hit).</summary>
        public static double Volume(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.GoalImpact: return HornVolume;
                case SoundCue.Cheer:
                case SoundCue.NewBestCheer: return CheerVolume;
                case SoundCue.Chant: return ChantVolume;
                case SoundCue.Groan:
                case SoundCue.Ooh: return GroanVolume;
                case SoundCue.SmallGroan: return SmallGroanVolume;
                default: return 1.0;
            }
        }

        /// <summary>
        /// As Update(round, hit), plus the Clap Chant, from the game's screen flow. realSeconds: real time
        /// since the last call. Needs the chant constructor.
        /// </summary>
        public IReadOnlyList<SoundCue> Update(ScreenFlow flow, double realSeconds, SoundCue? hit = null) =>
            Update(flow, ArenaShow.From(flow).Chant, realSeconds, hit);

        /// <summary>
        /// As Update(flow, realSeconds, hit), with the Clap Chant on when chant is: the frame's Arena Show
        /// (ArenaState.Chant), worked out once by the caller and shared with the rest of the arena.
        /// </summary>
        public IReadOnlyList<SoundCue> Update(ScreenFlow flow, bool chant, double realSeconds, SoundCue? hit = null)
        {
            if (_chantRandom == null) throw new InvalidOperationException("The chant needs RoundSoundCues(save, chantRandom, cueSeconds).");
            _clock += realSeconds;
            Update(flow.Round, hit);
            foreach (SoundCue cue in _cues)
                if (cue == SoundCue.Cheer || cue == SoundCue.GoalImpact || cue == SoundCue.NewBestCheer)
                    _cheerEndsAt = Math.Max(_cheerEndsAt, _clock + _cueSeconds(cue));
            UpdateChant(flow, chant);
            return _cues;
        }

        private void UpdateChant(ScreenFlow flow, bool chant)
        {
            const double Epsilon = 1e-6;
            ChantMode mode = !chant ? ChantMode.Off
                : flow.Screen == FlowScreen.End ? ChantMode.Once
                : ChantMode.Repeat;
            if (mode != _chantMode)
            {
                if (mode == ChantMode.Off && _chantEndsAt > _clock + Epsilon)
                {
                    _cues.Add(SoundCue.ChantEnd);
                    _chantEndsAt = _clock;
                }
                if (mode == ChantMode.Repeat)
                    _nextChantAt = Math.Max(_clock, Math.Max(_chantEndsAt, _cheerEndsAt)) + ChantGap();
                if (mode == ChantMode.Once) _roundOverChanted = false;
                _chantMode = mode;
            }

            if (mode == ChantMode.Repeat && _clock >= _nextChantAt - Epsilon)
            {
                PlayChant();
                _nextChantAt = _chantEndsAt + ChantGap();
            }
            else if (mode == ChantMode.Once && !_roundOverChanted && _clock >= _cheerEndsAt - Epsilon)
            {
                PlayChant();
                _roundOverChanted = true;
            }
        }

        private void PlayChant()
        {
            _cues.Add(SoundCue.Chant);
            _chantEndsAt = _clock + _cueSeconds(SoundCue.Chant);
        }

        private double ChantGap() => ChantGapMinSeconds + (ChantGapMaxSeconds - ChantGapMinSeconds) * _chantRandom.Next();

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
                if (hit.Value == SoundCue.PostPing) _hitThisShot = true; // only a frame hit earns the "ooh": not a board, glass or netting hit
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
                    _cues.Add(SoundCue.SmallGroan);
                    break;
                case ShotResultKind.Goal:
                    _cues.Add(SoundCue.GoalImpact);
                    _cues.Add(SoundCue.Cheer);
                    break;
                case ShotResultKind.Save:
                    // Until #111 (the save sound at each contact with the gear): the race's Save Reaction's sound, at the Whistle.
                    _cues.Add(SaveReactions.Sound(round.LastRace?.SaveReaction ?? SaveReactions.For(result.X, result.Y)));
                    _cues.Add(SoundCue.Groan);
                    break;
                default:
                    // The puck rang off the frame (its ping played at the hit): the crowd's "ooh" at the Whistle. Any other
                    // Miss gets the small groan; the boards and glass sound at their own contacts (#110).
                    _cues.Add(_hitThisShot ? SoundCue.Ooh : SoundCue.SmallGroan);
                    break;
            }
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
