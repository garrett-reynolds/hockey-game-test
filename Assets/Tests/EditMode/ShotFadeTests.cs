using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// #129: the screen is black at the camera cut between Shots, clear in the window and on Shot 1, and the Replay (look-back too) is
    /// not cut short. BreakawayView steps one ShotFade a frame, right after the Round's tick and before it places the camera, so
    /// the frame a Round's Phase first reads SkateIn here is the frame the camera cuts.
    /// </summary>
    public class ShotFadeTests
    {
        const double Tol = 1e-9;

        static Round Started()
        {
            var round = new Round(ScriptedRandom.Picking(AttackSide.Left, AttackSide.Middle, AttackSide.Right, AttackSide.Left, AttackSide.Middle, AttackSide.Right),
                new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            return round;
        }

        /// <summary>Plays the current Shot to its Whistle (shot 0.5 s into the window).</summary>
        static void ShootAndWhistle(Round round)
        {
            RoundSettings s = round.Settings;
            round.Tick(s.SkateInSeconds + s.SlowDownSeconds + 0.5);
            round.SubmitShotAndWhistle(50, 50, 100, false);
        }

        /// <summary>Ticks the Round and steps the fade in frames of dt, for this many seconds; returns the largest Amount seen.</summary>
        static double Run(Round round, ShotFade fade, double seconds, double dt)
        {
            double worst = 0;
            for (double t = 0; t < seconds - 1e-9; t += dt)
            {
                round.Tick(dt);
                worst = System.Math.Max(worst, fade.Step(round, dt));
            }
            return worst;
        }

        [Test]
        public void Shot_1_from_the_start_screen_has_no_fade()
        {
            Round round = Started();
            var fade = new ShotFade();
            Assert.AreEqual(0.0, fade.Step(round, 0.01), Tol);
            Assert.AreEqual(0.0, Run(round, fade, 1.0, 0.01), Tol);
        }

        [Test]
        public void The_shooting_window_and_the_skate_in_middle_are_clear()
        {
            Round round = Started();
            var fade = new ShotFade();
            ShootAndWhistle(round);
            Run(round, fade, round.ResultHoldLength + 1.0, 0.01);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(0.0, fade.Amount, Tol);
            Assert.AreEqual(0.0, Run(round, fade, round.Settings.SkateInSeconds - 1.0 + round.Settings.SlowDownSeconds + 0.5, 0.01), Tol, "clear through the slow-down and the window");
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
        }

        [Test]
        public void The_screen_is_black_at_the_cut_between_Shots_and_clear_a_moment_either_side()
        {
            Round round = Started();
            var fade = new ShotFade();
            ShootAndWhistle(round);
            Assert.AreEqual(0.0, fade.Step(round, 0), Tol, "just after the Whistle");
            Run(round, fade, round.ResultHoldLength - ShotFade.LeadSeconds - 0.005, 0.005);
            Assert.AreEqual(0.0, fade.Amount, Tol, "before the fade out");
            Run(round, fade, ShotFade.FadeSeconds / 2 + 0.005, 0.005);
            Assert.AreEqual(0.5, fade.Amount, 1e-6, "half way out");
            Run(round, fade, ShotFade.FadeSeconds / 2 + ShotFade.BlackSeconds - 0.005, 0.005);
            Assert.AreEqual(1.0, fade.Amount, Tol, "black before the cut");
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            Run(round, fade, 0.01, 0.01);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase, "the cut");
            Assert.AreEqual(1.0, fade.Amount, Tol, "black on the first frame of the next skate-in");
            Run(round, fade, ShotFade.BlackSeconds + ShotFade.FadeSeconds / 2, 0.005);
            Assert.AreEqual(0.5, fade.Amount, 1e-6, "half way in");
            Run(round, fade, ShotFade.FadeSeconds / 2, 0.005);
            Assert.AreEqual(0.0, fade.Amount, Tol, "clear");
        }

        // Long frames across the cut: the first frame of the new skate-in is fully black whatever the frame length.
        [TestCase(0.016)]
        [TestCase(0.1)]
        [TestCase(0.15)]
        [TestCase(0.3)]
        public void The_first_frame_of_the_next_skate_in_is_fully_black_with_long_frames(double dt)
        {
            for (double offset = 0; offset < dt; offset += dt / 5) // the cut lands at different places in the frame
            {
                Round round = Started();
                var fade = new ShotFade();
                ShootAndWhistle(round);
                if (offset > 0)
                {
                    round.Tick(offset); // shifts where the cut falls inside a frame
                    fade.Step(round, offset);
                }
                int frames = 0;
                while (round.Phase != RoundPhase.SkateIn && frames++ < 1000)
                {
                    round.Tick(dt);
                    fade.Step(round, dt);
                }
                Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
                Assert.AreEqual(1.0, fade.Amount, Tol, "dt " + dt + ", offset " + offset + ": the frame the camera cuts");
            }
        }

        [Test]
        public void The_Replay_runs_to_the_end_of_its_look_back_before_the_fade_starts()
        {
            Round round = Started();
            var fade = new ShotFade();
            ShootAndWhistle(round);
            Assert.IsTrue(ReplayTiming.Follows(round.LastResult), "a Replay follows this result");
            Assert.AreEqual(ReplayTiming.HoldSeconds + ShotFade.LeadSeconds, round.ResultHoldLength, Tol, "the hold is lengthened by the fade's lead");
            Assert.AreEqual(0.0, Run(round, fade, ReplayTiming.HoldSeconds, 0.005), Tol, "clear through the whole Replay, look-back included");
            Run(round, fade, 0.005 + ShotFade.FadeSeconds / 2, 0.005);
            Assert.Greater(fade.Amount, 0.0, "the fade starts after it");
        }

        [Test]
        public void Nothing_before_the_Whistle_and_nothing_after_Shot_6()
        {
            Round round = Started();
            var fade = new ShotFade();
            RoundSettings s = round.Settings;
            round.Tick(s.SkateInSeconds + s.SlowDownSeconds + 0.5);
            round.SubmitShot(50, 50, 100, false); // awaiting the Whistle
            Assert.AreEqual(0.0, Run(round, fade, 0.5, 0.01), Tol);

            // Shot 6 ends in the end screen: no lead, no fade.
            Round last = Started();
            var fade6 = new ShotFade();
            for (int shot = 1; shot < Round.ShotsPerRound; shot++)
            {
                ShootAndWhistle(last);
                last.Tick(last.ResultHoldLength);
            }
            Assert.AreEqual(Round.ShotsPerRound, last.ShotNumber);
            ShootAndWhistle(last);
            double expected = ReplayTiming.Follows(last.LastResult) ? ReplayTiming.HoldSeconds : last.Settings.ResultHoldSeconds;
            Assert.AreEqual(expected, last.ResultHoldLength, Tol, "Shot 6's hold is not lengthened");
            Assert.AreEqual(0.0, Run(last, fade6, expected - 0.01, 0.01), Tol);
        }
    }
}
