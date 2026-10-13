using System;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class PushPoseTests
    {
        const double Tol = 1e-9;
        const double Frame = 1.0 / 60;

        static PushShown Shuffle(PushPhase phase, double share = 1) => new PushShown(PushKind.Shuffle, Skate.Left, phase, share);
        static PushShown TPush(PushPhase phase, double share = 1) => new PushShown(PushKind.TPush, Skate.Left, phase, share);

        static PushPoseMoment Run(PushPoseTracker t, PushShown s, double seconds)
        {
            int frames = (int)Math.Round(seconds / Frame);
            for (int i = 0; i < frames; i++) t.Step(s, Frame);
            return t.Now;
        }

        // ---- the progress adapter ----

        [Test]
        public void AtProgress_a_move_left_pushes_from_the_right_skate_and_a_move_right_from_the_left()
        {
            var s = new RoundSettings();
            Assert.AreEqual(Skate.Right, PushShown.AtProgress(-0.5, 0.1, s).Skate);
            Assert.AreEqual(Skate.Left, PushShown.AtProgress(0.5, 0.1, s).Skate);
        }

        [Test]
        public void AtProgress_short_is_a_shuffle_and_long_a_T_push_at_ShuffleMaxMetres()
        {
            var s = new RoundSettings();
            Assert.AreEqual(PushKind.Shuffle, PushShown.AtProgress(0.3, 0.1, s).Kind);
            Assert.AreEqual(PushKind.TPush, PushShown.AtProgress(0.31, 0.1, s).Kind);
        }

        [Test]
        public void PushShare_is_the_pushs_share_of_the_move()
        {
            var s = new RoundSettings();
            var push = new SkatePush(0.5, s);
            Assert.AreEqual(push.PushSeconds / push.TotalSeconds, PushPose.PushShare(s), Tol);
        }

        [Test]
        public void AtProgress_goes_push_then_glide_then_done()
        {
            var s = new RoundSettings();
            double share = PushPose.PushShare(s);
            Assert.AreEqual(PushPhase.Push, PushShown.AtProgress(0.5, 0.1, s).Phase);
            Assert.AreEqual(PushPhase.Glide, PushShown.AtProgress(0.5, 0.5, s).Phase);
            Assert.AreEqual(PushPhase.Done, PushShown.AtProgress(0.5, 1.0, s).Phase);
            Assert.AreEqual(PushPhase.Push, PushShown.AtProgress(0.5, share - 1e-6, s).Phase);
            Assert.AreEqual(PushPhase.Glide, PushShown.AtProgress(0.5, share + 1e-6, s).Phase);
            foreach (double u in new[] { 0, 0.1, 0.5, 0.9, 1 })
            {
                Assert.AreEqual(PushPhase.Done, PushShown.AtProgress(0, u, s).Phase, "0 m at u " + u);
                Assert.AreEqual(PushPhase.Done, PushShown.AtProgress(0.005, u, s).Phase, "0.005 m at u " + u);
                Assert.AreEqual(PushPhase.Done, PushShown.AtProgress(-0.005, u, s).Phase, "-0.005 m at u " + u);
            }
        }

        [Test]
        public void AtProgress_speed_share_is_one_at_the_end_of_the_push_and_zero_at_the_end()
        {
            var s = new RoundSettings();
            double share = PushPose.PushShare(s);
            Assert.AreEqual(1, PushShown.AtProgress(0.5, share - 1e-6, s).SpeedShare, 1e-4);
            Assert.AreEqual(0, PushShown.AtProgress(0.5, 1.0, s).SpeedShare, Tol);
            double previous = 2;
            foreach (double u in new[] { 0.0, 0.05, share * 0.5, share - 1e-6, share + 1e-6, 0.5, 0.7, 0.9, 0.999 })
            {
                double v = PushShown.AtProgress(0.5, u, s).SpeedShare;
                Assert.LessOrEqual(v, 1 + Tol);
                Assert.GreaterOrEqual(v, 0);
                if (u > share) { Assert.Less(v, previous + Tol, "falls through the glide at u " + u); previous = v; }
                else previous = 2;
            }
            Assert.Greater(PushShown.AtProgress(0.5, share * 0.5, s).SpeedShare, 0.4);
            Assert.Less(PushShown.AtProgress(0.5, share * 0.5, s).SpeedShare, 0.6);
        }

        // ---- the tracker ----

        [Test]
        public void A_shuffle_pushes_the_skate_out_and_never_turns_the_lead_skate()
        {
            var t = new PushPoseTracker();
            double last = 0;
            for (int i = 0; i < 12; i++)
            {
                var m = t.Step(Shuffle(PushPhase.Push), Frame);
                Assert.AreEqual(0, m.LeadTurn, Tol);
                Assert.GreaterOrEqual(m.PushOut, last - Tol);
                last = m.PushOut;
            }
            Assert.AreEqual(1, t.Now.PushOut, Tol);
        }

        [Test]
        public void A_T_push_turns_the_lead_skate_first_then_pushes()
        {
            var t = new PushPoseTracker();
            var m = t.Step(TPush(PushPhase.Push), Frame);
            Assert.Greater(m.LeadTurn, 0);
            Assert.AreEqual(0, m.PushOut, Tol);
            m = t.Step(TPush(PushPhase.Push), Frame);
            Assert.Greater(m.LeadTurn, 0);
            Assert.AreEqual(0, m.PushOut, Tol, "still turning at 0.033 s");
            for (int i = 0; i < 12; i++)
            {
                m = t.Step(TPush(PushPhase.Push), Frame);
                if (m.PushOut > 0) Assert.GreaterOrEqual(m.LeadTurn, PushPose.TurnedEnough - Tol, "PushOut rises only after the lead skate has turned");
            }
            Assert.AreEqual(1, m.LeadTurn, Tol);
            Assert.AreEqual(1, m.PushOut, Tol);
        }

        [Test]
        public void A_T_push_squares_up_as_he_slows()
        {
            var t = new PushPoseTracker();
            Run(t, TPush(PushPhase.Push), 0.3);
            Assert.AreEqual(1, t.Now.LeadTurn, Tol);
            int frames = 18; // 0.3 s
            double previous = 1;
            bool fell = false;
            for (int i = 1; i <= frames; i++)
            {
                double share = 1 - (double)i / frames;
                var m = t.Step(TPush(PushPhase.Glide, share), Frame);
                if (share >= PushPose.SquareBelowShare) Assert.AreEqual(1, m.LeadTurn, Tol, "square-on turn while fast, share " + share);
                else
                {
                    if (m.LeadTurn < previous - Tol) fell = true;
                    Assert.LessOrEqual(m.LeadTurn, previous + Tol, "never rises again");
                }
                previous = m.LeadTurn;
            }
            Assert.IsTrue(fell);
            Assert.AreEqual(0, t.Now.LeadTurn, 1e-6);
        }

        [Test]
        public void PushOut_returns_to_zero_over_ReturnSeconds_when_done()
        {
            var t = new PushPoseTracker();
            Run(t, Shuffle(PushPhase.Push), 0.2);
            Assert.AreEqual(1, t.Now.PushOut, Tol);
            t.Step(PushShown.None, PushPose.HoldSeconds); // he holds the pose before he lets Done through
            Assert.AreEqual(1, t.Now.PushOut, Tol);
            var m = t.Step(PushShown.None, PushPose.ReturnSeconds);
            Assert.AreEqual(0, m.PushOut, 1e-6);
            var half = new PushPoseTracker();
            Run(half, Shuffle(PushPhase.Push), 0.2);
            half.Step(PushShown.None, PushPose.HoldSeconds);
            Assert.AreEqual(0.5, half.Step(PushShown.None, PushPose.ReturnSeconds / 2).PushOut, 1e-6);
        }

        [Test]
        public void Nothing_snaps()
        {
            double dt = Frame;
            double max = dt / Math.Min(PushPose.TurnInSeconds, PushPose.PushInSeconds) + 1e-6;
            var t = new PushPoseTracker();
            var script = new[]
            {
                TPush(PushPhase.Push), TPush(PushPhase.Push), TPush(PushPhase.Push), TPush(PushPhase.Push), TPush(PushPhase.Push),
                TPush(PushPhase.Push), TPush(PushPhase.Push), TPush(PushPhase.Push), TPush(PushPhase.Push), TPush(PushPhase.Push),
                TPush(PushPhase.Glide, 0.9), TPush(PushPhase.Glide, 0.2), TPush(PushPhase.Glide, 0.0), PushShown.None, PushShown.None,
                PushShown.None, PushShown.None, PushShown.None, PushShown.None, PushShown.None, PushShown.None, PushShown.None,
                Shuffle(PushPhase.Push), Shuffle(PushPhase.Push), TPush(PushPhase.Push), Shuffle(PushPhase.Glide, 0.5),
            };
            var before = t.Now;
            foreach (var s in script)
            {
                var m = t.Step(s, dt);
                Assert.LessOrEqual(Math.Abs(m.PushOut - before.PushOut), max);
                Assert.LessOrEqual(Math.Abs(m.LeadTurn - before.LeadTurn), max);
                before = m;
            }

            // A full Done return from a full pose: past the hold, then the return, each frame bounded by dt / ReturnSeconds.
            double returnMax = dt / PushPose.ReturnSeconds + 1e-6;
            Run(t, TPush(PushPhase.Push), 0.3);
            before = t.Now;
            int frames = (int)Math.Ceiling((PushPose.HoldSeconds + PushPose.ReturnSeconds) / dt) + 3;
            for (int i = 0; i < frames; i++)
            {
                var m = t.Step(PushShown.None, dt);
                Assert.LessOrEqual(Math.Abs(m.PushOut - before.PushOut), returnMax, "PushOut frame " + i);
                Assert.LessOrEqual(Math.Abs(m.LeadTurn - before.LeadTurn), returnMax, "LeadTurn frame " + i);
                before = m;
            }
            Assert.AreEqual(0, before.PushOut, Tol);
            Assert.AreEqual(0, before.LeadTurn, Tol);

            // A Glide return is bounded the same way.
            Run(t, Shuffle(PushPhase.Push), 0.2);
            before = t.Now;
            for (int i = 0; i < 12; i++)
            {
                var m = t.Step(Shuffle(PushPhase.Glide, 0.5), dt);
                Assert.LessOrEqual(Math.Abs(m.PushOut - before.PushOut), returnMax, "Glide frame " + i);
                before = m;
            }
        }

        [Test]
        public void A_new_push_starts_from_the_current_numbers()
        {
            var t = new PushPoseTracker();
            Run(t, Shuffle(PushPhase.Push), 0.2);
            t.Step(PushShown.None, PushPose.HoldSeconds);
            t.Step(PushShown.None, PushPose.ReturnSeconds / 2);
            double mid = t.Now.PushOut;
            Assert.AreEqual(0.5, mid, 1e-6);
            var m = t.Step(Shuffle(PushPhase.Push), Frame);
            Assert.Greater(m.PushOut, mid);
            Assert.LessOrEqual(m.PushOut, mid + Frame / PushPose.PushInSeconds + 1e-6);
        }

        [Test]
        public void A_one_frame_Done_between_pushes_does_not_flicker()
        {
            var t = new PushPoseTracker();
            Run(t, TPush(PushPhase.Push), 0.3);
            Assert.AreEqual(1, t.Now.PushOut, Tol);
            Assert.AreEqual(1, t.Now.LeadTurn, Tol);
            var m = t.Step(PushShown.None, Frame);
            Assert.AreEqual(1, m.PushOut, Tol);
            Assert.AreEqual(1, m.LeadTurn, Tol);
            m = t.Step(TPush(PushPhase.Push), Frame);
            Assert.AreEqual(1, m.PushOut, Tol);
            Assert.AreEqual(1, m.LeadTurn, Tol);
        }

        [Test]
        public void A_Done_that_lasts_starts_the_return_after_HoldSeconds()
        {
            var t = new PushPoseTracker();
            Run(t, Shuffle(PushPhase.Push), 0.2);
            Run(t, PushShown.None, 0.05);
            Assert.AreEqual(1, t.Now.PushOut, Tol);
            Run(t, PushShown.None, 0.3);
            Assert.AreEqual(0, t.Now.PushOut, Tol);
        }

        [Test]
        public void No_time_no_change()
        {
            var t = new PushPoseTracker();
            Run(t, TPush(PushPhase.Push), 0.1);
            var before = t.Now;
            var m = t.Step(PushShown.None, 0);
            Assert.AreEqual(before.PushOut, m.PushOut, Tol);
            Assert.AreEqual(before.LeadTurn, m.LeadTurn, Tol);
            m = t.Step(TPush(PushPhase.Push), -0.5);
            Assert.AreEqual(before.PushOut, m.PushOut, Tol);
            Assert.AreEqual(before.LeadTurn, m.LeadTurn, Tol);
            Assert.AreEqual(before.PushOut, t.Now.PushOut, Tol);
        }

        [Test]
        public void Reset_zeroes_it()
        {
            var t = new PushPoseTracker();
            Run(t, TPush(PushPhase.Push), 0.3);
            t.Reset();
            Assert.AreEqual(0, t.Now.PushOut, Tol);
            Assert.AreEqual(0, t.Now.LeadTurn, Tol);
            var m = t.Step(PushShown.None, Frame); // the hold is cleared too
            Assert.AreEqual(0, m.PushOut, Tol);

            // Mid-hold, Reset, then a Push rises from 0 at once. (Reset also clears the held state, so a surviving hold timer
            // alone could not be told apart from outside; this pins the whole Reset-then-Push path.)
            var u = new PushPoseTracker();
            Run(u, Shuffle(PushPhase.Push), 0.2);
            u.Step(PushShown.None, Frame);
            u.Reset();
            m = u.Step(Shuffle(PushPhase.Push), Frame);
            Assert.AreEqual(Frame / PushPose.PushInSeconds, m.PushOut, 1e-6);
        }

        [Test]
        public void Steps_of_different_sizes_end_the_same()
        {
            // 0.05 s is shorter than PushInSeconds, so the shuffle's PushOut is still rising (about 0.6), not saturated at 1.
            foreach (var shown in new[] { Shuffle(PushPhase.Push), TPush(PushPhase.Push) })
            {
                double seconds = shown.Kind == PushKind.Shuffle ? 0.05 : 0.1;
                var ten = new PushPoseTracker();
                for (int i = 0; i < 10; i++) ten.Step(shown, seconds / 10);
                var one = new PushPoseTracker();
                one.Step(shown, seconds);
                Assert.Less(one.Now.PushOut, 0.95, shown.Kind + " is not saturated");
                Assert.Greater(one.Now.PushOut, 0.05, shown.Kind + " has moved");
                Assert.AreEqual(ten.Now.PushOut, one.Now.PushOut, 0.02, shown.Kind + " PushOut");
                Assert.AreEqual(ten.Now.LeadTurn, one.Now.LeadTurn, 0.02, shown.Kind + " LeadTurn");
            }
        }
    }
}
