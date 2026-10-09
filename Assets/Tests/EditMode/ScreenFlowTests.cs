using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class ScreenFlowTests
    {
        private static ScreenFlow NewFlow(SaveData save = null) =>
            new ScreenFlow(new ScriptedRandom(ScriptedRandom.AlternatingButterflies()), save ?? new SaveData());

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
            Assert.AreEqual(ShotResultKind.Goal, flow.Round.LastResult.Value.Kind);
            Assert.AreEqual(1, flow.Round.Results.Count);
        }
    }
}
