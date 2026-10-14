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
        AdSlide,   // a brand ad slide: never in the shooting window
        CalmStill, // from goalie set to the Shot: one dim still colour, no words (the Calm Area rules, as for the Ribbon Boards)
        ResultWord // with the Replay off (#145): a Save or a Frame Hit's word and the score, for the whole hold (a Goal shows Goal)
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

    /// <summary>What the fans in one zone do (the crowd's clips: sit-idle, wave, stand-cheer, oh, clap, miss).</summary>
    public enum CrowdState
    {
        Sit,   // sit and make small moves
        Wave,  // sit, with some hand waves
        Still, // sit still and dim (the Calm Area rules)
        Cheer, // stand and cheer
        Oh,    // a short "oh!"
        Clap,  // clap (the start screen and Round over)
        Miss   // a frustrated seated swipe, once, then sit (a wide Miss or no Shot in time)
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

    /// <summary>
    /// The Goal Show's lights: the red goal light and the roof lights pulsing, at FlashesPerSecond (never over 3). The
    /// default (all off) is no show.
    /// </summary>
    public readonly struct GoalShowLights : IEquatable<GoalShowLights>
    {
        /// <summary>The red goal light above the net: on, still, for the whole Goal Show.</summary>
        public bool GoalLight { get; }
        public bool RoofPulse { get; }
        public double FlashesPerSecond { get; }
        /// <summary>Whether the pulsing lights are on at this moment of their flash (false when nothing pulses).</summary>
        public bool Lit { get; }
        /// <summary>The roof lights' colour while Lit; between flashes they are their normal white.</summary>
        public ArenaColour PulseColour { get; }

        public GoalShowLights(bool goalLight, bool roofPulse, double flashesPerSecond, bool lit, ArenaColour pulseColour)
        {
            GoalLight = goalLight;
            RoofPulse = roofPulse;
            FlashesPerSecond = flashesPerSecond;
            Lit = lit;
            PulseColour = pulseColour;
        }

        public bool Equals(GoalShowLights other) =>
            GoalLight == other.GoalLight && RoofPulse == other.RoofPulse && FlashesPerSecond.Equals(other.FlashesPerSecond) &&
            Lit == other.Lit && PulseColour.Equals(other.PulseColour);
        public override bool Equals(object obj) => obj is GoalShowLights other && Equals(other);
        public override int GetHashCode() => (GoalLight ? 1 : 0) | (RoofPulse ? 2 : 0) | (Lit ? 4 : 0) | (PulseColour.GetHashCode() << 3);
        public override string ToString() =>
            (GoalLight ? "goal light, " : "") + (RoofPulse ? "roof pulse " + FlashesPerSecond + "/s " + PulseColour + (Lit ? " lit" : " dark") : "no pulse");
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
        /// <summary>True from the Shot until its Whistle (Round.ResultShowing is false): the puck is still on its way.</summary>
        public bool ResultHidden;
        /// <summary>Real seconds from the Shot to its Whistle (Round.ResultDelaySeconds): the Goal Show and the Replay's camera count from there.</summary>
        public double ResultDelaySeconds;
        public bool IsNewBest;
        /// <summary>Goals so far (Round.Goals: a Goal counts from its Whistle, the line crossing).</summary>
        public int Goals;
        public int ShotNumber;
        /// <summary>The saved best score (the start card shows it).</summary>
        public int BestScore;
        /// <summary>Shots in the Round (ScreenFlow.ShotsPerRound); 0 reads as the default 6.</summary>
        public int ShotsPerRound;
        /// <summary>The last finished Shot's speed this Round; null before the first Shot, or when it was Too late (no puck).</summary>
        public double? LastShotSpeedKmh;
        /// <summary>True with the Replay turned off (#145): no Replay and no look-up; the board shows the result word. False (the default) is Replay on.</summary>
        public bool NoReplay;
        /// <summary>True in Test Mode (#156): no Replay (as NoReplay), whatever NoReplay says; the camera stays on the net.</summary>
        public bool TestMode;
        /// <summary>Shots whistled this Test Mode visit (Round.ShotsWhistled, with Goals); the Score Box counts them in Test Mode. Not read otherwise.</summary>
        public int ShotsSubmitted;
    }

    /// <summary>One answer of the Arena Show: what every arena part does now. The Unity parts only follow it.</summary>
    public readonly struct ArenaState
    {
        public VideoBoardContent VideoBoard { get; }
        /// <summary>The Video Board's (and the Centre Board's) words for VideoBoard.</summary>
        public VideoBoardWords VideoBoardWords { get; }
        public GoalShowLights GoalShow { get; }
        public CameraMode Camera { get; }
        /// <summary>How far the shooter's camera has turned up to the Video Board: 0 on the net, 1 on the board (the Replay).</summary>
        public double CameraLookUp { get; }
        /// <summary>Seconds into the Replay (0..ReplayTiming.ShowSeconds) while Camera is Replay; 0 otherwise.</summary>
        public double ReplaySeconds { get; }
        /// <summary>The Calm Area rules (still, soft and dim behind the near net): on from goalie set to the Shot.</summary>
        public bool CalmAreaRules { get; }
        public ScoreBox ScoreBox { get; }

        private readonly RibbonBoard _ribbon, _calmRibbon;
        private readonly CrowdState _calmCrowd, _nearCrowd, _farCrowd;

        public ArenaState(VideoBoardContent videoBoard, VideoBoardWords videoBoardWords, RibbonBoard ribbon, RibbonBoard calmRibbon, CrowdState calmCrowd,
                          CrowdState nearCrowd, CrowdState farCrowd, GoalShowLights goalShow, CameraMode camera, double cameraLookUp,
                          double replaySeconds, bool calmAreaRules, ScoreBox scoreBox)
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
            CameraLookUp = cameraLookUp;
            ReplaySeconds = replaySeconds;
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
    /// the camera, the Calm Area rules and the Score Box. Like RoundSoundCues it holds no rules of the Round.
    /// The goalie is set when the shooting window opens (EasyGlow): the Calm Area rules hold for the whole window, so a Late
    /// Settle keeps them a little early. After a Goal, a Save or a Frame Hit the camera stays on the net, looks up at the
    /// Video Board, which plays the Replay, and looks back down (ReplayTiming); after a Wide Miss or Too late it stays on the net.
    /// </summary>
    public static class ArenaShow
    {
        /// <summary>The game's name, as the start screen and the start card show it.</summary>
        public const string GameName = "SHOOTOUT";

        /// <summary>The photosensitive limit: no light flashes more often than this.</summary>
        public const double MaxFlashesPerSecond = 3.0;
        /// <summary>The Goal Show's (and the new-best light show's) flash rate.</summary>
        public const double GoalShowFlashesPerSecond = 2.0;

        public static readonly ArenaColour GoalRed = new ArenaColour(230, 30, 35);
        public static readonly ArenaColour SaveWhite = new ArenaColour(240, 242, 245);
        public static readonly ArenaColour FrameHitAmber = new ArenaColour(255, 185, 40);
        /// <summary>The roof lights' colour in the new-best light show.</summary>
        public static readonly ArenaColour NewBestGold = new ArenaColour(255, 205, 80);
        /// <summary>The Calm Area's one dim still Ribbon Board colour: a dark slate, near the dimmed stands round it (no reserved colour).</summary>
        public static readonly ArenaColour CalmRibbonColour = new ArenaColour(26, 31, 40);

        private static readonly RibbonBoard Ads = new RibbonBoard(RibbonMessage.Ads, "", SaveWhite, false);
        private static readonly RibbonBoard CalmStill = new RibbonBoard(RibbonMessage.CalmStill, "", CalmRibbonColour, false);
        private static readonly GoalShowLights NoLights = default;

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
        public static ArenaState From(ScreenFlow flow) => At(InputFrom(flow));

        /// <summary>The Arena Show's input now, from the game's screen flow (From and BottleKnock.Follow share it).</summary>
        public static ArenaShowInput InputFrom(ScreenFlow flow)
        {
            Round round = flow.Round;
            bool start = flow.Screen == FlowScreen.Start;
            RoundPhase phase = start ? RoundPhase.Idle : round.Phase;
            return new ArenaShowInput
            {
                Phase = phase,
                PhaseSeconds = start ? 0 : phase == RoundPhase.RoundOver ? flow.EndSecondsElapsed : round.PhaseSecondsElapsed,
                Result = ResultOf(round.LastResult),
                ResultHidden = !round.ResultShowing,
                ResultDelaySeconds = round.ResultDelaySeconds,
                IsNewBest = round.IsNewBest,
                Goals = round.Goals,
                ShotNumber = round.ShotNumber,
                BestScore = flow.BestScore,
                ShotsPerRound = flow.ShotsPerRound,
                LastShotSpeedKmh = round.LastShotSpeedKmh,
                NoReplay = !round.ReplayOn,
                TestMode = round.TestMode,
                ShotsSubmitted = round.ShotsWhistled
            };
        }

        public static ArenaState At(ArenaShowInput input)
        {
            // A result counts from when it shows (its Whistle), and only in its result hold.
            ArenaResult result = input.Phase == RoundPhase.ResultHold && !input.ResultHidden ? input.Result : ArenaResult.None;
            bool calm = input.Phase == RoundPhase.ShootingWindow;

            RibbonBoard ribbon = RibbonFor(result);
            CrowdState near = CrowdFor(input.Phase, result);
            int goalsShown = input.Goals; // the Round counts a Goal from its Whistle (the line crossing), when it shows
            // The Replay's part of the hold counts from the Whistle (the puck lands), like the Goal Show.
            bool noReplay = input.NoReplay || input.TestMode; // Test Mode (#156) reuses the Replay-off path; From already folds it into NoReplay, this keeps the pure show safe on its own (tested)
            bool replayHold = !noReplay && ReplayTiming.Follows(result); // Replay off (#145): the camera stays on the net
            double landed = input.PhaseSeconds - input.ResultDelaySeconds;
            CameraMode camera = replayHold ? ReplayTiming.CameraAt(landed) : CameraMode.OnNet;
            VideoBoardContent board = camera == CameraMode.Replay ? VideoBoardContent.Replay : VideoBoardFor(input.Phase, result, input.IsNewBest, noReplay);
            if (input.TestMode && (board == VideoBoardContent.StartCard || board == VideoBoardContent.Final || board == VideoBoardContent.NewBest))
                board = VideoBoardContent.Score; // Test Mode (#157) has no start card or Round end: the count only, never "OF 6" or "SHOOT TO PLAY"
            // Built once: the Score Box and the Video Board share it. Test Mode (#157) counts the visit's Goals and Shots (no Round of 6).
            string score = input.TestMode ? TestModeHud.CountText(goalsShown, input.ShotsSubmitted) : ScoreText(goalsShown, input.ShotNumber, ShotsOf(input));

            return new ArenaState(
                board,
                WordsFor(board, input, goalsShown, score),
                ribbon,
                calm ? CalmStill : ribbon,
                calm ? CrowdState.Still : near,
                near,
                result == ArenaResult.Save || result == ArenaResult.FrameHitPost || result == ArenaResult.FrameHitBar ? CrowdState.Sit : near,
                LightsFor(input.Phase, result, input.IsNewBest, input.PhaseSeconds, input.ResultDelaySeconds),
                camera,
                replayHold ? ReplayTiming.LookUpAt(landed) : 0,
                camera == CameraMode.Replay ? ReplayTiming.ShowSecondsAt(landed) : 0,
                calm,
                new ScoreBox(input.Phase != RoundPhase.Idle, score));
        }

        /// <summary>The Score Box's words, e.g. "GOALS 2 · SHOT 4 OF 6".</summary>
        public static string ScoreText(int goals, int shotNumber) => ScoreText(goals, shotNumber, Round.ShotsPerRound);

        public static string ScoreText(int goals, int shotNumber, int shotsPerRound) =>
            "GOALS " + goals + " · SHOT " + shotNumber + " OF " + shotsPerRound;

        private static int ShotsOf(ArenaShowInput input) => input.ShotsPerRound > 0 ? input.ShotsPerRound : Round.ShotsPerRound;
        private static string GoalsOf(ArenaShowInput input, int goalsShown) => goalsShown + " OF " + ShotsOf(input) + " GOALS";
        private static string BestOf(ArenaShowInput input) => input.BestScore + " OF " + ShotsOf(input);

        private static VideoBoardContent VideoBoardFor(RoundPhase phase, ArenaResult result, bool newBest, bool noReplay)
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
                        case ArenaResult.FrameHitBar: return noReplay ? VideoBoardContent.ResultWord : VideoBoardContent.Replay;
                        case ArenaResult.WideMiss:
                        case ArenaResult.TooLate: return VideoBoardContent.AdSlide;
                        default: return VideoBoardContent.Score;
                    }
                case RoundPhase.ShootingWindow: return VideoBoardContent.CalmStill; // the Video Board is in the Calm Area's view
                default: return VideoBoardContent.Score; // skate-in and slow-down
            }
        }

        // Each case builds only the words it shows: the show is worked out every frame.
        private static VideoBoardWords WordsFor(VideoBoardContent board, ArenaShowInput input, int goalsShown, string score)
        {
            switch (board)
            {
                case VideoBoardContent.StartCard: return new VideoBoardWords(GameName, "BEST SCORE " + BestOf(input) + "\nSHOOT TO PLAY");
                case VideoBoardContent.Goal: return new VideoBoardWords("GOAL!", score);
                case VideoBoardContent.ResultWord: return new VideoBoardWords(RibbonFor(input.Result).Text, score); // Replay off: the ribbon's word (SAVE, OFF THE POST!, OFF THE BAR!)
                case VideoBoardContent.Replay: return new VideoBoardWords("REPLAY", score); // the Replay picture covers it while the Replay plays
                case VideoBoardContent.Final: return new VideoBoardWords("FINAL SCORE", GoalsOf(input, goalsShown) + "\nBEST " + BestOf(input));
                case VideoBoardContent.NewBest: return new VideoBoardWords("NEW BEST!", GoalsOf(input, goalsShown));
                case VideoBoardContent.AdSlide:
                case VideoBoardContent.CalmStill: return new VideoBoardWords("", "");
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
                        default: return CrowdForMiss(result);
                    }
                default: return CrowdState.Sit;
            }
        }

        /// <summary>A Miss's crowd (a wide Shot or no Shot in time): a frustrated swipe; nothing else shows a result here.</summary>
        private static CrowdState CrowdForMiss(ArenaResult result) =>
            result == ArenaResult.WideMiss || result == ArenaResult.TooLate ? CrowdState.Miss : CrowdState.Sit;

        /// <summary>
        /// The Goal Show runs from when the puck lands (the horn), so its flashes fall the same on every Goal however long the
        /// puck flew: on for the first half of each flash. The new-best show starts dark for half a flash, so a last Goal's
        /// flash just before Round over and the show's first flash are never closer than a normal flash.
        /// </summary>
        private static GoalShowLights LightsFor(RoundPhase phase, ArenaResult result, bool newBest, double seconds, double resultDelay)
        {
            bool goal = result == ArenaResult.Goal;
            bool newBestShow = phase == RoundPhase.RoundOver && newBest;
            if (!goal && !newBestShow) return NoLights;
            double rate = GoalShowFlashesPerSecond;
            double showSeconds = Math.Max(0, goal ? seconds - resultDelay : seconds);
            bool firstHalf = Math.Floor(showSeconds * rate * 2.0) % 2 == 0;
            return new GoalShowLights(goal, true, rate, goal ? firstHalf : !firstHalf, goal ? GoalRed : NewBestGold);
        }
    }

    /// <summary>
    /// "Bottle knocked" (#164), the Arena Show's state beside the Goal Show: true from the Whistle of a Goal whose Shot speed is
    /// strictly above KnockSpeedKmh, and it stays true for that Shot's result hold (a second Knock changes nothing). It goes back
    /// to false before the next Shot (Garrett, 2026-10-13: "It needs to get reset between each shot"): any phase but the result
    /// hold, in a Round and in Test Mode. A Save, a Miss, a Frame Hit and a Shot with no puck never knock it.
    /// The Game side calls Follow once a frame with the same input as the Arena Show and reads Knocked: push the bottle on the change
    /// to true, put it back and stop it on the change to false. Plain logic, no Unity.
    /// </summary>
    public sealed class BottleKnock
    {
        /// <summary>A Goal strictly faster than this (km/h) knocks the bottle off the net.</summary>
        public const double KnockSpeedKmh = 100.0;

        /// <summary>The speed a Goal must beat now (km/h): KnockSpeedKmh until the game sets it from the Dev setting, each frame, so a change acts on the next Shot (#164).</summary>
        public double SpeedKmh { get; set; } = KnockSpeedKmh;

        public bool Knocked { get; private set; }

        /// <summary>Follows the game's screen flow (as ArenaShow.From does) and returns Knocked.</summary>
        public bool Follow(ScreenFlow flow) => Follow(ArenaShow.InputFrom(flow));

        /// <summary>Follows one frame of the Round and returns Knocked.</summary>
        public bool Follow(ArenaShowInput input)
        {
            if (input.Phase != RoundPhase.ResultHold)
                Knocked = false;
            else if (!Knocked && input.Phase == RoundPhase.ResultHold && !input.ResultHidden && input.Result == ArenaResult.Goal
                     && input.LastShotSpeedKmh.HasValue && input.LastShotSpeedKmh.Value > SpeedKmh)
                Knocked = true;
            return Knocked;
        }
    }
}
