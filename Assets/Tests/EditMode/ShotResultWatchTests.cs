using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class ShotResultWatchTests
    {
        private static Round StartedRound()
        {
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
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
            round.SubmitShotAndWhistle(50, 90, 100, false);
            Assert.IsTrue(watch.TakeNew(round));
            Assert.IsFalse(watch.TakeNew(round));
        }

        [Test]
        public void After_a_new_Round_starts_its_first_result_is_new_again()
        {
            var round = StartedRound();
            var watch = new ShotResultWatch();
            round.Tick(3.7);
            round.SubmitShotAndWhistle(50, 90, 100, false);
            watch.TakeNew(round);
            round.StartRound();
            Assert.IsFalse(watch.TakeNew(round));
            round.Tick(3.7);
            round.SubmitShotAndWhistle(50, 90, 100, false);
            Assert.IsTrue(watch.TakeNew(round));
        }

        [Test]
        public void A_new_Shot_is_reported_once_when_submitted_and_its_result_when_whistled()
        {
            var round = StartedRound();
            var watch = new ShotResultWatch();
            round.Tick(3.7);
            round.SubmitShot(50, 90, 100, false);
            Assert.IsTrue(watch.TakeNewShot(round));
            Assert.IsFalse(watch.TakeNewShot(round));
            Assert.IsFalse(watch.TakeNew(round));      // no result until the Whistle
            round.Whistle(ShotResultKind.Goal);
            Assert.IsTrue(watch.TakeNew(round));
            Assert.IsFalse(watch.TakeNew(round));
            Assert.IsFalse(watch.TakeNewShot(round));
        }

        [Test]
        public void No_Shot_in_time_is_a_new_result_but_not_a_new_Shot()
        {
            var round = StartedRound();
            var watch = new ShotResultWatch();
            round.Tick(3.7 + 3.0);
            Assert.IsFalse(watch.TakeNewShot(round));
            Assert.IsTrue(watch.TakeNew(round));
        }
    }
}
