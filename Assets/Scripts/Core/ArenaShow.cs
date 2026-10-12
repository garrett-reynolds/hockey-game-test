using System;

namespace HockeyGame.Core
{
    /// <summary>The last Shot's result as the arena shows it: a Miss splits into a Frame Hit (post or bar) and a wide Miss.</summary>
    public enum ArenaResult
    {
        None,
        Goal,
        Save,
        FrameHitPost,
        FrameHitBar,
        WideMiss,
        TooLate
    }

    /// <summary>What the Video Board (and the Centre Board, which always mirrors it) shows.</summary>
    public enum VideoBoardContent
    {
        StartCard, // the game name, the best score and "SHOOT TO PLAY"
        Score,     // the score and the last Shot speed
        Goal,
        Replay,
        Final,     // the final score at Round over
        NewBest,   // "NEW BEST!" with a small light show
        AdSlide    // a brand ad slide: never in the shooting window
    }

    public enum RibbonMessage
    {
        Ads,       // cycle the brand ads
        Goal,
        Save,
        OffThePost,
        OffTheBar,
        CalmStill  // one dim still colour, no words (the Calm Area rules)
    }

    /// <summary>The arena's zones: the Calm Area behind the near net, the rest of the near half, and the far half.</summary>
    public enum ArenaZone
    {
        CalmArea,
        Near,
        Far
    }

    /// <summary>What the fans in one zone do (the crowd's clips: sit-idle, wave, stand-cheer, oh, clap).</summary>
    public enum CrowdState
    {
        Sit,   // sit and make small moves
        Wave,  // sit, with some hand waves
        Still, // sit still and dim (the Calm Area rules)
        Cheer, // stand and cheer
        Oh,    // a short "oh!"
        Clap   // clap with the chant
    }

    public enum CameraMode
    {
        OnNet,
        LookUp,
        Replay,
        LookBack
    }

    /// <summary>An arena colour, 0-255 a channel. Core holds no engine colour.</summary>
    public readonly struct ArenaColour : IEquatable<ArenaColour>
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public ArenaColour(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public bool Equals(ArenaColour other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object obj) => obj is ArenaColour other && Equals(other);
        public override int GetHashCode() => (R << 16) | (G << 8) | B;
        public override string ToString() => "(" + R + ", " + G + ", " + B + ")";
    }

    /// <summary>What the Ribbon Boards in one zone show. Text is "" for ads (the ads bring their own) and for the still colour.</summary>
    public readonly struct RibbonBoard : IEquatable<RibbonBoard>
    {
        public RibbonMessage Message { get; }
        public string Text { get; }
        /// <summary>The words' colour; for CalmStill, the board's one colour. Ads use their own colours.</summary>
        public ArenaColour Colour { get; }
        /// <summary>Small words (SAVE).</summary>
        public bool Small { get; }

        public RibbonBoard(RibbonMessage message, string text, ArenaColour colour, bool small)
        {
            Message = message;
            Text = text;
            Colour = colour;
            Small = small;
        }

        public bool Equals(RibbonBoard other) =>
            Message == other.Message && Text == other.Text && Colour.Equals(other.Colour) && Small == other.Small;
        public override bool Equals(object obj) => obj is RibbonBoard other && Equals(other);
        public override int GetHashCode() => ((int)Message * 397) ^ Colour.GetHashCode();
        public override string ToString() => Message + " \"" + Text + "\" " + Colour + (Small ? " small" : "");
    }

    /// <summary>The Goal Show's lights: the red goal light and the roof lights pulsing, at FlashesPerSecond (never over 3).</summary>
    public readonly struct GoalShowLights
    {
        public bool GoalLight { get; }
        public bool RoofPulse { get; }
        public double FlashesPerSecond { get; }
        /// <summary>Whether the pulsing lights are on at this moment of their flash (false when nothing pulses).</summary>
        public bool Lit { get; }

        public GoalShowLights(bool goalLight, bool roofPulse, double flashesPerSecond, bool lit)
        {
            GoalLight = goalLight;
            RoofPulse = roofPulse;
            FlashesPerSecond = flashesPerSecond;
            Lit = lit;
        }
    }

