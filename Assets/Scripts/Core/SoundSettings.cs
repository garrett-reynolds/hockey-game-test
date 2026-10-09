namespace HockeyGame.Core
{
    /// <summary>Where sound settings are saved on the PC. The game uses PlayerPrefs; tests use memory.</summary>
    public interface ISoundSettingsStore
    {
        bool TryGet(string key, out float value);
        void Set(string key, float value);
    }

    /// <summary>
    /// Master volume, mute and the shooting window tick on/off. Plain fields so the settings screen (#17) can
    /// change them; call Save after a change so they persist.
    /// </summary>
    public sealed class SoundSettings
    {
        public const float DefaultMasterVolume = 0.8f;
        private const string VolumeKey = "Sound.MasterVolume";
        private const string MutedKey = "Sound.Muted";
        private const string TickOnKey = "Sound.TickOn";

        private float _masterVolume = DefaultMasterVolume;

        /// <summary>0 (silent) to 1 (full). Values outside are clamped.</summary>
        public float MasterVolume
        {
            get => _masterVolume;
            set => _masterVolume = value < 0f ? 0f : value > 1f ? 1f : value;
        }

        public bool Muted;

        /// <summary>Tick each second of the shooting window.</summary>
        public bool TickOn = true;

        /// <summary>Volume to play at: 0 when muted, else MasterVolume.</summary>
        public float EffectiveVolume => Muted ? 0f : _masterVolume;

        public static SoundSettings Load(ISoundSettingsStore store)
        {
            var settings = new SoundSettings();
            if (store.TryGet(VolumeKey, out float volume)) settings.MasterVolume = volume;
            if (store.TryGet(MutedKey, out float muted)) settings.Muted = muted != 0f;
            if (store.TryGet(TickOnKey, out float tickOn)) settings.TickOn = tickOn != 0f;
            return settings;
        }

        public void Save(ISoundSettingsStore store)
        {
            store.Set(VolumeKey, _masterVolume);
            store.Set(MutedKey, Muted ? 1f : 0f);
            store.Set(TickOnKey, TickOn ? 1f : 0f);
        }
    }
}
