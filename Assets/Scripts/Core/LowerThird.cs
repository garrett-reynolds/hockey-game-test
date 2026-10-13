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

    /// <summary>The lower third this frame: whether it is on screen (Amount 1; 0 off) and its words. It never slides.</summary>
    public readonly struct LowerThirdShow
    {
        public double Amount { get; }
        public LowerThirdText Text { get; }
        public LowerThirdShow(double amount, LowerThirdText text) { Amount = amount; Text = text; }
    }

    /// <summary>
    /// The lower third (#141): a TV-style bar with a true number about the player's Round. Which number
    /// (Choose) and when it is on screen (Of) are decided here; the game only draws it.
    /// <para>
    /// It stays on screen, in the bottom-left corner, all through a Round (Garrett, 2026-10-13: "It should always be on the screen in
    /// the bottom left corner"): no slide in or out. Only its words change, with each Shot's result (before the first, the tally is
    /// Fresh). It is off on the start and end screens and in Test Mode.
    /// </para>
    /// It says nothing about the player's best score (#151: people play once, they never need to beat it).
    /// </summary>
    public static class LowerThird
    {
        /// <summary>This many Goals in a row, ending with the last Shot, is a streak worth a lower third.</summary>
        public const int StreakToShow = 3;

        /// <summary>The words before the first Shot's result: the tally with nothing in it yet.</summary>
        public static readonly LowerThirdText Fresh = new LowerThirdText("YOUR ROUND", "0 Goals from 0 Shots");
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

        /// <summary>The lower third now, from the Round: on (Amount 1) all through a Round, off (Amount 0, no words) on the start and end screens and in Test Mode. Builds its words every call.</summary>
        public static LowerThirdShow Of(Round round) => Of(round, null);

        /// <summary>As Of, but the words come from words (built once per Shot, no garbage per frame); null builds them every call.</summary>
        public static LowerThirdShow Of(Round round, LowerThirdWords words)
        {
            if (round.TestMode || round.Phase == RoundPhase.Idle || round.Phase == RoundPhase.RoundOver)
                return default; // none in Test Mode (#157): there is no Round to tally
            LowerThirdText? text = words != null ? words.Get(round.Results, round.ShotSpeedsKmh) : Choose(round.Results, round.ShotSpeedsKmh);
            return new LowerThirdShow(1.0, text ?? Fresh);
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