    /// <summary>
    /// The Video Board's words (the Centre Board shows the same): a big Headline and smaller Details lines ("\n" between
    /// lines). Both are "" for an ad slide, which brings its own brand.
    /// </summary>
    public readonly struct VideoBoardWords : IEquatable<VideoBoardWords>
    {
        public string Headline { get; }
        public string Details { get; }

        public VideoBoardWords(string headline, string details)
        {
            Headline = headline;
            Details = details;
        }

        public bool Equals(VideoBoardWords other) => Headline == other.Headline && Details == other.Details;
        public override bool Equals(object obj) => obj is VideoBoardWords other && Equals(other);
        public override int GetHashCode() => (Headline ?? "").GetHashCode() ^ (Details ?? "").GetHashCode();
        public override string ToString() => "\"" + Headline + "\" / \"" + Details + "\"";
    }

    /// <summary>The small TV-style score in a screen corner.</summary>
    public readonly struct ScoreBox
    {
        public bool Shown { get; }
        public string Text { get; }

        public ScoreBox(bool shown, string text)
        {
            Shown = shown;
            Text = text;
        }
    }

    /// <summary>What the Round tells the arena, as plain values (ArenaShow.From reads them from the game's screen flow).</summary>
    public struct ArenaShowInput
    {
        /// <summary>Idle on the start screen, even after a Round.</summary>
        public RoundPhase Phase;
        /// <summary>Real seconds in this phase.</summary>
        public double PhaseSeconds;
        /// <summary>The last Shot's result this Shot (None before the Shot).</summary>
        public ArenaResult Result;
        /// <summary>True while the result is held back for the on-screen puck flight (Round.ResultShowing is false).</summary>
        public bool ResultHidden;
        public bool IsNewBest;
        /// <summary>Goals so far, counting a Goal whose puck is still in flight (as Round.Goals does).</summary>
        public int Goals;
        public int ShotNumber;
        /// <summary>The saved best score (the start card shows it).</summary>
        public int BestScore;
        /// <summary>The last finished Shot's speed this Round; null before the first Shot, or when it was Too late (no puck).</summary>
        public double? LastShotSpeedKmh;
    }

    /// <summary>One answer of the Arena Show: what every arena part does now. The Unity parts only follow it.</summary>
    public readonly struct ArenaState
    {
        public VideoBoardContent VideoBoard { get; }
        /// <summary>The Video Board's (and the Centre Board's) words for VideoBoard.</summary>
        public VideoBoardWords VideoBoardWords { get; }
        public GoalShowLights GoalShow { get; }
        public CameraMode Camera { get; }
        public bool Chant { get; }
        /// <summary>The Calm Area rules (still, soft and dim behind the near net): on from goalie set to the Shot.</summary>
        public bool CalmAreaRules { get; }
        public ScoreBox ScoreBox { get; }

        private readonly RibbonBoard _ribbon, _calmRibbon;
        private readonly CrowdState _calmCrowd, _nearCrowd, _farCrowd;

        public ArenaState(VideoBoardContent videoBoard, VideoBoardWords videoBoardWords, RibbonBoard ribbon, RibbonBoard calmRibbon, CrowdState calmCrowd,
                          CrowdState nearCrowd, CrowdState farCrowd, GoalShowLights goalShow, CameraMode camera, bool chant,
                          bool calmAreaRules, ScoreBox scoreBox)
        {
            VideoBoard = videoBoard;
            VideoBoardWords = videoBoardWords;
            _ribbon = ribbon;
            _calmRibbon = calmRibbon;
            _calmCrowd = calmCrowd;
            _nearCrowd = nearCrowd;
            _farCrowd = farCrowd;
            GoalShow = goalShow;
            Camera = camera;
            Chant = chant;
            CalmAreaRules = calmAreaRules;
            ScoreBox = scoreBox;
        }

        public RibbonBoard RibbonIn(ArenaZone zone) => zone == ArenaZone.CalmArea ? _calmRibbon : _ribbon;

        public CrowdState CrowdIn(ArenaZone zone) =>
            zone == ArenaZone.CalmArea ? _calmCrowd : zone == ArenaZone.Near ? _nearCrowd : _farCrowd;
    }

    /// <summary>
    /// The Arena Show: a pure function of the Round state (phase, time in it, the last Shot result, new best, score) that
    /// answers what each arena part does: the Video Board, the Ribbon Boards, the crowd in each zone, the Goal Show lights,
    /// the camera, the chant, the Calm Area rules and the Score Box. Like RoundSoundCues it holds no rules of the Round.
    /// The goalie is set when the shooting window opens (EasyGlow): the Calm Area rules hold for the whole window, so a Late
    /// Settle keeps them a little early. The Replay timing (camera look up and back) waits for the Replay ticket (#91): the
    /// camera stays on the net.
    /// </summary>
    public static class ArenaShow
    {
        /// <summary>The game's name, as the start screen and the start card show it.</summary>
        public const string GameName = "HOCKEY GAME";

