using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class GoalieViewTests
    {
        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_parent); }

        Transform Group(string name) => _parent.transform.Find(name);

        static bool AnyDrawn(Transform group)
        {
            foreach (Renderer r in group.GetComponentsInChildren<Renderer>())
                if (r.enabled) return true;
            return false;
        }

        [Test]
        public void With_the_character_the_blocks_stay_in_the_scene_but_are_not_drawn_and_the_character_shows_instead()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            view.Show(true, GoaliePose.MiddleStandUp, false);

            Transform blocks = Group("Goalie MiddleStandUp");
            Assert.IsTrue(blocks.gameObject.activeInHierarchy, "the blocks are still there, as the rules reference");
            Assert.Greater(blocks.GetComponentsInChildren<Renderer>().Length, 0);
            Assert.IsFalse(AnyDrawn(blocks), "the blocks are hidden");
            Assert.IsTrue(character.Root.activeInHierarchy, "the character shows");

            view.Show(false, GoaliePose.MiddleStandUp, false);
            Assert.IsFalse(character.Root.activeInHierarchy, "the character hides with the goalie");
        }

        [Test]
        public void Easy_mode_glow_still_shows_with_the_character()
        {
            var view = new GoalieView(_parent.transform, new GoalieCharacter(_parent.transform));
            view.Show(true, GoaliePose.MiddleButterfly, true);
            Transform glow = Group("Glow MiddleButterfly");
            Assert.IsTrue(glow.gameObject.activeInHierarchy);
            Assert.IsTrue(AnyDrawn(glow));
        }

        [Test]
        public void Without_the_character_model_the_blocks_show_as_before()
        {
            var character = new GoalieCharacter(_parent.transform, name => null);
            var view = new GoalieView(_parent.transform, character);
            view.Show(true, GoaliePose.LeftButterfly, false);
            Assert.IsTrue(AnyDrawn(Group("Goalie LeftButterfly")));

            var noCharacter = new GoalieView(_parent.transform);
            Assert.DoesNotThrow(() => noCharacter.Show(true, GoaliePose.LeftButterfly, false));
        }

        [Test]
        public void The_set_blend_moves_the_character_from_the_ready_stance_into_the_pose()
        {
            var reference = new GoalieCharacter(new GameObject("Reference").transform);
            Transform referenceKnee = reference.Bone(HumanBodyBones.LeftLowerLeg);
            var character = new GoalieCharacter(_parent.transform);
            Transform knee = character.Bone(HumanBodyBones.LeftLowerLeg);
            var view = new GoalieView(_parent.transform, character);
            view.Show(true, GoaliePose.MiddleButterfly, false);

            view.SetProgress(0f);
            reference.Pose(GoaliePose.MiddleButterfly, 0f);
            Assert.AreEqual(0f, Vector3.Distance(referenceKnee.localPosition, knee.localPosition) + Quaternion.Angle(referenceKnee.rotation, knee.rotation), 0.01f, "neutral at the start of the slow-down");

            view.SetProgress(0.5f);
            reference.Pose(GoaliePose.MiddleButterfly, 0.5f);
            Assert.AreEqual(0f, Quaternion.Angle(referenceKnee.rotation, knee.rotation), 0.01f, "half set");

            view.SetProgress(1f);
            reference.Pose(GoaliePose.MiddleButterfly, 1f);
            Assert.AreEqual(0f, Quaternion.Angle(referenceKnee.rotation, knee.rotation), 0.01f, "fully set");

            view.Show(true, GoaliePose.LeftPostHug, false);
            reference.Pose(GoaliePose.LeftPostHug, 1f);
            Assert.AreEqual(0f, Quaternion.Angle(referenceKnee.rotation, knee.rotation), 0.01f, "a new pose shows at the current set");
            Object.DestroyImmediate(reference.Root.transform.parent.gameObject);
        }

        [Test]
        public void A_save_stops_the_puck_just_in_front_of_the_gear_that_made_it_on_the_puck_line()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            var from = new Vector3(0f, 1.2f, -8f);

            Assert.IsFalse(view.SaveStop(from, new Vector3(0.6f, 1f, 0f), SoundCue.GloveCatch, 0.05f, out _), "no stop while the goalie is hidden");

            view.Show(true, GoaliePose.MiddleStandUp, false);
            foreach (var (cue, piece, net) in new[]
            {
                (SoundCue.GloveCatch, "Catcher", new Vector3(0.6f, 1f, 0f)),
                (SoundCue.BlockerHit, "Blocker", new Vector3(-0.6f, 1f, 0f)),
                (SoundCue.SaveImpact, "Right Pad", new Vector3(-0.6f, 0.2f, 0f)), // the goalie's right pad is on the shooter's left
            })
            {
                Assert.IsTrue(view.SaveStop(from, net, cue, 0.05f, out Vector3 stop), cue.ToString());
                Assert.IsTrue(character.GearBounds(piece, out Bounds gear));
                Assert.AreEqual(gear.min.z - 0.025f, stop.z, 0.001f, cue + ": just in front of the " + piece);
                Vector3 along = (net - from).normalized;
                Assert.Less(Vector3.Cross(stop - from, along).magnitude, 0.001f, cue + ": on the puck's line");
            }

            var blocks = new GoalieView(_parent.transform);
            blocks.Show(true, GoaliePose.MiddleStandUp, false);
            Assert.IsFalse(blocks.SaveStop(from, new Vector3(0.6f, 1f, 0f), SoundCue.GloveCatch, 0.05f, out _), "the blocks keep the save block");
        }
    }
}
