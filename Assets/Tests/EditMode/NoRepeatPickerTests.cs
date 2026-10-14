using System.Collections.Generic;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The goalie's celebration and upset pools: never the same clip twice in a row (Garrett, 2026-10-14).</summary>
    public class NoRepeatPickerTests
    {
        static string[] Pool(int n)
        {
            var pool = new string[n];
            for (int i = 0; i < n; i++) pool[i] = "clip-" + i;
            return pool;
        }

        static string Pick(NoRepeatPicker picker, string preferred = null)
        {
            string clip = picker.Next(preferred);
            picker.Remember(clip);
            return clip;
        }

        [TestCase(2), TestCase(3), TestCase(5)]
        public void The_same_clip_never_comes_twice_in_a_row(int size)
        {
            foreach (int seed in new[] { 1, 7, 42 })
            {
                var picker = new NoRepeatPicker(Pool(size), new SeededRandom(seed));
                string last = null;
                for (int i = 0; i < 500; i++)
                {
                    string clip = Pick(picker);
                    Assert.AreNotEqual(last, clip, $"pool {size}, seed {seed}, pick {i}");
                    last = clip;
                }
            }
        }

        [TestCase(2), TestCase(3), TestCase(5)]
        public void Every_clip_in_the_pool_can_be_picked(int size)
        {
            var picker = new NoRepeatPicker(Pool(size), new SeededRandom(3));
            var seen = new HashSet<string>();
            for (int i = 0; i < 200; i++) seen.Add(Pick(picker));
            CollectionAssert.AreEquivalent(Pool(size), seen);
        }

        [Test]
        public void A_pool_of_one_clip_still_plays_it_every_time()
        {
            var picker = new NoRepeatPicker(new[] { "clip-only" }, new SeededRandom(5));
            for (int i = 0; i < 5; i++) Assert.AreEqual("clip-only", Pick(picker));
        }

        [Test]
        public void An_empty_pool_gives_no_clip()
        {
            Assert.IsNull(new NoRepeatPicker(new string[0], new SeededRandom(5)).Next());
        }

        [Test]
        public void Two_clips_take_turns()
        {
            var picker = new NoRepeatPicker(new[] { "a", "b" }, new SeededRandom(9));
            string first = Pick(picker);
            for (int i = 1; i < 10; i++) Assert.AreEqual(i % 2 == 0 ? first : (first == "a" ? "b" : "a"), Pick(picker));
        }

        [Test]
        public void The_preferred_clip_plays_when_it_was_not_the_last()
        {
            var picker = new NoRepeatPicker(new[] { "glove", "cheer" }, new SeededRandom(2));
            Assert.AreEqual("glove", Pick(picker, "glove"));
            picker.Remember("cheer");
            Assert.AreEqual("glove", Pick(picker, "glove"));
        }

        [Test]
        public void The_rule_wins_over_the_preferred_clip()
        {
            var picker = new NoRepeatPicker(new[] { "glove", "cheer" }, new SeededRandom(2));
            Assert.AreEqual("glove", Pick(picker, "glove"));
            Assert.AreEqual("cheer", Pick(picker, "glove"));
            Assert.AreEqual("glove", Pick(picker, "glove"));
        }

        [Test]
        public void A_pick_that_never_played_does_not_move_the_memory()
        {
            var picker = new NoRepeatPicker(new[] { "a", "b" }, new SeededRandom(4));
            picker.Remember("a");
            for (int i = 0; i < 5; i++) Assert.AreEqual("b", picker.Next());
            Assert.AreEqual("a", picker.Last);
        }
    }
}
