using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Plays sound for the Round (real files from SoundFiles where present, else placeholders): crowd loop, result sounds, window ticks, slow-down muffle and
    /// heartbeat. Watches the Round through RoundSoundCues; holds no rules of its own. Uses real time.
    /// The tick plays unfiltered so it stays clear during the slow-down.
    /// </summary>
    public sealed class RoundSound
    {
        const float CrowdFadePerSecond = 0.6f;

        readonly SaveData _save;
        readonly RoundSoundCues _cues;
        readonly AudioSource _crowd, _heartbeat, _world, _tick;
        readonly AudioLowPassFilter _crowdFilter, _heartbeatFilter, _worldFilter;
        readonly Dictionary<SoundCue, AudioClip[]> _clips = new Dictionary<SoundCue, AudioClip[]>();

        /// <summary>Tick on/off, master volume and mute are read from save every frame; the settings screen just changes them.</summary>
        public RoundSound(GameObject listener, SaveData save, RoundSettings settings)
        {
            _save = save;
            _cues = new RoundSoundCues(save);

            _crowd = Source(listener, "Crowd", out _crowdFilter);
            _crowd.clip = SoundFiles.Load(SoundFiles.CrowdLoop, PlaceholderSounds.CrowdLoop);
            _crowd.loop = true;
            _crowd.volume = (float)RoundSoundCues.CrowdLevel(RoundPhase.Idle, 0, settings);
            _crowd.Play();

            _heartbeat = Source(listener, "Heartbeat", out _heartbeatFilter);
            _heartbeat.clip = SoundFiles.Load(SoundFiles.HeartbeatLoop, PlaceholderSounds.HeartbeatLoop);
            _heartbeat.loop = true;

            _world = Source(listener, "Result Sounds", out _worldFilter);
            _tick = Source(listener, "Tick", out _);

            _clips[SoundCue.Tick] = new[] { SoundFiles.Load(SoundFiles.Tick, PlaceholderSounds.Tick) };
            _clips[SoundCue.FastTick] = new[] { SoundFiles.Load(SoundFiles.FastTick, PlaceholderSounds.FastTick) };
            _clips[SoundCue.Buzzer] = new[] { SoundFiles.Load(SoundFiles.Buzzer, PlaceholderSounds.Buzzer) };
            _clips[SoundCue.GoalImpact] = new[]
            {
                SoundFiles.Load(SoundFiles.NetHit, PlaceholderSounds.NetHit),
                SoundFiles.Load(SoundFiles.GoalHorn, PlaceholderSounds.Horn)
            };
            _clips[SoundCue.SaveImpact] = new[] { SoundFiles.Load(SoundFiles.PadThud, PlaceholderSounds.PadThud) };
            _clips[SoundCue.MissImpact] = new[] { SoundFiles.Load(SoundFiles.BoardsClang, PlaceholderSounds.BoardsClang) };
            _clips[SoundCue.Cheer] = new[] { SoundFiles.Load(SoundFiles.Cheer, PlaceholderSounds.Cheer) };
            _clips[SoundCue.Groan] = new[] { SoundFiles.Load(SoundFiles.Groan, PlaceholderSounds.Groan) };
            _clips[SoundCue.NewBestCheer] = new[] { SoundFiles.Load(SoundFiles.NewBestCheer, PlaceholderSounds.NewBestCheer) };

            AudioListener.volume = (float)RoundSoundCues.ListenerVolume(save);
        }

        /// <summary>Call once per frame after the Round and ShotEffects update. puckArrived: ShotEffects.PuckArrivedThisFrame.</summary>
        public void Update(Round round, bool puckArrived, float realSeconds)
        {
            AudioListener.volume = (float)RoundSoundCues.ListenerVolume(_save);
            RoundPhase phase = round.Phase;
            double phaseSeconds = round.PhaseSecondsElapsed;

            foreach (SoundCue cue in _cues.Update(round, puckArrived))
            {
                AudioSource source = cue == SoundCue.Tick || cue == SoundCue.FastTick ? _tick : _world;
                foreach (AudioClip clip in _clips[cue])
                    source.PlayOneShot(clip);
            }

            float target = (float)RoundSoundCues.CrowdLevel(phase, phaseSeconds, round.Settings);
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
