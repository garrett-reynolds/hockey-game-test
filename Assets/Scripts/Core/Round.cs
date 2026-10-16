using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>
    /// A Round of ShotCount Shots (3 to 10, set by StartRound(best, shots); the default is 6). Pure logic: time only enters through Tick(realSeconds).
    /// <para>
    /// A Shot on the Net Face is raced by Reach in time: a Save when the goalie's Reach Time is no longer than the puck's
    /// Flight Time, otherwise a Goal (see ShotRace). The goalie-reach port says how his body, in the Shot's Set-up, gets gear
    /// into the puck's line. The race drives how he moves, but the Round takes the Shot's result only at the Whistle
    /// (Whistle, WhistleRule): SubmitShot records the Shot and the Round waits.
    /// </para>
    /// <para>
    /// Random call order (public contract), all on the one IRandomSource:
    /// (n = ShotCount, the Shots in this Round; the default is 6.)
    /// 1. When a Round starts, the Set-up deck (RoundDecks.SetUps): one Next() for the 4th Set-up Mistake's type (Cheat,
    ///    Depth, Late Settle by (int)(Next() * 3)), then n - 1 shuffle rolls over the deck (n / 3 Clean Set-ups, the rest
    ///    Mistakes in turn Cheat, Depth, Late Settle, the 4th, Cheat ...): for card i = 0 to n - 2, one Next(), and card i
    ///    swaps with card i + (int)(Next() * (n - i)). For 6: 5 rolls on Cheat, Depth, Late Settle, the 4th, Clean, Clean.
    ///    (Settings.LateSettles false, the default: Mistakes in turn Cheat, Depth, Cheat ...; the 4th-type roll is still taken.)
    ///    Shot k gets card k - 1.
    /// 2. Then the Attack Sides (RoundDecks.AttackSides): the same n - 1 shuffle rolls over Left, Middle and Right in equal
    ///    runs (index i holds side i * 3 / n). For 6: Left, Left, Middle, Middle, Right, Right. Shot k gets side k - 1.
    /// 3. When each Shot starts (Shot 1 right after 2): one Next() for the post side, the left post when Next() &lt; 0.5,
    ///    else the right. It is rolled for every Shot; a Cheat sets toward that post and a Late Settle is still coming
    ///    across from it (on a middle attack too); a Clean Set-up and a Depth do not use it.
    /// 4. Then one Next() for the Reaction Time wobble: ReactionSeconds + ReactionWobbleSeconds * (2 * Next() - 1).
    /// Nothing else rolls: not the Shot's result, a Miss or TooLate.
    /// </para>
    /// <para>
    /// Test Mode (#156, StartTestMode) rolls the same way: steps 1 to 4 for Shot 1 (a deck of 6), and the same two decks again
    /// (steps 1 and 2) each time the Shots pass a multiple of 6, just before step 3 for that Shot.
    /// </para>
    /// </summary>
    public sealed class Round
    {
        /// <summary>The default number of Shots in a Round (SaveData.DefaultShotsPerRound). A Round plays ShotCount, set when it starts.</summary>
        public const int ShotsPerRound = SaveData.DefaultShotsPerRound;

        /// <summary>Shots in this Round (3 to 10): the number given to StartRound(best, shots), the default 6 until then.</summary>
        public int ShotCount { get; private set; } = ShotsPerRound;

        private readonly IRandomSource _random;
        private readonly RoundSettings _settings;
        private readonly IGoalieReach _reach;
        private readonly List<ShotResultKind> _results = new List<ShotResultKind>();
        private readonly List<double?> _speeds = new List<double?>();
        private double _phaseElapsed;
        private double _resultDelay; // real seconds from the Shot to its Whistle
        private SetUpKind[] _setUps = new SetUpKind[ShotsPerRound];
        private AttackSide[] _sides = new AttackSide[ShotsPerRound];

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

        /// <summary>Current Shot number, 1 to ShotCount. 0 before the Round starts.</summary>
        public int ShotNumber { get; private set; }

        /// <summary>Real seconds left in the shooting window; 0 outside the window.</summary>
        public double WindowSecondsLeft =>
            Phase == RoundPhase.ShootingWindow && !_settings.TestMode ? _settings.ShootingWindowSeconds - _phaseElapsed : 0; // Test Mode: the window never closes, no clock

        /// <summary>Real seconds left of the result hold; infinity before the Whistle (the hold has not started) and outside the hold.</summary>
        public double ResultHoldSecondsLeft =>
            Phase == RoundPhase.ResultHold && !AwaitingWhistle ? _resultDelay + ResultHoldLength - _phaseElapsed : double.PositiveInfinity;

        /// <summary>Real seconds spent in the current phase.</summary>
        public double PhaseSecondsElapsed => _phaseElapsed;

        /// <summary>Result of the current Shot; null until it has one.</summary>
        public ShotResult? LastResult { get; private set; }

        /// <summary>The last finished Shot's speed this Round (kept into the next Shot); null before the first, or when it was Too late.</summary>
        public double? LastShotSpeedKmh { get; private set; }

        /// <summary>Result per finished Shot, in order (TooLate counts as a Miss in the tally).</summary>
        public IReadOnlyList<ShotResultKind> Results => _results;

        /// <summary>Speed (km/h) per finished Shot, in order, parallel to Results; null for a Shot with no puck (Too Late) and for a Test Shot (dev only: it never counts in the Round's average speed, #153).</summary>
        public IReadOnlyList<double?> ShotSpeedsKmh => _speeds;

        public int Goals { get; private set; }

        /// <summary>True only at RoundOver, when Goals beat the current BestScore (which updates when beaten, so a reused Round compares each round against the latest best).</summary>
        public bool IsNewBest { get; private set; }

        /// <summary>Best score so far; changes only when beaten at RoundOver.</summary>
        public int BestScore { get; private set; }

        /// <summary>Attack Side of the current Shot, from the Round's shuffled pick (left, middle and right twice each).</summary>
        public AttackSide AttackSide { get; private set; }

        /// <summary>The goalie's Set-up for the current Shot, from the Round's Set-up deck and the Shot's post side.</summary>
        public SetUp SetUp { get; private set; }

        /// <summary>The goalie's Reaction Time for the current Shot (seconds), rolled when the Shot starts.</summary>
        public double ReactionSeconds { get; private set; }

        /// <summary>The goalie's race against the current Shot (Reach in time), run when it is submitted; null until a Shot on the Net Face. It drives how he moves; the Whistle gives the result.</summary>
        public ShotRace? LastRace { get; private set; }

        /// <summary>
        /// The Gaps a puck can fit through now, in the shooting window (the goalie-reach port, asked as a Shot now would be:
        /// this Set-up and Attack Side, from where the shooter has skated to); None outside it. Rolls nothing, changes nothing.
        /// </summary>
        public NetFaceGaps GapsNow() => GapsNow(0, NetFaceGaps.Cells);

        /// <summary>As GapsNow(), measuring only the run of cells from firstCell (cellCount long); the rest are not Gaps.</summary>
        public NetFaceGaps GapsNow(int firstCell, int cellCount)
        {
            if (Phase != RoundPhase.ShootingWindow) return NetFaceGaps.None;
            return _reach.Gaps(SetUp, AttackSide, _phaseElapsed, Breakaway.ShotStart(AttackSide, _phaseElapsed, _settings), firstCell, cellCount);
        }

        /// <summary>
        /// False from the Shot until the Whistle (the Shot is in flight and has no result yet); true otherwise. The scene shows
        /// the result text and plays the result sound only once this is true.
        /// </summary>
        public bool ResultShowing => Phase != RoundPhase.ResultHold || !AwaitingWhistle;

        /// <summary>True from a Shot on the clock until the game reports its Whistle (Whistle). The result hold has not started.</summary>
        public bool AwaitingWhistle { get; private set; }
        /// <summary>
        /// True when a Replay follows this result: the setting is on (RoundSettings.ReplayOn, #145) and it is a Goal, a Save or a
        /// Frame Hit. The hold length does not depend on it: with the Replay off a Goal, Save or Frame Hit still holds 4.5 s.
        /// </summary>
        public bool ReplayFollows => ReplayOn && ReplayTiming.Follows(LastResult);

        /// <summary>The Replay setting as it counts: on, and never in Test Mode (#156), whatever RoundSettings.ReplayOn says.</summary>
        public bool ReplayOn => _settings.ReplayOn && !_settings.TestMode;

        /// <summary>
        /// How long the current result holds once it shows (after the Whistle): ReplayTiming.HoldSeconds (about 4.5 s)
        /// when a Replay follows (a Goal, a Save or a Frame Hit), otherwise ResultHoldSeconds (2.5 s: a Wide Miss, Too late).
        /// Before Shots 2 to 6 a Replay's hold runs ShotFade.LeadSeconds (0.35 s) longer, so the fade to black starts after the
        /// Replay's look-back has ended, never over it (#129): 4.85 s.
        /// </summary>
        public double ResultHoldLength => _settings.TestMode ? TestModeHoldLength : // Test Mode (#156): one short hold, no Replay, no fade lead
            (ReplayTiming.Follows(LastResult) ? ReplayTiming.HoldSeconds : _settings.ResultHoldSeconds)
            + (ReplayTiming.Follows(LastResult) && ShotNumber < ShotCount ? ShotFade.LeadSeconds : 0)
            + ClipHoldExtra;

        /// <summary>Test Mode's hold (PROTOTYPE keep-clips): TestModeHoldSeconds, except while the AI clips are on (ClipHoldSeconds above 0)
        /// a Goal, a Save or a Miss holds as long as normal play with no Replay (ResultHoldSeconds, plus ClipHoldSeconds for a Goal or a Save), so a clip fits.
        /// A Frame Hit and Too Late keep the short hold.</summary>
        double TestModeHoldLength
        {
            get
            {
                if (_settings.ClipHoldSeconds <= 0) return _settings.TestModeHoldSeconds;
                ArenaResult r = ArenaShow.ResultOf(LastResult);
                if (r == ArenaResult.Goal || r == ArenaResult.Save) return _settings.ResultHoldSeconds + _settings.ClipHoldSeconds;
                return r == ArenaResult.WideMiss ? _settings.ResultHoldSeconds : _settings.TestModeHoldSeconds;
            }
        }

        /// <summary>RoundSettings.ClipHoldSeconds on a Goal or a Save with no Replay (not a Frame Hit, Miss or Too Late): the clip starts at the Whistle, and a Replay's own hold already fits it.</summary>
        double ClipHoldExtra
        {
            get
            {
                ArenaResult r = ArenaShow.ResultOf(LastResult);
                return (r == ArenaResult.Goal || r == ArenaResult.Save) && !ReplayFollows ? _settings.ClipHoldSeconds : 0;
            }
        }

        /// <summary>
        /// Real seconds from the Shot to its Whistle (Shot-to-Whistle, since #108: not the puck's flight); 0 until the Whistle.
        /// The result hold runs ResultHoldLength after it. (Named before the Whistle; kept, as other code reads it.)
        /// </summary>
        public double ResultDelaySeconds => _resultDelay;

        /// <summary>The Shot just submitted (where it went, how fast); null until one is, and after a Too Late. Its result comes at the Whistle.</summary>
        public SubmittedShot? LastShot { get; private set; }

        /// <summary>How many Shots this Round has had submitted (Too Late is none).</summary>
        public int ShotsSubmitted { get; private set; }

        /// <summary>Shots whose Whistle has come: ShotsSubmitted without the Shot whose puck is still flying. Moves with Goals (#157).</summary>
        public int ShotsWhistled => Math.Max(0, ShotsSubmitted - (AwaitingWhistle ? 1 : 0)); // never below 0 (EnterPhase clears AwaitingWhistle in every reset)

        /// <summary>
        /// Starts a Round of `shots` Shots (clamped to 3 to 10) against the given best score for that number. The game starts every
        /// Round this way (ScreenFlow), from the save's Shots per Round and the best score kept for it.
        /// </summary>
        public void StartRound(int bestScore, int shots)
        {
            ShotCount = (int)SaveData.ShotsPerRoundRange.Clamp(shots);
            StartRound(bestScore);
        }

        /// <summary>
        /// Starts a Round against the given best score (for example after a reset on the settings screen), with the SAME number of
        /// Shots as the last Round started (ShotCount; 6 if none yet). It does not reset ShotCount: use StartRound(best, shots) to change it.
        /// </summary>
        public void StartRound(int bestScore)
        {
            BestScore = bestScore;
            StartRound();
        }

        /// <summary>True while Test Mode runs (RoundSettings.TestMode).</summary>
        public bool TestMode => _settings.TestMode;

        /// <summary>
        /// Starts Test Mode (#156): endless Shots from the shooter's spot. The first Shot is in the shooting window at once (no skate-in,
        /// no slow-down); the window never closes (no Too late); each Whistle holds TestModeHoldSeconds, then the next Shot (a new Set-up
        /// and Attack Side) is in the window again. The Round never ends, and bestScore never changes (IsNewBest stays false).
        /// ShotNumber counts on past ShotCount; the Set-up and Attack Side decks (ShotCount cards each, dealt as StartRound deals them) are
        /// dealt again, on the same source, each time they run out. Test Shots and real Shots both count. Leave with EndTestMode.
        /// </summary>
        public void StartTestMode(int bestScore)
        {
            _settings.TestMode = true;
            ShotCount = ShotsPerRound;
            BestScore = bestScore;
            DealDecks();
            _results.Clear();
            _speeds.Clear();
            Goals = 0;
            IsNewBest = false;
            LastResult = null;
            ShotsSubmitted = 0;
            LastShotSpeedKmh = null;
            ShotNumber = 1;
            StartShot();
            EnterPhase(RoundPhase.ShootingWindow);
        }

        /// <summary>Leaves Test Mode: the Round goes back to Idle (the start screen), with nothing of the Test Mode Shots kept.</summary>
        public void EndTestMode()
        {
            if (!_settings.TestMode) return;
            // ShotCount stays 6 (the Test Mode deck size) until the next StartRound(best, shots) sets it; nothing reads it on the start screen.
            ResetToIdle();
        }

        /// <summary>
        /// Quit to start from the Pause box (#196): the Round goes back to Idle with nothing kept (no score, no Shots). The best score
        /// is never touched here. Does nothing when the Round is already Idle or over.
        /// </summary>
        public void Abandon()
        {
            if (Phase == RoundPhase.Idle || Phase == RoundPhase.RoundOver) return;
            ResetToIdle();
        }

        // What Abandon and EndTestMode share: the Round goes back to Idle with nothing of the Shots kept.
        private void ResetToIdle()
        {
            _settings.TestMode = false;
            _results.Clear();
            _speeds.Clear();
            Goals = 0;
            IsNewBest = false;
            LastResult = null;
            LastShot = null;
            LastRace = null;
            ShotsSubmitted = 0;
            LastShotSpeedKmh = null;
            ShotNumber = 0;
            EnterPhase(RoundPhase.Idle);
        }

        private void DealDecks()
        {
            _setUps = RoundDecks.SetUps(_random, ShotCount, Settings.LateSettles);
            _sides = RoundDecks.AttackSides(_random, ShotCount);
        }

        /// <summary>Starts a Round with the best score and the number of Shots (ShotCount; 6 if none yet) the Round already holds.</summary>
        public void StartRound()
        {
            _settings.TestMode = false; // a Round is never Test Mode (#156)
            DealDecks();
            _results.Clear();
            _speeds.Clear();
            Goals = 0;
            IsNewBest = false;
            LastResult = null;
            ShotsSubmitted = 0;
            LastShotSpeedKmh = null;
            ShotNumber = 1;
            StartShot();
            EnterPhase(RoundPhase.SkateIn);
        }

        /// <summary>
        /// Submit a Shot. Counts only as the first one in the shooting window. The Round records the Shot and runs the goalie's
        /// race (it drives how he moves), then waits for the Whistle: the result comes from Whistle, not from here.
        /// </summary>
        public void SubmitShot(double x, double y, double speedKmh, bool isTestShot)
        {
            if (Phase != RoundPhase.ShootingWindow)
                return;

            bool onNetFace = x >= 0 && x <= 100 && y >= 0 && y <= 100;
            if (onNetFace)
                LastRace = Race(x, y, speedKmh);
            LastShot = new SubmittedShot(x, y, speedKmh, isTestShot);
            ShotsSubmitted++;
            EnterPhase(RoundPhase.ResultHold);
            AwaitingWhistle = true;
        }

        /// <summary>
        /// The game reports the Whistle with the Shot's result (Goal, Save or Miss). The Round records it and holds it
        /// ResultHoldLength from now (2.5 s; 4.5 s with a Replay). Counts only while waiting for a Whistle: a later call changes nothing. A Whistle is
        /// never Too Late (that comes from the shooting window running out): it throws.
        /// </summary>
        public void Whistle(ShotResultKind kind)
        {
            if (kind == ShotResultKind.TooLate)
                throw new ArgumentException("A Whistle gives Goal, Save or Miss; Too Late is the Round's own result.", nameof(kind));
            if (!AwaitingWhistle)
                return;

            SubmittedShot shot = LastShot.Value;
            AwaitingWhistle = false;
            _resultDelay = _phaseElapsed;
            Record(new ShotResult(kind, true, shot.X, shot.Y, shot.SpeedKmh, shot.IsTestShot));
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
                    if (_settings.TestMode)
                    {
                        // Test Mode: endless. The decks run out every ShotCount Shots and are dealt again; the next Shot is in the window at once.
                        ShotNumber++;
                        _results.Clear(); // endless: Results holds only the Shot now showing (Goals and ShotsSubmitted keep the totals)
                        _speeds.Clear();
                        if ((ShotNumber - 1) % ShotCount == 0) DealDecks();
                        StartShot();
                        LastResult = null;
                        EnterPhase(RoundPhase.ShootingWindow);
                    }
                    else if (ShotNumber >= ShotCount)
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

        /// <summary>Reach in time for a Shot at Net Face point (x, y), now in the shooting window, on this Shot's rolled Reaction Time.</summary>
        private ShotRace Race(double x, double y, double speedKmh) =>
            ShotRace.At(SetUp, AttackSide, _phaseElapsed, x, y, speedKmh, ReactionSeconds, _reach, _settings);

        /// <summary>A new Shot: its Attack Side and Set-up from the Round's shuffles, its post side and its Reaction Time, in the roll order.</summary>
        private void StartShot()
        {
            int card = (ShotNumber - 1) % ShotCount; // Test Mode goes past the deck and deals it again (Advance)
            AttackSide = _sides[card];
            Post post = _random.Next() < 0.5 ? Post.Left : Post.Right;
            SetUp = SetUp.Of(_setUps[card], post);
            ReactionSeconds = _settings.ReactionSeconds + _settings.ReactionWobbleSeconds * (2 * _random.Next() - 1);
            LastRace = null;
            LastShot = null;
        }

        /// <summary>A result with no Whistle to wait for (Too Late): recorded now, held from now.</summary>
        private void Finish(ShotResult result)
        {
            EnterPhase(RoundPhase.ResultHold);
            Record(result);
        }

        private void Record(ShotResult result)
        {
            LastResult = result;
            LastShotSpeedKmh = result.HasShot ? result.SpeedKmh : (double?)null;
            _results.Add(result.Kind);
            _speeds.Add(result.IsTestShot ? null : LastShotSpeedKmh); // a Test Shot is a Shot in the Results but has no speed in the average (#153)
            if (result.Kind == ShotResultKind.Goal)
                Goals++;
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
            AwaitingWhistle = false;
        }

        private double PhaseLength(RoundPhase phase)
        {
            switch (phase)
            {
                case RoundPhase.SkateIn: return _settings.SkateInSeconds;
                case RoundPhase.SlowDown: return _settings.SlowDownSeconds;
                case RoundPhase.ShootingWindow: return _settings.TestMode ? double.PositiveInfinity : _settings.ShootingWindowSeconds; // Test Mode: never Too late
                default: return AwaitingWhistle ? double.PositiveInfinity : _resultDelay + ResultHoldLength;
            }
        }
    }
}
