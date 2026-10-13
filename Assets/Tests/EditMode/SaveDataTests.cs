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
                LastUsbPort = "COM4",
                EasyMode = true,
                TestShotOnAtStart = false,
                InputIgnoreSeconds = 3.5,
                AutoReturnSeconds = 20.25,
                TickOn = false,
                MasterVolume = 0.4,
                Mute = true,
                ReplayOn = false,
                ForceGoalieBlocks = true,
                TeamSkin = "bears",
            };
        }

        private static void AssertSame(SaveData expected, SaveData actual)
        {
            Assert.AreEqual(expected.BestScore, actual.BestScore);
            Assert.AreEqual(expected.UsbPort, actual.UsbPort);
            Assert.AreEqual(expected.LastUsbPort, actual.LastUsbPort);
            Assert.AreEqual(expected.EasyMode, actual.EasyMode);
            Assert.AreEqual(expected.TestShotOnAtStart, actual.TestShotOnAtStart);
            Assert.AreEqual(expected.InputIgnoreSeconds, actual.InputIgnoreSeconds);
            Assert.AreEqual(expected.AutoReturnSeconds, actual.AutoReturnSeconds);
            Assert.AreEqual(expected.TickOn, actual.TickOn);
            Assert.AreEqual(expected.MasterVolume, actual.MasterVolume);
            Assert.AreEqual(expected.Mute, actual.Mute);
            Assert.AreEqual(expected.ReplayOn, actual.ReplayOn);
            Assert.AreEqual(expected.ForceGoalieBlocks, actual.ForceGoalieBlocks);
            Assert.AreEqual(expected.TeamSkin, actual.TeamSkin);
        }

        [Test]
        public void New_SaveData_HasSpecDefaults()
        {
            var d = new SaveData();
            Assert.AreEqual(0, d.BestScore);
            Assert.AreEqual("Auto", d.UsbPort);
            Assert.AreEqual("", d.LastUsbPort);
            Assert.IsFalse(d.EasyMode);
            Assert.IsTrue(d.TestShotOnAtStart);
            Assert.AreEqual(2, d.InputIgnoreSeconds);
            Assert.AreEqual(15, d.AutoReturnSeconds);
            Assert.IsTrue(d.TickOn);
            Assert.AreEqual(1.0, d.MasterVolume);
            Assert.IsFalse(d.Mute);
            Assert.IsTrue(d.ReplayOn, "the Replay plays unless it is turned off");
            Assert.IsFalse(d.ForceGoalieBlocks, "the goalie character shows by default; the blocks are a developer fallback");
            Assert.AreEqual("league", d.TeamSkin, "the League Skin plays unless the file picks another");
        }

        [Test]
        public void The_settings_file_picks_the_Team_Skin_and_a_blank_one_keeps_the_League_Skin()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "teamSkin=bears\n");
            Assert.AreEqual("bears", SaveData.Load(_path).TeamSkin);
            File.WriteAllText(_path, "teamSkin=\n");
            Assert.AreEqual(SaveData.LeagueSkin, SaveData.Load(_path).TeamSkin);
        }

        [Test]
        public void An_old_settings_file_with_the_character_turned_off_still_shows_the_goalie_character()
        {
            // Every file saved before #27 holds goalieCharacterOn=false; that old flag no longer hides the character.
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "bestScore=3\ngoalieCharacterOn=false\n");
            var d = SaveData.Load(_path);
            Assert.AreEqual(3, d.BestScore);
            Assert.IsFalse(d.ForceGoalieBlocks);
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
            var text = File.ReadAllText(_path).Replace("autoReturnSeconds=20.25", "autoReturnSeconds=soon");
            File.WriteAllText(_path, text);

            var loaded = SaveData.Load(_path);

            Assert.AreEqual(15, loaded.AutoReturnSeconds);
            original.AutoReturnSeconds = 15;
            AssertSame(original, loaded);
        }

        [Test]
        public void Load_OutOfRangeNumbers_ClampToTheSettingsScreenLimits()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path,
                "inputIgnoreSeconds=-3\nautoReturnSeconds=0\nmasterVolume=-1\n");

            var loaded = SaveData.Load(_path);

            Assert.AreEqual(0.0, loaded.InputIgnoreSeconds);
            Assert.AreEqual(1.0, loaded.AutoReturnSeconds);
            Assert.AreEqual(0.0, loaded.MasterVolume);
        }

        [Test]
        public void Load_TooLargeNumbers_ClampToTheSettingsScreenMaximums()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "inputIgnoreSeconds=99\nautoReturnSeconds=500\nmasterVolume=3\n");

            var loaded = SaveData.Load(_path);

            Assert.AreEqual(30.0, loaded.InputIgnoreSeconds);
            Assert.AreEqual(120.0, loaded.AutoReturnSeconds);
            Assert.AreEqual(1.0, loaded.MasterVolume);
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
                StringAssert.Contains("autoReturnSeconds=20.25", File.ReadAllText(_path));
                AssertSame(original, SaveData.Load(_path));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Test]
        public void Load_AFileFromBeforeReachInTime_IgnoresTheOldContestedKeysAndKeepsTheRest()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "bestScore=4\ncontestedLowChance=0.3\ncontestedTopChance=0.9\ntopSpeedKmh=100\nmasterVolume=0.5\n");

            var loaded = SaveData.Load(_path);

            Assert.AreEqual(4, loaded.BestScore);
            Assert.AreEqual(0.5, loaded.MasterVolume);
            loaded.Save(_path);
            StringAssert.DoesNotContain("contested", File.ReadAllText(_path));
            StringAssert.DoesNotContain("topSpeed", File.ReadAllText(_path));
        }
    }
}
