using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Stance Life (#44): the goalie's breathing, weight shifts and stick taps in the Ready Stance, as a pure function of a
    /// clock. Each part is a share of its full size (GoalieCharacter turns them into degrees and metres).
    /// </summary>
    public class StanceLifeTests
    {
        const double Frame = 1.0 / 60.0;

        [Test]
        public void At_time_zero_the_goalie_shows_the_plain_Ready_Stance()
        {
            StanceLifeMoment m = StanceLife.At(0);
            Assert.AreEqual(0, m.Breath, 1e-9);
            Assert.AreEqual(0, m.Shift, 1e-9);
            Assert.AreEqual(0, m.Tap, 1e-9);
        }

        [Test]
        public void Every_part_stays_within_its_bounds_and_never_jumps()
        {
            StanceLifeMoment last = StanceLife.At(0);
            for (int i = 1; i < 60 * 600; i++)
            {
                StanceLifeMoment m = StanceLife.At(i * Frame);
                Assert.That(m.Breath, Is.InRange(0.0, 1.0), "breath at " + i * Frame);
                Assert.That(m.Shift, Is.InRange(-1.0, 1.0), "shift at " + i * Frame);
                Assert.That(m.Tap, Is.InRange(0.0, 1.0), "tap at " + i * Frame);
                Assert.Less(System.Math.Abs(m.Breath - last.Breath), 0.02, "breath moves smoothly at " + i * Frame);
                Assert.Less(System.Math.Abs(m.Shift - last.Shift), 0.02, "shift moves smoothly at " + i * Frame);
                Assert.Less(System.Math.Abs(m.Tap - last.Tap), 0.25, "a tap is quick but never a snap at " + i * Frame);
                last = m;
            }
        }

        [Test]
        public void The_goalie_breathes_shifts_his_weight_both_ways_and_taps_his_stick_now_and_then()
        {
            double breathLow = 1, breathHigh = 0, shiftLow = 1, shiftHigh = -1;
            int taps = 0;
            bool up = false;
            for (int i = 0; i < 60 * 60; i++)
            {
                StanceLifeMoment m = StanceLife.At(i * Frame);
                breathLow = System.Math.Min(breathLow, m.Breath);
                breathHigh = System.Math.Max(breathHigh, m.Breath);
                shiftLow = System.Math.Min(shiftLow, m.Shift);
                shiftHigh = System.Math.Max(shiftHigh, m.Shift);
                if (!up && m.Tap > 0.5) taps++; // each touch lifts the blade past half way once
                up = m.Tap > 0.5;
            }
            Assert.Less(breathLow, 0.05, "breathes out fully");
            Assert.Greater(breathHigh, 0.95, "breathes in fully");
            Assert.Less(shiftLow, -0.7, "onto one skate");
            Assert.Greater(shiftHigh, 0.7, "onto the other");
            Assert.That(taps, Is.InRange(8, 30), "a few stick taps a minute (each tap is two touches)");
        }

        [Test]
        public void The_same_moment_always_looks_the_same()
        {
            foreach (double t in new[] { 0.37, 4.2, 17.9, 123.45 })
            {
                StanceLifeMoment a = StanceLife.At(t), b = StanceLife.At(t);
                Assert.AreEqual(a.Breath, b.Breath);
                Assert.AreEqual(a.Shift, b.Shift);
                Assert.AreEqual(a.Tap, b.Tap);
            }
        }
    }
}
