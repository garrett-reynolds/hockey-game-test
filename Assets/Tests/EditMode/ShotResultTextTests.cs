using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class ShotResultTextTests
    {
        [TestCase(ShotResultKind.Goal, 92.0, "92 km/h")]
        [TestCase(ShotResultKind.Save, 91.6, "92 km/h")]
        [TestCase(ShotResultKind.Miss, 120.2, "120 km/h")]
        public void ShotWithPuck_ShowsSpeed(ShotResultKind kind, double kmh, string expected)
        {
            var result = new ShotResult(kind, true, 50, 50, kmh, true);
            Assert.AreEqual(expected, ShotResultText.Speed(result));
        }

        [Test]
        public void TooLate_ShowsNoSpeed()
        {
            var result = new ShotResult(ShotResultKind.TooLate, false, 0, 0, 0, false);
            Assert.IsNull(ShotResultText.Speed(result));
        }
    }
}
