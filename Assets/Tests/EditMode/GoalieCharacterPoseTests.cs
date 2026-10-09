using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class GoalieCharacterPoseTests
    {
        const float NearIce = 0.12f;
        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_parent); }

        [Test]
        public void Set_0_is_the_ready_stance_and_each_pose_looks_the_same_whatever_pose_came_before()
        {
            var character = new GoalieCharacter(_parent.transform);
            var ready = Snapshot(character);

            character.Pose(GoaliePose.MiddleButterfly, 0f);
            AssertSame(ready, Snapshot(character), "set 0 = ready stance");

            character.Pose(GoaliePose.MiddleButterfly, 1f);
            var butterfly = Snapshot(character);
            Assert.Greater(Difference(ready, butterfly), 10f, "the butterfly moves the bones away from the ready stance");

            character.Pose(GoaliePose.LeftPostHug, 1f);
            character.Pose(GoaliePose.RightButterfly, 0.5f);
            character.Pose(GoaliePose.MiddleButterfly, 1f);
            AssertSame(butterfly, Snapshot(character), "a pose does not depend on the poses before it");

            character.Pose(GoaliePose.MiddleButterfly, 0.5f);
            float half = Difference(ready, Snapshot(character));
            Assert.That(half, Is.InRange(Difference(ready, butterfly) * 0.2f, Difference(ready, butterfly) * 0.8f), "half set is between the stance and the pose");
        }

        [Test]
        public void Every_pose_keeps_the_goalie_on_the_ice_in_front_of_the_net()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            foreach (GoaliePose pose in System.Enum.GetValues(typeof(GoaliePose)))
            {
                character.Pose(pose, 1f);
                Bounds body = character.BodyBounds();
                Assert.Less(body.max.z, -0.02f, pose + ": in front of the Net Face");
                float lowest = float.MaxValue;
                foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
                    if (r.name.Contains("Pad") || r.name.Contains("Skate"))
                        lowest = Mathf.Min(lowest, LowestY(r));
                Assert.AreEqual(0f, lowest, 0.03f, pose + ": pads or skates on the ice");
            }
        }

        [Test]
        public void Every_pose_looks_like_a_real_goalie_stick_on_the_ice_pads_down_head_upright_hands_on_their_own_side()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            foreach (GoaliePose pose in System.Enum.GetValues(typeof(GoaliePose)))
            {
                character.Pose(pose, 1f);
                // On or near the ice: within NearIce (the stand-up holds its hands up, so its blade sits ~11 cm up).
                Assert.That(LowestY(Gear(character, "Stick")), Is.InRange(-0.03f, NearIce), pose + ": stick blade on or near the ice");
                foreach (string pad in new[] { "Left Pad", "Right Pad" })
                    Assert.Less(LowestY(Gear(character, pad)), NearIce, pose + ": " + pad + " down on the ice, not floating");
                Vector3 neck = character.Bone(HumanBodyBones.Neck).position, head = character.Bone(HumanBodyBones.Head).position;
                Assert.Less(Vector3.Angle(head - neck, Vector3.up), 20f, pose + ": head upright");
                // The goalie faces the shooter (-z), so the goalie's right (blocker) side is -x. Neither hand reaches across the chest.
                float chest = character.Bone(HumanBodyBones.Chest).position.x;
                Assert.Less(character.Bone(HumanBodyBones.RightHand).position.x, chest + 0.05f, pose + ": blocker stays on the goalie's right");
                Assert.Greater(character.Bone(HumanBodyBones.LeftHand).position.x, chest - 0.05f, pose + ": catcher stays on the goalie's left");
            }
        }

        [Test]
        public void The_pads_blocker_and_catcher_are_close_to_their_real_size_in_the_model_file()
        {
            var character = new GoalieCharacter(_parent.transform);
            GameObject file = ModelFiles.Load(ModelFiles.GoalieDressing, () => null);
            try
            {
                foreach (string piece in new[] { "Left Pad", "Right Pad", "Blocker", "Catcher" })
                {
                    Vector3 posed = Gear(character, piece).transform.localScale, real = Find(file.transform, "Gear " + piece).localScale;
                    for (int axis = 0; axis < 3; axis++)
                        Assert.LessOrEqual(posed[axis] / real[axis], 1.15f + 1e-4f, piece + " is at most 15% bigger than in the model file");
                }
            }
            finally { Object.DestroyImmediate(file); }
        }

        [Test]
        public void With_no_model_posing_does_nothing()
        {
            var character = new GoalieCharacter(_parent.transform, name => null);
            Assert.DoesNotThrow(() => character.Pose(GoaliePose.LeftButterfly, 1f));
        }

        static Renderer Gear(GoalieCharacter character, string name) =>
            Find(character.Root.transform, "Gear " + name).GetComponent<Renderer>();

        static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            Assert.Fail("no " + name);
            return null;
        }

        static float LowestY(Renderer r)
        {
            Matrix4x4 m = r.transform.localToWorldMatrix;
            float lowest = float.MaxValue;
            foreach (Vector3 v in r.GetComponent<MeshFilter>().sharedMesh.vertices) lowest = Mathf.Min(lowest, m.MultiplyPoint3x4(v).y);
            return lowest;
        }

        static Dictionary<string, (Vector3 position, Quaternion rotation)> Snapshot(GoalieCharacter character)
        {
            var snap = new Dictionary<string, (Vector3, Quaternion)>();
            foreach (Transform t in character.Root.GetComponentsInChildren<Transform>(true))
                snap[t.name] = (t.position, t.rotation);
            return snap;
        }

        /// <summary>Largest bone turn (degrees) plus largest move (centimetres) between two snapshots.</summary>
        static float Difference(Dictionary<string, (Vector3 position, Quaternion rotation)> a, Dictionary<string, (Vector3 position, Quaternion rotation)> b)
        {
            float angle = 0f, move = 0f;
            foreach (var kv in a)
            {
                angle = Mathf.Max(angle, Quaternion.Angle(kv.Value.rotation, b[kv.Key].rotation));
                move = Mathf.Max(move, Vector3.Distance(kv.Value.position, b[kv.Key].position) * 100f);
            }
            return angle + move;
        }

        static void AssertSame(Dictionary<string, (Vector3 position, Quaternion rotation)> a, Dictionary<string, (Vector3 position, Quaternion rotation)> b, string message)
        {
            Assert.Less(Difference(a, b), 0.5f, message);
        }
    }
}
