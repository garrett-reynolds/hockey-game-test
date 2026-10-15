using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>The words of the lower third: the tally ("GOALS 1  ·  SHOTS 2"), one gold stat, and the cap ("SHOT 2 OF 3").</summary>
    public readonly struct LowerThirdText
    {
        public string Tally { get; }
        public string Stat { get; }
        public string ShotOf { get; }
        public LowerThirdText(string tally, string stat, string shotOf) { Tally = tally; Stat = stat; ShotOf = shotOf; }
    }

    /// <summary>
    /// The lower third (#141, look A): a TV-style bar along the bottom with true numbers about the player's Round. What it says (Choose)
    /// and when it is on screen (Shown) are decided here; the game only draws it.
    /// <para>
    /// It is on screen the whole Round: while the player aims, while the puck flies, through the result and the Replay (on or off). The
    /// stat changes in place at each Shot's Whistle, when the Round gets the result. It is not on the Start or End screen, and not in Test
    /// Mode (#157), where the Score Box already counts the visit's Goals and Shots.
    /// </para>
    /// It says nothing about the player's best score (#151: people play once, they never need to beat it).
    /// </summary>
    public static class LowerThird
    {
        /// <summary>This many Goals in a row, ending with the last Shot, is a streak worth the stat.</summary>
        public const int StreakToShow = 3;
        /// <summary>The stat before the Round's first result.</summary>
        public const string FirstShot = "First Shot";
        /// <summary>Between two numbers on one line.</summary>
        public const string Separator = "  ·  ";

        /// <summary>
        /// The words after the Shots so far (results in order; TooLate counts as a Shot; speedsKmh has one entry per result, null for a Shot
        /// with no puck), during Shot shotNumber of shotCount. The stat is, in order: FirstShot before any result; a streak of StreakToShow or
        /// more Goals ending now ("3 in a row!"); the last Shot's speed, with the average of the Shots that have one once there are two;
        /// the average alone when the last Shot had no speed; with no speed at all, the Shots left.
        /// </summary>
        public static LowerThirdText Choose(IReadOnlyList<ShotResultKind> results, IReadOnlyList<double?> speedsKmh, int shotNumber, int shotCount)
        {
            int shots = results.Count, goals = 0, streak = 0;
            foreach (ShotResultKind kind in results)
                if (kind == ShotResultKind.Goal) goals++;
            for (int i = shots - 1; i >= 0 && results[i] == ShotResultKind.Goal; i--)
                streak++;

            string tally = "GOALS " + goals + Separator + "SHOTS " + shots;
            string shotOf = "SHOT " + Math.Max(1, Math.Min(shotNumber, shotCount)) + " OF " + shotCount;
            return new LowerThirdText(tally, StatOf(speedsKmh, shots, streak, shotCount), shotOf);
        }

        static string StatOf(IReadOnlyList<double?> speedsKmh, int shots, int streak, int shotCount)
        {
            if (shots == 0) return FirstShot;
            if (streak >= StreakToShow) return streak + " in a row!";

            double total = 0;
            int counted = 0;
            for (int i = 0; i < shots && i < speedsKmh.Count; i++)
                if (speedsKmh[i].HasValue) { total += speedsKmh[i].Value; counted++; }
            double? last = shots <= speedsKmh.Count ? speedsKmh[shots - 1] : null;

            if (counted == 0)
            {
                int left = shotCount - shots;
                return left > 0 ? left + " " + (left == 1 ? "Shot" : "Shots") + " left" : "Round over";
            }
            string average = ShotResultText.Speed(total / counted);
            if (!last.HasValue) return "Average " + average;
            string lastShot = "Last Shot " + ShotResultText.Speed(last.Value);
            return counted == 1 ? lastShot : lastShot + Separator + "average " + average;
        }

        /// <summary>The words now, from the Round. Builds them every call; the game asks LowerThirdWords, which keeps them.</summary>
        public static LowerThirdText Of(Round round) => Choose(round.Results, round.ShotSpeedsKmh, round.ShotNumber, round.ShotCount);

        /// <summary>True while the bar is on screen: every moment of the Playing screen, except in Test Mode.</summary>
        public static bool Shown(ScreenFlow flow) => flow.Screen == FlowScreen.Playing && !flow.TestMode;
    }

    /// <summary>
    /// The lower third's words, built only when the Round's numbers change (a result, its speed, the Shot number) and kept otherwise:
    /// asking again for the same numbers gives the same strings and allocates nothing (the bar asks every frame).
    /// </summary>
    public sealed class LowerThirdWords
    {
        readonly List<ShotResultKind> _results = new List<ShotResultKind>();
        readonly List<double?> _speeds = new List<double?>();
        int _shotNumber, _shotCount;
        LowerThirdText _text;
        bool _built;

        /// <summary>How many times the words were built (for tests).</summary>
        public int Builds { get; private set; }

        public LowerThirdText Get(Round round) => Get(round.Results, round.ShotSpeedsKmh, round.ShotNumber, round.ShotCount);

        public LowerThirdText Get(IReadOnlyList<ShotResultKind> results, IReadOnlyList<double?> speedsKmh, int shotNumber, int shotCount)
        {
            if (!_built || shotNumber != _shotNumber || shotCount != _shotCount || !Same(results, speedsKmh))
            {
                _results.Clear();
                _speeds.Clear();
                for (int i = 0; i < results.Count; i++)
                {
                    _results.Add(results[i]);
                    _speeds.Add(i < speedsKmh.Count ? speedsKmh[i] : null);
                }
                _shotNumber = shotNumber;
                _shotCount = shotCount;
                _text = LowerThird.Choose(results, speedsKmh, shotNumber, shotCount);
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
