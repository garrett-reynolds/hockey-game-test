using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>The words of a lower third: a small label, one big line, and a small speed line (empty when no Shot had a speed).</summary>
    public readonly struct LowerThirdText
    {
        public string Kicker { get; }
        public string Headline { get; }
        public string SpeedLine { get; }
        public LowerThirdText(string kicker, string headline, string speedLine = "") { Kicker = kicker; Headline = headline; SpeedLine = speedLine ?? ""; }
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
    /// the Whistle, holds, and is out when the camera looks up at the Video Board for the Replay (ReplayTiming.OnNetSeconds,
    /// the Arena Show's timing: there is no copy of that time here). The timing never asks whether a Replay follows, so a Round
    /// with the Replay turned off (#145) shows it just the same. It is never on screen while the player aims or the puck flies.
    /// </para>
    /// It says nothing about the player's best score (#151: people play once, they never need to beat it).
    /// </summary>
    public static class LowerThird
    {
        /// <summary>This many Goals in a row, ending with the last Shot, is a streak worth a lower third.</summary>
        public const int StreakToShow = 3;
        /// <summary>Real seconds after the Whistle that the slide in starts; the slide takes SlideSeconds. It is fully out at ReplayTiming.OnNetSeconds.</summary>
        public const double InDelaySeconds = 0.2, SlideSeconds = 0.25;

        /// <summary>Between the Shot's speed and the Round's average on the speed line.</summary>
        public const string SpeedSeparator = "  |  ";

        /// <summary>
        /// The words after the Shots so far (results in order; TooLate counts as a Shot; speedsKmh has one entry per result, null for a
        /// Shot with no puck). The big line is a streak of StreakToShow or more Goals ending now ("3 in a row!"), otherwise the tally
        /// ("2 Goals from 3 Shots"). The speed line is the last Shot's speed and the average of the Shots that have one. Null before the first result.
        /// </summary>
        public static LowerThirdText? Choose(IReadOnlyList<ShotResultKind> results, IReadOnlyList<double?> speedsKmh)
        {
            int shots = results.Count;
            if (shots == 0) return null;

            int goals = 0;
            foreach (ShotResultKind kind in results)
                if (kind == ShotResultKind.Goal) goals++;

            int streak = 0;
            for (int i = shots - 1; i >= 0 && results[i] == ShotResultKind.Goal; i--)
                streak++;

            string speedLine = SpeedLine(speedsKmh, shots);
            if (streak >= StreakToShow)
                return new LowerThirdText("ON FIRE", streak + " in a row!", speedLine);
            return new LowerThirdText("YOUR ROUND", goals + " " + Plural(goals, "Goal") + " from " + shots + " " + Plural(shots, "Shot"), speedLine);
        }

        // "92 km/h  |  average 85 km/h"; the first part is left out when the last Shot has no speed; empty when no Shot has one.
        static string SpeedLine(IReadOnlyList<double?> speedsKmh, int shots)
        {
            double total = 0;
            int counted = 0;
            for (int i = 0; i < shots && i < speedsKmh.Count; i++)
                if (speedsKmh[i].HasValue) { total += speedsKmh[i].Value; counted++; }
            if (counted == 0) return "";
            string average = "average " + ShotResultText.Speed(total / counted);
            double? last = shots <= speedsKmh.Count ? speedsKmh[shots - 1] : null;
            return last.HasValue ? ShotResultText.Speed(last.Value) + SpeedSeparator + average : average;
        }

        static string Plural(int n, string word) => n == 1 ? word : word + "s";

        /// <summary>How far in (0 off screen .. 1 in place, eased) the lower third is, secondsSinceWhistle real seconds after the Whistle.</summary>
        public static double SlideAmount(double secondsSinceWhistle)
        {
            double t = secondsSinceWhistle - InDelaySeconds;
            double slideIn = Numbers.SmoothStep01(t / SlideSeconds);
            double slideOut = Numbers.SmoothStep01((secondsSinceWhistle - (ReplayTiming.OnNetSeconds - SlideSeconds)) / SlideSeconds);
            return slideIn * (1 - slideOut);
        }

        /// <summary>The lower third now, from the Round: off (Amount 0, no words) until a result shows, and after the first part of the result hold. Builds its words every call.</summary>
        public static LowerThirdShow Of(Round round) => Of(round, null);

        /// <summary>As Of, but the words come from words (built once per Shot, no garbage per frame); null builds them every call.</summary>
        public static LowerThirdShow Of(Round round, LowerThirdWords words)
        {
            if (round.Phase != RoundPhase.ResultHold || !round.ResultShowing)
                return default;
            double amount = SlideAmount(round.PhaseSecondsElapsed - round.ResultDelaySeconds);
            if (amount <= 0) return default; // off screen: no words built (this runs every frame)
            LowerThirdText? text = words != null ? words.Get(round.Results, round.ShotSpeedsKmh) : Choose(round.Results, round.ShotSpeedsKmh);
            if (!text.HasValue) return default;
            return new LowerThirdShow(amount, text.Value);
        }
    }

    /// <summary>
    /// The lower third's words, built once when a Shot's result is in and kept while the Round's results stay the same: asking again
    /// for the same results gives the same strings and allocates nothing (the bar asks every frame it is on screen).
    /// </summary>
    public sealed class LowerThirdWords
    {
        readonly List<ShotResultKind> _results = new List<ShotResultKind>();
        readonly List<double?> _speeds = new List<double?>();
        LowerThirdText? _text;
        bool _built;

        /// <summary>How many times the words were built (for tests).</summary>
        public int Builds { get; private set; }

        public LowerThirdText? Get(IReadOnlyList<ShotResultKind> results, IReadOnlyList<double?> speedsKmh)
        {
            if (!_built || !Same(results, speedsKmh))
            {
                _results.Clear();
                _speeds.Clear();
                for (int i = 0; i < results.Count; i++)
                {
                    _results.Add(results[i]);
                    _speeds.Add(i < speedsKmh.Count ? speedsKmh[i] : null);
                }
                _text = LowerThird.Choose(results, speedsKmh);
                _built = true;
                Builds++;
            }
            return _text;
        }

        bool Same(IReadOnlyList<ShotResultKind> results, IReadOnlyList<double?> speedsKmh)
        {
            if (results.Count != _results.Count) return false;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i] != _results[i]) return false;
                if ((i < speedsKmh.Count ? speedsKmh[i] : null) != _speeds[i]) return false;
            }
            return true;
        }
    }
}
