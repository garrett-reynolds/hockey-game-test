using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class ShotFlightTests
    {
        [TestCase(9.0, 90.0, 0.36)]   // 90 km/h = 25 m/s; 9 m / 25 m/s
        [TestCase(9.0, 36.0, 0.9)]    // 36 km/h = 10 m/s
        [TestCase(5.0, 18.0, 1.0)]    // 18 km/h = 5 m/s
        public void FlightTime_IsDistanceOverSpeed(double metres, double kmh, double expectedSeconds)
        {
            Assert.AreEqual(expectedSeconds, ShotFlight.Seconds(metres, kmh), 1e-9);
        }

        [Test]
        public void FasterShot_FliesFaster()
        {
            Assert.Less(ShotFlight.Seconds(9.0, 120.0), ShotFlight.Seconds(9.0, 60.0));
        }

        [TestCase(9.0, 200.0)]   // 0.162 s unclamped
        [TestCase(0.0, 80.0)]
        public void FlightTime_IsNeverShorterThanMinimum(double metres, double kmh)
        {
            Assert.AreEqual(0.3, ShotFlight.Seconds(metres, kmh), 1e-9);
        }

        [Test]
        public void FlightTime_ExactlyAtMinimum()
        {
            Assert.AreEqual(0.3, ShotFlight.Seconds(3.0, 36.0), 1e-9); // 10 m/s over 3 m
        }

        [TestCase(0.0)]
        [TestCase(-5.0)]
        public void ZeroOrNegativeSpeed_GivesFiniteFlight(double kmh)
        {
            double seconds = ShotFlight.Seconds(9.0, kmh);
            Assert.IsFalse(double.IsInfinity(seconds) || double.IsNaN(seconds));
            Assert.GreaterOrEqual(seconds, 0.3);
        }
    }
}
