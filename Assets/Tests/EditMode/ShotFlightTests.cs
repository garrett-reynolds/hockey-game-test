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

        [Test]
        public void A_200_kmh_Shot_flies_in_less_than_half_the_time_of_an_80_kmh_Shot_from_the_same_spot()
        {
            Assert.Less(ShotFlight.Seconds(6.0, 200.0), ShotFlight.Seconds(6.0, 80.0) / 2.0);
        }

        [Test]
        public void Flight_time_strictly_falls_as_speed_rises_over_the_whole_Test_Shot_range()
        {
            foreach (double metres in new[] { 2.0, 6.0, 9.0 }) // 2 m: the closest start, the camera stopped 3 m out
                for (int kmh = 15; kmh <= 200; kmh += 5)
                    Assert.Less(ShotFlight.Seconds(metres, kmh), ShotFlight.Seconds(metres, kmh - 5), metres + " m at " + kmh + " km/h");
        }

        [Test]
        public void A_zero_distance_still_gives_a_flight_of_one_frame_never_zero()
        {
            Assert.AreEqual(1.0 / 60.0, ShotFlight.Seconds(0.0, 80.0), 1e-9);
        }

        [TestCase(0.0)]
        [TestCase(-5.0)]
        public void ZeroOrNegativeSpeed_GivesFiniteFlight(double kmh)
        {
            double seconds = ShotFlight.Seconds(9.0, kmh);
            Assert.IsFalse(double.IsInfinity(seconds) || double.IsNaN(seconds));
            Assert.AreEqual(9.0 / (10.0 / 3.6), seconds, 1e-9, "flies as if at 10 km/h");
        }
    }
}
