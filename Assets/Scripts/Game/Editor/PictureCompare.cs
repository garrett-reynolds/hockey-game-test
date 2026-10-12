using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HockeyGame.Editor
{
    /// <summary>One Gap's stand-out from the gear in one view (PictureMeasures.GapStandOut).</summary>
    [Serializable]
    public sealed class GapMeasure
    {
        /// <summary>The Gap's name, from its first cell (PictureMeasures.GapName), for example "Gap c3r2".</summary>
        public string Gap;
        /// <summary>How many Net Face cells the Gap spans (for reading the numbers; not checked).</summary>
        public int Cells;
        /// <summary>The brightness difference between the Gap and the gear, 0 to 1. Lower is worse.</summary>
        public double StandOut;
    }

    /// <summary>One view's measures (PictureMeasures): saved in the baseline file and in each run's file.</summary>
    [Serializable]
    public sealed class ViewMeasures
    {
        public string View;
        /// <summary>Calm Area contrast: the spread of brightness, 0 to 0.5. Higher is worse.</summary>
        public double Contrast;
        /// <summary>Calm Area busy-ness: the share of its pixels on an edge, 0 to 1. Higher is worse.</summary>
        public double Edges;
        /// <summary>Each Gap's stand-out from the gear.</summary>
        public List<GapMeasure> Gaps = new List<GapMeasure>();
    }

    /// <summary>Every view's measures, and the picture size they were rendered at.</summary>
    [Serializable]
    public sealed class PictureMeasureSet
    {
        public int Width, Height;
        public List<ViewMeasures> Views = new List<ViewMeasures>();
    }

    /// <summary>One measure of one view against the baseline. Every number is finite.</summary>
    [Serializable]
    public sealed class MeasureCheck
    {
        public string View, Measure;
        public double Baseline, Now;
        /// <summary>False when the baseline is 0: no percent of it exists, so the percents are 0 and any worse change fails.</summary>
        public bool PercentApplies;
        /// <summary>(Now - Baseline) / Baseline, in percent (0 when PercentApplies is false).</summary>
        public double ChangePercent;
        /// <summary>How much worse than the baseline, in percent (negative: better; 0 when PercentApplies is false).</summary>
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
        /// <summary>Anything that kept a check from being made (a missing view, Gap, picture or number). Any problem fails.</summary>
        public List<string> Problems = new List<string>();
    }

    /// <summary>
    /// The picture test's fail rule (#84, #85): it fails when any measure of any view (each Gap's stand-out on its own) is
    /// more than FailPercent worse than the baseline, or when anything is missing on either side (a view, a Gap, a picture),
    /// a number is not finite, or the picture size differs. Exactly FailPercent worse passes. From a baseline of 0 any worse
    /// change fails. The result never holds an infinite or not-a-number value.
    /// </summary>
    public static class PictureCompare
    {
        public const double FailPercent = 10;
        public const string ContrastName = "Calm Area contrast", EdgesName = "Calm Area edges", GapStandOutName = "Gap stand-out";

        /// <summary>Which way a measure gets worse.</summary>
        public enum Worse { Higher, Lower }

        /// <summary>How much worse now is than the baseline, in percent of the baseline (negative: better); 0 from a baseline of 0.</summary>
        public static double WorsePercent(double baseline, double now, Worse worse) =>
            baseline == 0 ? 0 : 100.0 * WorseBy(baseline, now, worse) / Math.Abs(baseline);

        /// <summary>True when now is more than FailPercent worse than the baseline (rounding error aside: 10% passes); from 0, when any worse.</summary>
        public static bool Fails(double baseline, double now, Worse worse) =>
            baseline == 0 ? WorseBy(baseline, now, worse) > 0 : WorsePercent(baseline, now, worse) > FailPercent + 1e-9;

        static double WorseBy(double baseline, double now, Worse worse) => worse == Worse.Higher ? now - baseline : baseline - now;

        /// <summary>Adds a problem: the result then fails.</summary>
        public static void AddProblem(PictureTestResult result, string problem)
        {
            result.Problems.Add(problem);
            result.Pass = false;
        }

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
                Check(result, view.View, ContrastName, before.Contrast, view.Contrast, Worse.Higher);
                Check(result, view.View, EdgesName, before.Edges, view.Edges, Worse.Higher);
                foreach (GapMeasure gap in view.Gaps)
                {
                    GapMeasure gapBefore = before.Gaps.Find(g => g.Gap == gap.Gap);
                    if (gapBefore == null)
                        result.Problems.Add("View " + view.View + ": " + gap.Gap + " has no baseline.");
                    else
                        Check(result, view.View, GapStandOutName + " " + gap.Gap, gapBefore.StandOut, gap.StandOut, Worse.Lower);
                }
                foreach (GapMeasure gapBefore in before.Gaps)
                    if (!view.Gaps.Exists(g => g.Gap == gapBefore.Gap))
                        result.Problems.Add("View " + view.View + ": " + gapBefore.Gap + " is in the baseline but was not measured.");
            }
            foreach (ViewMeasures before in baseline.Views)
                if (!now.Views.Exists(v => v.View == before.View))
                    result.Problems.Add("View " + before.View + " is in the baseline but was not rendered.");
            result.Pass = result.Problems.Count == 0 && !result.Checks.Exists(c => c.Fails);
            return result;
        }

        static void Check(PictureTestResult result, string view, string measure, double baseline, double now, Worse worse)
        {
            if (!IsFinite(baseline) || !IsFinite(now))
            {
                result.Problems.Add("View " + view + ": " + measure + " is not a number (baseline " + Num(baseline) + ", now " + Num(now) + ").");
                return;
            }
            bool percentApplies = baseline != 0;
            result.Checks.Add(new MeasureCheck
            {
                View = view, Measure = measure, Baseline = baseline, Now = now,
                PercentApplies = percentApplies,
                ChangePercent = percentApplies ? 100.0 * (now - baseline) / Math.Abs(baseline) : 0,
                WorsePercent = WorsePercent(baseline, now, worse),
                Fails = Fails(baseline, now, worse)
            });
        }

        static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        /// <summary>The result as lines to read: PASS or FAIL, then each measure against the baseline.</summary>
        public static string Text(PictureTestResult result)
        {
            var text = new StringBuilder();
            text.AppendLine("Picture test: " + (result.Pass ? "PASS" : "FAIL") + " (fails when a measure is more than " +
                            Num(FailPercent) + "% worse than the baseline)");
            foreach (string problem in result.Problems) text.AppendLine("  Problem: " + problem);
            foreach (MeasureCheck c in result.Checks)
                text.AppendLine("  " + (c.Fails ? "FAIL " : "ok   ") + c.View + "  " + c.Measure + ": " + Num(c.Now) + " vs baseline " +
                                Num(c.Baseline) + (c.PercentApplies
                                    ? " (" + Signed(c.ChangePercent) + "%, " + Signed(c.WorsePercent) + "% worse)"
                                    : " (baseline 0: any worse change fails)"));
            foreach (PictureChange p in result.Pictures)
                text.AppendLine("  picture " + p.View + ": " + Num(p.PixelsChangedPercent) + "% of pixels changed");
            return text.ToString();
        }

        static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        static string Signed(double v) => (v > 0 ? "+" : "") + v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
