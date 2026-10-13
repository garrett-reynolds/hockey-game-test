using System.Collections.Generic;
using System.IO;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>#146: Shots per Round (3 to 10, default 6) on the Round, the Save, the Settings page, the Arena Show and the Shot fade.</summary>
    public class ShotsPerRoundTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hockey-shots-" + Path.GetRandomFileName());
            _path = Path.Combine(_dir, "save.txt");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static Round Started(int shots, int best = 0)
        {
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound(best, shots);
            return round;
        }

        /// <summary>Plays every Shot of the Round as a Goal; returns how many Shots it took to reach RoundOver.</summary>
        private static int PlayAllAsGoals(Round round)
        {
            int played = 0;
            while (round.Phase != RoundPhase.RoundOver && played < 50)
            {
                round.Tick(3.7);
                round.SubmitShotAndWhistle(50, 90, 100 + played, false);
                played++;
                round.Tick(round.ResultHoldLength);
            }
            return played;
        }

        private static ScreenFlow Flow(SaveData save) => new ScreenFlow(ScriptedRandom.Halves(), save, FakeReach.NeverReaches());

        private static void Select(SettingsMenu menu, string label)
        {
            for (int i = 0; i < menu.RowCount; i++)
                if (menu.Label(i) == label) { menu.Select(i); return; }
            Assert.Fail("no row " + label);
        }

        // ---- the Round plays exactly the chosen number ----

        [TestCase(3)]
        [TestCase(6)]
        [TestCase(10)]
        public void A_Round_plays_exactly_the_chosen_number_of_Shots(int shots)
        {
            Round round = Started(shots);
            Assert.AreEqual(shots, round.ShotCount);
            Assert.AreEqual(shots, PlayAllAsGoals(round));
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(shots, round.ShotNumber);
            Assert.AreEqual(shots, round.Goals);
            Assert.AreEqual(shots, round.Results.Count);
            Assert.AreEqual(shots, round.ShotSpeedsKmh.Count);
        }

        [Test]
        public void A_Round_started_without_a_count_keeps_the_default_6()
        {
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            Assert.AreEqual(6, round.ShotCount);
            Assert.AreEqual(Round.ShotsPerRound, round.ShotCount);
        }

        [Test]
        public void The_next_Round_can_use_a_different_number()
        {
            Round round = Started(3);
            Assert.AreEqual(3, PlayAllAsGoals(round));
            round.StartRound(0, 10);
            Assert.AreEqual(10, PlayAllAsGoals(round));
        }

        [Test]
        public void StartRound_without_a_count_keeps_the_count_of_the_last_Round_as_documented()
        {
            Round round = Started(4);
            Assert.AreEqual(4, PlayAllAsGoals(round));
            round.StartRound(2);
            Assert.AreEqual(4, round.ShotCount);
            Assert.AreEqual(4, PlayAllAsGoals(round));
            round.StartRound();
            Assert.AreEqual(4, round.ShotCount);
        }

        [Test]
        public void The_result_hold_before_the_last_Shot_has_no_fade_lead_for_any_count()
        {
            foreach (int shots in new[] { 3, 10 })
            {
                Round round = Started(shots);
                for (int shot = 1; shot < shots; shot++)
                {
                    round.Tick(3.7);
                    round.SubmitShotAndWhistle(50, 90, 100, false);
                    Assert.AreEqual(ReplayTiming.HoldSeconds + ShotFade.LeadSeconds, round.ResultHoldLength, 1e-9, "Shot " + shot + " of " + shots);
                    round.Tick(round.ResultHoldLength);
                }
                round.Tick(3.7);
                round.SubmitShotAndWhistle(50, 90, 100, false);
                Assert.AreEqual(ReplayTiming.HoldSeconds, round.ResultHoldLength, 1e-9, "the last of " + shots);
            }
        }

        [Test]
        public void The_fade_to_black_runs_between_Shots_up_to_the_chosen_last_and_not_after_it()
        {
            Round round = Started(3);
            var fade = new ShotFade();
            for (int shot = 1; shot <= 3; shot++)
            {
                round.Tick(3.7);
                round.SubmitShotAndWhistle(50, 90, 100, false);
                round.Tick(round.ResultHoldLength - 0.1);
                double amount = 0;
                for (int i = 0; i < 40 && round.Phase == RoundPhase.ResultHold; i++)
                {
                    round.Tick(0.01);
                    amount = System.Math.Max(amount, fade.Step(round, 0.01));
                }
                if (shot < 3) Assert.Greater(amount, 0, "a fade after Shot " + shot);
                else Assert.AreEqual(0, amount, 1e-9, "no fade after the last Shot");
            }
        }

        [Test]
        public void The_decks_hold_one_card_per_Shot_and_keep_the_6_Shot_rolls()
        {
            foreach (int n in new[] { 3, 4, 5, 6, 7, 8, 9, 10 })
            {
                Assert.AreEqual(n, RoundDecks.SetUps(ScriptedRandom.Halves(), n).Length, "Set-ups for " + n);
                Assert.AreEqual(n, RoundDecks.AttackSides(ScriptedRandom.Halves(), n).Length, "sides for " + n);
            }
            CollectionAssert.AreEqual(RoundDecks.SetUps(ScriptedRandom.Halves()), RoundDecks.SetUps(ScriptedRandom.Halves(), 6));
            CollectionAssert.AreEqual(RoundDecks.AttackSides(ScriptedRandom.Halves()), RoundDecks.AttackSides(ScriptedRandom.Halves(), 6));
        }

        [Test]
        public void Any_count_has_a_fair_deck_with_every_Set_up_Mistake_and_every_side_in_use()
        {
            foreach (int n in new[] { 3, 10 })
            {
                var sides = new HashSet<AttackSide>(RoundDecks.AttackSides(ScriptedRandom.Halves(), n));
                Assert.AreEqual(3, sides.Count, "all three sides in a Round of " + n);
                int clean = 0;
                foreach (SetUpKind kind in RoundDecks.SetUps(ScriptedRandom.Halves(), n))
                    if (kind == SetUpKind.Clean) clean++;
                Assert.AreEqual(n / 3, clean, "Clean Set-ups in a Round of " + n);
            }
        }

        // ---- ScreenFlow and the save ----

        [TestCase(3)]
        [TestCase(10)]
        public void The_screen_flow_starts_the_Round_with_the_saved_number(int shots)
        {
            var save = new SaveData { ShotsPerRound = shots };
            ScreenFlow flow = Flow(save);
            Assert.AreEqual(shots, flow.ShotsPerRound);
            flow.Press();
            Assert.AreEqual(shots, flow.Round.ShotCount);
            Assert.AreEqual(shots, flow.ShotsPerRound);
            for (int shot = 0; shot < shots; shot++)
            {
                Assert.AreEqual(FlowScreen.Playing, flow.Screen);
                flow.Tick(3.7);
                flow.SubmitShot(50, 90, 100, false);
                flow.Round.WhistleNow();
                flow.Tick(flow.Round.ResultHoldLength);
            }
            Assert.AreEqual(FlowScreen.End, flow.Screen);
            Assert.AreEqual(shots, flow.Round.Goals);
        }

        [Test]
        public void A_new_best_for_8_Shots_does_not_touch_the_6_Shot_best()
        {
            var save = new SaveData { BestScore = 4 };
            save.ShotsPerRound = 8;
            ScreenFlow flow = Flow(save);
            Assert.AreEqual(0, flow.BestScore, "no 8-Shot best yet");
            flow.Press();
            for (int shot = 0; shot < 8; shot++)
            {
                flow.Tick(3.7);
                flow.SubmitShot(shot < 5 ? 50 : -10, shot < 5 ? 90 : 50, 100, false);
                flow.Round.WhistleNow();
                flow.Tick(flow.Round.ResultHoldLength);
            }
            Assert.IsTrue(flow.Round.IsNewBest);
            Assert.AreEqual(5, save.BestFor(8));
            Assert.AreEqual(4, save.BestFor(6));
            Assert.AreEqual(5, flow.BestScore);
        }

        [Test]
        public void Best_scores_are_kept_per_number_and_a_score_for_one_number_is_not_another()
        {
            var save = new SaveData();
            save.SetBest(3, 2);
            save.SetBest(10, 7);
            Assert.AreEqual(2, save.BestFor(3));
            Assert.AreEqual(7, save.BestFor(10));
            Assert.AreEqual(0, save.BestFor(6));
            Assert.AreEqual(0, save.BestFor(8));
            save.ShotsPerRound = 10;
            Assert.AreEqual(7, save.BestScore, "BestScore is the best for the chosen number");
            save.BestScore = 9;
            Assert.AreEqual(9, save.BestFor(10));
            Assert.AreEqual(2, save.BestFor(3));
        }

        [Test]
        public void Shots_per_Round_and_every_best_score_save_and_load()
        {
            var save = new SaveData { ShotsPerRound = 8 };
            save.SetBest(3, 1);
            save.SetBest(6, 4);
            save.SetBest(8, 5);
            save.SetBest(10, 9);
            Assert.IsTrue(save.Save(_path));
            SaveData loaded = SaveData.Load(_path);
            Assert.AreEqual(8, loaded.ShotsPerRound);
            Assert.AreEqual(1, loaded.BestFor(3));
            Assert.AreEqual(4, loaded.BestFor(6));
            Assert.AreEqual(5, loaded.BestFor(8));
            Assert.AreEqual(9, loaded.BestFor(10));
            Assert.AreEqual(0, loaded.BestFor(7));
            Assert.AreEqual(5, loaded.BestScore);
        }

        [Test]
        public void An_old_save_with_one_best_score_loads_it_as_the_6_Shot_best()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "bestScore=5\nusbPort=COM3\n");
            SaveData loaded = SaveData.Load(_path);
            Assert.AreEqual(6, loaded.ShotsPerRound, "no Shots per Round in an old file: the default");
            Assert.AreEqual(5, loaded.BestFor(6));
            Assert.AreEqual(5, loaded.BestScore);
            Assert.AreEqual(0, loaded.BestFor(3));
            Assert.AreEqual(0, loaded.BestFor(10));
        }

        [Test]
        public void The_6_Shot_best_is_still_written_as_bestScore_so_an_older_build_reads_it()
        {
            var save = new SaveData();
            save.SetBest(6, 3);
            save.Save(_path);
            StringAssert.Contains("bestScore=3\n", File.ReadAllText(_path));
        }

        [Test]
        public void Shots_per_Round_is_clamped_to_3_to_10_and_a_bad_value_gives_6()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "shotsPerRound=99\n");
            Assert.AreEqual(10, SaveData.Load(_path).ShotsPerRound);
            File.WriteAllText(_path, "shotsPerRound=1\n");
            Assert.AreEqual(3, SaveData.Load(_path).ShotsPerRound);
            File.WriteAllText(_path, "shotsPerRound=many\n");
            Assert.AreEqual(6, SaveData.Load(_path).ShotsPerRound);
        }

        [Test]
        public void A_negative_best_score_for_a_number_loads_as_0()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "bestScore8=-4\n");
            Assert.AreEqual(0, SaveData.Load(_path).BestFor(8));
        }

        [Test]
        public void Clear_best_scores_clears_every_number()
        {
            var save = new SaveData();
            save.SetBest(3, 1);
            save.SetBest(6, 4);
            save.SetBest(10, 9);
            save.ClearBestScores();
            for (int n = 3; n <= 10; n++) Assert.AreEqual(0, save.BestFor(n), "number " + n);
        }

        // ---- the Settings page ----

        [Test]
        public void The_Player_part_has_a_Shots_per_Round_row_that_steps_3_to_10()
        {
            var save = new SaveData();
            var menu = new SettingsMenu(save, new string[0]);
            Select(menu, "Shots per Round");
            int row = menu.Selected;
            Assert.AreEqual("6", menu.Value(row));
            Assert.IsTrue(menu.Change(1));
            Assert.AreEqual(7, save.ShotsPerRound);
            for (int i = 0; i < 6; i++) menu.Change(1);
            Assert.AreEqual(10, save.ShotsPerRound);
            Assert.IsFalse(menu.Change(1), "stays at 10");
            for (int i = 0; i < 9; i++) menu.Change(-1);
            Assert.AreEqual(3, save.ShotsPerRound);
            Assert.IsFalse(menu.Change(-1), "stays at 3");
            Assert.IsTrue(menu.SetFraction(row, 1.0));
            Assert.AreEqual(10, save.ShotsPerRound);
        }

        [Test]
        public void Reset_to_default_in_the_Player_part_sets_Shots_per_Round_back_to_6_and_keeps_every_best()
        {
            var save = new SaveData { ShotsPerRound = 9 };
            save.SetBest(9, 7);
            save.SetBest(6, 2);
            var menu = new SettingsMenu(save, new string[0]);
            Assert.IsTrue(menu.Press(SettingsKey.Reset));
            Assert.AreEqual(6, save.ShotsPerRound);
            Assert.AreEqual(7, save.BestFor(9));
            Assert.AreEqual(2, save.BestFor(6));
        }

        [Test]
        public void Reset_best_score_clears_the_best_of_every_number_and_the_row_says_so()
        {
            var save = new SaveData { ShotsPerRound = 8 };
            save.SetBest(3, 1);
            save.SetBest(8, 5);
            save.SetBest(10, 9);
            var menu = new SettingsMenu(save, new string[0]);
            Select(menu, "Reset best score");
            StringAssert.Contains("all numbers", menu.Hint(menu.Selected) ?? "");
            Assert.IsTrue(menu.Activate());
            for (int n = 3; n <= 10; n++) Assert.AreEqual(0, save.BestFor(n), "number " + n);
            Assert.AreEqual(8, save.ShotsPerRound, "the chosen number stays");
        }

        // ---- the Arena Show words ----

        [Test]
        public void The_start_card_shows_the_best_for_the_chosen_number()
        {
            var save = new SaveData { ShotsPerRound = 10 };
            save.SetBest(10, 7);
            save.SetBest(6, 3);
            ScreenFlow flow = Flow(save);
            Assert.AreEqual("BEST SCORE 7 OF 10\nSHOOT TO PLAY", ArenaShow.From(flow).VideoBoardWords.Details);
            save.ShotsPerRound = 3; // the Settings page changes it on the start screen
            Assert.AreEqual("BEST SCORE 0 OF 3\nSHOOT TO PLAY", ArenaShow.From(flow).VideoBoardWords.Details);
        }

        [TestCase(3)]
        [TestCase(10)]
        public void The_Score_Box_and_the_end_card_say_the_chosen_number(int shots)
        {
            var save = new SaveData { ShotsPerRound = shots };
            save.SetBest(shots, 1);
            ScreenFlow flow = Flow(save);
            flow.Press();
            Assert.AreEqual("GOALS 0 · SHOT 1 OF " + shots, ArenaShow.From(flow).ScoreBox.Text);
            for (int shot = 0; shot < shots; shot++)
                flow.Tick(3.7 + 3.0 + 2.5 + 0.01); // no Shot in time, then the hold
            Assert.AreEqual(FlowScreen.End, flow.Screen);
            VideoBoardWords end = ArenaShow.From(flow).VideoBoardWords;
            StringAssert.Contains("0 OF " + shots + " GOALS", end.Details);
            StringAssert.Contains("BEST 1 OF " + shots, end.Details);
        }

        [Test]
        public void An_input_with_no_count_reads_as_the_default_6()
        {
            VideoBoardWords w = ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.Idle, BestScore = 2 }).VideoBoardWords;
            Assert.AreEqual("BEST SCORE 2 OF 6\nSHOOT TO PLAY", w.Details);
        }

        // ---- the lower third, with 3 and 10 Shots ----

        [Test]
        public void The_lower_third_tallies_and_averages_over_3_and_10_Shots()
        {
            var three = new List<double?> { 80, 90, 100 };
            LowerThirdText t3 = LowerThird.Choose(new List<ShotResultKind> { ShotResultKind.Goal, ShotResultKind.Miss, ShotResultKind.Save }, three).Value;
            Assert.AreEqual("100 km/h  |  average 90 km/h", t3.SpeedLine);

            var ten = new List<double?>();
            var results = new List<ShotResultKind>();
            for (int i = 0; i < 10; i++)
            {
                ten.Add(90 + 2 * i);
                results.Add(i % 3 == 0 ? ShotResultKind.Goal : ShotResultKind.Miss);
            }
            LowerThirdText t10 = LowerThird.Choose(results, ten).Value;
            Assert.AreEqual("108 km/h  |  average 99 km/h", t10.SpeedLine);
            Assert.AreEqual("4 Goals from 10 Shots", t10.Headline);
        }

        [Test]
        public void The_lower_third_of_a_Round_reads_the_speeds_the_Round_kept_for_3_and_10_Shots()
        {
            foreach (int shots in new[] { 3, 10 })
            {
                Round round = Started(shots);
                for (int shot = 0; shot < shots; shot++)
                {
                    round.Tick(3.7);
                    round.SubmitShotAndWhistle(50, 90, 100, false);
                    if (shot < shots - 1) round.Tick(round.ResultHoldLength);
                }
                Assert.AreEqual(shots, round.ShotSpeedsKmh.Count);
                LowerThirdText text = LowerThird.Choose(round.Results, round.ShotSpeedsKmh).Value;
                Assert.AreEqual("100 km/h  |  average 100 km/h", text.SpeedLine, "Round of " + shots);
            }
        }
    }
}
