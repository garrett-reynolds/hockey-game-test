namespace HockeyGame.Core
{
    /// <summary>
    /// The black fade over the cut between Shots (#129). The skate-in of Shots 2 to 6 starts at the wide #14 start, so the camera
    /// cuts from the end of the last Shot to it; the screen goes black over the end of the result hold and back over the start of
    /// the next skate-in, so the cut is never seen. Never during the shooting window. The fade starts only after the Replay has
    /// ended, look-back included (the Round lengthens the hold by LeadSeconds for that, see Round.ResultHoldLength).
    /// Shot 1 comes from the start screen, which is already a cut: no fade there. After Shot 6 comes the end screen: no fade.
    /// Step it once a frame, in the same frame the camera is placed: it counts the fade-in from the first frame of the new
    /// skate-in, so even a long frame at the cut shows full black.
    /// </summary>
    public sealed class ShotFade
    {
        /// <summary>Real seconds to fade out, and to fade back in.</summary>
        public const double FadeSeconds = 0.25;
        /// <summary>Real seconds fully black each side of the cut.</summary>
        public const double BlackSeconds = 0.1;
        /// <summary>The fade out and its black, before the cut: the result hold runs this long past the end of a Replay.</summary>
        public const double LeadSeconds = FadeSeconds + BlackSeconds;

        RoundPhase _last = RoundPhase.Idle;
        double _sinceCut;

        /// <summary>How black the screen is (0 clear .. 1 black) as of the last Step.</summary>
        public double Amount { get; private set; }

        /// <summary>Fade out, holdSecondsLeft real seconds before the end of the result hold (infinity before the Whistle).</summary>
        public static double Out(double holdSecondsLeft) =>
            double.IsInfinity(holdSecondsLeft) ? 0 : Numbers.Clamp01(1.0 - (holdSecondsLeft - BlackSeconds) / FadeSeconds);

        /// <summary>Fade in, secondsSinceCut real seconds after the first frame of the new skate-in.</summary>
        public static double In(double secondsSinceCut) => Numbers.Clamp01(1.0 - (secondsSinceCut - BlackSeconds) / FadeSeconds);

        /// <summary>One frame of realDt real seconds, the Round as it is after this frame's tick. Returns Amount.</summary>
        public double Step(Round round, double realDt)
        {
            RoundPhase phase = round.Phase;
            if (round.TestMode) Amount = 0; // Test Mode (#156): one still camera, no cut between Shots, no black
            else if (phase == RoundPhase.ResultHold && round.ShotNumber < round.ShotCount)
                Amount = Out(round.ResultHoldSecondsLeft);
            else if (phase == RoundPhase.SkateIn && round.ShotNumber > 1) // Shot 1 follows the start screen: no fade
            {
                if (_last != RoundPhase.SkateIn) _sinceCut = 0; // the frame the camera cuts
                else _sinceCut += realDt;
                Amount = In(_sinceCut);
            }
            else Amount = 0;
            _last = phase;
            return Amount;
        }
    }
}
