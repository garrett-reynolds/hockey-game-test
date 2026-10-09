using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class UsbLineTests
    {
        static void AssertShot(UsbLine line, double x, double y, double speedKmh)
        {
            Assert.AreEqual(UsbLineKind.Shot, line.Kind);
            Assert.AreEqual(x, line.X);
            Assert.AreEqual(y, line.Y);
            Assert.AreEqual(speedKmh, line.SpeedKmh);
        }

        [Test]
        public void GoodShotLine_IsAShot()
        {
            AssertShot(UsbLine.Read("SHOT,12.5,40,88.25"), 12.5, 40, 88.25);
        }

        [Test]
        public void ReadyLine_IsReady()
        {
            Assert.AreEqual(UsbLineKind.Ready, UsbLine.Read("READY").Kind);
        }

        [TestCase("SHOT,12.5,40,88.25\r\n")]
        [TestCase("SHOT,12.5,40,88.25\n")]
        [TestCase("SHOT,12.5,40,88.25\r")]
        public void CrLfOrLfEnding_IsStillAShot(string text)
        {
            AssertShot(UsbLine.Read(text), 12.5, 40, 88.25);
        }

        [Test]
        public void CrLfEnding_OnReady_IsStillReady()
        {
            Assert.AreEqual(UsbLineKind.Ready, UsbLine.Read("READY\r\n").Kind);
        }

        [TestCase("  SHOT,12.5,40,88.25  ")]
        [TestCase("\tSHOT,12.5,40,88.25 \r\n")]
        public void ExtraSpacesAtTheEnds_AreTrimmed(string text)
        {
            AssertShot(UsbLine.Read(text), 12.5, 40, 88.25);
        }

        [TestCase("SHOOT,12.5,40,88.25")]
        [TestCase("shot,12.5,40,88.25")]
        [TestCase("ready")]
        [TestCase("HELLO")]
        [TestCase("")]
        [TestCase(null)]
        public void WrongTag_IsIgnored(string text)
        {
            Assert.AreEqual(UsbLineKind.Ignore, UsbLine.Read(text).Kind);
        }

        [TestCase("SHOT,12.5,40")]
        [TestCase("SHOT,12.5,40,88.25,7")]
        [TestCase("SHOT")]
        [TestCase("READY,1")]
        public void WrongFieldCount_IsIgnored(string text)
        {
            Assert.AreEqual(UsbLineKind.Ignore, UsbLine.Read(text).Kind);
        }

        [TestCase("SHOT,abc,40,88")]
        [TestCase("SHOT,12.5,,88")]
        [TestCase("SHOT,12.5,40,fast")]
        [TestCase("SHOT,12.5, 40,88")]
        [TestCase("SHOT,NaN,40,88")]
        [TestCase("SHOT,12.5,Infinity,88")]
        [TestCase("SHOT,1e3,40,88")]
        public void BadNumber_IsIgnored(string text)
        {
            Assert.AreEqual(UsbLineKind.Ignore, UsbLine.Read(text).Kind);
        }

        [TestCase("SHOT,-5,40,88", -5.0, 40.0)]
        [TestCase("SHOT,140,40,88", 140.0, 40.0)]
        [TestCase("SHOT,50,-0.5,88", 50.0, -0.5)]
        [TestCase("SHOT,50,100.5,88", 50.0, 100.5)]
        public void OutOfRangeXOrY_IsStillAShot(string text, double x, double y)
        {
            AssertShot(UsbLine.Read(text), x, y, 88);
        }

        [Test, SetCulture("de-DE")]
        public void CommaDecimalCulture_StillReadsADotAsTheDecimal()
        {
            // In de-DE "." is the thousands separator, so a current-culture read would give 125.
            AssertShot(UsbLine.Read("SHOT,12.5,40.25,88.5"), 12.5, 40.25, 88.5);
        }
    }
}
