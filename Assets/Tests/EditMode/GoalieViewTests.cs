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
            Assert.AreEqual(0f, Vector3.Distance(referenceKnee.localPosition, knee.localPosition) + Quaternion.Angle(referenceKnee.rotation, knee.rotation), 0.01f, "ready stance before the Shot");

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

        [Test]
        public void Before_the_Shot_every_pose_shows_the_same_ready_stance()
        {
            var reference = new GoalieCharacter(new GameObject("Reference").transform);
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            foreach (GoaliePose pose in System.Enum.GetValues(typeof(GoaliePose)))
            {
                view.Show(true, pose, false);
                view.SetProgress(0f);
                foreach (var bone in new[] { HumanBodyBones.Hips, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                {
                    Transform mine = character.Bone(bone), theirs = reference.Bone(bone);
                    Assert.AreEqual(0f, Vector3.Distance(theirs.position - reference.Root.transform.position, mine.position - character.Root.transform.position), 0.001f, pose + " " + bone + " position");
                    Assert.AreEqual(0f, Quaternion.Angle(theirs.rotation, mine.rotation), 0.01f, pose + " " + bone + " rotation");
                }
            }
            Object.DestroyImmediate(reference.Root.transform.parent.gameObject);
        }

        [Test]
        public void A_save_taken_while_the_goalie_is_still_ready_stops_on_the_gear_where_the_pose_puts_it()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            var reference = new GoalieCharacter(new GameObject("Reference").transform);
            var from = new Vector3(0f, 1.2f, -8f);
            var net = new Vector3(0.6f, 1f, 0f);
            view.Show(true, GoaliePose.LeftButterfly, false);

            view.SetProgress(1f);
            Assert.IsTrue(view.SaveStop(from, net, SoundCue.GloveCatch, 0.05f, out Vector3 posed));

            view.SetProgress(0f); // the Shot frame: the reaction has not started
            Assert.IsTrue(view.SaveStop(from, net, SoundCue.GloveCatch, 0.05f, out Vector3 stop));
            Assert.AreEqual(0f, Vector3.Distance(posed, stop), 0.001f, "measured on the posed gear");

            reference.Pose(GoaliePose.LeftButterfly, 0f);
            Transform knee = character.Bone(HumanBodyBones.LeftLowerLeg), referenceKnee = reference.Bone(HumanBodyBones.LeftLowerLeg);
            Assert.AreEqual(0f, Quaternion.Angle(referenceKnee.rotation, knee.rotation), 0.01f, "still in the ready stance afterwards");
            Object.DestroyImmediate(reference.Root.transform.parent.gameObject);
        }

        [Test]
        public void The_goalie_squares_up_to_the_shooter_from_where_it_stands_in_the_crease()
        {
            // 0.4 m out from the Net Face: a shooter 2.7 m to the left and 9 m out is 8.6 m ahead of the goalie.
            Assert.AreEqual(17.43f, GoalieCharacter.FacingToward(new Vector3(-2.7f, 1.5f, -9f)), 0.01f);
            Assert.AreEqual(-17.43f, GoalieCharacter.FacingToward(new Vector3(2.7f, 1.5f, -9f)), 0.01f);
            Assert.AreEqual(0f, GoalieCharacter.FacingToward(new Vector3(0f, 1.5f, -3f)), 0.01f);
        }

        [Test]
        public void The_goalie_turns_about_its_own_spot_to_face_the_shooter_and_reacts_into_the_pose_at_the_shooters_angle()
        {
            var reference = new GoalieCharacter(new GameObject("Reference").transform);
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            Transform hips = character.Bone(HumanBodyBones.Hips), referenceHips = reference.Bone(HumanBodyBones.Hips);
            view.Show(true, GoaliePose.MiddleStandUp, false);

            void AssertTurned(float degrees, string why)
            {
                Assert.AreEqual(0f, Quaternion.Angle(Quaternion.AngleAxis(degrees, Vector3.up) * referenceHips.rotation, hips.rotation), 0.05f, why + ": turned");
                Assert.AreEqual(0f, new Vector2(hips.position.x - referenceHips.position.x, hips.position.z - referenceHips.position.z).magnitude, 0.001f, why + ": on its own spot");
            }

            view.Face(12f, 18f);
            view.SetProgress(0f);
            reference.Pose(GoaliePose.MiddleStandUp, 0f);
            AssertTurned(12f, "ready stance, facing where the goalie has turned so far");

            view.SetProgress(0.5f);
            reference.Pose(GoaliePose.MiddleStandUp, 0.5f);
            AssertTurned(15f, "half way through the reaction");

            view.SetProgress(1f);
            reference.Pose(GoaliePose.MiddleStandUp, 1f);
            AssertTurned(18f, "posed at the shooter's angle (a Middle pose keeps the whole turn)");

            view.Face(0f, 0f);
            AssertTurned(0f, "straight out again");
            Object.DestroyImmediate(reference.Root.transform.parent.gameObject);
        }

        [Test]
        public void A_save_stops_on_the_turned_gear_measured_where_the_reaction_ends()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            var from = new Vector3(-2.4f, 1.2f, -8f);
            var net = new Vector3(0.6f, 1f, 0f);
            view.Show(true, GoaliePose.MiddleStandUp, false);

            view.SetProgress(1f);
            Assert.IsTrue(view.SaveStop(from, net, SoundCue.GloveCatch, 0.05f, out Vector3 straight));
            var straightGear = new PuckObstacles();
            view.AddObstacles(straightGear);

            view.Face(17.4f, 17.4f);
            Assert.IsTrue(view.SaveStop(from, net, SoundCue.GloveCatch, 0.05f, out Vector3 turned));
            Assert.IsTrue(character.GearBounds("Catcher", out Bounds gear));
            Assert.AreEqual(gear.min.z - 0.025f, turned.z, 0.001f, "just in front of the turned catcher");
            Assert.Less(Vector3.Cross(turned - from, (net - from).normalized).magnitude, 0.001f, "on the puck's line");
            Assert.Greater(Vector3.Distance(straight, turned), 0.01f, "the turn moves the catcher");
            var turnedGear = new PuckObstacles();
            view.AddObstacles(turnedGear);
            Assert.IsTrue(turnedGear.Nearest(turned, "Catcher", out _, out _, out float onTurned));
            Assert.IsTrue(straightGear.Nearest(turned, "Catcher", out _, out _, out float onStraight));
            Assert.AreNotEqual(onStraight, onTurned, "the puck meets the turned gear, not the straight one");

            // The Shot frame: the goalie has shuffled to 15 degrees, the shooter is at 17.4; nothing has reacted yet.
            view.Face(15f, 17.4f);
            view.SetProgress(0f);
            Assert.IsTrue(view.SaveStop(from, net, SoundCue.GloveCatch, 0.05f, out Vector3 onShot));
            Assert.AreEqual(0f, Vector3.Distance(turned, onShot), 0.001f, "measured where the reaction ends");
            var shotGear = new PuckObstacles();
            view.AddObstacles(shotGear);
            Assert.IsTrue(shotGear.Nearest(turned, "Catcher", out _, out _, out float onShotGear));
            Assert.AreEqual(onTurned, onShotGear, 0.001f, "obstacles too");
            Assert.IsTrue(view.SaveSurface(shotGear, onShot, net, SoundCue.GloveCatch, out _, out PuckSurface surface));
            Assert.AreEqual(PuckSurface.Catcher, surface);

            Transform hips = character.Bone(HumanBodyBones.Hips);
            view.Face(0f, 0f);
            Quaternion straightHips = hips.rotation;
            view.Face(15f, 17.4f);
            Assert.AreEqual(15f, Quaternion.Angle(straightHips, hips.rotation), 0.05f, "still shown at 15 degrees, in the ready stance, afterwards");
        }

        [Test]
        public void Squared_up_the_goalie_shoulders_lie_square_across_the_line_to_the_shooter()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            view.Show(true, GoaliePose.MiddleStandUp, false);
            view.SetProgress(0f);
            Transform left = character.Bone(HumanBodyBones.LeftUpperArm), right = character.Bone(HumanBodyBones.RightUpperArm);
            Transform hips = character.Bone(HumanBodyBones.Hips);
            foreach (var shooter in new[] { new Vector3(-2.7f, 1.5f, -9f), new Vector3(2.7f, 1.5f, -9f), new Vector3(-8.2f, 1.5f, -15.5f) })
            {
                float SquareOff()
                {
                    Vector3 shoulders = Vector3.ProjectOnPlane(right.position - left.position, Vector3.up).normalized;
                    Vector3 toShooter = Vector3.ProjectOnPlane(shooter - hips.position, Vector3.up).normalized;
                    return Mathf.Abs(Vector3.Dot(shoulders, toShooter));
                }
                view.Face(0f, 0f);
                Assert.Greater(SquareOff(), 0.2f, shooter + ": straight out, the shoulders are not square to a side shooter");
                float facing = GoalieCharacter.FacingToward(shooter);
                view.Face(facing, facing);
                Assert.Less(SquareOff(), 0.03f, shooter + ": turned, they are");
            }
        }

        [TestCase(GoaliePose.LeftButterfly, 2f)]
        [TestCase(GoaliePose.RightButterfly, -2f)]
        [TestCase(GoaliePose.LeftPostHug, 6f)]
        [TestCase(GoaliePose.RightPostHug, -6f)]
        public void A_side_pose_keeps_only_the_turn_that_still_reads_as_the_pose_table_and_saves_are_measured_there(GoaliePose pose, float posed)
        {
            var reference = new GoalieCharacter(new GameObject("Reference").transform);
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            Transform hips = character.Bone(HumanBodyBones.Hips), referenceHips = reference.Bone(HumanBodyBones.Hips);
            float shooter = GoalieCharacter.FacingToward(new Vector3(posed > 0 ? -2.7f : 2.7f, 1.5f, -9f));
            var from = new Vector3(posed > 0 ? -2.4f : 2.4f, 1.2f, -8f);
            var net = new Vector3(0.6f, 1f, 0f);
            view.Show(true, pose, false);

            view.Face(posed, posed);
            view.SetProgress(1f);
            Assert.IsTrue(view.SaveStop(from, net, SoundCue.GloveCatch, 0.05f, out Vector3 atLimit));

            view.Face(shooter, shooter);
            view.SetProgress(0f);
            reference.Pose(pose, 0f);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.AngleAxis(shooter, Vector3.up) * referenceHips.rotation, hips.rotation), 0.05f, "the ready stance squares right up to the shooter");
            Assert.IsTrue(view.SaveStop(from, net, SoundCue.GloveCatch, 0.05f, out Vector3 onShot));
            Assert.AreEqual(0f, Vector3.Distance(atLimit, onShot), 0.001f, "the save is measured on the pose at its own limit");

            view.SetProgress(1f);
            reference.Pose(pose, 1f);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.AngleAxis(posed, Vector3.up) * referenceHips.rotation, hips.rotation), 0.05f, "posed at the pose's limit");
            Object.DestroyImmediate(reference.Root.transform.parent.gameObject);
        }
    }
}
