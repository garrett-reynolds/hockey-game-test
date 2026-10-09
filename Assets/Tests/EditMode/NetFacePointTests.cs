using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class NetFacePointTests
    {
        // Net Face is 1.83 m wide, 1.22 m high. Result: metres right of the Net Face centre, metres up from its bottom.
        [TestCase(50.0, 50.0, 0.0, 0.61)]
        [TestCase(0.0, 0.0, -0.915, 0.0)]
        [TestCase(100.0, 100.0, 0.915, 1.22)]
        [TestCase(-10.0, 50.0, -1.098, 0.61)]   // off the net, left of the post (a Miss)
        [TestCase(50.0, 120.0, 0.0, 1.464)]     // over the crossbar (a Miss)
        public void NetFacePosition_MapsToMetres(double x, double y, double expectedRight, double expectedUp)
        {
            NetFaceMapping.ToMetres(x, y, out double right, out double up);
            Assert.AreEqual(expectedRight, right, 1e-9);
            Assert.AreEqual(expectedUp, up, 1e-9);
        }
    }
}
