using System.Linq;
using HockeyGame.Editor;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The frame budget (#97): the slowest 1% of frames, the limits for each moment and the cut order.</summary>
    public class FrameBudgetTests
    {
        [Test]
        public void The_limits_are_12_ms_GPU_8_ms_CPU_and_2_ms_for_the_crowd()
        {
            Assert.AreEqual(12.0, FrameBudget.GpuMs);
            Assert.AreEqual(8.0, FrameBudget.CpuMs);
            Assert.AreEqual(2.0, FrameBudget.CrowdGpuMs);
        }

        [Test]
        public void The_slowest_1_percent_is_the_mean_of_the_slowest_hundredth_of_the_frames()
        {
            double[] frames = Enumerable.Repeat(5.0, 198).Concat(new[] { 20.0, 30.0 }).ToArray();
            Assert.AreEqual(25.0, FrameBudget.Slowest1Pct(frames), 1e-9);
        }

        [Test]
        public void With_fewer_than_100_frames_the_slowest_1_percent_is_the_slowest_frame()
        {
            Assert.AreEqual(9.0, FrameBudget.Slowest1Pct(new[] { 3.0, 9.0, 4.0 }), 1e-9);
        }

        [Test]
        public void The_median_is_the_middle_value_or_the_mean_of_the_middle_two()
        {
            Assert.AreEqual(4.0, FrameBudget.Median(new[] { 9.0, 1.0, 4.0 }), 1e-9);
            Assert.AreEqual(5.0, FrameBudget.Median(new[] { 9.0, 1.0, 4.0, 6.0 }), 1e-9);
        }

        [Test]
        public void A_moment_under_every_limit_is_within_budget()
        {
            MomentVerdict verdict = FrameBudget.Judge(11.9, 7.9, 1.9);
            Assert.IsTrue(verdict.Within);
            Assert.IsFalse(verdict.GpuOver || verdict.CpuOver || verdict.CrowdOver);
        }

        [Test]
        public void A_moment_at_or_over_a_limit_is_over_on_that_limit_only()
        {
            MomentVerdict gpu = FrameBudget.Judge(12.0, 1, 0);
            Assert.IsTrue(gpu.GpuOver);
            Assert.IsFalse(gpu.CpuOver || gpu.CrowdOver);
            Assert.IsFalse(gpu.Within);

            Assert.IsTrue(FrameBudget.Judge(1, 8.5, 0).CpuOver);
            Assert.IsTrue(FrameBudget.Judge(1, 1, 2.0).CrowdOver);
        }

        [Test]
        public void The_cut_order_starts_with_far_fans_to_cards_and_ends_with_softer_post_processing()
        {
            Assert.AreEqual(6, FrameBudget.CutOrder.Count);
            StringAssert.Contains("far fans", FrameBudget.CutOrder[0]);
            StringAssert.Contains("Replay", FrameBudget.CutOrder[1]);
            StringAssert.Contains("post-processing", FrameBudget.CutOrder[5]);
        }

        [Test]
        public void The_report_gives_each_moment_its_numbers_and_says_over_when_over()
        {
            var result = new FrameBudgetTool.Result { Gpu = "Test GPU", Width = 1920, Height = 1080, Within = false };
            result.Moments.Add(new FrameBudgetTool.Moment
            {
                Name = "skate-in", Frames = 600, GpuSlowest1PctMs = 9.5, CpuSlowest1PctMs = 3.25, CrowdCostMs = 1.0,
                GpuOver = false, CpuOver = false, CrowdOver = false
            });
            result.Moments.Add(new FrameBudgetTool.Moment
            {
                Name = "Replay after a Goal", Frames = 900, GpuSlowest1PctMs = 14.0, CpuSlowest1PctMs = 4.0, CrowdCostMs = 2.5,
                GpuOver = true, CpuOver = false, CrowdOver = true
            });

            string text = FrameBudgetTool.Markdown(result);

            StringAssert.Contains("Test GPU", text);
            StringAssert.Contains("| skate-in | 600 | 9.50 | 3.25 | 1.00 | within |", text);
            StringAssert.Contains("| Replay after a Goal | 900 | 14.00 | 4.00 | 2.50 | over: GPU, crowd |", text);
            StringAssert.Contains("far fans", text);
        }
    }
}
