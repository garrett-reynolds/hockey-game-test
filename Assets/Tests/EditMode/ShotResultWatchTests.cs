using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class ShotResultWatchTests
    {
        private static Round StartedRound()
        {
            var round = new Round(new ScriptedRandom(ScriptedRandom.AlternatingButterflies()), new RoundSettings(), 0);
            round.StartRound();
            return round;
        }

        [Test]
        public void A_new_Shot_result_is_reported_once()
        {
            var round = StartedRound();
            var watch = new ShotResultWatch();
            round.Tick(3.7);
            Assert.IsFalse(watch.TakeNew(round));
            round.SubmitShot(50, 90, 100, false);
            Assert.IsTrue(watch.TakeNew(round));
            Assert.IsFalse(watch.TakeNew(round));
        }

        [Test]
        public void After_a_new_Round_starts_its_first_result_is_new_again()
        {
            var round = StartedRound();
            var watch = new ShotResultWatch();
            round.Tick(3.7);
            round.SubmitShot(50, 90, 100, false);
            watch.TakeNew(round);
            round.StartRound();
            Assert.IsFalse(watch.TakeNew(round));
            round.Tick(3.7);
            round.SubmitShot(50, 90, 100, false);
            Assert.IsTrue(watch.TakeNew(round));
        }
    }
}
