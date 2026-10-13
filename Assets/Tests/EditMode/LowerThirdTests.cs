using System.Collections.Generic;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class LowerThirdTests
    {
        const ShotResultKind G = ShotResultKind.Goal, S = ShotResultKind.Save, M = ShotResultKind.Miss, T = ShotResultKind.TooLate;
        const int Per = Round.ShotsPerRound;

        static string Headline(int best, params ShotResultKind[] results) =>
            LowerThird.Choose(new List<ShotResultKind>(results), best, Per).Value.Headline;

        // ---- number choice ----

        [Test]
        public void No_Shots_yet_has_nothing_to_show()
        {
            Assert.IsNull(LowerThird.Choose(new List<ShotResultKind>(), 0, Per));
        }

        [Test]
        public void A_streak_of_3_Goals_in_a_row_wins()
        {
            Assert.AreEqual("3 in a row!", Headline(0, G, G, G));
            Assert.AreEqual("4 in a row!", Headline(0, S, G, G, G, G));
        }

        [Test]
        public void A_streak_beats_the_chase_for_the_best_score()
        {
            Assert.AreEqual("3 in a row!", Headline(4, G, G, G));
        }

        [Test]
        public void A_streak_must_be_running_now_and_2_in_a_row_is_not_one()
        {
            Assert.AreEqual("2 Goals from 2 Shots", Headline(0, G, G));
            Assert.AreEqual("3 Goals from 4 Shots", Headline(0, G, G, S, G), "the streak is 1 now");
            Assert.AreEqual("3 Goals from 4 Shots", Headline(0, G, G, G, S), "a streak that ended is not shown");
        }

        [Test]
        public void A_chance_to_beat_the_best_score_says_how_many_Goals_are_left_to_score()
        {
            Assert.AreEqual("2 more Goals to beat your best of 3", Headline(3, G, G, S));
            Assert.AreEqual("1 more Goal to beat your best of 2", Headline(2, G, S, G));
        }

        [Test]
        public void No_chance_to_beat_the_best_score_falls_back_to_the_tally()
        {
            // best 5, 1 Goal after 4 Shots: needs 5 more Goals, only 2 Shots left
            Assert.AreEqual("1 Goal from 4 Shots", Headline(5, G, S, S, S));
            // exactly enough Shots left is still a chance (best 3, 0 Goals after 2 Shots: needs 4 of 4 left)
            Assert.AreEqual("4 more Goals to beat your best of 3", Headline(3, S, S));
        }

        [Test]
        public void With_no_best_score_yet_it_is_the_tally()
        {
            Assert.AreEqual("0 Goals from 1 Shot", Headline(0, S));
            Assert.AreEqual("1 Goal from 1 Shot", Headline(0, G));
            Assert.AreEqual("2 Goals from 3 Shots", Headline(0, G, M, G));
        }

        [Test]
        public void A_Too_Late_Shot_counts_as_a_Shot_in_the_tally()
        {
            Assert.AreEqual("0 Goals from 1 Shot", Headline(0, T));
        }

        [Test]
        public void Already_beating_the_best_score_is_the_tally()
        {
            Assert.AreEqual("2 Goals from 2 Shots", Headline(1, G, G));
        }

        // ---- when it is on screen ----

        [Test]
        public void It_slides_in_after_the_Whistle_holds_then_slides_out_before_the_Replay_looks_up()
        {
            Assert.AreEqual(0, LowerThird.SlideAmount(0), 1e-9);
            Assert.AreEqual(0, LowerThird.SlideAmount(LowerThird.InDelaySeconds), 1e-9);
            Assert.AreEqual(1, LowerThird.SlideAmount(LowerThird.InDelaySeconds + LowerThird.SlideSeconds), 1e-9);
            Assert.AreEqual(1, LowerThird.SlideAmount(1.0), 1e-9);
            Assert.AreEqual(0, LowerThird.SlideAmount(LowerThird.OutBySeconds), 1e-9);
            Assert.AreEqual(0, LowerThird.SlideAmount(ReplayTiming.OnNetSeconds + ReplayTiming.LookUpSeconds), 1e-9);
            Assert.AreEqual(0, LowerThird.SlideAmount(ReplayTiming.OnNetSeconds + ReplayTiming.LookUpSeconds + 1.0), 1e-9, "the Replay itself");
            Assert.AreEqual(0, LowerThird.SlideAmount(-1), 1e-9);
        }

        [Test]
        public void It_is_off_the_screen_before_the_camera_looks_up_at_the_Video_Board()
        {
            Assert.LessOrEqual(LowerThird.OutBySeconds, ReplayTiming.OnNetSeconds);
            for (double t = ReplayTiming.OnNetSeconds; t < ReplayTiming.HoldSeconds; t += 0.05)
                Assert.AreEqual(0, LowerThird.SlideAmount(t), 1e-9, "at " + t);
        }

        static Round InWindow()
        {
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            round.Tick(3.7);
            return round;
        }

        [Test]
        public void It_is_off_while_the_player_aims_and_while_the_puck_flies()
        {
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            Assert.AreEqual(0, LowerThird.Of(round).Amount, "skating in");
            round.Tick(3.7);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            Assert.AreEqual(0, LowerThird.Of(round).Amount, "aiming");
            round.SubmitShot(50, 50, 100, false);
            round.Tick(1.0);
            Assert.IsTrue(round.AwaitingWhistle);
            Assert.AreEqual(0, LowerThird.Of(round).Amount, "the puck flies");
        }

        [Test]
        public void After_the_Whistle_of_a_Goal_it_shows_the_Round_numbers()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 100, false);
            round.Tick(0.8);
            LowerThirdShow show = LowerThird.Of(round);
            Assert.AreEqual(1, show.Amount, 1e-9);
            Assert.AreEqual("1 Goal from 1 Shot", show.Text.Headline);
        }

        [Test]
        public void It_counts_from_the_Whistle_not_from_the_Shot()
        {
            var round = InWindow();
            round.SubmitShot(50, 50, 100, false);
            round.Tick(2.0); // a long flight
            round.WhistleNow();
            Assert.AreEqual(0, LowerThird.Of(round).Amount, 1e-9, "just whistled");
            round.Tick(0.8);
            Assert.AreEqual(1, LowerThird.Of(round).Amount, 1e-9);
        }

        [Test]
        public void It_is_gone_by_the_end_of_the_hold_and_in_the_next_Shot()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 100, false);
            round.Tick(2.0);
            Assert.AreEqual(0, LowerThird.Of(round).Amount, 1e-9);
            round.Tick(round.ResultHoldLength);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(0, LowerThird.Of(round).Amount, 1e-9);
        }

        [Test]
        public void It_still_shows_when_no_Replay_plays()
        {
            // The Replay on/off setting is not built (#145). A Wide Miss has no Replay today: the same case the setting will make.
            var round = InWindow();
            round.SubmitShotAndWhistle(-20, 50, 100, false);
            Assert.IsFalse(ReplayTiming.Follows(round.LastResult), "a Wide Miss has no Replay");
            Assert.AreEqual(ShotResultKind.Miss, round.LastResult.Value.Kind);
            Assert.AreEqual(2.5, round.ResultHoldLength, 1e-9, "the hold without a Replay");
            round.Tick(0.8);
            LowerThirdShow show = LowerThird.Of(round);
            Assert.AreEqual(1, show.Amount, 1e-9);
            Assert.AreEqual("0 Goals from 1 Shot", show.Text.Headline);
        }

        [Test]
        public void It_shows_the_same_with_and_without_a_Replay()
        {
            double[] ts = { 0.1, 0.3, 0.45, 0.8, 1.2, 1.4, 1.6, 2.2 };
            foreach (double t in ts)
            {
                var goal = InWindow();
                goal.SubmitShotAndWhistle(50, 50, 100, false);
                goal.Tick(t);
                var wide = InWindow();
                wide.SubmitShotAndWhistle(-20, 50, 100, false);
                wide.Tick(t);
                Assert.AreEqual(LowerThird.Of(goal).Amount, LowerThird.Of(wide).Amount, 1e-9, "at " + t);
            }
        }

        [Test]
        public void It_shows_after_a_Too_Late_Shot()
        {
            var round = InWindow();
            round.Tick(3.0); // the window runs out
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(0.8);
            LowerThirdShow show = LowerThird.Of(round);
            Assert.AreEqual(1, show.Amount, 1e-9);
            Assert.AreEqual("0 Goals from 1 Shot", show.Text.Headline);
        }
    }
}
