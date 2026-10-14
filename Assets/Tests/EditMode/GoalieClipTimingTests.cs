using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// When the AI clips start and how they blend (Garrett, 2026-10-14: "the new animations come after the replay?? They
    /// should happen right when the goal or miss happens", "The goalie gets into the animations so quickly and not human
    /// like at all"). A clip's start try counts as Starts (the clip played) or Fallbacks (Assets/AIClips missing, the code
    /// gesture stays): both happen at the same moment, so this runs with or without the uncommitted clips.
    /// </summary>
    public class GoalieClipTimingTests
    {
        const float Frame = 1f / 30f;
        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { if (_parent != null) Object.DestroyImmediate(_parent); }

        static Round NewRound(GoalieCharacter character, RoundSettings settings, GoalieClipReactions clips)
        {
            var round = new Round(new SeededRandom(11), settings, new GoalieCharacterReach(character, settings), 0);
            round.StartRound();
            for (int i = 0; i < 4000 && round.Phase != RoundPhase.ShootingWindow; i++)
            {
                round.Tick(0.05);
                clips.Post(round, true, 0.05f);
            }
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase, "the Round never reached the shooting window");
            return round;
        }

        /// <summary>Seconds after the Whistle that the clip tried to start (Starts or Fallbacks went up); infinity if never in 8 s.</summary>
        static double SecondsToClipTry(GoalieClipReactions clips, Round round)
        {
            int tries = clips.Starts + clips.Fallbacks;
            for (int i = 0; i < 240; i++)
            {
                round.Tick(Frame);
                clips.Pre();
                clips.Post(round, true, Frame);
                if (clips.Starts + clips.Fallbacks > tries) return (i + 1) * Frame;
            }
            return double.PositiveInfinity;
        }

        [Test]
        public void With_the_Replay_on_a_Goal_clip_starts_within_half_a_second_of_the_Whistle()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            character.Show(true);
            var settings = new RoundSettings { ReplayOn = true };
            var clips = new GoalieClipReactions(character.Animator);
            Round round = NewRound(character, settings, clips);
            round.SubmitShot(50, 50, 90, false);
            round.Whistle(ShotResultKind.Goal);
            Assert.IsTrue(round.ReplayFollows, "a Goal with the Replay on gets a Replay");
            double at = SecondsToClipTry(clips, round);
            Assert.That(at, Is.LessThanOrEqualTo(0.5), "the Goal clip starts at the Goal, not after the Replay");
        }

        [Test]
        public void With_the_Replay_off_a_Goal_clip_starts_within_half_a_second_of_the_Whistle()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            character.Show(true);
            var settings = new RoundSettings { ReplayOn = false };
            var clips = new GoalieClipReactions(character.Animator);
            Round round = NewRound(character, settings, clips);
            round.SubmitShot(50, 50, 90, false);
            round.Whistle(ShotResultKind.Goal);
            double at = SecondsToClipTry(clips, round);
            Assert.That(at, Is.LessThanOrEqualTo(0.5));
        }

        // ---- the blend in and out: three choices, 0.8 s by default, a key cycles them ----

        [Test]
        public void The_blend_choices_are_0_5_0_8_and_1_2_s_and_0_8_is_the_default()
        {
            Assert.AreEqual(new[] { 0.5f, 0.8f, 1.2f }, GoalieClipReactions.BlendChoices);
            Assert.AreEqual(0.8f, GoalieClipReactions.DefaultBlendSeconds);
            var clips = new GoalieClipReactions(null);
            Assert.AreEqual(0.8f, clips.BlendSeconds);
        }

        [Test]
        public void Cycling_the_blend_goes_0_8_then_1_2_then_0_5_then_0_8()
        {
            var clips = new GoalieClipReactions(null);
            Assert.AreEqual(1.2f, clips.CycleBlend());
            Assert.AreEqual(0.5f, clips.CycleBlend());
            Assert.AreEqual(0.8f, clips.CycleBlend());
            Assert.AreEqual(0.8f, clips.BlendSeconds);
        }

        [Test]
        public void The_blend_eases_in_and_out()
        {
            Assert.AreEqual(0f, GoalieClipReactions.Ease(0f), 1e-6);
            Assert.AreEqual(0.5f, GoalieClipReactions.Ease(0.5f), 1e-6);
            Assert.AreEqual(1f, GoalieClipReactions.Ease(1f), 1e-6);
            // slow at both ends: the first and last tenth move far less than a tenth
            Assert.Less(GoalieClipReactions.Ease(0.1f), 0.05f);
            Assert.Greater(GoalieClipReactions.Ease(0.9f), 0.95f);
        }

        [Test]
        public void The_on_screen_note_names_the_blend()
        {
            Assert.AreEqual("Clip blend 0.8 s", GoalieClipReactions.BlendNote(0.8f));
            Assert.AreEqual("Clip blend 1.2 s", GoalieClipReactions.BlendNote(1.2f));
        }
    }
}
