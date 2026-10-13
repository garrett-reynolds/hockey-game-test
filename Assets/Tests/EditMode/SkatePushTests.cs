using System;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class SkatePushTests
    {
        const double Tol = 1e-9;

        [Test]
        public void A_move_left_pushes_from_the_right_skate_and_a_move_right_from_the_left()
        {
            var s = new RoundSettings();
            Assert.AreEqual(Skate.Right, new SkatePush(-0.5, s).PushingSkate);
            Assert.AreEqual(Skate.Left, new SkatePush(0.5, s).PushingSkate);
        }

        [Test]
        public void A_move_up_to_ShuffleMaxMetres_is_a_shuffle_and_a_longer_one_a_T_push()
        {
            var s = new RoundSettings();
            Assert.AreEqual(PushKind.Shuffle, new SkatePush(0.3, s).Kind);
            Assert.AreEqual(PushKind.Shuffle, new SkatePush(-0.3, s).Kind);
            Assert.AreEqual(PushKind.TPush, new SkatePush(0.31, s).Kind);
            Assert.AreEqual(PushKind.Shuffle, new SkatePush(0.05, s).Kind);
            s.ShuffleMaxMetres = 0.5;
            Assert.AreEqual(PushKind.Shuffle, new SkatePush(0.45, s).Kind);
            Assert.AreEqual(PushKind.TPush, new SkatePush(0.55, s).Kind);
        }

        [TestCase(0.2)]
        [TestCase(0.3)]
        [TestCase(0.6)]
        public void Speed_rises_after_the_push_and_falls_to_zero_on_the_spot(double metres)
        {
            var s = new RoundSettings();
            var p = new SkatePush(metres, s);
            const int n = 1000;
            Assert.AreEqual(0, p.Speed(0), Tol);
            Assert.AreEqual(0, p.Speed(p.TotalSeconds), Tol);
            double prev = 0;
            int nearPeak = 0;
            for (int i = 1; i < n; i++)
            {
                double t = p.TotalSeconds * i / n;
                double v = Math.Abs(p.Speed(t));
                if (t <= p.PushSeconds) Assert.GreaterOrEqual(v, prev - Tol, "rises until the push ends at " + t);
                else Assert.LessOrEqual(v, prev + Tol, "falls after the push at " + t);
                prev = v;
                if (v >= p.PeakSpeed * 0.99) nearPeak++;
            }
            Assert.Less(nearPeak, n * 0.05, "no steady-speed stretch");
        }

        [Test]
        public void It_stops_exactly_on_the_spot()
        {
            var s = new RoundSettings();
            var p = new SkatePush(-0.6, s);
            Assert.AreEqual(-0.6, p.Position(p.TotalSeconds), Tol);
            Assert.AreEqual(-0.6, p.Position(10), Tol);
            Assert.AreEqual(0, p.Position(0), Tol);
            Assert.AreEqual(0, p.Position(-1), Tol);
            double prev = 0;
            for (int i = 0; i <= 1000; i++)
            {
                double x = p.Position(p.TotalSeconds * i / 1000);
                Assert.LessOrEqual(Math.Abs(x), 0.6 + Tol);
                Assert.LessOrEqual(x, prev + Tol, "monotone toward -X");
                prev = x;
            }
        }

        [Test]
        public void Position_and_Speed_agree()
        {
            var s = new RoundSettings();
            var p = new SkatePush(-0.6, s);
            const double h = 1e-5;
            foreach (double f in new[] { 0.1, 0.25, 0.5, 0.75, 0.9 })
            {
                double t = p.TotalSeconds * f;
                Assert.AreEqual(2 * h * p.Speed(t), p.Position(t + h) - p.Position(t - h), 1e-4);
            }
        }

        [Test]
        public void The_time_for_a_distance_is_SecondsFor()
        {
            var s = new RoundSettings();
            Assert.AreEqual(SkatePush.SecondsFor(0.6, s), new SkatePush(0.6, s).TotalSeconds, Tol);
            Assert.AreEqual(0.30, SkatePush.SecondsFor(0.6, s), Tol);
            Assert.AreEqual(0, SkatePush.SecondsFor(0, s), Tol);
        }

        [Test]
        public void The_phase_is_push_then_glide_then_done()
        {
            var s = new RoundSettings();
            var p = new SkatePush(0.6, s);
            Assert.AreEqual(PushPhase.Push, p.PhaseAt(0.0));
            Assert.AreEqual(PushPhase.Glide, p.PhaseAt(p.PushSeconds + 1e-6));
            Assert.AreEqual(PushPhase.Done, p.PhaseAt(p.TotalSeconds + 1e-6));
            Assert.AreEqual(1.0 / 3, p.PushSeconds / p.TotalSeconds, Tol);
        }

        [Test]
        public void Fraction_is_0_at_0_and_1_at_1_and_matches_the_push()
        {
            var s = new RoundSettings();
            Assert.AreEqual(0, SkatePush.Fraction(0, s), Tol);
            Assert.AreEqual(1, SkatePush.Fraction(1, s), Tol);
            double mid = SkatePush.Fraction(0.5, s);
            Assert.Greater(mid, 0);
            Assert.Less(mid, 1);
            var p = new SkatePush(0.4, s);
            foreach (double f in new[] { 0.2, 0.5, 0.8 })
            {
                double t = p.TotalSeconds * f;
                Assert.AreEqual(p.Position(t), SkatePush.Fraction(t / p.TotalSeconds, s) * 0.4, Tol);
            }
        }

        [Test]
        public void A_zero_move_is_a_shuffle_that_is_already_done()
        {
            var s = new RoundSettings();
            var p = new SkatePush(0, s);
            Assert.AreEqual(PushKind.Shuffle, p.Kind);
            Assert.AreEqual(0, p.TotalSeconds, Tol);
            Assert.AreEqual(PushPhase.Done, p.PhaseAt(0));
            Assert.AreEqual(0, p.Position(5), Tol);
        }
    }
}
