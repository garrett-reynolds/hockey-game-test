using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class SmokeTests
    {
        [Test]
        public void A_Round_Is_Six_Shots()
        {
            Assert.AreEqual(6, Round.ShotsPerRound);
        }
    }
}
