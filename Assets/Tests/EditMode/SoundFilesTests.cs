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
        public void Every_contact_sound_has_a_real_file_in_Resources_Sounds()
        {
            foreach (string name in new[] { SoundFiles.PostPing, SoundFiles.GloveCatch, SoundFiles.BlockerHit, SoundFiles.PadThud })
                Assert.IsNotNull(Resources.Load<AudioClip>(SoundFiles.Folder + "/" + name), name);
        }
    }
}
