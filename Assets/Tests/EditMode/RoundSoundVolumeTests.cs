using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The volume sliders reach sounds that are already playing (#155). A one-shot's loudness is its source's volume times the
    /// PlayOneShot scale, so each group's one-shot source follows its slider every frame; the numbers are checked, not the ears.
    /// </summary>
    public class RoundSoundVolumeTests
    {
        // The goal song is the Music group: Music slider times the horn's cue volume, whatever the Game slider says (mapping test).
        [TestCase(1.0, 1.0, 1.0)]
        [TestCase(0.5, 1.0, 0.5)]
        [TestCase(0.5, 0.2, 0.5)]
        [TestCase(0.0, 1.0, 0.0)]
        [TestCase(1.5, 1.0, 1.0)]
        public void The_goal_song_follows_the_Music_slider_only(double music, double game, double sliderShare)
        {
            var save = new SaveData { MusicVolume = music, GameVolume = game, CrowdVolume = 0.1 };
            Assert.AreEqual(RoundSoundCues.Volume(SoundCue.GoalImpact) * sliderShare, RoundSoundCues.GoalSongVolume(save), 1e-9);
        }

        [Test]
        public void The_goal_song_and_a_cue_scale_the_same_way()
        {
            var save = new SaveData { MusicVolume = 0.4, GameVolume = 0.7, CrowdVolume = 0.9 };
            Assert.AreEqual(
                RoundSoundCues.Volume(SoundCue.GoalImpact) * RoundSoundCues.GroupVolume(save, SoundGroup.Music),
                RoundSoundCues.CueVolume(save, SoundCue.GoalImpact, SoundGroup.Music), 1e-9);
            Assert.AreEqual(
                RoundSoundCues.Volume(SoundCue.Cheer) * RoundSoundCues.GroupVolume(save, SoundGroup.Crowd),
                RoundSoundCues.CueVolume(save, SoundCue.Cheer, RoundSoundCues.GroupOf(SoundCue.Cheer)), 1e-9);
        }

        static AudioClip Clip(string name) => AudioClip.Create(name, 10, 1, 44100, false);

        // Builds a RoundSound with a goal song, runs one frame, moves the sliders, runs another frame, and checks each group's
        // one-shot source (cheer and horn already playing sit on these sources) now sits at its new slider.
        [Test]
        public void A_one_shot_already_playing_follows_its_slider_each_frame()
        {
            var listener = new GameObject("Listener");
            var skin = ScriptableObject.CreateInstance<TeamSkin>();
            skin.GoalHorn = "horn-test";
            skin.GoalSong = "song-test";
            skin.Chant = "chant-test";
            var made = new System.Collections.Generic.List<AudioClip>();
            AudioClip Find(string name) { AudioClip c = Clip(name); made.Add(c); return c; }
            try
            {
                var save = new SaveData { CrowdVolume = 1, GameVolume = 1, MusicVolume = 1 };
                var flow = new ScreenFlow(ScriptedRandom.Halves(), save, FakeReach.NeverReaches());
                var sound = new RoundSound(listener, save, new RoundSettings(), new SeededRandom(1), skin, Find);
                sound.Update(flow, ArenaShow.From(flow), 0.016f);
                Assert.AreEqual(1f, sound.OneShotVolume(SoundGroup.Crowd), 1e-6);
                Assert.AreEqual(1f, sound.OneShotVolume(SoundGroup.Game), 1e-6);

                save.CrowdVolume = 0.3;
                save.GameVolume = 0.6;
                save.MusicVolume = 0.1;
                sound.Update(flow, ArenaShow.From(flow), 0.016f);

                Assert.AreEqual(0.3f, sound.OneShotVolume(SoundGroup.Crowd), 1e-6, "cheers and groans follow the Crowd slider");
                Assert.AreEqual(0.6f, sound.OneShotVolume(SoundGroup.Game), 1e-6, "horn, buzzer and puck follow the Game slider");
                Assert.AreEqual((float)RoundSoundCues.GoalSongVolume(save), sound.GoalSongPlayVolume, 1e-6, "the goal song follows Music");
            }
            finally
            {
                Object.DestroyImmediate(listener);
                Object.DestroyImmediate(skin);
                foreach (AudioClip c in made) Object.DestroyImmediate(c);
            }
        }
    }
}
