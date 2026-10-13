using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>#129: the screen is black at the camera cut between Shots, clear in the window and on Shot 1, and the Replay is not cut short.</summary>
    public class ShotFadeTests
    {
        const double Tol = 1e-9;

        static Round Started(RoundSettings settings = null)
        {
            var round = new Round(ScriptedRandom.Picking(AttackSide.Left, AttackSide.Middle, AttackSide.Right, AttackSide.Left, AttackSide.Middle, AttackSide.Right),
                settings ?? new RoundSettings(), FakeReach.NeverReaches(), 0);
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

        [Test]
        public void Shot_1_from_the_start_screen_has_no_fade()
        {
            Round round = Started();
            for (double t = 0; t < 1.0; t += 0.01)
            {
                Assert.AreEqual(0.0, ShotFade.Amount(round), Tol);
                round.Tick(0.01);
            }
        }

        [Test]
        public void The_shooting_window_and_the_skate_in_middle_are_clear()
        {
            Round round = Started();
            ShootAndWhistle(round);
            round.Tick(round.ResultHoldLength);
            round.Tick(1.0);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(0.0, ShotFade.Amount(round), Tol);
            round.Tick(round.Settings.SkateInSeconds - 1.0 + round.Settings.SlowDownSeconds + 0.1);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            Assert.AreEqual(0.0, ShotFade.Amount(round), Tol);
        }

        [Test]
        public void The_screen_is_black_at_the_cut_between_Shots_and_clear_a_moment_either_side()
        {
            Round round = Started();
            ShootAndWhistle(round);
            Assert.AreEqual(0.0, ShotFade.Amount(round), Tol, "just after the Whistle");
            round.Tick(round.ResultHoldLength - ShotFade.FadeSeconds - ShotFade.BlackSeconds - 0.01);
            Assert.AreEqual(0.0, ShotFade.Amount(round), Tol, "before the fade out");
            round.Tick(0.01 + ShotFade.FadeSeconds / 2);
            Assert.AreEqual(0.5, ShotFade.Amount(round), 1e-6, "half way out");
            // The frame before the cut and the frame of the cut (a 20 ms frame).
            round.Tick(ShotFade.FadeSeconds / 2 + ShotFade.BlackSeconds - 0.02);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            Assert.AreEqual(1.0, ShotFade.Amount(round), Tol, "black before the cut");
            round.Tick(0.03);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase, "the cut");
            Assert.AreEqual(1.0, ShotFade.Amount(round), Tol, "black on the first frame of the next skate-in");
            round.Tick(ShotFade.BlackSeconds + ShotFade.FadeSeconds / 2 - 0.01);
            Assert.AreEqual(0.5, ShotFade.Amount(round), 1e-6, "half way in");
            round.Tick(ShotFade.FadeSeconds);
            Assert.AreEqual(0.0, ShotFade.Amount(round), Tol, "clear");
        }

        [Test]
        public void The_Replay_runs_its_whole_length_before_the_fade_starts()
        {
            Round round = Started();
            ShootAndWhistle(round);
            Assert.IsTrue(ReplayTiming.Follows(round.LastResult), "a Replay follows this result");
            // Up to the end of the Replay's look back less the fade: clear.
            round.Tick(ReplayTiming.OnNetSeconds + ReplayTiming.LookUpSeconds + ReplayTiming.ShowSeconds);
            Assert.AreEqual(0.0, ShotFade.Amount(round), Tol, "clear to the end of the Replay show");
        }

        [Test]
        public void Nothing_before_the_Whistle_and_nothing_after_Shot_6()
        {
            Round round = Started();
            RoundSettings s = round.Settings;
            round.Tick(s.SkateInSeconds + s.SlowDownSeconds + 0.5);
            round.SubmitShot(50, 50, 100, false); // awaiting the Whistle
            round.Tick(0.5);
            Assert.AreEqual(0.0, ShotFade.Amount(round), Tol);
            Assert.AreEqual(0.0, ShotFade.Amount(RoundPhase.ResultHold, Round.ShotsPerRound, 5.0, 0.0), Tol, "Shot 6 goes to the end screen");
        }
    }
}
