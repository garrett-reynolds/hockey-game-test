using System;
using System.Linq;
using System.Reflection;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The Clap Chant is gone, and the code-made glass bang, netting rustle and "ooh" with it (Garrett, 2026-10-13).
    /// Those three keep their cues and file names; with no file in Assets/Resources/Sounds they play nothing.
    /// Reflection keeps these checks compiling before and after the removal.
    /// </summary>
    public class NoCodeMadeSoundsTests
    {
        [TestCase(RoundPhase.Idle)]
        [TestCase(RoundPhase.RoundOver)]
        public void The_Arena_Show_has_no_chant_on_the_start_screen_or_at_Round_over(RoundPhase phase)
        {
            ArenaState state = ArenaShow.At(new ArenaShowInput { Phase = phase });
            Assert.IsNull(state.GetType().GetProperty("Chant"), phase.ToString());
        }

        [Test]
        public void No_chant_cue_exists()
        {
            string[] names = Enum.GetNames(typeof(SoundCue));
            Assert.IsFalse(names.Any(n => n.IndexOf("Chant", StringComparison.OrdinalIgnoreCase) >= 0), string.Join(",", names));
        }

        [TestCase("Chant")]
        [TestCase("GlassBang")]
        [TestCase("NetRustle")]
        [TestCase("Ooh")]
        public void There_is_no_code_made_placeholder_for(string name)
        {
            Type placeholders = typeof(RoundSound).Assembly.GetType("HockeyGame.PlaceholderSounds");
            Assert.IsNotNull(placeholders);
            Assert.IsNull(placeholders.GetMethod(name, BindingFlags.Public | BindingFlags.Static), name);
        }

        // With no file in Assets/Resources/Sounds, glass-bang, net-rustle and ooh play nothing; the sounds that have files still play.
        [Test]
        public void With_no_file_the_glass_netting_and_ooh_cues_play_nothing_and_there_is_no_Chant_source()
        {
            var listener = new UnityEngine.GameObject("Listener");
            try
            {
                var save = new SaveData();
                var flow = new ScreenFlow(ScriptedRandom.Halves(), save, FakeReach.NeverReaches());
                var sound = new RoundSound(listener, save, new RoundSettings());
                for (int i = 0; i < 20; i++) sound.Update(flow, 0.5f); // the start screen
                Assert.AreEqual(0, sound.ClipCount(SoundCue.NetRustle), "netting");
                // Garrett's picked real files are in Resources/Sounds: hard and soft glass, and three oohs.
                Assert.AreEqual(1, sound.ClipCount(SoundCue.GlassImpact), "glass hard");
                Assert.AreEqual(1, sound.ClipCount(SoundCue.GlassImpactSoft), "glass soft");
                Assert.AreEqual(3, sound.ClipCount(SoundCue.Ooh), "ooh variations");
                Assert.Greater(sound.ClipCount(SoundCue.Cheer), 0, "a sound with a file still plays");
                foreach (UnityEngine.AudioSource source in listener.GetComponentsInChildren<UnityEngine.AudioSource>())
                    StringAssert.DoesNotContain("Chant", source.gameObject.name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(listener);
            }
        }

        [Test]
        public void The_Team_Skin_has_no_chant_file_name()
        {
            Assert.IsNull(typeof(TeamSkin).GetField("Chant"));
        }
    }
}
