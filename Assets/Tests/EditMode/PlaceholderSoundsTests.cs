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
                PlaceholderSounds.Cheer(), PlaceholderSounds.Groan(), PlaceholderSounds.NewBestCheer()
            };
            foreach (var clip in clips)
            {
                Assert.IsNotNull(clip);
                Assert.Greater(clip.samples, 0, clip.name);
                Object.DestroyImmediate(clip);
            }
        }
    }
}
