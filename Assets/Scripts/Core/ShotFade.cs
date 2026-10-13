namespace HockeyGame.Core
{
    /// <summary>
    /// The black fade over the cut between Shots (#129). The skate-in of Shots 2 to 6 starts at the wide #14 start, so the camera
    /// cuts from the end of the last Shot to it; the screen goes black over the end of the result hold (the Replay has played by
    /// then) and back over the start of the next skate-in, so the cut is never seen. Never during the shooting window.
    /// Shot 1 comes from the start screen, which is already a cut: no fade there. After Shot 6 comes the end screen: no fade.
    /// </summary>
    public static class ShotFade
    {
        /// <summary>Real seconds to fade out, and to fade back in.</summary>
        public const double FadeSeconds = 0.25;
        /// <summary>Real seconds fully black each side of the cut, so a long frame at the cut never shows it.</summary>
        public const double BlackSeconds = 0.05;

        /// <summary>How black the screen is (0 clear .. 1 black). holdSecondsLeft: real seconds left of the result hold (infinity before the Whistle).</summary>
        public static double Amount(RoundPhase phase, int shotNumber, double phaseSeconds, double holdSecondsLeft)
        {
            if (phase == RoundPhase.ResultHold && shotNumber < Round.ShotsPerRound && !double.IsInfinity(holdSecondsLeft))
                return Numbers.Clamp01(1.0 - (holdSecondsLeft - BlackSeconds) / FadeSeconds);
            if (phase == RoundPhase.SkateIn && shotNumber > 1)
                return Numbers.Clamp01(1.0 - (phaseSeconds - BlackSeconds) / FadeSeconds);
            return 0;
        }

        /// <summary>The fade for the Round as it is now.</summary>
        public static double Amount(Round round) =>
            Amount(round.Phase, round.ShotNumber, round.PhaseSecondsElapsed, round.ResultHoldSecondsLeft);
    }
}
