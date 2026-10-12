using System;
using System.Collections.Generic;
using System.Linq;

namespace HockeyGame.Core
{
    /// <summary>Which of the frame budget's limits a moment is over.</summary>
    public readonly struct MomentVerdict
    {
        public readonly bool GpuOver, CpuOver, CrowdOver;

        public MomentVerdict(bool gpuOver, bool cpuOver, bool crowdOver)
        {
            GpuOver = gpuOver;
            CpuOver = cpuOver;
            CrowdOver = crowdOver;
        }

        /// <summary>Under every limit.</summary>
        public bool Within => !GpuOver && !CpuOver && !CrowdOver;
    }

    /// <summary>
    /// The 60 fps frame budget (#84, #97): in the worst moments (the Replay after a Goal and the skate-in) the slowest 1% of
    /// frames stay under 12 ms GPU and 8 ms CPU (main thread), and the crowd costs under 2 ms GPU. Judged on the dev PC; once
    /// on the event PC before the event. When a moment is over, cut in CutOrder, first item first. Never cut the Calm Area test
    /// or the goalie.
    /// </summary>
    public static class FrameBudget
    {
        public const double GpuMs = 12.0, CpuMs = 8.0, CrowdGpuMs = 2.0;

        /// <summary>What to cut, in order, when a moment is over budget.</summary>
        public static readonly IReadOnlyList<string> CutOrder = new[]
        {
            "far fans to cards",
            "lower Replay and board picture size",
            "bake the ice reflection once",
            "fewer roof-light shadows",
            "less far-end detail",
            "softer post-processing",
        };

        /// <summary>The mean of the slowest 1% of frame times (at least the one slowest).</summary>
        public static double Slowest1Pct(IEnumerable<double> frameMs)
        {
            List<double> sorted = frameMs.OrderByDescending(v => v).ToList();
            if (sorted.Count == 0) throw new ArgumentException("No frames.", nameof(frameMs));
            return sorted.Take(Math.Max(1, sorted.Count / 100)).Average();
        }

        /// <summary>The middle value (the mean of the middle two for an even count).</summary>
        public static double Median(IEnumerable<double> values)
        {
            List<double> sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            if (n == 0) throw new ArgumentException("No values.", nameof(values));
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
        }

        /// <summary>A moment's verdict from its slowest-1% GPU and CPU ms and the crowd's GPU cost in it: over at or above a limit.</summary>
        public static MomentVerdict Judge(double gpuSlowest1PctMs, double cpuSlowest1PctMs, double crowdGpuMs) =>
            new MomentVerdict(gpuSlowest1PctMs >= GpuMs, cpuSlowest1PctMs >= CpuMs, crowdGpuMs >= CrowdGpuMs);
    }
}
