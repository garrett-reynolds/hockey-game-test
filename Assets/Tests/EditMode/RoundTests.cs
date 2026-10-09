using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class RoundTests
    {
        private static Round NewRound(int best = 0) => new Round(new ScriptedRandom(ScriptedRandom.AlternatingButterflies()), new RoundSettings(), best);

        private static Round StartedRound(int best = 0)
        {
            var round = NewRound(best);
            round.StartRound();
            return round;
        }

        private static Round InWindow(int best = 0)
        {
            var round = StartedRound(best);
            round.Tick(3.7);
            return round;
        }

        // Plays one full Shot: window opens, Shot (or none), hold ends.
        private static void PlayShot(Round round, double x, double y, bool shoot = true)
        {
            round.Tick(3.7);
            if (shoot)
            {
                round.SubmitShot(x, y, 100, false);
                round.Tick(2.5);
            }
            else
            {
                round.Tick(3.0 + 2.5);
            }
        }

        [Test]
        public void Before_start_the_core_is_idle_and_ignores_Shots_and_time()
        {
            var round = NewRound();
            round.Tick(10);
            round.SubmitShot(50, 50, 100, false);
            Assert.AreEqual(RoundPhase.Idle, round.Phase);
            Assert.AreEqual(0, round.Results.Count);
        }

        [Test]
        public void StartRound_begins_Shot_1_skating_in()
        {
            var round = StartedRound();
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(1, round.ShotNumber);
        }

        [Test]
        public void Skate_in_lasts_2_5_seconds()
        {
            var round = StartedRound();
            round.Tick(2.49);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            round.Tick(0.02);
            Assert.AreEqual(RoundPhase.SlowDown, round.Phase);
        }

        [Test]
        public void Slow_down_lasts_1_2_seconds()
        {
            var round = StartedRound();
            round.Tick(2.5 + 1.19);
            Assert.AreEqual(RoundPhase.SlowDown, round.Phase);
            round.Tick(0.02);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
        }

        [Test]
        public void Shooting_window_lasts_3_seconds_and_reports_time_left()
        {
            var round = StartedRound();
            round.Tick(3.7);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            Assert.AreEqual(3.0, round.WindowSecondsLeft, 1e-9);
            round.Tick(2.99);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            Assert.AreEqual(0.01, round.WindowSecondsLeft, 1e-9);
            round.Tick(0.02);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
        }

        [Test]
        public void Window_time_left_is_zero_outside_the_window()
        {
            var round = StartedRound();
            Assert.AreEqual(0, round.WindowSecondsLeft);
            round.Tick(3.7);
            round.SubmitShot(50, 50, 100, false);
            Assert.AreEqual(0, round.WindowSecondsLeft);
        }

        [Test]
        public void Result_hold_lasts_2_5_seconds_then_the_next_Shot_skates_in()
        {
            var round = InWindow();
            round.Tick(3.0);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(2.49);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            Assert.AreEqual(1, round.ShotNumber);
            round.Tick(0.02);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(2, round.ShotNumber);
        }

        [Test]
        public void Shot_during_skate_in_is_ignored()
        {
            var round = StartedRound();
            round.SubmitShot(50, 50, 100, false);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(0, round.Results.Count);
        }

        [Test]
        public void Shot_during_slow_down_is_ignored()
        {
            var round = StartedRound();
            round.Tick(3.0);
            round.SubmitShot(50, 50, 100, false);
            Assert.AreEqual(RoundPhase.SlowDown, round.Phase);
            Assert.AreEqual(0, round.Results.Count);
        }

        [Test]
        public void Shot_during_the_hold_is_ignored()
        {
            var round = InWindow();
            round.SubmitShot(50, 50, 100, false);
            round.SubmitShot(0, 0, 100, false);
            Assert.AreEqual(1, round.Results.Count);
            Assert.AreEqual(50, round.LastResult.Value.X);
        }

        [Test]
        public void First_Shot_in_the_window_counts_and_moves_straight_to_the_hold()
        {
            var round = InWindow();
            round.Tick(1.0);
            round.SubmitShot(60, 80, 95, false);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            var result = round.LastResult.Value;
            Assert.AreEqual(ShotResultKind.Goal, result.Kind);
            Assert.IsTrue(result.HasShot);
            Assert.AreEqual(60, result.X);
            Assert.AreEqual(80, result.Y);
            Assert.AreEqual(95, result.SpeedKmh);
        }

        [Test]
        public void Second_Shot_real_or_Test_is_ignored()
        {
            var round = InWindow();
            round.SubmitShot(10, 10, 80, false);
            round.SubmitShot(500, 500, 80, false);
            round.SubmitShot(90, 90, 80, true);
            Assert.AreEqual(1, round.Results.Count);
            Assert.AreEqual(10, round.LastResult.Value.X);
        }

        [Test]
        public void A_Test_Shot_counts_as_the_first_Shot_in_the_window()
        {
            var round = InWindow();
            round.SubmitShot(50, 90, 80, true);
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind);
            round.SubmitShot(0, 0, 80, false);
            Assert.AreEqual(1, round.Results.Count);
        }

        [Test]
        public void No_Shot_in_the_window_is_TooLate_counted_as_a_Miss_with_no_Shot_data()
        {
            var round = InWindow();
            round.Tick(3.0);
            Assert.AreEqual(ShotResultKind.TooLate, round.LastResult.Value.Kind);
            Assert.IsFalse(round.LastResult.Value.HasShot);
            Assert.AreEqual(0, round.Goals);
            Assert.AreEqual(1, round.Results.Count);
        }

        [Test]
        public void A_Tick_spanning_the_window_end_gives_TooLate()
        {
            var round = StartedRound();
            round.Tick(2.5 + 1.2 + 3.1);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            Assert.AreEqual(ShotResultKind.TooLate, round.LastResult.Value.Kind);
        }

        [TestCase(-0.01, 50)]
        [TestCase(100.01, 50)]
        [TestCase(50, -0.01)]
        [TestCase(50, 100.01)]
        public void Shot_outside_0_to_100_is_a_Miss(double x, double y)
        {
            var round = InWindow();
            round.SubmitShot(x, y, 100, false);
            Assert.AreEqual(ShotResultKind.Miss, round.LastResult.Value.Kind);
            Assert.AreEqual(0, round.Goals);
        }

        [TestCase(0, 0)]
        [TestCase(100, 100)]
        [TestCase(0, 100)]
        [TestCase(100, 0)]
        [TestCase(50, 50)]
        public void Shot_on_the_Net_Face_including_edges_is_not_a_Miss(double x, double y)
        {
            var round = InWindow();
            round.SubmitShot(x, y, 100, false);
            Assert.AreNotEqual(ShotResultKind.Miss, round.LastResult.Value.Kind);
            Assert.AreEqual(1, round.Results.Count);
        }

        [Test]
        public void Goal_count_and_result_list_match_the_results()
        {
            var round = StartedRound();
            PlayShot(round, 50, 90);          // Goal
            PlayShot(round, 150, 50);         // Miss
            PlayShot(round, 0, 0, false);     // TooLate
            PlayShot(round, 50, 90);        // Goal
            Assert.AreEqual(2, round.Goals);
            CollectionAssert.AreEqual(
                new[] { ShotResultKind.Goal, ShotResultKind.Miss, ShotResultKind.TooLate, ShotResultKind.Goal },
                round.Results);
            Assert.AreEqual(5, round.ShotNumber);
        }

        [Test]
        public void A_Round_has_exactly_6_Shots_then_ends()
        {
            var round = StartedRound();
            for (int i = 1; i <= 5; i++)
            {
                PlayShot(round, 50, 90);
                Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
                Assert.AreEqual(i + 1, round.ShotNumber);
            }
            round.Tick(3.7);
            round.SubmitShot(50, 50, 100, false);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(2.49);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(0.02);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(6, round.Results.Count);
            Assert.AreEqual(6, round.ShotNumber);
            round.Tick(100);
            round.SubmitShot(50, 50, 100, false);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(6, round.Results.Count);
        }

        [Test]
        public void One_large_Tick_carries_across_several_phases()
        {
            var round = StartedRound();
            round.Tick(10);   // 2.5 + 1.2 + 3.0 + 2.5 = 9.2, then 0.8 into Shot 2
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(2, round.ShotNumber);
            Assert.AreEqual(ShotResultKind.TooLate, round.Results[0]);
            round.Tick(2.0);  // 0.8 + 2.0 = 2.8 -> 0.3 into slow-down
            Assert.AreEqual(RoundPhase.SlowDown, round.Phase);
        }

        [Test]
        public void A_huge_Tick_runs_the_whole_Round_to_RoundOver_with_all_TooLate()
        {
            var round = StartedRound();
            round.Tick(1000);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(6, round.Results.Count);
            Assert.AreEqual(0, round.Goals);
        }

        private static Round RoundWithGoals(int best, int goals)
        {
            var round = StartedRound(best);
            for (int i = 0; i < 6; i++)
                PlayShot(round, i < goals ? 50 : 150, 90);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            return round;
        }

        [Test]
        public void More_goals_than_the_old_best_is_a_new_best()
        {
            var round = RoundWithGoals(best: 3, goals: 4);
            Assert.AreEqual(4, round.Goals);
            Assert.IsTrue(round.IsNewBest);
            Assert.AreEqual(4, round.BestScore);
        }

        [Test]
        public void Equal_goals_is_not_a_new_best()
        {
            var round = RoundWithGoals(best: 3, goals: 3);
            Assert.IsFalse(round.IsNewBest);
            Assert.AreEqual(3, round.BestScore);
        }

        [Test]
        public void Fewer_goals_keeps_the_old_best()
        {
            var round = RoundWithGoals(best: 5, goals: 2);
            Assert.IsFalse(round.IsNewBest);
            Assert.AreEqual(5, round.BestScore);
        }

        [Test]
        public void New_best_flag_is_false_before_RoundOver()
        {
            var round = StartedRound(best: 0);
            PlayShot(round, 50, 90);
            Assert.IsFalse(round.IsNewBest);
            Assert.AreEqual(0, round.BestScore);
        }
    }
}
