using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Test Mode screen (#157): the TEST MODE label, the Goals and Shots count, and the clean view (no lower third, no Shot dots).</summary>
    public class TestModeScreenTests
    {
        private static ScreenFlow NewFlow() =>
            new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches(), null);

        private static ScreenFlow NewTestModeFlow()
        {
            ScreenFlow flow = NewFlow();
            flow.OpenSettings();
            flow.StartTestMode();
            return flow;
        }

        // One Shot with the whole hold: a Goal (x 50, y 90) or a wide Miss (x -10).
        private static void Shoot(ScreenFlow flow, bool goal)
        {
            flow.SubmitShot(goal ? 50 : -10, goal ? 90 : 50, 100, !goal);
            flow.Round.WhistleNow();
            flow.Tick(flow.Round.ResultHoldLength);
        }

        // ---- the label ----

        [Test]
        public void The_TEST_MODE_label_shows_only_in_Test_Mode()
        {
            ScreenFlow flow = NewFlow();
            Assert.IsFalse(TestModeHud.LabelShown(flow), "the start screen");
            flow.Press();
            Assert.IsFalse(TestModeHud.LabelShown(flow), "a Round");
            Assert.AreEqual("TEST MODE", TestModeHud.LabelText);

            ScreenFlow test = NewTestModeFlow();
            Assert.IsTrue(TestModeHud.LabelShown(test));
            Shoot(test, true);
            Assert.IsTrue(TestModeHud.LabelShown(test), "in the next Shot's window too");
            test.LeaveTestMode();
            Assert.IsFalse(TestModeHud.LabelShown(test), "back on the start screen");
        }

        // ---- the count ----

        [Test]
        public void The_Score_Box_starts_at_0_Goals_and_0_Shots_and_counts_the_visit()
        {
            ScreenFlow flow = NewTestModeFlow();
            ScoreBox box = ArenaShow.From(flow).ScoreBox;
            Assert.IsTrue(box.Shown);
            Assert.AreEqual("GOALS 0 · SHOTS 0", box.Text);
            Shoot(flow, true);
            Assert.AreEqual("GOALS 1 · SHOTS 1", ArenaShow.From(flow).ScoreBox.Text);
            Shoot(flow, false);
            Assert.AreEqual("GOALS 1 · SHOTS 2", ArenaShow.From(flow).ScoreBox.Text);
            Shoot(flow, true);
            Assert.AreEqual("GOALS 2 · SHOTS 3", ArenaShow.From(flow).ScoreBox.Text);
        }

        [Test]
        public void The_count_goes_on_past_a_Round_of_Shots_though_Results_holds_one_Shot()
        {
            ScreenFlow flow = NewTestModeFlow();
            int goals = 0;
            for (int shot = 1; shot <= 14; shot++)
            {
                bool goal = shot % 3 != 0;
                if (goal) goals++;
                Shoot(flow, goal);
                Assert.LessOrEqual(flow.Round.Results.Count, 1, "Results is cleared each Shot");
                Assert.AreEqual("GOALS " + goals + " · SHOTS " + shot, ArenaShow.From(flow).ScoreBox.Text);
            }
        }

        [Test]
        public void The_count_starts_at_0_each_time_Test_Mode_is_entered()
        {
            ScreenFlow flow = NewTestModeFlow();
            Shoot(flow, true);
            Shoot(flow, true);
            flow.LeaveTestMode();
            flow.OpenSettings();
            flow.StartTestMode();
            Assert.AreEqual("GOALS 0 · SHOTS 0", ArenaShow.From(flow).ScoreBox.Text);
            Shoot(flow, false);
            Assert.AreEqual("GOALS 0 · SHOTS 1", ArenaShow.From(flow).ScoreBox.Text);
        }

        [Test]
        public void Leaving_with_the_puck_in_flight_then_entering_again_counts_from_0_never_below()
        {
            ScreenFlow flow = NewTestModeFlow();
            flow.SubmitShot(50, 90, 100, false);
            Assert.IsTrue(flow.Round.AwaitingWhistle);
            flow.LeaveTestMode(); // Esc mid-flight
            flow.OpenSettings();
            flow.StartTestMode();
            Assert.IsFalse(flow.Round.AwaitingWhistle);
            Assert.AreEqual(0, flow.Round.ShotsWhistled);
            Assert.AreEqual("GOALS 0 · SHOTS 0", ArenaShow.From(flow).ScoreBox.Text);
            // a Round started mid-flight too
            ScreenFlow other = NewTestModeFlow();
            other.SubmitShot(50, 90, 100, false);
            other.Round.StartRound();
            Assert.IsFalse(other.Round.AwaitingWhistle);
            Assert.GreaterOrEqual(other.Round.ShotsWhistled, 0);
        }

        [Test]
        public void A_Round_keeps_its_own_Score_Box_words()
        {
            ScreenFlow flow = NewFlow();
            flow.Press();
            Assert.AreEqual("GOALS 0 · SHOT 1 OF 6", ArenaShow.From(flow).ScoreBox.Text);
        }

        // ---- the clean view ----

        [Test]
        public void There_is_no_lower_third_in_Test_Mode_though_a_Round_has_one()
        {
            ScreenFlow flow = NewTestModeFlow();
            flow.SubmitShot(50, 90, 100, false);
            flow.Round.WhistleNow();
            for (double t = 0; t < 0.95; t += 0.05)
            {
                flow.Tick(0.05);
                Assert.AreEqual(0, LowerThird.Of(flow.Round).Amount, "Test Mode at " + t);
            }

            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            round.Tick(3.7);
            round.SubmitShotAndWhistle(50, 90, 100, false);
            round.Tick(0.8);
            Assert.AreEqual(1, LowerThird.Of(round).Amount, 1e-9, "a Round at the same moment");
        }


        [Test]
        public void The_count_moves_at_the_Whistle_both_numbers_together_not_while_the_puck_flies()
        {
            ScreenFlow flow = NewTestModeFlow();
            flow.SubmitShot(50, 90, 100, false);
            Assert.IsTrue(flow.Round.AwaitingWhistle);
            Assert.AreEqual("GOALS 0 · SHOTS 0", ArenaShow.From(flow).ScoreBox.Text, "puck in flight");
            flow.Tick(0.5);
            Assert.AreEqual("GOALS 0 · SHOTS 0", ArenaShow.From(flow).ScoreBox.Text, "still in flight");
            flow.Round.WhistleNow();
            Assert.AreEqual("GOALS 1 · SHOTS 1", ArenaShow.From(flow).ScoreBox.Text, "at the Whistle");
            flow.Tick(flow.Round.ResultHoldLength);
            flow.SubmitShot(-10, 50, 100, true);
            Assert.AreEqual("GOALS 1 · SHOTS 1", ArenaShow.From(flow).ScoreBox.Text, "second puck in flight");
            flow.Round.WhistleNow();
            Assert.AreEqual("GOALS 1 · SHOTS 2", ArenaShow.From(flow).ScoreBox.Text);
        }

        [Test]
        public void The_boards_show_no_Round_words_in_any_phase_of_Test_Mode()
        {
            foreach (RoundPhase phase in System.Enum.GetValues(typeof(RoundPhase)))
                foreach (ArenaResult result in System.Enum.GetValues(typeof(ArenaResult)))
                    foreach (bool newBest in new[] { false, true })
                    {
                        ArenaState state = ArenaShow.At(new ArenaShowInput
                        {
                            Phase = phase, PhaseSeconds = 0.5, Result = result, ResultDelaySeconds = 0.4, TestMode = true, IsNewBest = newBest,
                            ShotNumber = 3, BestScore = 4, ShotsPerRound = 6, ShotsSubmitted = 2, Goals = 1
                        });
                        string words = state.VideoBoardWords.Headline + "|" + state.VideoBoardWords.Details;
                        string at = phase + " / " + result + " / " + state.VideoBoard + ": " + words;
                        foreach (string round in new[] { "OF 6", "SHOOT TO PLAY", "BEST", "FINAL", "SHOT 3" })
                            StringAssert.DoesNotContain(round, words, at);
                        Assert.AreNotEqual(VideoBoardContent.StartCard, state.VideoBoard, at);
                        Assert.AreNotEqual(VideoBoardContent.Final, state.VideoBoard, at);
                        Assert.AreNotEqual(VideoBoardContent.NewBest, state.VideoBoard, at);
                    }
        }

        [Test]
        public void The_Goal_Show_still_plays_in_Test_Mode()
        {
            ArenaState state = ArenaShow.At(new ArenaShowInput
            {
                Phase = RoundPhase.ResultHold, PhaseSeconds = 0.5, Result = ArenaResult.Goal, ResultDelaySeconds = 0.4, TestMode = true, ShotsSubmitted = 1, Goals = 1
            });
            Assert.AreEqual(VideoBoardContent.Goal, state.VideoBoard);
            Assert.AreEqual(RibbonMessage.Goal, state.RibbonIn(ArenaZone.Near).Message);
            Assert.IsTrue(state.GoalShow.GoalLight, "the goal light");
            Assert.IsTrue(state.GoalShow.RoofPulse, "the roof lights");
            Assert.AreEqual("GOALS 1 · SHOTS 1", state.ScoreBox.Text);
        }

        [Test]
        public void The_Shot_dots_are_hidden_in_Test_Mode_and_drawn_in_a_Round()
        {
            ScreenFlow flow = NewFlow();
            Assert.IsFalse(TestModeHud.DotsShown(flow), "the start screen shows no old Round");
            flow.Press();
            Assert.IsTrue(TestModeHud.DotsShown(flow), "a Round");
            ScreenFlow test = NewTestModeFlow();
            Assert.IsFalse(TestModeHud.DotsShown(test));
            Shoot(test, true);
            Assert.IsFalse(TestModeHud.DotsShown(test));
        }
    }
}
