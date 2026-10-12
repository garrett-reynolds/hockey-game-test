using System;
using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Plays sound for the Round (real files from SoundFiles where present, else placeholders): crowd loop, result sounds, window ticks, slow-down muffle and
    /// heartbeat. Watches the Round through RoundSoundCues; holds no rules of its own. Uses real time.
    /// The tick plays unfiltered so it stays clear during the slow-down.
    /// The skate-in sounds have their own sources and are never muffled or slowed: one stride sound on each push of the
    /// camera's stride (a different variation each time, panned to the side the camera leans to), and the hockey stop,
    /// placed so it ends with the skate-in, fading in briefly if it starts mid-clip and fading out when the skate-in ends.
    /// The Clap Chant has its own source too (RoundSoundCues times it); a chant cut short by a new Round fades out.
    /// Each cue plays at RoundSoundCues.Volume; the master volume and mute apply to everything through the AudioListener.
    /// </summary>
    public sealed class RoundSound
    {
        const float CrowdFadePerSecond = 0.6f;
        const float HockeyStopFadeSeconds = 0.06f;
        /// <summary>How fast a chant cut short by a new Round fades out (seconds from full).</summary>
        const float ChantFadeSeconds = 0.15f;
        /// <summary>How far a stride sound is panned toward the side the camera leans to (-1..1 stereo pan).</summary>
        const float StridePan = 0.3f;
        /// <summary>Each stride plays this much higher or lower at random (pitch ratio), so repeats do not sound looped.</summary>
        const float StridePitchSpread = 0.05f;

        readonly SaveData _save;
        readonly RoundSoundCues _cues;
        readonly AudioSource _crowd, _heartbeat, _world, _tick, _hockeyStop, _strideLeft, _strideRight, _chant;
        readonly AudioSource _goalSong; // the Team Skin's goal song, with the horn; null when the skin has none

        /// <summary>The goal horn, Clap Chant and goal song clips (from the Team Skin's files); GoalSongClip is null with no song.</summary>
        public AudioClip GoalHornClip { get; }
        public AudioClip ChantClip => _chant.clip;
        public AudioClip GoalSongClip => _goalSong != null ? _goalSong.clip : null;
        readonly AudioLowPassFilter _crowdFilter, _heartbeatFilter, _worldFilter;
        readonly Dictionary<SoundCue, AudioClip[]> _clips = new Dictionary<SoundCue, AudioClip[]>();
        readonly AudioClip[] _strides;
        readonly System.Random _random = new System.Random();
        int _lastStride = -1;
        float _hockeyStopTarget;
        float _chantTarget;

        /// <summary>
        /// Tick on/off, master volume and mute are read from save every frame; the settings screen just changes them.
        /// chantRandom rolls the chant gaps: its own source, not the Round's. The goal horn, goal song and chant are the
        /// skin's (null: the League Skin's). findSkinSound looks those three files up (tests); by default SoundFiles' folder.
        /// </summary>
        public RoundSound(GameObject listener, SaveData save, RoundSettings settings, IRandomSource chantRandom, TeamSkin skin = null,
                          Func<string, AudioClip> findSkinSound = null)
        {
            skin = TeamSkin.OrLeague(skin);
            _save = save;

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
            _hockeyStop = Source(listener, "Hockey Stop", out _); // the skate-in is never slowed: no muffle or pitch
            _hockeyStop.clip = SoundFiles.Load(SoundFiles.HockeyStop, PlaceholderSounds.HockeyStop);
            _strideLeft = Source(listener, "Stride Left", out _);
            _strideLeft.panStereo = -StridePan;
            _strideRight = Source(listener, "Stride Right", out _);
            _strideRight.panStereo = StridePan;
            _strides = new AudioClip[SoundFiles.Strides.Length];
            for (int i = 0; i < _strides.Length; i++)
            {
                int variation = i;
                _strides[i] = SoundFiles.Load(SoundFiles.Strides[i], () => PlaceholderSounds.Stride(variation));
            }

            _clips[SoundCue.Tick] = new[] { SoundFiles.Load(SoundFiles.Tick, PlaceholderSounds.Tick) };
            _clips[SoundCue.FastTick] = new[] { SoundFiles.Load(SoundFiles.FastTick, PlaceholderSounds.FastTick) };
            _clips[SoundCue.Buzzer] = new[] { SoundFiles.Load(SoundFiles.Buzzer, PlaceholderSounds.Buzzer) };
            GoalHornClip = SoundFiles.Load(skin.GoalHorn, PlaceholderSounds.Horn, findSkinSound);
            _clips[SoundCue.GoalImpact] = new[] { SoundFiles.Load(SoundFiles.NetHit, PlaceholderSounds.NetHit), GoalHornClip };
            _clips[SoundCue.SaveImpact] = new[] { SoundFiles.Load(SoundFiles.PadThud, PlaceholderSounds.PadThud) };
            _clips[SoundCue.GloveCatch] = new[] { SoundFiles.Load(SoundFiles.GloveCatch, PlaceholderSounds.GloveCatch) };
            _clips[SoundCue.BlockerHit] = new[] { SoundFiles.Load(SoundFiles.BlockerHit, PlaceholderSounds.BlockerHit) };
            _clips[SoundCue.MissImpact] = new[] { SoundFiles.Load(SoundFiles.BoardsClang, PlaceholderSounds.BoardsClang) };
            _clips[SoundCue.PostPing] = new[] { SoundFiles.Load(SoundFiles.PostPing, PlaceholderSounds.PostPing) };
            _clips[SoundCue.Cheer] = new[] { SoundFiles.Load(SoundFiles.Cheer, PlaceholderSounds.Cheer) };
            AudioClip groan = SoundFiles.Load(SoundFiles.Groan, PlaceholderSounds.Groan);
            _clips[SoundCue.Groan] = new[] { groan };
            _clips[SoundCue.SmallGroan] = new[] { SoundFiles.Load(SoundFiles.SmallGroan, () => groan) };
            _clips[SoundCue.Ooh] = new[] { SoundFiles.Load(SoundFiles.Ooh, PlaceholderSounds.Ooh) };
            _clips[SoundCue.NewBestCheer] = new[] { SoundFiles.Load(SoundFiles.NewBestCheer, PlaceholderSounds.NewBestCheer) };

            _chant = Source(listener, "Chant", out _); // never in the slow-down: no muffle or pitch
            _chant.clip = SoundFiles.Load(skin.Chant, PlaceholderSounds.Chant, findSkinSound);
            AudioClip song = string.IsNullOrEmpty(skin.GoalSong) ? null : SoundFiles.Load(skin.GoalSong, () => null, findSkinSound);
            if (song != null)
            {
                _goalSong = Source(listener, "Goal Song", out _); // its own source: the chant's timing follows the horn alone
                _goalSong.clip = song;
            }
            _cues = new RoundSoundCues(save, chantRandom, CueSeconds);

            AudioListener.volume = (float)RoundSoundCues.ListenerVolume(save);
        }

        /// <summary>
        /// Call once per frame after the screen flow and ShotEffects update. puckArrived: ShotEffects.PuckArrivedThisFrame;
        /// contact: ShotEffects.ContactSound (what the puck visibly hits; see RoundSoundCues.Update). show: this frame's Arena
        /// Show (the Clap Chant follows it).
        /// </summary>
        public void Update(ScreenFlow flow, ArenaState show, bool puckArrived, float realSeconds, SoundCue? contact = null)
        {
            AudioListener.volume = (float)RoundSoundCues.ListenerVolume(_save);
            Round round = flow.Round;
            RoundPhase phase = round.Phase;
            double phaseSeconds = round.PhaseSecondsElapsed;

            foreach (SoundCue cue in _cues.Update(flow, show.Chant, puckArrived, realSeconds, contact))
            {
                if (cue == SoundCue.Chant) { StartChant(); continue; }
                if (cue == SoundCue.ChantEnd) { _chantTarget = 0f; continue; }
                if (cue == SoundCue.HockeyStop) { StartHockeyStop(round); continue; }
                if (cue == SoundCue.HockeyStopEnd) { _hockeyStopTarget = 0f; continue; }
                if (cue == SoundCue.StrideLeft || cue == SoundCue.StrideRight)
                {
                    PlayStride(cue == SoundCue.StrideLeft ? _strideLeft : _strideRight);
                    continue;
                }
                if (cue == SoundCue.GoalImpact && _goalSong != null)
                {
                    _goalSong.volume = (float)RoundSoundCues.Volume(cue);
                    _goalSong.Play();
                }
                AudioSource source = cue == SoundCue.Tick || cue == SoundCue.FastTick ? _tick : _world;
                foreach (AudioClip clip in _clips[cue])
                    source.PlayOneShot(clip, (float)RoundSoundCues.Volume(cue));
            }

            _chant.volume = Mathf.MoveTowards(_chant.volume, _chantTarget,
                (float)RoundSoundCues.ChantVolume * realSeconds / ChantFadeSeconds);
            if (_chantTarget == 0f && _chant.volume == 0f && _chant.isPlaying) _chant.Stop();

            _hockeyStop.volume = Mathf.MoveTowards(_hockeyStop.volume, _hockeyStopTarget, realSeconds / HockeyStopFadeSeconds);
            if (_hockeyStopTarget == 0f && _hockeyStop.volume == 0f && _hockeyStop.isPlaying) _hockeyStop.Stop();

            float target = (float)RoundSoundCues.CrowdLevel(phase, phaseSeconds, round.Settings);
            _crowd.volume = Mathf.MoveTowards(_crowd.volume, target, CrowdFadePerSecond * realSeconds);

            SlowMoSound slow = RoundSoundCues.SlowMo(phase, phaseSeconds);
            float cutoff = (float)slow.LowPassHz, pitch = (float)slow.Pitch;
            _crowdFilter.cutoffFrequency = _heartbeatFilter.cutoffFrequency = _worldFilter.cutoffFrequency = cutoff;
            _crowd.pitch = _world.pitch = pitch;

            if (slow.Heartbeat && !_heartbeat.isPlaying) _heartbeat.Play();
            else if (!slow.Heartbeat && _heartbeat.isPlaying) _heartbeat.Stop();
        }

        /// <summary>One stride variation, never the one played last, at a slightly random pitch, from this side's source.</summary>
        void PlayStride(AudioSource side)
        {
            int pick;
            if (_lastStride < 0) pick = _random.Next(_strides.Length);
            else
            {
                pick = _random.Next(_strides.Length - 1);
                if (pick >= _lastStride) pick++; // skip the last one
            }
            _lastStride = pick;
            side.pitch = 1f + StridePitchSpread * (float)(_random.NextDouble() * 2 - 1);
            side.PlayOneShot(_strides[pick]);
        }

        /// <summary>How long a cue's sound plays (its longest clip), so RoundSoundCues can time the chant around it.</summary>
        double CueSeconds(SoundCue cue)
        {
            if (cue == SoundCue.Chant) return _chant.clip.length;
            if (!_clips.TryGetValue(cue, out AudioClip[] clips)) return 0;
            float longest = 0f;
            foreach (AudioClip clip in clips) longest = Mathf.Max(longest, clip.length);
            return longest;
        }

        /// <summary>Plays the chant from its start (the file's own fade-in keeps it click-free).</summary>
        void StartChant()
        {
            _chantTarget = (float)RoundSoundCues.ChantVolume;
            _chant.volume = _chantTarget;
            _chant.Stop();
            _chant.Play();
        }

        /// <summary>Plays the hockey stop placed so it ends with the skate-in (RoundSoundCues.HockeyStopClipTime).</summary>
        void StartHockeyStop(Round round)
        {
            AudioClip clip = _hockeyStop.clip;
            double at = RoundSoundCues.HockeyStopClipTime(clip.length, round.Settings.SkateInSeconds, round.PhaseSecondsElapsed);
            _hockeyStop.Stop();
            _hockeyStopTarget = 1f;
            if (at < 0)
            {
                _hockeyStop.volume = 1f; // the clip's own fade-in plays
                _hockeyStop.PlayDelayed((float)-at);
            }
            else if (at < clip.length)
            {
                _hockeyStop.volume = at > 0 ? 0f : 1f; // starting mid-clip: fade in so it does not click
                _hockeyStop.Play();
                _hockeyStop.time = (float)at;
            }
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
