using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>The words of a lower third: a small label over one big line.</summary>
    public readonly struct LowerThirdText
    {
        public string Kicker { get; }
        public string Headline { get; }
        public LowerThirdText(string kicker, string headline) { Kicker = kicker; Headline = headline; }
    }

    /// <summary>The lower third this frame: how far it has slid in (0 off screen .. 1 in place) and its words.</summary>
    public readonly struct LowerThirdShow
    {
        public double Amount { get; }
        public LowerThirdText Text { get; }
        public LowerThirdShow(double amount, LowerThirdText text) { Amount = amount; Text = text; }
    }

    /// <summary>
    /// The lower third (#141): a TV-style bar with a true number about the player's Round, after each Shot. Which number
    /// (Choose) and when it is on screen (SlideAmount, Of) are decided here; the game only draws it.
    /// <para>
    /// It shows in the first part of the result hold, once the result shows (the Whistle): it slides in InDelaySeconds after
    /// the Whistle, holds, and is out by OutBySeconds, which is not later than the camera looking up at the Video Board for
    /// the Replay (ReplayTiming.OnNetSeconds). The timing never asks whether a Replay follows, so a Round with the Replay turned
    /// off (#145) shows it just the same. It is never on screen while the player aims or the puck flies (no result shows).
    /// </para>
    /// Not shown: Shot speed and where the Shot went. The Round keeps only the last Shot's speed, so "fastest of the Round"
    /// has no data; the speed of the last Shot is already on screen with the result word.
    /// </summary>
    public static class LowerThird
    {
        /// <summary>This many Goals in a row, ending with the last Shot, is a streak worth a lower third.</summary>
        public const int StreakToShow = 3;
        /// <summary>Real seconds after the Whistle that the slide in starts; the slide takes SlideSeconds; and it is fully out by OutBySeconds.</summary>
        public const double InDelaySeconds = 0.2, SlideSeconds = 0.25, OutBySeconds = ReplayTiming.OnNetSeconds;

        /// <summary>
        /// The most interesting number after the Shots so far (results in order; TooLate counts as a Shot). In this order:
        /// a streak of StreakToShow or more Goals ending now ("3 in a row!"); a chance to beat bestScore (the Goals still
        /// needed fit in the Shots left); otherwise the tally ("2 Goals from 3 Shots"). Null before the first result.
        /// </summary>
        public static LowerThirdText? Choose(IReadOnlyList<ShotResultKind> results, int bestScore, int shotsPerRound)
        {
            int shots = results.Count;
            if (shots == 0) return null;

            int goals = 0;
            foreach (ShotResultKind kind in results)
                if (kind == ShotResultKind.Goal) goals++;

            int streak = 0;
            for (int i = shots - 1; i >= 0 && results[i] == ShotResultKind.Goal; i--)
                streak++;
            if (streak >= StreakToShow)
                return new LowerThirdText("ON FIRE", streak + " in a row!");

            int needed = bestScore + 1 - goals;
            if (bestScore > 0 && needed >= 1 && needed <= shotsPerRound - shots)
                return new LowerThirdText("CHASING YOUR BEST", needed + " more " + Plural(needed, "Goal") + " to beat your best of " + bestScore);

            return new LowerThirdText("YOUR ROUND", goals + " " + Plural(goals, "Goal") + " from " + shots + " " + Plural(shots, "Shot"));
        }

        static string Plural(int n, string word) => n == 1 ? word : word + "s";

        /// <summary>How far in (0 off screen .. 1 in place, eased) the lower third is, secondsSinceWhistle real seconds after the Whistle.</summary>
        public static double SlideAmount(double secondsSinceWhistle)
        {
            double t = secondsSinceWhistle - InDelaySeconds;
            double slideIn = Numbers.SmoothStep01(t / SlideSeconds);
            double slideOut = Numbers.SmoothStep01((secondsSinceWhistle - (OutBySeconds - SlideSeconds)) / SlideSeconds);
            return slideIn * (1 - slideOut);
        }

        /// <summary>The lower third now, from the Round: off (Amount 0, no words) until a result shows, and after the first part of the result hold.</summary>
        public static LowerThirdShow Of(Round round)
        {
            if (round.Phase != RoundPhase.ResultHold || !round.ResultShowing)
                return default;
            double amount = SlideAmount(round.PhaseSecondsElapsed - round.ResultDelaySeconds);
            if (amount <= 0) return default; // off screen: no words built (this runs every frame)
            LowerThirdText? text = Choose(round.Results, round.BestScore, Round.ShotsPerRound);
            if (!text.HasValue) return default;
            return new LowerThirdShow(amount, text.Value);
        }
    }
}
