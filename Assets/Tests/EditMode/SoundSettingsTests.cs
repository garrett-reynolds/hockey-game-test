using System.Collections.Generic;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class SoundSettingsTests
    {
        private sealed class MemoryStore : ISoundSettingsStore
        {
            private readonly Dictionary<string, float> _values = new Dictionary<string, float>();
            public bool TryGet(string key, out float value) => _values.TryGetValue(key, out value);
            public void Set(string key, float value) => _values[key] = value;
        }

        [Test]
        public void Defaults_are_loud_with_tick_on_and_not_muted()
        {
            var settings = new SoundSettings();
            Assert.AreEqual(0.8f, settings.MasterVolume, 1e-6);
            Assert.IsFalse(settings.Muted);
            Assert.IsTrue(settings.TickOn);
            Assert.AreEqual(0.8f, settings.EffectiveVolume, 1e-6);
        }

        [TestCase(1.5f, 1f)]
        [TestCase(-0.2f, 0f)]
        [TestCase(0.3f, 0.3f)]
        public void Master_volume_stays_between_0_and_1(float set, float expected)
        {
            var settings = new SoundSettings { MasterVolume = set };
            Assert.AreEqual(expected, settings.MasterVolume, 1e-6);
        }

        [Test]
        public void Mute_silences_without_losing_the_volume()
        {
            var settings = new SoundSettings { MasterVolume = 0.6f, Muted = true };
            Assert.AreEqual(0f, settings.EffectiveVolume, 1e-6);
            settings.Muted = false;
            Assert.AreEqual(0.6f, settings.EffectiveVolume, 1e-6);
        }

        [Test]
        public void Settings_saved_on_the_PC_load_back()
        {
            var store = new MemoryStore();
            new SoundSettings { MasterVolume = 0.35f, Muted = true, TickOn = false }.Save(store);

            var loaded = SoundSettings.Load(store);
            Assert.AreEqual(0.35f, loaded.MasterVolume, 1e-6);
            Assert.IsTrue(loaded.Muted);
            Assert.IsFalse(loaded.TickOn);
        }

        [Test]
        public void Nothing_saved_loads_the_defaults()
        {
            var loaded = SoundSettings.Load(new MemoryStore());
            Assert.AreEqual(0.8f, loaded.MasterVolume, 1e-6);
            Assert.IsFalse(loaded.Muted);
            Assert.IsTrue(loaded.TickOn);
        }
    }
}
