using HockeyGame.Core;

namespace HockeyGame.Core.Tests
{
    public static class RoundTestExtensions
    {
        /// <summary>
        /// Submits a Shot and sounds the Whistle at once with the result the Round computes (ScriptedShot),
        /// with no flight time in between. A Shot the Round ignores is just ignored.
        /// </summary>
        public static void SubmitShotAndWhistle(this Round round, double x, double y, double speedKmh, bool isTestShot)
        {
            round.SubmitShot(x, y, speedKmh, isTestShot);
            round.WhistleNow();
        }

        /// <summary>Sounds the Whistle now with today's scripted result, if the Round is waiting for one.</summary>
        public static void WhistleNow(this Round round)
        {
            if (round.AwaitingWhistle)
                round.Whistle(ScriptedShot.Result(round).Kind);
        }
    }
}
