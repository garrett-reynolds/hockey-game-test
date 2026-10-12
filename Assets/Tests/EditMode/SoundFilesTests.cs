using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class SoundFilesTests
    {
        [Test]
        public void A_sound_file_that_is_present_is_used_instead_of_the_placeholder()
        {
            var real = AudioClip.Create("real tick", 10, 1, 44100, false);
            var clip = SoundFiles.Load(SoundFiles.Tick, PlaceholderSounds.Tick, name => name == "tick" ? real : null);
            Assert.AreSame(real, clip);
            Object.DestroyImmediate(real);
        }

        [Test]
        public void With_no_sound_file_the_placeholder_is_used()
        {
            var clip = SoundFiles.Load(SoundFiles.Tick, PlaceholderSounds.Tick, name => null);
            Assert.AreEqual("Tick", clip.name);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void A_name_with_no_file_in_Resources_Sounds_falls_back_to_the_placeholder()
        {
            var clip = SoundFiles.Load("no-such-sound-file", PlaceholderSounds.Buzzer);
            Assert.IsNotNull(clip);
            Assert.AreEqual("Buzzer", clip.name);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void Chant_loads_the_chant_file_when_there_is_one()
        {
            var real = AudioClip.Create("real chant", 10, 1, 44100, false);
            var clip = SoundFiles.Load("chant", PlaceholderSounds.Chant, name => name == "chant" ? real : null);
            Assert.AreSame(real, clip);
            Object.DestroyImmediate(real);
        }

        [Test]
        public void Chant_falls_back_to_its_placeholder_when_the_file_is_missing()
        {
            var clip = SoundFiles.Load("chant", PlaceholderSounds.Chant, name => null);
            Assert.AreEqual("Chant", clip.name);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void Ooh_falls_back_to_its_placeholder_when_the_file_is_missing()
        {
            var clip = SoundFiles.Load(SoundFiles.Ooh, PlaceholderSounds.Ooh, name => null);
            Assert.AreEqual("Ooh", clip.name);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void Every_contact_sound_has_a_real_file_in_Resources_Sounds()
        {
            foreach (string name in new[] { SoundFiles.PostPing, SoundFiles.GloveCatch, SoundFiles.BlockerHit, SoundFiles.PadThud })
                Assert.IsNotNull(Resources.Load<AudioClip>(SoundFiles.Folder + "/" + name), name);
        }

        [Test]
        public void The_picked_post_ping_cheer_and_hockey_stop_clips_are_in_Resources_Sounds()
        {
            foreach (string name in new[] { SoundFiles.PostPing, SoundFiles.Cheer, SoundFiles.HockeyStop })
                Assert.IsNotNull(Resources.Load<AudioClip>(SoundFiles.Folder + "/" + name), name);
        }

        [Test]
        public void At_least_three_stride_variations_are_in_Resources_Sounds_each_shorter_than_one_stride()
        {
            Assert.GreaterOrEqual(SoundFiles.Strides.Length, 3);
            foreach (string name in SoundFiles.Strides)
            {
                var clip = Resources.Load<AudioClip>(SoundFiles.Folder + "/" + name);
                Assert.IsNotNull(clip, name);
                Assert.Less(clip.length, 0.6, name); // one bob cycle is 0.63 s: a stride never runs into the next
            }
        }

        [Test]
        public void The_hockey_stop_file_and_its_placeholder_last_as_long_as_the_hockey_stop_the_strides_leave_room_for()
        {
            var file = Resources.Load<AudioClip>(SoundFiles.Folder + "/" + SoundFiles.HockeyStop);
            Assert.AreEqual(RoundSoundCues.HockeyStopSeconds, file.length, 0.02);
            var placeholder = PlaceholderSounds.HockeyStop();
            Assert.AreEqual(RoundSoundCues.HockeyStopSeconds, placeholder.length, 0.02);
            Object.DestroyImmediate(placeholder);
        }

        [Test]
        public void Each_stride_variation_has_its_own_placeholder()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < SoundFiles.Strides.Length; i++)
            {
                int variation = i;
                var clip = SoundFiles.Load(SoundFiles.Strides[i], () => PlaceholderSounds.Stride(variation), name => null);
                Assert.Greater(clip.samples, 0);
                Assert.Less(clip.length, 0.6);
                names.Add(clip.name);
                Object.DestroyImmediate(clip);
            }
            Assert.AreEqual(SoundFiles.Strides.Length, names.Count);
        }
    }
}
