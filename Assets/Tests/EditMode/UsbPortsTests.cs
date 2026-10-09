using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class UsbPortsTests
    {
        [Test]
        public void Auto_TriesEveryPort_Sorted()
        {
            var toTry = UsbPorts.ToTry(SaveData.AutoUsbPort, "", new[] { "COM7", "COM3", "COM12" });
            CollectionAssert.AreEqual(new[] { "COM12", "COM3", "COM7" }, toTry);
        }

        [Test]
        public void Auto_TriesTheRememberedPortFirst()
        {
            var toTry = UsbPorts.ToTry(SaveData.AutoUsbPort, "COM7", new[] { "COM7", "COM3", "COM12" });
            CollectionAssert.AreEqual(new[] { "COM7", "COM12", "COM3" }, toTry);
        }

        [Test]
        public void Auto_SkipsARememberedPortThatIsGone()
        {
            var toTry = UsbPorts.ToTry(SaveData.AutoUsbPort, "COM9", new[] { "COM3", "COM4" });
            CollectionAssert.AreEqual(new[] { "COM3", "COM4" }, toTry);
        }

        [Test]
        public void Auto_ListsEachPortOnce()
        {
            var toTry = UsbPorts.ToTry(SaveData.AutoUsbPort, "", new[] { "COM3", "COM3", "", null, "COM4" });
            CollectionAssert.AreEqual(new[] { "COM3", "COM4" }, toTry);
        }

        [Test]
        public void Auto_NoPorts_TriesNothing()
        {
            CollectionAssert.IsEmpty(UsbPorts.ToTry(SaveData.AutoUsbPort, "COM3", new string[0]));
            CollectionAssert.IsEmpty(UsbPorts.ToTry(SaveData.AutoUsbPort, "COM3", null));
        }

        [Test]
        public void Manual_TriesOnlyTheChosenPort_EvenIfNotListed()
        {
            var toTry = UsbPorts.ToTry("COM5", "COM7", new[] { "COM3", "COM7" });
            CollectionAssert.AreEqual(new[] { "COM5" }, toTry);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void BlankSetting_CountsAsAuto(string setting)
        {
            var toTry = UsbPorts.ToTry(setting, "", new[] { "COM4", "COM3" });
            CollectionAssert.AreEqual(new[] { "COM3", "COM4" }, toTry);
        }

        [Test]
        public void IsAuto_OnlyForAutoOrBlank()
        {
            Assert.IsTrue(UsbPorts.IsAuto("Auto"));
            Assert.IsTrue(UsbPorts.IsAuto(""));
            Assert.IsFalse(UsbPorts.IsAuto("COM3"));
        }

        [Test]
        public void Status_Connected_NamesThePort()
        {
            Assert.AreEqual("Connected (COM3)", UsbPorts.StatusText("COM3"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Status_NotConnected_SaysNoElectronics(string port)
        {
            Assert.AreEqual("No electronics found", UsbPorts.StatusText(port));
        }
    }
}
