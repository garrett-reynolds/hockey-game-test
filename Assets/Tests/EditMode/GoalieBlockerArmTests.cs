using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The blocker (right) arm holds the stick, so a small drop (SitLower, the 3 cm he sits lower once set) must move it only a
    /// little: the stick hand lifts the blade back up (KeepBladeUp) without swinging the forearm forward and twisting it.
    /// </summary>
    public class GoalieBlockerArmTests
    {
        const float SetDropMetres = 0.03f; // RoundSettings.SetDropMetres, the default
        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_parent); }

        static Vector3 ForearmDirection(GoalieCharacter c) =>
            (c.Bone(HumanBodyBones.RightHand).position - c.Bone(HumanBodyBones.RightLowerArm).position).normalized;

        static Vector3 UpperArmDirection(GoalieCharacter c) =>
            (c.Bone(HumanBodyBones.RightLowerArm).position - c.Bone(HumanBodyBones.RightUpperArm).position).normalized;

        /// <summary>How far (degrees) the hand is turned about the forearm's own length (its local rotation's twist about the wrist's direction).</summary>
        static float WristTwist(GoalieCharacter c)
        {
            Transform hand = c.Bone(HumanBodyBones.RightHand);
            Vector3 axis = hand.localPosition.normalized;
            Quaternion q = hand.localRotation;
            return Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(Vector3.Dot(new Vector3(q.x, q.y, q.z), axis), q.w) * Mathf.Rad2Deg);
        }

        [Test]
        public void Sitting_lower_once_set_keeps_the_blocker_forearm_and_upper_arm_where_they_were()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            character.ShowReaction(0f, 0f);
            Vector3 forearm = ForearmDirection(character), upper = UpperArmDirection(character);
            Quaternion hand = character.Bone(HumanBodyBones.RightHand).rotation;
            float twist = WristTwist(character);
            character.SitLower(SetDropMetres);
            // 3 cm lower with the hand held up bends the elbow a little more (11.6 degrees measured); the fixed-pole IK swung it 22.6.
            Assert.Less(Vector3.Angle(forearm, ForearmDirection(character)), 15f, "forearm swung (degrees)");
            Assert.Less(Vector3.Angle(upper, UpperArmDirection(character)), 15f, "upper arm swung (degrees)");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(twist, WristTwist(character))), 5f, "forearm twisted about its own length (degrees)");
            Assert.Less(Quaternion.Angle(hand, character.Bone(HumanBodyBones.RightHand).rotation), 2f, "the hand keeps its turn, so the stick keeps its angle (degrees)");
        }

        static readonly Vector3 Eye = new Vector3(0f, 1.5f, -9f);

        [Test]
        public void A_goal_bend_forward_keeps_the_blocker_arm_hanging_with_the_blade_lifted_back()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            character.ShowReaction(0f, 0f);
            float readyHandY = character.Bone(HumanBodyBones.RightHand).position.y;
            NetFaceMapping.ToMetres(95, 95, out double right, out double up);
            character.React(SaveReaction.Glove, Eye, new Vector3((float)right, (float)up, 0f), 0f, false);
            foreach (double t in new[] { 0.6, 1.0, 1.5, 2.5 })
            {
                character.ShowReaction(1f, 0f, AfterShotGesture.Slump, AfterShot.At(AfterShotGesture.Slump, t, -1), Eye);
                Vector3 shoulder = character.Bone(HumanBodyBones.RightUpperArm).position, elbow = character.Bone(HumanBodyBones.RightLowerArm).position;
                Vector3 hand = character.Bone(HumanBodyBones.RightHand).position;
                string at = " at " + t + " s";
                Assert.Less(elbow.y, shoulder.y, "elbow below the shoulder" + at);
                Assert.Less(hand.y, elbow.y, "hand below the elbow: the forearm hangs" + at);
                Assert.GreaterOrEqual(hand.y, readyHandY - 0.1f, "the stick hand does not sink below the stance (blade kept off the ice)" + at);
            }
        }

        [Test]
        public void Sitting_lower_still_lifts_the_blade_back_to_its_stance_height()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            character.ShowReaction(0f, 0f);
            Vector3 hand = character.Bone(HumanBodyBones.RightHand).position;
            character.SitLower(SetDropMetres);
            // The model sits 3 cm lower; the stick hand goes back up to hold the blade at its stance height.
            Assert.AreEqual(hand.y, character.Bone(HumanBodyBones.RightHand).position.y, 0.01f, "hand height (metres)");
        }
    }
}
