using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>Saves sound settings on the PC with PlayerPrefs.</summary>
    public sealed class PlayerPrefsSoundStore : ISoundSettingsStore
    {
        public bool TryGet(string key, out float value)
        {
            value = PlayerPrefs.GetFloat(key, 0f);
            return PlayerPrefs.HasKey(key);
        }

        public void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Plays placeholder sound for the Round: crowd loop, result sounds, window ticks, slow-down muffle and
    /// heartbeat. Watches the Round through RoundSoundCues; holds no rules of its own. Uses real time.
    /// The tick plays unfiltered so it stays clear during the slow-down.
    /// </summary>
    public sealed class RoundSound
    {
        const float CrowdFadePerSecond = 0.6f;

        readonly ISoundSettingsStore _store;
        readonly RoundSoundCues _cues;
        readonly AudioSource _crowd, _heartbeat, _world, _tick;
        readonly AudioLowPassFilter _crowdFilter, _heartbeatFilter, _worldFilter;
        readonly Dictionary<SoundCue, AudioClip[]> _clips = new Dictionary<SoundCue, AudioClip[]>();

        /// <summary>Master volume, mute and tick on/off. Change fields, then call ApplySettings to use and save them.</summary>
        public SoundSettings Settings { get; }

        public RoundSound(GameObject listener, ISoundSettingsStore store)
        {
            _store = store;
            Settings = SoundSettings.Load(store);
            _cues = new RoundSoundCues(Settings);

            _crowd = Source(listener, "Crowd", out _crowdFilter);
            _crowd.clip = PlaceholderSounds.CrowdLoop();
            _crowd.loop = true;
            _crowd.volume = (float)RoundSoundCues.CrowdLevel(RoundPhase.Idle, 0);
            _crowd.Play();

            _heartbeat = Source(listener, "Heartbeat", out _heartbeatFilter);
            _heartbeat.clip = PlaceholderSounds.HeartbeatLoop();
            _heartbeat.loop = true;

            _world = Source(listener, "Result Sounds", out _worldFilter);
            _tick = Source(listener, "Tick", out _);

            var cheer = PlaceholderSounds.Cheer();
            var groan = PlaceholderSounds.Groan();
            _clips[SoundCue.Tick] = new[] { PlaceholderSounds.Tick() };
            _clips[SoundCue.FastTick] = new[] { PlaceholderSounds.FastTick() };
            _clips[SoundCue.Buzzer] = new[] { PlaceholderSounds.Buzzer() };
            _clips[SoundCue.GoalImpact] = new[] { PlaceholderSounds.NetHit(), PlaceholderSounds.Horn() };
            _clips[SoundCue.SaveImpact] = new[] { PlaceholderSounds.PadThud() };
            _clips[SoundCue.MissImpact] = new[] { PlaceholderSounds.BoardsClang() };
            _clips[SoundCue.Cheer] = new[] { cheer };
            _clips[SoundCue.Groan] = new[] { groan };
            _clips[SoundCue.NewBestCheer] = new[] { PlaceholderSounds.NewBestCheer() };

            ApplySettings();
        }

        /// <summary>Use the current Settings now and save them on the PC.</summary>
        public void ApplySettings()
        {
            AudioListener.volume = Settings.EffectiveVolume;
            Settings.Save(_store);
        }

        /// <summary>Call once per frame after the Round and ShotEffects update. puckArrived: ShotEffects.PuckArrivedThisFrame.</summary>
        public void Update(Round round, bool puckArrived, float realSeconds)
        {
            RoundPhase phase = round.Phase;
            double phaseSeconds = round.PhaseSecondsElapsed;

            foreach (SoundCue cue in _cues.Update(round, puckArrived))
            {
                AudioSource source = cue == SoundCue.Tick || cue == SoundCue.FastTick ? _tick : _world;
                foreach (AudioClip clip in _clips[cue])
                    source.PlayOneShot(clip);
            }

            float target = (float)RoundSoundCues.CrowdLevel(phase, phaseSeconds);
            _crowd.volume = Mathf.MoveTowards(_crowd.volume, target, CrowdFadePerSecond * realSeconds);

            SlowMoSound slow = RoundSoundCues.SlowMo(phase, phaseSeconds);
            float cutoff = (float)slow.LowPassHz, pitch = (float)slow.Pitch;
            _crowdFilter.cutoffFrequency = _heartbeatFilter.cutoffFrequency = _worldFilter.cutoffFrequency = cutoff;
            _crowd.pitch = _world.pitch = pitch;

            if (slow.Heartbeat && !_heartbeat.isPlaying) _heartbeat.Play();
            else if (!slow.Heartbeat && _heartbeat.isPlaying) _heartbeat.Stop();
        }

        static AudioSource Source(GameObject parent, string name, out AudioLowPassFilter filter)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D
            filter = child.AddComponent<AudioLowPassFilter>(); // filters only this child's source
            filter.cutoffFrequency = (float)RoundSoundCues.OpenLowPassHz;
            return source;
        }
    }
}
