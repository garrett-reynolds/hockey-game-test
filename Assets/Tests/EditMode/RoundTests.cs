using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class RoundTests
    {
        // No gear ever reaches: every Shot on the Net Face is a Goal (the race itself: RoundReachTests).
        private static Round NewRound(int best = 0) =>
            new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), best);

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
                round.SubmitShotAndWhistle(x, y, 100, false);
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
            round.SubmitShotAndWhistle(50, 50, 100, false);
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
            round.SubmitShotAndWhistle(50, 50, 100, false);
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
            round.SubmitShotAndWhistle(50, 50, 100, false);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(0, round.Results.Count);
        }

        [Test]
        public void Shot_during_slow_down_is_ignored()
        {
            var round = StartedRound();
            round.Tick(3.0);
            round.SubmitShotAndWhistle(50, 50, 100, false);
            Assert.AreEqual(RoundPhase.SlowDown, round.Phase);
            Assert.AreEqual(0, round.Results.Count);
        }

        [Test]
        public void Shot_during_the_hold_is_ignored()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 100, false);
            round.SubmitShotAndWhistle(0, 0, 100, false);
            Assert.AreEqual(1, round.Results.Count);
            Assert.AreEqual(50, round.LastResult.Value.X);
        }

        [Test]
        public void First_Shot_in_the_window_counts_and_moves_straight_to_the_hold()
        {
            var round = InWindow();
            round.Tick(1.0);
            round.SubmitShotAndWhistle(60, 80, 95, false);
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
            round.SubmitShotAndWhistle(10, 10, 80, false);
            round.SubmitShotAndWhistle(500, 500, 80, false);
            round.SubmitShotAndWhistle(90, 90, 80, true);
            Assert.AreEqual(1, round.Results.Count);
            Assert.AreEqual(10, round.LastResult.Value.X);
        }

        [Test]
        public void A_Test_Shot_counts_as_the_first_Shot_in_the_window()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 90, 80, true);
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind);
            round.SubmitShotAndWhistle(0, 0, 80, false);
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
            round.SubmitShotAndWhistle(x, y, 100, false);
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
            round.SubmitShotAndWhistle(x, y, 100, false);
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
            round.SubmitShotAndWhistle(50, 50, 100, false);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(2.49);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(0.02);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(6, round.Results.Count);
            Assert.AreEqual(6, round.ShotNumber);
            round.Tick(100);
            round.SubmitShotAndWhistle(50, 50, 100, false);
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
        public void StartRound_with_a_best_score_compares_against_that_best()
        {
            var round = RoundWithGoals(best: 5, goals: 2);
            round.StartRound(1);
            Assert.AreEqual(1, round.BestScore);
            for (int i = 0; i < 6; i++)
                PlayShot(round, i < 2 ? 50 : 150, 90);
            Assert.IsTrue(round.IsNewBest);
            Assert.AreEqual(2, round.BestScore);
        }

        [Test]
        public void New_best_flag_is_false_before_RoundOver()
        {
            var round = StartedRound(best: 0);
            PlayShot(round, 50, 90);
            Assert.IsFalse(round.IsNewBest);
            Assert.AreEqual(0, round.BestScore);
        }

        // ---- The Whistle: the Round takes its result when the game reports it, not at submit ----

        [Test]
        public void A_Shot_has_no_result_until_the_Whistle()
        {
            var round = InWindow();
            round.SubmitShot(50, 90, 100, false);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            Assert.IsTrue(round.AwaitingWhistle);
            Assert.IsFalse(round.ResultShowing);
            Assert.IsFalse(round.LastResult.HasValue);
            Assert.AreEqual(0, round.Results.Count);
            Assert.AreEqual(0, round.Goals);
            Assert.AreEqual(50, round.LastShot.Value.X);
            Assert.AreEqual(1, round.ShotsSubmitted);
        }

        [Test]
        public void The_Round_waits_as_long_as_the_Whistle_takes()
        {
            var round = InWindow();
            round.SubmitShot(50, 90, 100, false);
            round.Tick(30);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            Assert.AreEqual(1, round.ShotNumber);
            Assert.IsFalse(round.LastResult.HasValue);
        }

        [Test]
        public void The_Whistle_records_the_result_and_the_Shot_with_it()
        {
            var round = InWindow();
            round.SubmitShot(50, 90, 120, true);
            round.Whistle(ShotResultKind.Goal);
            Assert.IsFalse(round.AwaitingWhistle);
            Assert.IsTrue(round.ResultShowing);
            ShotResult result = round.LastResult.Value;
            Assert.AreEqual(ShotResultKind.Goal, result.Kind);
            Assert.IsTrue(result.HasShot);
            Assert.AreEqual(50, result.X);
            Assert.AreEqual(90, result.Y);
            Assert.AreEqual(120, result.SpeedKmh);
            Assert.IsTrue(result.IsTestShot);
            Assert.AreEqual(1, round.Goals);
            CollectionAssert.AreEqual(new[] { ShotResultKind.Goal }, round.Results);
        }

        [Test]
        public void The_Whistle_gives_whatever_result_the_game_reports()
        {
            var round = InWindow();
            round.SubmitShot(50, 90, 100, false);
            round.Whistle(ShotResultKind.Save);
            Assert.AreEqual(ShotResultKind.Save, round.LastResult.Value.Kind);
            Assert.AreEqual(0, round.Goals);
        }

        [Test]
        public void The_result_is_held_2_5_seconds_after_the_Whistle_not_after_the_Shot()
        {
            var round = InWindow();
            round.SubmitShot(50, 90, 100, false);
            round.Tick(2.9);                           // the puck flies for 2.9 s
            round.Whistle(ShotResultKind.Goal);
            round.Tick(2.4);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(0.2);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(2, round.ShotNumber);
        }

        [Test]
        public void The_result_shows_from_the_Whistle_and_the_time_to_it_is_kept()
        {
            var round = InWindow();
            round.SubmitShot(50, 90, 100, false);
            round.Tick(0.4);
            Assert.IsFalse(round.ResultShowing);
            round.Whistle(ShotResultKind.Goal);
            Assert.IsTrue(round.ResultShowing);
            Assert.AreEqual(0.4, round.ResultDelaySeconds, 1e-9);
        }

        [Test]
        public void A_Whistle_at_once_holds_2_5_seconds()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 90, 100, false);
            Assert.IsTrue(round.ResultShowing);
            round.Tick(2.4);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(0.2);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
        }

        [Test]
        public void A_Test_Shot_takes_its_result_at_the_Whistle_too()
        {
            var round = InWindow();
            round.SubmitShot(50, 90, 100, true);
            Assert.IsFalse(round.LastResult.HasValue);
            round.Tick(0.5);
            round.Whistle(ShotResultKind.Miss);
            Assert.IsTrue(round.LastResult.Value.IsTestShot);
            round.Tick(2.4);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(0.2);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
        }

        [Test]
        public void A_Whistle_with_no_Shot_waiting_changes_nothing()
        {
            var round = InWindow();
            round.Whistle(ShotResultKind.Goal);        // shooting window: no Shot yet
            Assert.IsFalse(round.LastResult.HasValue);
            round.SubmitShot(50, 90, 100, false);
            round.Whistle(ShotResultKind.Save);
            round.Whistle(ShotResultKind.Goal);        // too late to change it
            Assert.AreEqual(ShotResultKind.Save, round.LastResult.Value.Kind);
            Assert.AreEqual(1, round.Results.Count);
            Assert.AreEqual(0, round.Goals);
        }

        [Test]
        public void No_Shot_in_time_is_still_an_immediate_Miss_with_no_Whistle_to_wait_for()
        {
            var round = InWindow();
            round.Tick(3.0);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            Assert.IsFalse(round.AwaitingWhistle);
            Assert.IsTrue(round.ResultShowing);
            Assert.AreEqual(ShotResultKind.TooLate, round.LastResult.Value.Kind);
            Assert.IsFalse(round.LastShot.HasValue);
            round.Whistle(ShotResultKind.Goal);
            Assert.AreEqual(ShotResultKind.TooLate, round.LastResult.Value.Kind);
            round.Tick(2.4);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(0.2);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
        }

        [Test]
        public void The_Whistle_does_not_carry_to_the_next_Shot()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 90, 100, false);
            round.Tick(2.5 + 3.7);                     // hold ends, next Shot's window opens
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            Assert.IsFalse(round.AwaitingWhistle);
            Assert.IsFalse(round.LastShot.HasValue);
            round.SubmitShot(50, 90, 100, false);
            Assert.IsFalse(round.ResultShowing);
            Assert.AreEqual(1, round.Results.Count);
        }

        [Test]
        public void A_Shot_still_runs_the_goalie_race_at_submit()
        {
            var round = InWindow();
            round.SubmitShot(50, 90, 100, false);
            Assert.IsTrue(round.LastRace.HasValue);
        }

        [Test]
        public void A_Shot_off_the_Net_Face_has_no_race_and_waits_for_its_Whistle()
        {
            var round = InWindow();
            round.SubmitShot(-10, 50, 100, false);
            Assert.IsFalse(round.LastRace.HasValue);
            Assert.IsFalse(round.LastResult.HasValue);
            round.Whistle(ShotResultKind.Miss);
            Assert.AreEqual(ShotResultKind.Miss, round.LastResult.Value.Kind);
        }

        [Test]
        public void The_goalie_recovery_finishes_inside_the_hold()
        {
            var round = InWindow();
            round.SubmitShot(50, 90, 100, false);
            round.Tick(0.4);
            round.Whistle(ShotResultKind.Goal);
            Assert.Less(AfterShot.RecoveredBy, round.Settings.ResultHoldSeconds);
            round.Tick(AfterShot.RecoveredBy);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(round.Settings.ResultHoldSeconds - AfterShot.RecoveredBy + 0.01);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
        }
    }
}
