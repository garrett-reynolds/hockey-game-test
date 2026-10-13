using HockeyGame.Editor;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Goal-rate tool (#68) is a tool, not a test of balance: only that it runs, repeats by seed and returns numbers.</summary>
    public class GoalRateToolTests
    {
        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void The_tool_runs_both_shooters_and_repeats_with_the_same_seed()
        {
            GoalRateTool.Report a = GoalRateTool.Run(2, 5, GoalRateTool.DefaultAimNoise);
            GoalRateTool.Report b = GoalRateTool.Run(2, 5, GoalRateTool.DefaultAimNoise);
            Assert.AreEqual(2 * Round.ShotsPerRound, a.Random.Shots);
            Assert.AreEqual(2 * Round.ShotsPerRound, a.GapReader.Shots);
            Assert.AreEqual(a.Random.Shots, a.Random.Goals + a.Random.Saves + a.Random.Misses);
            Assert.AreEqual(a.GapReader.Shots, a.GapReader.Goals + a.GapReader.Saves + a.GapReader.Misses);
            Assert.AreEqual(a.Text, b.Text, "same seed, same numbers");
            StringAssert.Contains("Gap-reader", a.Text);
        }
    }
}
