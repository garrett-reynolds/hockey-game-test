using System.Globalization;
using System.IO;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class SaveDataTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hockey-savedata-" + Path.GetRandomFileName());
            _path = Path.Combine(_dir, "save.txt");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static SaveData NonDefault()
        {
            return new SaveData
            {
                BestScore = 5,
                UsbPort = "COM7",
                EasyMode = true,
                ContestedLowChance = 0.35,
                ContestedTopChance = 0.7,
                TopSpeedKmh = 95.5,
                TestShotOnAtStart = false,
                InputIgnoreSeconds = 3.5,
                AutoReturnSeconds = 20.25,
                TickOn = false,
                MasterVolume = 0.4,
                Mute = true,
            };
        }

        private static void AssertSame(SaveData expected, SaveData actual)
        {
            Assert.AreEqual(expected.BestScore, actual.BestScore);
            Assert.AreEqual(expected.UsbPort, actual.UsbPort);
            Assert.AreEqual(expected.EasyMode, actual.EasyMode);
            Assert.AreEqual(expected.ContestedLowChance, actual.ContestedLowChance);
            Assert.AreEqual(expected.ContestedTopChance, actual.ContestedTopChance);
            Assert.AreEqual(expected.TopSpeedKmh, actual.TopSpeedKmh);
            Assert.AreEqual(expected.TestShotOnAtStart, actual.TestShotOnAtStart);
            Assert.AreEqual(expected.InputIgnoreSeconds, actual.InputIgnoreSeconds);
            Assert.AreEqual(expected.AutoReturnSeconds, actual.AutoReturnSeconds);
            Assert.AreEqual(expected.TickOn, actual.TickOn);
            Assert.AreEqual(expected.MasterVolume, actual.MasterVolume);
            Assert.AreEqual(expected.Mute, actual.Mute);
        }

        [Test]
        public void New_SaveData_HasSpecDefaults()
        {
            var d = new SaveData();
            Assert.AreEqual(0, d.BestScore);
            Assert.AreEqual("Auto", d.UsbPort);
            Assert.IsFalse(d.EasyMode);
            Assert.AreEqual(0.50, d.ContestedLowChance);
            Assert.AreEqual(0.85, d.ContestedTopChance);
            Assert.AreEqual(120, d.TopSpeedKmh);
            Assert.IsTrue(d.TestShotOnAtStart);
            Assert.AreEqual(2, d.InputIgnoreSeconds);
            Assert.AreEqual(15, d.AutoReturnSeconds);
            Assert.IsTrue(d.TickOn);
            Assert.AreEqual(1.0, d.MasterVolume);
            Assert.IsFalse(d.Mute);
        }

        [Test]
        public void SaveThenLoad_RoundTripsEveryField()
        {
            var original = NonDefault();
            Assert.IsTrue(original.Save(_path));
            AssertSame(original, SaveData.Load(_path));
        }

        [Test]
        public void Load_MissingFile_GivesDefaults()
        {
            AssertSame(new SaveData(), SaveData.Load(_path));
        }

        [Test]
        public void Load_GarbageFile_GivesDefaultsWithoutThrowing()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "\u0001 not a save file\n=\n===\n");
            AssertSame(new SaveData(), SaveData.Load(_path));
        }

        [Test]
        public void Load_BadNumberForOneKey_DefaultsThatKeyAndKeepsTheOthers()
        {
            var original = NonDefault();
            original.Save(_path);
            var text = File.ReadAllText(_path).Replace("topSpeedKmh=95.5", "topSpeedKmh=fast");
            File.WriteAllText(_path, text);

            var loaded = SaveData.Load(_path);

            Assert.AreEqual(120, loaded.TopSpeedKmh);
            original.TopSpeedKmh = 120;
            AssertSame(original, loaded);
        }

        [Test]
        public void SaveThenLoad_UnderCommaDecimalCulture_KeepsDecimals()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var original = NonDefault();
                original.Save(_path);
                StringAssert.Contains("topSpeedKmh=95.5", File.ReadAllText(_path));
                AssertSame(original, SaveData.Load(_path));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Test]
        public void ToRoundSettings_CopiesChancesAndTopSpeed()
        {
            var settings = NonDefault().ToRoundSettings();
            Assert.AreEqual(0.35, settings.ContestedLowChance);
            Assert.AreEqual(0.7, settings.ContestedTopChance);
            Assert.AreEqual(95.5, settings.TopSpeedKmh);
        }
    }
}
