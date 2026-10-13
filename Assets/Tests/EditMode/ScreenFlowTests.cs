using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class ScreenFlowTests
    {
        private static ScreenFlow NewFlow(SaveData save = null) =>
            new ScreenFlow(ScriptedRandom.Halves(), save ?? new SaveData(), FakeReach.NeverReaches());

        // Starts a Round and plays all 6 Shots: the first `goals` score (no gear ever reaches), the rest miss. Ends on the tick that finishes Shot 6's hold.
        private static void PlayRound(ScreenFlow flow, int goals)
        {
            flow.Press();
            for (int shot = 0; shot < Round.ShotsPerRound; shot++)
            {
                flow.Tick(3.7);
                if (shot < goals)
                    flow.SubmitShot(50, 90, 100, false);
                else
                    flow.SubmitShot(-10, 50, 100, false);
                flow.Round.WhistleNow();
                flow.Tick(flow.Round.ResultHoldLength); // 4.5 s after a Goal (its Replay), 2.5 s after a Wide Miss
            }
        }

        [Test]
        public void After_Shot_6_the_end_screen_shows()
        {
            var flow = NewFlow();
            PlayRound(flow, 2);
            Assert.AreEqual(FlowScreen.End, flow.Screen);
            Assert.AreEqual(2, flow.Round.Goals);
        }

        [Test]
        public void The_end_screen_ignores_keys_clicks_and_Shots_for_the_input_ignore_time()
        {
            var flow = NewFlow();
            PlayRound(flow, 0);
            flow.Tick(1.9);
            Assert.IsFalse(flow.EndAcceptsInput);
            flow.Press();
            flow.SubmitShot(50, 90, 100, false);
            Assert.AreEqual(FlowScreen.End, flow.Screen);
        }

        [Test]
        public void At_the_input_ignore_time_a_key_or_click_goes_to_the_start_screen_without_starting_a_Round()
        {
            var flow = NewFlow();
            PlayRound(flow, 0);
            flow.Tick(2.0);
            Assert.IsTrue(flow.EndAcceptsInput);
            flow.Press();
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
            Assert.AreEqual(RoundPhase.RoundOver, flow.Round.Phase); // not restarted
        }

        [Test]
        public void After_the_input_ignore_time_a_Shot_goes_to_the_start_screen()
        {
            var flow = NewFlow();
            PlayRound(flow, 0);
            flow.Tick(2.5);
            flow.SubmitShot(50, 90, 100, false);
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
        }

        [Test]
        public void The_end_screen_goes_back_to_start_by_itself_after_the_auto_return_time()
        {
            var flow = NewFlow();
            PlayRound(flow, 0);
            flow.Tick(14.9);
            Assert.AreEqual(FlowScreen.End, flow.Screen);
            flow.Tick(0.1);
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
        }

        [Test]
        public void A_long_tick_on_the_end_screen_stops_on_the_start_screen()
        {
            var flow = NewFlow();
            PlayRound(flow, 0);
            flow.Tick(1000);
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
            Assert.AreEqual(RoundPhase.RoundOver, flow.Round.Phase); // not restarted
        }

        [Test]
        public void End_screen_times_come_from_the_saved_settings()
        {
            var flow = NewFlow(new SaveData { InputIgnoreSeconds = 4, AutoReturnSeconds = 6 });
            PlayRound(flow, 0);
            flow.Tick(3.9);
            flow.Press();
            Assert.AreEqual(FlowScreen.End, flow.Screen);
            flow.Tick(1.9);
            Assert.AreEqual(FlowScreen.End, flow.Screen); // 5.8 s: not yet auto-returned, and no Press since
            flow.Tick(0.3);
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
        }

        [Test]
        public void Beating_the_best_score_updates_the_saved_best_and_reports_it_once()
        {
            var save = new SaveData { BestScore = 2 };
            var flow = NewFlow(save);
            int reports = 0;
            flow.BestScoreChanged += () => reports++;
            PlayRound(flow, 3);
            Assert.AreEqual(3, save.BestScore);
            Assert.IsTrue(flow.Round.IsNewBest);
            Assert.AreEqual(1, reports);
            flow.Tick(20);
            Assert.AreEqual(1, reports);
        }

        [Test]
        public void Equalling_the_best_score_does_not_change_it()
        {
            var save = new SaveData { BestScore = 3 };
            var flow = NewFlow(save);
            int reports = 0;
            flow.BestScoreChanged += () => reports++;
            PlayRound(flow, 3);
            Assert.AreEqual(3, save.BestScore);
            Assert.AreEqual(0, reports);
        }

        [Test]
        public void The_next_Round_compares_against_the_new_best()
        {
            var save = new SaveData();
            var flow = NewFlow(save);
            PlayRound(flow, 2);
            flow.Tick(20);
            PlayRound(flow, 1);
            Assert.IsFalse(flow.Round.IsNewBest);
            Assert.AreEqual(2, save.BestScore);
            Assert.AreEqual(2, flow.Round.BestScore);
        }

        [Test]
        public void The_flow_keeps_one_Round_for_the_whole_run_so_views_can_hold_it()
        {
            var flow = NewFlow();
            var round = flow.Round;
            PlayRound(flow, 1);
            flow.Tick(20);
            flow.Press();
            Assert.AreSame(round, flow.Round);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(0, round.Results.Count);
        }

        [Test]
        public void A_best_score_reset_in_SaveData_applies_to_the_next_Round()
        {
            var save = new SaveData();
            var flow = NewFlow(save);
            PlayRound(flow, 3);
            flow.Tick(20);
            save.BestScore = 0; // e.g. "Reset best score" on the settings screen
            PlayRound(flow, 1);
            Assert.IsTrue(flow.Round.IsNewBest);
            Assert.AreEqual(1, save.BestScore);
        }

        [Test]
        public void A_new_flow_shows_the_start_screen()
        {
            Assert.AreEqual(FlowScreen.Start, NewFlow().Screen);
        }

        [Test]
        public void A_key_or_click_on_the_start_screen_starts_a_Round_at_Shot_1_skate_in()
        {
            var flow = NewFlow();
            flow.Press();
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            Assert.AreEqual(RoundPhase.SkateIn, flow.Round.Phase);
            Assert.AreEqual(1, flow.Round.ShotNumber);
        }

        [Test]
        public void A_Shot_on_the_start_screen_starts_a_Round_and_does_not_count()
        {
            var flow = NewFlow();
            flow.SubmitShot(50, 90, 100, false);
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            Assert.AreEqual(RoundPhase.SkateIn, flow.Round.Phase);
            Assert.AreEqual(0, flow.Round.Results.Count);

            // Shot 1 still gets its own window, and a Shot there counts.
            flow.Tick(3.7);
            Assert.AreEqual(RoundPhase.ShootingWindow, flow.Round.Phase);
            flow.SubmitShot(50, 90, 100, false);
            flow.Round.WhistleNow();
            Assert.AreEqual(ShotResultKind.Goal, flow.Round.LastResult.Value.Kind);
            Assert.AreEqual(1, flow.Round.Results.Count);
        }

        // ---- Settings screen (F2) ----

        [Test]
        public void F2_opens_and_closes_the_settings_screen_from_the_start_screen()
        {
            var flow = NewFlow();
            flow.ToggleSettings();
            Assert.IsTrue(flow.SettingsOpen);
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
            flow.ToggleSettings();
            Assert.IsFalse(flow.SettingsOpen);
        }

        [Test]
        public void The_Settings_button_opens_the_page_on_the_start_screen_and_a_second_click_leaves_it_open()
        {
            var flow = NewFlow();
            flow.OpenSettings();
            Assert.IsTrue(flow.SettingsOpen);
            flow.OpenSettings();
            Assert.IsTrue(flow.SettingsOpen, "the button never closes the page");
            flow.Press();
            Assert.AreEqual(FlowScreen.Start, flow.Screen, "a press on the page never starts a Round");
        }

        [Test]
        public void The_Settings_button_does_nothing_during_a_Round_or_on_the_end_screen()
        {
            var flow = NewFlow();
            flow.Press();
            flow.OpenSettings();
            Assert.IsFalse(flow.SettingsOpen);
            PlayRound(flow, 1);
            flow.OpenSettings();
            Assert.IsFalse(flow.SettingsOpen);
        }

        [Test]
        public void F2_does_nothing_during_a_Round()
        {
            var flow = NewFlow();
            flow.Press();
            flow.ToggleSettings();
            Assert.IsFalse(flow.SettingsOpen);
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
        }

        [Test]
        public void F2_does_nothing_on_the_end_screen()
        {
            var flow = NewFlow();
            PlayRound(flow, 1);
            flow.ToggleSettings();
            Assert.IsFalse(flow.SettingsOpen);
            Assert.AreEqual(FlowScreen.End, flow.Screen);
        }

        [Test]
        public void A_key_click_or_Shot_on_the_settings_screen_does_not_start_a_Round()
        {
            var flow = NewFlow();
            flow.ToggleSettings();
            flow.Press();
            flow.SubmitShot(50, 90, 100, false);
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
            Assert.IsTrue(flow.SettingsOpen);
        }

        [Test]
        public void After_closing_settings_a_key_starts_a_Round_with_the_reset_best_score()
        {
            var save = new SaveData { BestScore = 2 };
            var flow = NewFlow(save);
            flow.ToggleSettings();
            save.BestScore = 0; // "Reset best score" on the settings screen
            flow.ToggleSettings();
            flow.Press();
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            Assert.AreEqual(0, flow.Round.BestScore);
        }

        [Test]
        public void The_Round_runs_on_the_settings_object_it_is_given_so_the_goalie_reach_port_can_share_it()
        {
            var settings = new RoundSettings();
            var flow = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches(), settings);
            Assert.AreSame(settings, flow.Round.Settings);
        }
    }
}
