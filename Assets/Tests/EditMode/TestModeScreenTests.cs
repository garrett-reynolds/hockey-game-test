using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Test Mode screen (#157): the TEST MODE label, the Goals and Shots count, and the clean view (no lower third, no Clap Chant, no Shot dots).</summary>
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
        public void There_is_no_Clap_Chant_in_Test_Mode()
        {
            foreach (RoundPhase phase in new[] { RoundPhase.Idle, RoundPhase.ShootingWindow, RoundPhase.ResultHold, RoundPhase.RoundOver })
                Assert.IsFalse(ArenaShow.At(new ArenaShowInput { Phase = phase, TestMode = true }).Chant, phase.ToString());
            Assert.IsTrue(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.RoundOver }).Chant, "a Round still has it");

            ScreenFlow flow = NewTestModeFlow();
            for (int i = 0; i < 4; i++)
            {
                Assert.IsFalse(ArenaShow.From(flow).Chant);
                Shoot(flow, true);
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
