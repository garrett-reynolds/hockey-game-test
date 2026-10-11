namespace HockeyGame.Core
{
    /// <summary>
    /// Before the Shot (#70): how alive the goalie looks while he waits for the Shot, each piece behind its own RoundSettings
    /// switch. His head follows the puck (HeadOnPuck); once set his Stance Life quietens and he sits lower (StillOnceSet). The
    /// skate-in shuffles are SkateInShuffles. Shown only: nothing here reaches the race, and each piece hands over to the
    /// Save Reaction as it comes in (set, 0 Ready Stance .. 1 reacted), so a fully reacted pose is what the Shot measured.
    /// </summary>
    public static class BeforeTheShot
    {
        /// <summary>The head finds the puck over this long as the shooter starts the skate-in (real seconds).</summary>
        public const double FindPuckSeconds = 0.5;

        /// <summary>
        /// How set he is (0..1): 0 on the skate-in, easing to 1 through the slow-down, 1 in the window; from the Shot on it
        /// eases back to 0 over the result hold (so with no Shot he comes back to life before the next skate-in). 0 before and
        /// after a Round.
        /// </summary>
        public static double Settled(RoundPhase phase, double phaseSeconds, RoundSettings settings)
        {
            switch (phase)
            {
                case RoundPhase.SlowDown: return Numbers.SmoothStep01(phaseSeconds / settings.SlowDownSeconds);
                case RoundPhase.ShootingWindow: return 1;
                case RoundPhase.ResultHold: return 1 - Numbers.SmoothStep01(phaseSeconds / settings.ResultHoldSeconds);
                default: return 0;
            }
        }

        /// <summary>
        /// How fully his head is on the puck (0..1) before the Save Reaction: finding it over FindPuckSeconds of the skate-in,
        /// on it through the slow-down and the window, letting go over the result hold. 0 before and after a Round.
        /// </summary>
        public static double Watching(RoundPhase phase, double phaseSeconds, RoundSettings settings)
        {
            switch (phase)
            {
                case RoundPhase.SkateIn: return Numbers.SmoothStep01(phaseSeconds / FindPuckSeconds);
                case RoundPhase.SlowDown:
                case RoundPhase.ShootingWindow: return 1;
                case RoundPhase.ResultHold: return 1 - Numbers.SmoothStep01(phaseSeconds / settings.ResultHoldSeconds);
                default: return 0;
            }
        }

        /// <summary>How far (0..1) the head turns to the puck: watching, as the reaction has not yet taken over. 0 with HeadOnPuck off.</summary>
        public static double HeadWeight(double watching, double set, RoundSettings settings) =>
            settings.HeadOnPuck ? Numbers.Clamp01(watching) * (1 - Numbers.Clamp01(set)) : 0;

        /// <summary>The share of Stance Life he shows (1 all of it): SetStillness of it goes once set. 1 with StillOnceSet off.</summary>
        public static double LifeShare(double settled, double set, RoundSettings settings) =>
            settings.StillOnceSet ? 1 - settings.SetStillness * Numbers.Clamp01(settled) * (1 - Numbers.Clamp01(set)) : 1;

        /// <summary>How much lower he sits (metres): SetDropMetres once set. 0 with StillOnceSet off.</summary>
        public static double DropMetres(double settled, double set, RoundSettings settings) =>
            settings.StillOnceSet ? settings.SetDropMetres * Numbers.Clamp01(settled) * (1 - Numbers.Clamp01(set)) : 0;
    }
}
