using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The crowd's loudness order and the master volume, through the sound cue seam (RoundSoundCues).</summary>
    public class RoundSoundLoudnessTests
    {
        [Test]
        public void Loudness_order_is_horn_then_cheer_then_crowd_loop()
        {
            var settings = new RoundSettings();
            double horn = RoundSoundCues.Volume(SoundCue.GoalImpact);
            double cheer = RoundSoundCues.Volume(SoundCue.Cheer);
            Assert.Greater(horn, cheer);
            Assert.Greater(cheer, RoundSoundCues.CrowdLevel(RoundPhase.Idle, 0, settings));
            Assert.Greater(cheer, RoundSoundCues.CrowdLevel(RoundPhase.RoundOver, 0, settings));
            Assert.AreEqual(RoundSoundCues.Volume(SoundCue.Cheer), RoundSoundCues.Volume(SoundCue.NewBestCheer));
        }

        [Test]
        public void A_small_groan_is_softer_than_the_Save_groan()
        {
            Assert.Less(RoundSoundCues.Volume(SoundCue.SmallGroan), RoundSoundCues.Volume(SoundCue.Groan));
        }

        // Every sound plays through the AudioListener, whose volume is ListenerVolume (RoundSound).
        [TestCase(false, 1.0, 1.0)]
        [TestCase(false, 0.5, 0.5)]
        [TestCase(true, 1.0, 0.0)]
        [TestCase(true, 0.5, 0.0)]
        public void Mute_and_master_volume_apply_to_every_sound(bool mute, double master, double scale)
        {
            var save = new SaveData { Mute = mute, MasterVolume = master };
            Assert.AreEqual(scale, RoundSoundCues.ListenerVolume(save), 1e-9);
        }
    }
}