        /// <summary>The photosensitive limit: no light flashes more often than this.</summary>
        public const double MaxFlashesPerSecond = 3.0;
        /// <summary>The Goal Show's (and the new-best light show's) flash rate.</summary>
        public const double GoalShowFlashesPerSecond = 2.0;

        public static readonly ArenaColour GoalRed = new ArenaColour(230, 30, 35);
        public static readonly ArenaColour SaveWhite = new ArenaColour(240, 242, 245);
        public static readonly ArenaColour FrameHitAmber = new ArenaColour(255, 185, 40);
        /// <summary>The Calm Area's one dim still Ribbon Board colour: a dark slate, near the dimmed stands round it (no reserved colour).</summary>
        public static readonly ArenaColour CalmRibbonColour = new ArenaColour(26, 31, 40);

        private static readonly RibbonBoard Ads = new RibbonBoard(RibbonMessage.Ads, "", SaveWhite, false);
        private static readonly RibbonBoard CalmStill = new RibbonBoard(RibbonMessage.CalmStill, "", CalmRibbonColour, false);
        private static readonly GoalShowLights NoLights = new GoalShowLights(false, false, 0, false);

        /// <summary>The arena's reading of a Shot result (null: no Shot yet).</summary>
        public static ArenaResult ResultOf(ShotResult? result)
        {
            if (!result.HasValue) return ArenaResult.None;
            ShotResult r = result.Value;
            switch (r.Kind)
            {
                case ShotResultKind.Goal: return ArenaResult.Goal;
                case ShotResultKind.Save: return ArenaResult.Save;
                case ShotResultKind.TooLate: return ArenaResult.TooLate;
            }
            switch (NetFrame.At(r.X, r.Y))
            {
                case FrameHit.LeftPost:
                case FrameHit.RightPost:
                    return ArenaResult.FrameHitPost;
                case FrameHit.Crossbar:
                    return ArenaResult.FrameHitBar;
                default:
                    return ArenaResult.WideMiss;
            }
        }

        /// <summary>
        /// The show now, from the game's screen flow: the start screen is Idle even when the Round has ended; at Round over the
        /// time is the end screen's.
        /// </summary>
        public static ArenaState From(ScreenFlow flow)
        {
            Round round = flow.Round;
            bool start = flow.Screen == FlowScreen.Start;
            RoundPhase phase = start ? RoundPhase.Idle : round.Phase;
            return At(new ArenaShowInput
            {
                Phase = phase,
                PhaseSeconds = start ? 0 : phase == RoundPhase.RoundOver ? flow.EndSecondsElapsed : round.PhaseSecondsElapsed,
                Result = ResultOf(round.LastResult),
                ResultHidden = !round.ResultShowing,
                IsNewBest = round.IsNewBest,
                Goals = round.Goals,
                ShotNumber = round.ShotNumber,
                BestScore = flow.BestScore,
                LastShotSpeedKmh = round.LastShotSpeedKmh
            });
        }

        public static ArenaState At(ArenaShowInput input)
        {
            // A result counts from when it shows (the puck lands), and only in its result hold.
            ArenaResult result = input.Phase == RoundPhase.ResultHold && !input.ResultHidden ? input.Result : ArenaResult.None;
            bool goalInFlight = input.Phase == RoundPhase.ResultHold && input.ResultHidden && input.Result == ArenaResult.Goal;
            bool calm = input.Phase == RoundPhase.ShootingWindow;

            RibbonBoard ribbon = RibbonFor(result);
            CrowdState near = CrowdFor(input.Phase, result);
            bool chant = input.Phase == RoundPhase.Idle || input.Phase == RoundPhase.RoundOver;
            int goalsShown = input.Goals - (goalInFlight ? 1 : 0);
            VideoBoardContent board = VideoBoardFor(input.Phase, result, input.IsNewBest);

            return new ArenaState(
                board,
                WordsFor(board, input, goalsShown),
                ribbon,
                calm ? CalmStill : ribbon,
                calm ? CrowdState.Still : near,
                near,
                result == ArenaResult.Save || result == ArenaResult.FrameHitPost || result == ArenaResult.FrameHitBar ? CrowdState.Sit : near,
                LightsFor(input.Phase, result, input.IsNewBest, input.PhaseSeconds),
                CameraMode.OnNet,
                chant,
                calm,
                new ScoreBox(input.Phase != RoundPhase.Idle, ScoreText(goalsShown, input.ShotNumber)));
        }

