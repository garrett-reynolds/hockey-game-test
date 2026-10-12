using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HockeyGame.Editor
{
    /// <summary>One view's measures (PictureMeasures): saved in the baseline file and in each run's file.</summary>
    [Serializable]
    public sealed class ViewMeasures
    {
        public string View;
        /// <summary>Calm Area contrast: the spread of brightness, 0 to 0.5. Higher is worse.</summary>
        public double Contrast;
        /// <summary>Calm Area busy-ness: the share of its pixels on an edge, 0 to 1. Higher is worse.</summary>
        public double Edges;
        /// <summary>Gap stand-out: the brightness difference between the Gaps and the gear, 0 to 1. Lower is worse.</summary>
        public double GapStandOut;
        /// <summary>How many Net Face cells were Gaps (for reading the numbers; not checked).</summary>
        public int GapCells;
    }

    /// <summary>Every view's measures, and the picture size they were rendered at.</summary>
    [Serializable]
    public sealed class PictureMeasureSet
    {
        public int Width, Height;
        public List<ViewMeasures> Views = new List<ViewMeasures>();
    }

    /// <summary>One measure of one view against the baseline.</summary>
    [Serializable]
    public sealed class MeasureCheck
    {
        public string View, Measure;
        public double Baseline, Now;
        /// <summary>(Now - Baseline) / Baseline, in percent.</summary>
        public double ChangePercent;
        /// <summary>How much worse than the baseline, in percent (negative: better).</summary>
        public double WorsePercent;
        public bool Fails;
    }

    /// <summary>How much of one view's picture changed from the baseline picture (for reading only; not checked).</summary>
    [Serializable]
    public sealed class PictureChange
    {
        public string View;
        public double PixelsChangedPercent;
    }

    /// <summary>The picture test's answer: pass or fail, each measure against the baseline, and anything that stopped a check.</summary>
    [Serializable]
    public sealed class PictureTestResult
    {
        public bool Pass;
        public string BaselineFolder;
        public List<MeasureCheck> Checks = new List<MeasureCheck>();
        public List<PictureChange> Pictures = new List<PictureChange>();
        public List<string> Problems = new List<string>();
    }

    /// <summary>
    /// The picture test's fail rule (#84, #85): it fails when any measure of any view is more than FailPercent worse than the
    /// baseline, or when a view is missing on either side, or the picture size differs. Exactly FailPercent worse passes.
    /// </summary>
    public static class PictureCompare
    {
        public const double FailPercent = 10;
        public const string ContrastName = "Calm Area contrast", EdgesName = "Calm Area edges", GapStandOutName = "Gap stand-out";

        /// <summary>Which way a measure gets worse.</summary>
        public enum Worse { Higher, Lower }

        /// <summary>
        /// How much worse now is than the baseline, in percent of the baseline (negative: better). From a baseline of 0, any
        /// change the worse way is infinitely worse and any other is 0.
        /// </summary>
        public static double WorsePercent(double baseline, double now, Worse worse)
        {
            double worseBy = worse == Worse.Higher ? now - baseline : baseline - now;
            if (baseline == 0) return worseBy > 0 ? double.PositiveInfinity : 0;
            return 100.0 * worseBy / Math.Abs(baseline);
        }

        /// <summary>True when worsePercent is more than FailPercent (rounding error aside: 10% worse passes).</summary>
        public static bool Fails(double worsePercent) => worsePercent > FailPercent + 1e-9;

        public static PictureTestResult Compare(PictureMeasureSet baseline, PictureMeasureSet now)
        {
            var result = new PictureTestResult();
            if (baseline.Width != now.Width || baseline.Height != now.Height)
                result.Problems.Add("Picture size " + now.Width + "x" + now.Height + " differs from the baseline's " +
                                    baseline.Width + "x" + baseline.Height + ".");
            foreach (ViewMeasures view in now.Views)
            {
                ViewMeasures before = baseline.Views.Find(v => v.View == view.View);
                if (before == null)
                {
                    result.Problems.Add("View " + view.View + " has no baseline.");
                    continue;
                }
                result.Checks.Add(Check(view.View, ContrastName, before.Contrast, view.Contrast, Worse.Higher));
                result.Checks.Add(Check(view.View, EdgesName, before.Edges, view.Edges, Worse.Higher));
                result.Checks.Add(Check(view.View, GapStandOutName, before.GapStandOut, view.GapStandOut, Worse.Lower));
            }
            foreach (ViewMeasures before in baseline.Views)
                if (!now.Views.Exists(v => v.View == before.View))
                    result.Problems.Add("View " + before.View + " is in the baseline but was not rendered.");
            result.Pass = result.Problems.Count == 0 && !result.Checks.Exists(c => c.Fails);
            return result;
        }

        static MeasureCheck Check(string view, string measure, double baseline, double now, Worse worse)
        {
            double worsePercent = WorsePercent(baseline, now, worse);
            return new MeasureCheck
            {
                View = view, Measure = measure, Baseline = baseline, Now = now,
                ChangePercent = baseline == 0 ? (now == 0 ? 0 : double.PositiveInfinity * Math.Sign(now)) : 100.0 * (now - baseline) / Math.Abs(baseline),
                WorsePercent = worsePercent,
                Fails = Fails(worsePercent)
            };
        }

        /// <summary>The result as lines to read: PASS or FAIL, then each measure against the baseline.</summary>
        public static string Text(PictureTestResult result)
        {
            var text = new StringBuilder();
            text.AppendLine("Picture test: " + (result.Pass ? "PASS" : "FAIL") + " (fails when a measure is more than " +
                            Num(FailPercent) + "% worse than the baseline)");
            foreach (string problem in result.Problems) text.AppendLine("  Problem: " + problem);
            foreach (MeasureCheck c in result.Checks)
                text.AppendLine("  " + (c.Fails ? "FAIL " : "ok   ") + c.View + "  " + c.Measure + ": " + Num(c.Now) + " vs baseline " +
                                Num(c.Baseline) + " (" + Signed(c.ChangePercent) + "%, " + Signed(c.WorsePercent) + "% worse)");
            foreach (PictureChange p in result.Pictures)
                text.AppendLine("  picture " + p.View + ": " + Num(p.PixelsChangedPercent) + "% of pixels changed");
            return text.ToString();
        }

        static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        static string Signed(double v) => (v > 0 ? "+" : "") + v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
