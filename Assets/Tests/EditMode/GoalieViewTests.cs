using System.Collections.Generic;
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
        public void Before_the_Shot_every_pose_shows_the_same_ready_stance()
        {
            var reference = new GoalieCharacter(new GameObject("Reference").transform);
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            reference.Pose(0f);
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
        public void The_goalie_squares_up_to_the_shooter_from_where_it_stands_in_the_crease()
        {
            // 0.4 m out from the Net Face: a shooter 2.7 m to the left and 9 m out is 8.6 m ahead of the goalie.
            Assert.AreEqual(17.43f, GoalieCharacter.FacingToward(new Vector3(-2.7f, 1.5f, -9f)), 0.01f);
            Assert.AreEqual(-17.43f, GoalieCharacter.FacingToward(new Vector3(2.7f, 1.5f, -9f)), 0.01f);
            Assert.AreEqual(0f, GoalieCharacter.FacingToward(new Vector3(0f, 1.5f, -3f)), 0.01f);
        }

        [Test]
        public void The_goalie_turns_about_its_own_spot_to_face_the_shooter_and_finishes_the_turn_as_it_reacts()
        {
            var reference = new GoalieCharacter(new GameObject("Reference").transform);
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            Transform hips = character.Bone(HumanBodyBones.Hips), referenceHips = reference.Bone(HumanBodyBones.Hips);
            view.Show(true, GoaliePose.MiddleStandUp, false);
            reference.Pose(0f);

            void AssertTurned(float degrees, string why)
            {
                Assert.AreEqual(0f, Quaternion.Angle(Quaternion.AngleAxis(degrees, Vector3.up) * referenceHips.rotation, hips.rotation), 0.05f, why + ": turned");
                Assert.AreEqual(0f, new Vector2(hips.position.x - referenceHips.position.x, hips.position.z - referenceHips.position.z).magnitude, 0.001f, why + ": on its own spot");
            }

            view.Face(12f, 18f);
            view.SetProgress(0f);
            AssertTurned(12f, "Ready Stance, facing where the goalie has turned so far");
            view.SetProgress(0.5f);
            AssertTurned(15f, "half way through the reaction");
            view.SetProgress(1f);
            AssertTurned(18f, "reacted, squared up to the shooter");
            view.Face(0f, 0f);
            AssertTurned(0f, "straight out again");
            Object.DestroyImmediate(reference.Root.transform.parent.gameObject);
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

        static ShotResult Result(ShotResultKind kind, double x, double y) => new ShotResult(kind, true, x, y, 100, false);

        static Vector3 NetPoint(double x, double y)
        {
            NetFaceMapping.ToMetres(x, y, out double right, out double up);
            return new Vector3((float)right, (float)up, 0f);
        }

        /// <summary>The gear each Save Reaction makes the save with: the test's own table (a pad save uses the pad on the puck's side).</summary>
        static string Gear(double x, double y)
        {
            switch (SaveReactions.For(x, y))
            {
                case SaveReaction.Glove: return GearPiece.Catcher;
                case SaveReaction.Blocker: return GearPiece.Blocker;
                case SaveReaction.Stick: return GearPiece.Stick;
                case SaveReaction.Body: return GearPiece.Body;
                default: return x > 50 ? GearPiece.LeftPad : GearPiece.RightPad; // the goalie's left pad is on the shooter's right
            }
        }

        [Test]
        public void A_save_reaction_comes_from_the_Shot_point_alone_whatever_the_Goalie_Pose()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            var from = new Vector3(0f, 1.15f, -9f);
            foreach (var (x, y, piece) in new[] { (85.0, 85.0, GearPiece.Catcher), (15.0, 85.0, GearPiece.Blocker), (15.0, 10.0, GearPiece.RightPad),
                (85.0, 30.0, GearPiece.LeftPad), (50.0, 5.0, GearPiece.Stick), (50.0, 55.0, GearPiece.Body) })
            {
                Quaternion[] first = null;
                foreach (GoaliePose pose in new[] { GoaliePose.LeftPostHug, GoaliePose.MiddleStandUp, GoaliePose.RightButterfly })
                {
                    view.Show(true, pose, false);
                    view.SetProgress(0f);
                    view.React(Result(ShotResultKind.Save, x, y), from);
                    Assert.AreEqual(piece, view.ReactionPiece, "(" + x + ", " + y + ") in " + pose);
                    view.SetProgress(1f);
                    var bones = new Quaternion[(int)HumanBodyBones.LastBone];
                    for (int b = 0; b < bones.Length; b++) bones[b] = Quaternion.identity;
                    for (var b = HumanBodyBones.Hips; b < HumanBodyBones.LastBone; b++)
                        if (character.Bone(b) != null) bones[(int)b] = character.Bone(b).rotation;
                    if (first == null) { first = bones; continue; }
                    for (int b = 0; b < bones.Length; b++)
                        Assert.AreEqual(0f, Quaternion.Angle(first[b], bones[b]), 0.01f, "(" + x + ", " + y + ") the same reaction in " + pose + ", bone " + (HumanBodyBones)b);
                }
            }
        }

        [Test]
        public void The_goalie_holds_the_Ready_Stance_until_the_Shot_then_the_set_blend_moves_it_into_the_reaction()
        {
            var reference = new GoalieCharacter(new GameObject("Reference").transform);
            reference.Pose(0f);
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            view.Show(true, GoaliePose.MiddleButterfly, false);
            Transform hand = character.Bone(HumanBodyBones.LeftHand), referenceHand = reference.Bone(HumanBodyBones.LeftHand);

            view.React(Result(ShotResultKind.Save, 90, 80), new Vector3(0f, 1.15f, -9f)); // the Shot frame: a glove save
            view.SetProgress(0f);
            Assert.AreEqual(0f, Vector3.Distance(referenceHand.position, hand.position), 0.001f, "still in the Ready Stance on the Shot frame");

            view.SetProgress(1f);
            Assert.IsTrue(character.ReactionGearBounds(GearPiece.Catcher, out Bounds measured));
            Assert.IsTrue(character.GearBounds(GearPiece.Catcher, out Bounds shown));
            Assert.AreEqual(0f, Vector3.Distance(measured.center, shown.center), 0.001f, "fully reacted, the catcher is where the save was measured");
            Vector3 reached = hand.position;
            Assert.Greater(Vector3.Distance(referenceHand.position, reached), 0.1f, "the catching hand has moved to the puck");

            view.SetProgress(0.5f);
            float part = Vector3.Distance(referenceHand.position, hand.position);
            Assert.Greater(part, 0.01f);
            Assert.Less(part, Vector3.Distance(referenceHand.position, reached) - 0.01f, "half way, part of the way there");

            view.React(new ShotResult(ShotResultKind.Miss, false, 0, 0, 0, false), Vector3.zero); // too late: no Shot
            Assert.IsNull(view.ReactionPiece);
            Object.DestroyImmediate(reference.Root.transform.parent.gameObject);
        }

        static IEnumerable<(string, Vector3)> AllEyes()
        {
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
                foreach (var eye in ShooterEyes.Of(side)) yield return (side + " " + eye.Item1, eye.Item2);
        }

        [Test]
        public void From_every_shooter_eye_a_save_stops_where_the_puck_meets_the_reaction_s_gear_on_its_line()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            view.Show(true, GoaliePose.MiddleStandUp, false);
            var failures = new List<string>();
            int saves = 0, padSaves = 0, padRivalFirst = 0;
            foreach (var (eyeName, eye) in AllEyes())
            {
                float facing = GoalieCharacter.FacingToward(eye);
                view.Face(facing, facing);
                Vector3 from = eye + Vector3.down * 0.35f; // where ShotEffects starts the puck: below the camera
                for (double x = 5; x < 100; x += 10)
                for (double y = 4; y < 100; y += 9)
                {
                    saves++;
                    string where = eyeName + " (" + x + ", " + y + ") ";
                    string piece = Gear(x, y);
                    Vector3 net = NetPoint(x, y);
                    view.SetProgress(0f);
                    view.React(Result(ShotResultKind.Save, x, y), from);
                    if (view.ReactionPiece != piece) { failures.Add(where + "reacts with " + view.ReactionPiece + ", not " + piece); continue; }
                    if (!view.SaveStop(from, net, PuckMotion.Thickness, out Vector3 stop)) { failures.Add(where + "no stop"); continue; }
                    if (Vector3.Cross(stop - from, (net - from).normalized).magnitude > 0.001f) failures.Add(where + "the stop is off the puck's line");
                    var obstacles = new PuckObstacles();
                    view.AddObstacles(obstacles);
                    // Touching the gear's surface: no gap (the puck's middle at most its half thickness off it), not sunk into it.
                    obstacles.Nearest(stop, piece, out _, out _, out float gap);
                    if (gap > PuckMotion.Thickness / 2f + 0.005f || gap < 0.002f) failures.Add(where + "the stop is " + gap.ToString("F3") + " m from the " + piece);
                    // Nothing of the goalie is in front of it on the line, but the stick or the blocker of a pad save may be (ShotEffects then stops it there).
                    Vector3 back = stop - (stop - from).normalized * 0.003f;
                    if (obstacles.Cast(from, back, PuckMotion.Thickness / 2f, out _, out _, out PuckSurface first))
                    {
                        bool rival = piece.EndsWith("Pad") && (first == PuckSurface.Stick || first == PuckSurface.Blocker);
                        if (rival) padRivalFirst++;
                        else failures.Add(where + first + " is in front of the " + piece);
                    }
                    if (piece.EndsWith("Pad")) padSaves++;
                }
            }
            Assert.IsEmpty(failures, failures.Count + " of " + saves + ":\n" + string.Join("\n", failures));
            Assert.Less(padRivalFirst, 0.02f * padSaves, padRivalFirst + " of " + padSaves + " pad saves meet the stick or blocker first");
        }

        [Test]
        public void A_goal_or_miss_reach_never_touches_the_puck()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            view.Show(true, GoaliePose.MiddleStandUp, false);
            var failures = new List<string>();
            int shots = 0;
            foreach (var (eyeName, eye) in AllEyes())
            {
                float facing = GoalieCharacter.FacingToward(eye);
                view.Face(facing, facing);
                Vector3 from = eye + Vector3.down * 0.35f;
                for (double x = -10; x <= 110; x += 15)
                for (double y = 5; y <= 110; y += 15)
                {
                    bool inside = x >= 0 && x <= 100 && y <= 100;
                    shots++;
                    view.SetProgress(0f);
                    view.React(Result(inside ? ShotResultKind.Goal : ShotResultKind.Miss, x, y), from);
                    Assert.IsNull(view.ReactionPiece);
                    view.SetProgress(1f);
                    if (GoalieTriangles.PuckTouches(GoalieTriangles.Of(character), from, NetPoint(x, y)))
                        failures.Add(eyeName + " (" + x + ", " + y + ") " + (inside ? "goal" : "miss"));
                }
            }
            Assert.IsEmpty(failures, failures.Count + " of " + shots + " touch the puck:\n" + string.Join("\n", failures));
        }

        [Test]
        public void A_save_is_measured_where_the_reaction_ends_turned_to_the_shooter_while_the_Ready_Stance_still_shows()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            var from = new Vector3(-2.4f, 1.2f, -8f);
            var net = NetPoint(83, 82);
            view.Show(true, GoaliePose.MiddleStandUp, false);

            view.Face(0f, 0f);
            view.React(Result(ShotResultKind.Save, 83, 82), from);
            Assert.IsTrue(view.SaveStop(from, net, PuckMotion.Thickness, out Vector3 straight));

            // The Shot frame: the goalie has shuffled to 15 degrees, the shooter is at 17.4; nothing has moved yet.
            view.Face(15f, 17.4f);
            view.SetProgress(0f);
            Transform hips = character.Bone(HumanBodyBones.Hips);
            Quaternion shown = hips.rotation;
            view.React(Result(ShotResultKind.Save, 83, 82), from);
            Assert.IsTrue(view.SaveStop(from, net, PuckMotion.Thickness, out Vector3 onShot));
            Assert.Greater(Vector3.Distance(straight, onShot), 0.01f, "the turn moves the catcher");
            Assert.AreEqual(0f, Quaternion.Angle(shown, hips.rotation), 0.01f, "still shown in the Ready Stance at 15 degrees");

            view.SetProgress(1f);
            Assert.IsTrue(character.GearBounds(GearPiece.Catcher, out Bounds gear));
            Assert.IsTrue(character.ReactionGearBounds(GearPiece.Catcher, out Bounds measured));
            Assert.AreEqual(0f, Vector3.Distance(gear.center, measured.center), 0.001f, "fully reacted at 17.4 degrees, the catcher is where it was measured");
            var obstacles = new PuckObstacles();
            view.AddObstacles(obstacles);
            Assert.IsTrue(view.SaveSurface(obstacles, onShot, out _, out PuckSurface surface));
            Assert.AreEqual(PuckSurface.Catcher, surface);

            var blocks = new GoalieView(_parent.transform);
            blocks.Show(true, GoaliePose.MiddleStandUp, false);
            blocks.React(Result(ShotResultKind.Save, 83, 82), from);
            Assert.IsFalse(blocks.SaveStop(from, net, PuckMotion.Thickness, out _), "the blocks keep the save block");
        }
    }
}
