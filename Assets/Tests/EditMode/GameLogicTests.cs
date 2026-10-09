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

        [TestCase(0, 0, 1f, -0.915f, 0f, -0.305f, 0.40667f)]
        [TestCase(2, 2, 1f, 0.305f, 0.81333f, 0.915f, 1.22f)]
        [TestCase(1, 1, 0.5f, -0.1525f, 0.50833f, 0.1525f, 0.71167f)]
        public void ZoneRectangle_MapsZoneToNetFaceMetres(int column, int row, float fraction,
            float minX, float minY, float maxX, float maxY)
        {
            NetFaceMapping.ZoneRectangle(column, row, fraction, out float a, out float b, out float c, out float d);
            Assert.AreEqual(minX, a, 1e-3f);
            Assert.AreEqual(minY, b, 1e-3f);
            Assert.AreEqual(maxX, c, 1e-3f);
            Assert.AreEqual(maxY, d, 1e-3f);
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