        /// <summary>The Score Box's words, e.g. "GOALS 2 · SHOT 4 OF 6".</summary>
        public static string ScoreText(int goals, int shotNumber) =>
            "GOALS " + goals + " · SHOT " + shotNumber + " OF " + Round.ShotsPerRound;

        private static VideoBoardContent VideoBoardFor(RoundPhase phase, ArenaResult result, bool newBest)
        {
            switch (phase)
            {
                case RoundPhase.Idle: return VideoBoardContent.StartCard;
                case RoundPhase.RoundOver: return newBest ? VideoBoardContent.NewBest : VideoBoardContent.Final;
                case RoundPhase.ResultHold:
                    switch (result)
                    {
                        case ArenaResult.Goal: return VideoBoardContent.Goal;
                        case ArenaResult.Save:
                        case ArenaResult.FrameHitPost:
                        case ArenaResult.FrameHitBar: return VideoBoardContent.Replay;
                        case ArenaResult.WideMiss:
                        case ArenaResult.TooLate: return VideoBoardContent.AdSlide;
                        default: return VideoBoardContent.Score;
                    }
                default: return VideoBoardContent.Score; // skate-in, slow-down and shooting window
            }
        }

        private static VideoBoardWords WordsFor(VideoBoardContent board, ArenaShowInput input, int goalsShown)
        {
            string score = ScoreText(goalsShown, input.ShotNumber);
            string goalsOf = goalsShown + " OF " + Round.ShotsPerRound + " GOALS";
            string best = input.BestScore + " OF " + Round.ShotsPerRound;
            switch (board)
            {
                case VideoBoardContent.StartCard: return new VideoBoardWords(GameName, "BEST SCORE " + best + "\nSHOOT TO PLAY");
                case VideoBoardContent.Goal: return new VideoBoardWords("GOAL!", score);
                case VideoBoardContent.Replay: return new VideoBoardWords("REPLAY", score); // the Replay picture comes with #91
                case VideoBoardContent.Final: return new VideoBoardWords("FINAL SCORE", goalsOf + "\nBEST " + best);
                case VideoBoardContent.NewBest: return new VideoBoardWords("NEW BEST!", goalsOf);
                case VideoBoardContent.AdSlide: return new VideoBoardWords("", "");
                default: // the score, still, from the skate-in to the Shot
                    double? kmh = input.LastShotSpeedKmh;
                    return new VideoBoardWords(score, kmh.HasValue ? "LAST SHOT " + ShotResultText.Speed(kmh.Value) : "");
            }
        }

        private static RibbonBoard RibbonFor(ArenaResult result)
        {
            switch (result)
            {
                case ArenaResult.Goal: return new RibbonBoard(RibbonMessage.Goal, "GOAL!", GoalRed, false);
                case ArenaResult.Save: return new RibbonBoard(RibbonMessage.Save, "SAVE", SaveWhite, true);
                case ArenaResult.FrameHitPost: return new RibbonBoard(RibbonMessage.OffThePost, "OFF THE POST!", FrameHitAmber, false);
                case ArenaResult.FrameHitBar: return new RibbonBoard(RibbonMessage.OffTheBar, "OFF THE BAR!", FrameHitAmber, false);
                default: return Ads; // nothing for a Wide Miss or Too late
            }
        }

        /// <summary>The near zones' crowd (the Calm Area's outside the shooting window; the far zones' unless a close call).</summary>
        private static CrowdState CrowdFor(RoundPhase phase, ArenaResult result)
        {
            switch (phase)
            {
                case RoundPhase.Idle:
                case RoundPhase.RoundOver: return CrowdState.Clap;
                case RoundPhase.SkateIn: return CrowdState.Wave;
                case RoundPhase.ResultHold:
                    switch (result)
                    {
                        case ArenaResult.Goal: return CrowdState.Cheer;
                        case ArenaResult.Save:
                        case ArenaResult.FrameHitPost:
                        case ArenaResult.FrameHitBar: return CrowdState.Oh;
                        default: return CrowdState.Sit;
                    }
                default: return CrowdState.Sit;
            }
        }

        private static GoalShowLights LightsFor(RoundPhase phase, ArenaResult result, bool newBest, double seconds)
        {
            bool goal = result == ArenaResult.Goal;
            bool newBestShow = phase == RoundPhase.RoundOver && newBest;
            if (!goal && !newBestShow) return NoLights;
            double rate = GoalShowFlashesPerSecond;
            bool lit = Math.Floor(Math.Max(0, seconds) * rate * 2.0) % 2 == 0; // on for the first half of each flash
            return new GoalShowLights(goal, true, rate, lit);
        }
    }
}
