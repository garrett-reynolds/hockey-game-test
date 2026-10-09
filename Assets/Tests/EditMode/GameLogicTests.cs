using NUnit.Framework;

namespace HockeyGame.Tests
{
    public class GameLogicTests
    {
        [TestCase(0.0, 0.0, 50.0, 50.0)]
        [TestCase(-0.915, -0.61, 0.0, 0.0)]
        [TestCase(0.915, 0.61, 100.0, 100.0)]
        [TestCase(-1.83, 0.0, -50.0, 50.0)]
        [TestCase(0.0, 1.83, 50.0, 200.0)]
        public void OffsetFromCentre_MapsToNetFaceCoordinates(double right, double up, double expectedX, double expectedY)
        {
            NetFaceMapping.FromOffsetFromCentre(right, up, out double x, out double y);
            Assert.AreEqual(expectedX, x, 1e-9);
            Assert.AreEqual(expectedY, y, 1e-9);
        }

        [TestCase(80, 1, 85)]
        [TestCase(80, -2, 70)]
        [TestCase(200, 1, 200)]
        [TestCase(10, -1, 10)]
        public void TestShotSpeed_StepsInFiveAndClamps(int current, int steps, int expected)
        {
            Assert.AreEqual(expected, TestShotSpeed.Step(current, steps));
        }
    }
}
