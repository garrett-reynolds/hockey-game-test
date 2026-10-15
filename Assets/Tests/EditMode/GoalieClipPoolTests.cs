using System.Collections.Generic;
using System.Linq;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The 19 Keep clips (Garrett, 2026-10-15): the Goal look back by side (clip-18-look-back is over the goalie's left
    /// shoulder, goal-look-back over the right), the 10 Save clips, the 2 Idle clips and miss-relief.
    /// </summary>
    public class GoalieClipPoolTests
    {
        const float Frame = 1f / 30f;
        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { if (_parent != null) Object.DestroyImmediate(_parent); }

        [TestCase(80.0, "clip-18-look-back")]
        [TestCase(100.0, "clip-18-look-back")]
        [TestCase(50.0, "clip-18-look-back")]
        [TestCase(49.9, "goal-look-back")]
        [TestCase(20.0, "goal-look-back")]
        [TestCase(0.0, "goal-look-back")]
        public void The_look_back_clip_follows_the_side_of_the_Goal(double x, string expected)
        {
            Assert.AreEqual(expected, GoalieClipReactions.LookBackFor(x));
        }

        [Test]
        public void The_Goal_pool_is_four_clips_and_one_look_back_entry()
        {
            CollectionAssert.AreEquivalent(
                new[] { "clip-16-slump", "clip-17-stick-slap", "goal-slump", "goal-look-up", GoalieClipReactions.LookBackEntry },
                GoalieClipReactions.GoalClips);
            CollectionAssert.AreEquivalent(
                new[] { "clip-16-slump", "clip-17-stick-slap", "goal-slump", "goal-look-up", "clip-18-look-back", "goal-look-back" },
                GoalieClipReactions.GoalClipFiles);
        }

        [TestCase(80.0, "goal-look-back")]
        [TestCase(20.0, "clip-18-look-back")]
        public void The_wrong_side_look_back_never_plays_and_the_no_repeat_rule_holds(double x, string wrongSide)
        {
            var picker = new NoRepeatPicker(GoalieClipReactions.GoalClips, new SeededRandom(3));
            string lastEntry = null;
            var seen = new HashSet<string>();
            for (int i = 0; i < 600; i++)
            {
                string entry = picker.Next();
                string file = GoalieClipReactions.ResolveGoalClip(entry, x);
                picker.Remember(entry);
                Assert.AreNotEqual(wrongSide, file, "the wrong-side look-back played at pick " + i);
                Assert.AreNotEqual(lastEntry, entry, "the same entry twice in a row (a look-back counts as one) at pick " + i);
                lastEntry = entry;
                seen.Add(file);
            }
            Assert.AreEqual(5, seen.Count, "four clips and the side's look-back: " + string.Join(", ", seen));
            Assert.AreEqual(1, seen.Count(f => f.EndsWith("look-back")));
        }

        [Test]
        public void The_Save_pool_holds_all_ten_Save_clips()
        {
            string[] ten =
            {
                "clip-20-show-hand-up", "clip-30-cheer-point", "save-calm-nod", "save-chest-tap", "save-fist-pump",
                "save-glove-shake", "save-glove-up", "save-head-shake", "save-reset", "save-shout"
            };
            CollectionAssert.AreEquivalent(ten, GoalieClipReactions.SaveClips);
        }

        [Test]
        public void A_Glove_save_prefers_save_glove_up_or_clip_20_whichever_the_no_repeat_rule_allows()
        {
            CollectionAssert.AreEquivalent(new[] { "save-glove-up", "clip-20-show-hand-up" }, GoalieClipReactions.GlovePreferred);
            var picker = new NoRepeatPicker(GoalieClipReactions.SaveClips, new SeededRandom(5));
            var got = new HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                string clip = picker.NextPreferring(GoalieClipReactions.GlovePreferred);
                CollectionAssert.Contains(GoalieClipReactions.GlovePreferred, clip);
                Assert.AreNotEqual(picker.Last, clip);
                got.Add(clip);
                picker.Remember(clip);
            }
            Assert.AreEqual(2, got.Count, "both glove clips come up");

            picker.Remember("save-glove-up");
            Assert.AreEqual("clip-20-show-hand-up", picker.NextPreferring(GoalieClipReactions.GlovePreferred));
            picker.Remember("clip-20-show-hand-up");
            Assert.AreEqual("save-glove-up", picker.NextPreferring(GoalieClipReactions.GlovePreferred));
            picker.Remember("save-shout"); // neither preferred clip was last: one of them still wins
            CollectionAssert.Contains(GoalieClipReactions.GlovePreferred, picker.NextPreferring(GoalieClipReactions.GlovePreferred));
        }

        [Test]
        public void The_preferred_list_falls_back_to_the_others_when_no_preferred_clip_is_allowed()
        {
            var picker = new NoRepeatPicker(new[] { "a", "b", "c" }, new SeededRandom(1));
            picker.Remember("a");
            Assert.AreNotEqual("a", picker.NextPreferring(new[] { "a" }));
            Assert.AreNotEqual("a", picker.NextPreferring(new[] { "x", "y" }), "preferred clips outside the pool do not count");
        }

        [Test]
        public void The_two_Idle_clips_take_turns()
        {
            CollectionAssert.AreEquivalent(new[] { "clip-31-idle-shoulder-roll", "idle-shoulder-roll" }, GoalieClipReactions.IdleClips);
            var picker = new NoRepeatPicker(GoalieClipReactions.IdleClips, new SeededRandom(9));
            string last = null;
            var seen = new HashSet<string>();
            for (int i = 0; i < 20; i++)
            {
                string clip = picker.Next();
                Assert.AreNotEqual(last, clip);
                picker.Remember(clip);
                last = clip;
                seen.Add(clip);
            }
            Assert.AreEqual(2, seen.Count);
        }

        [TestCase("goal-slump", "clip-16-slump")]
        [TestCase("goal-look-back", "clip-18-look-back")]
        [TestCase("idle-shoulder-roll", "clip-31-idle-shoulder-roll")]
        public void Load_finds_the_exact_name_never_a_clip_whose_name_only_contains_it(string name, string notThis)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/AIClips/Keep"))
                Assert.Ignore("Assets/AIClips is missing (the AI clips are never committed): nothing to load.");
            var clips = new GoalieClipReactions(null);
            AnimationClip clip = clips.LoadClip(name);
            Assert.IsNotNull(clip, name + " not found");
            Assert.AreEqual(name, clip.name);
            Assert.AreNotEqual(notThis, clip.name);
        }

        [Test]
        public void Every_pool_name_loads_when_the_clips_are_there()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/AIClips/Keep"))
                Assert.Ignore("Assets/AIClips is missing (the AI clips are never committed): nothing to load.");
            var clips = new GoalieClipReactions(null);
            var missing = new List<string>();
            foreach (string name in GoalieClipReactions.GoalClipFiles.Concat(GoalieClipReactions.SaveClips)
                         .Concat(GoalieClipReactions.IdleClips).Append(GoalieClipReactions.MissClip))
                if (clips.LoadClip(name) == null) missing.Add(name);
            Assert.IsEmpty(missing, "not found: " + string.Join(", ", missing));
        }

        // ---- the Miss clip ----

        static Round NewRound(GoalieCharacter character, RoundSettings settings, GoalieClipReactions clips, bool testMode = false)
        {
            var round = new Round(new SeededRandom(11), settings, new GoalieCharacterReach(character, settings), 0);
            if (testMode) round.StartTestMode(0); else round.StartRound();
            for (int i = 0; i < 4000 && round.Phase != RoundPhase.ShootingWindow; i++)
            {
                round.Tick(0.05);
                clips.Post(round, true, 0.05f);
            }
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase, "the Round never reached the shooting window");
            return round;
        }

        /// <summary>Plays the hold after a Miss; whether ReplacesReaction(Relief) was ever true.</summary>
        static bool HoldAfterMiss(GoalieClipReactions clips, Round round)
        {
            round.SubmitShot(50, 50, 90, false);
            round.Whistle(ShotResultKind.Miss);
            bool ever = false;
            for (int i = 0; i < 400 && round.Phase == RoundPhase.ResultHold; i++)
            {
                round.Tick(Frame);
                clips.Pre();
                clips.Post(round, true, Frame);
                if (round.Phase != RoundPhase.ResultHold) break;
                ever |= clips.ReplacesReaction(AfterShotGesture.Relief);
            }
            return ever;
        }

        [Test]
        public void A_Miss_plays_miss_relief_in_a_hold_with_room_for_it()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            character.Show(true);
            var settings = new RoundSettings { ReplayOn = false, ClipHoldSeconds = 1.5 };
            var clips = new GoalieClipReactions(character.Animator) { ClipsFoundOverride = true };
            Round round = NewRound(character, settings, clips);
            int tries = clips.Starts + clips.Fallbacks;
            bool replaces = HoldAfterMiss(clips, round);
            Assert.AreEqual(tries + 1, clips.Starts + clips.Fallbacks, "the Miss tried one clip");
            Assert.IsTrue(replaces, "miss-relief replaces the code Relief");
            if (clips.Starts > 0) Assert.AreEqual("miss-relief", clips.LastClipName);
        }

        [Test]
        public void A_Miss_in_Test_Mode_s_short_hold_keeps_the_code_Relief()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            character.Show(true);
            var settings = new RoundSettings { ReplayOn = false };
            var clips = new GoalieClipReactions(character.Animator) { ClipsFoundOverride = true };
            Round round = NewRound(character, settings, clips, testMode: true);
            int tries = clips.Starts + clips.Fallbacks;
            bool replaces = HoldAfterMiss(clips, round);
            Assert.IsFalse(replaces, "no room: the code Relief stays");
            Assert.AreEqual(tries, clips.Starts + clips.Fallbacks, "no clip tried");
        }

        [Test]
        public void A_Miss_keeps_the_code_Relief_when_miss_relief_is_missing()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            character.Show(true);
            var settings = new RoundSettings { ReplayOn = false, ClipHoldSeconds = 1.5 };
            var clips = new GoalieClipReactions(character.Animator) { ClipsFoundOverride = false };
            Round round = NewRound(character, settings, clips);
            Assert.IsFalse(HoldAfterMiss(clips, round));
        }
    }
}
