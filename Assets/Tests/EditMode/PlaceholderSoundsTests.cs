using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class PlaceholderSoundsTests
    {
        [Test]
        public void Every_placeholder_sound_builds_a_clip()
        {
            var clips = new[]
            {
                PlaceholderSounds.CrowdLoop(), PlaceholderSounds.HeartbeatLoop(), PlaceholderSounds.Tick(),
                PlaceholderSounds.FastTick(), PlaceholderSounds.Buzzer(), PlaceholderSounds.NetHit(),
                PlaceholderSounds.Horn(), PlaceholderSounds.PadThud(), PlaceholderSounds.BoardsClang(),
                PlaceholderSounds.Cheer(), PlaceholderSounds.Groan(), PlaceholderSounds.NewBestCheer(),
                PlaceholderSounds.PostPing(), PlaceholderSounds.GloveCatch(), PlaceholderSounds.BlockerHit(), PlaceholderSounds.NetRustle(),
                PlaceholderSounds.HockeyStop(), PlaceholderSounds.Stride(0), PlaceholderSounds.Chant(), PlaceholderSounds.Ooh()
            };
            foreach (var clip in clips)
            {
                Assert.IsNotNull(clip);
                Assert.Greater(clip.samples, 0, clip.name);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Chant_and_ooh_placeholders_start_and_end_silent_so_they_do_not_click()
        {
            foreach (var clip in new[] { PlaceholderSounds.Chant(), PlaceholderSounds.Ooh() })
            {
                var samples = new float[clip.samples];
                clip.GetData(samples, 0);
                Assert.Less(Mathf.Abs(samples[0]), 0.01f, clip.name + " start");
                Assert.Less(Mathf.Abs(samples[samples.Length - 1]), 0.01f, clip.name + " end");
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Chant_placeholder_is_a_Lets_go_and_five_claps_long()
        {
            var clip = PlaceholderSounds.Chant();
            Assert.Greater(clip.length, 2f);
            Assert.Less(clip.length, 4f);
            Object.DestroyImmediate(clip);
        }
    }
}