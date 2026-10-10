using System;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Finds each sound's clip. To use a real sound, drop an audio file (.wav, .ogg or .mp3) into
    /// Assets/Resources/Sounds/ named after the sound below, for example Assets/Resources/Sounds/goal-horn.wav.
    /// A sound with no file plays its code-made placeholder from PlaceholderSounds. No code change is needed.
    /// File names: crowd-loop, heartbeat-loop, tick, fast-tick, buzzer, net-hit, goal-horn, pad-thud,
    /// boards-clang, cheer, groan, new-best-cheer, post-ping, glove-catch, blocker-hit, hockey-stop, stride-1 .. stride-4.
    /// </summary>
    public static class SoundFiles
    {
        /// <summary>Folder under Assets/Resources that holds the sound files.</summary>
        public const string Folder = "Sounds";

        public const string CrowdLoop = "crowd-loop";
        public const string HeartbeatLoop = "heartbeat-loop";
        public const string Tick = "tick";
        public const string FastTick = "fast-tick";
        public const string Buzzer = "buzzer";
        public const string NetHit = "net-hit";
        public const string GoalHorn = "goal-horn";
        public const string PadThud = "pad-thud";
        public const string BoardsClang = "boards-clang";
        public const string Cheer = "cheer";
        public const string Groan = "groan";
        public const string NewBestCheer = "new-best-cheer";
        public const string PostPing = "post-ping";
        public const string GloveCatch = "glove-catch";
        public const string BlockerHit = "blocker-hit";
        /// <summary>The hockey stop that ends the skate-in, cut to RoundSoundCues.HockeyStopSeconds.</summary>
        public const string HockeyStop = "hockey-stop";
        /// <summary>One blade push each; a stride plays one of them, never the same one twice running.</summary>
        public static readonly string[] Strides = { "stride-1", "stride-2", "stride-3", "stride-4" };

        /// <summary>
        /// The clip in the sound file with this name (no extension), or the placeholder when there is none.
        /// findFile looks a name up; by default it loads from Assets/Resources/Sounds.
        /// </summary>
        public static AudioClip Load(string fileName, Func<AudioClip> placeholder, Func<string, AudioClip> findFile = null)
        {
            AudioClip clip = (findFile ?? FromResources)(fileName);
            return clip != null ? clip : placeholder(); // Unity null check: no ??
        }

        static AudioClip FromResources(string fileName) => Resources.Load<AudioClip>(Folder + "/" + fileName);
    }
}
