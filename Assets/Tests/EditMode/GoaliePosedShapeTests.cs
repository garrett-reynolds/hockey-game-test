using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The goalie's gear and body for the Shot are measured once per Goalie Pose (GoalieCharacter.Prepare) and only placed
    /// at the facing on the Shot. They must match posing the character there and measuring it fresh, at any facing, and
    /// measuring must leave the character as it was shown.
    /// </summary>
    public class GoaliePosedShapeTests
    {
        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_parent); }

        static readonly string[] Pieces =
        {
            GearPiece.LeftPad, GearPiece.RightPad, GearPiece.Blocker, GearPiece.Catcher, GearPiece.Stick,
            GearPiece.Mask, GearPiece.Visor, GearPiece.LeftSkate, GearPiece.RightSkate, GearPiece.Body
        };

        [TestCase(GoaliePose.LeftPostHug)]
        [TestCase(GoaliePose.MiddleButterfly)]
        [TestCase(GoaliePose.RightButterfly)]
        public void Measured_once_the_pose_matches_a_fresh_pose_at_every_facing(GoaliePose pose)
        {
            var character = new GoalieCharacter(_parent.transform);
            Assert.IsTrue(character.Present);
            character.Show(true);
            character.Pose(pose, 0f, 0f);
            character.Prepare(pose); // measured at facing 0, in the Ready Stance shown
            foreach (float facing in new[] { 0f, 17.5f, -28f })
            {
                character.Pose(pose, 0.3f, 4f); // something else on show when the Shot comes
                var cached = new PuckObstacles();
                character.AddPosed(cached, pose, facing);
                var posedBounds = new Bounds[Pieces.Length - 1];
                for (int i = 0; i < posedBounds.Length; i++)
                    Assert.IsTrue(character.PosedGearBounds(pose, facing, Pieces[i], out posedBounds[i]), Pieces[i]);

                character.Pose(pose, 1f, facing);
                var fresh = new PuckObstacles();
                character.AddTo(fresh);
                for (int i = 0; i < posedBounds.Length; i++)
                {
                    Assert.IsTrue(character.GearBounds(Pieces[i], out Bounds b));
                    Assert.Less(Vector3.Distance(b.min, posedBounds[i].min) + Vector3.Distance(b.max, posedBounds[i].max), 1e-4f,
                        Pieces[i] + " bounds at " + facing);
                }
                foreach (string piece in Pieces)
                {
                    Assert.IsTrue(character.GearBounds(piece == GearPiece.Body ? GearPiece.Catcher : piece, out Bounds near));
                    for (int k = 0; k < 6; k++)
                    {
                        Vector3 point = near.center + new Vector3((k % 3 - 1) * 0.2f, (k / 3) * 0.3f - 0.15f, -0.25f);
                        Assert.IsTrue(fresh.Nearest(point, piece, out Vector3 n1, out PuckSurface s1, out float d1), piece);
                        Assert.IsTrue(cached.Nearest(point, piece, out Vector3 n2, out PuckSurface s2, out float d2), piece);
                        Assert.AreEqual(s1, s2, piece);
                        Assert.AreEqual(d1, d2, 1e-4f, piece + " distance at " + facing);
                        Assert.Less(Vector3.Angle(n1, n2), 0.5f, piece + " normal at " + facing);
                    }
                }
                Vector3 from = new Vector3(0.4f, 0.5f, -6f), to = new Vector3(0.3f, 0.3f, 0.5f);
                bool hit1 = fresh.Cast(from, to, 0.025f, out Vector3 at1, out _, out PuckSurface c1);
                bool hit2 = cached.Cast(from, to, 0.025f, out Vector3 at2, out _, out PuckSurface c2);
                Assert.AreEqual(hit1, hit2);
                if (hit1) { Assert.AreEqual(c1, c2); Assert.Less(Vector3.Distance(at1, at2), 1e-4f); }
            }
        }

        [Test]
        public void Measuring_a_pose_leaves_the_character_as_shown()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            character.Pose(GoaliePose.LeftButterfly, 0.25f, 12f);
            Transform knee = character.Bone(HumanBodyBones.LeftLowerLeg);
            Quaternion kneeBefore = knee.rotation;
            Vector3 rootBefore = character.Root.transform.position;
            Quaternion turnBefore = character.Root.transform.rotation;

            character.Prepare(GoaliePose.RightPostHug);
            character.AddPosed(new PuckObstacles(), GoaliePose.MiddleStandUp, -20f);

            Assert.AreEqual(0f, Quaternion.Angle(kneeBefore, knee.rotation), 1e-3f);
            Assert.AreEqual(0f, Vector3.Distance(rootBefore, character.Root.transform.position), 1e-6f);
            Assert.AreEqual(0f, Quaternion.Angle(turnBefore, character.Root.transform.rotation), 1e-3f);
        }
    }
}
