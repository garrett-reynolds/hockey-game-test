using HockeyGame;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The skate push on the goalie character (#126, GoalieSkatePush): the far-side skate plants out and back on the ice, the
    /// lead skate turns on a T-push, and nothing else moves. ShowReaction sets every bone, so each test shows the Ready
    /// Stance, records it, shows it again and then adds the push.
    /// </summary>
    public class GoalieSkatePushTests
    {
        GameObject _rink;
        GoalieCharacter _goalie;

        [SetUp]
        public void SetUp()
        {
            _rink = new GameObject("Rink (test)");
            _goalie = new GoalieCharacter(_rink.transform);
            Assert.IsTrue(_goalie.Present, "Assets/Resources/Models/goalie-dressing.fbx is missing");
            _goalie.Show(true);
            _goalie.ShowReaction(0f);
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_rink); }

        static PushShown Shown(PushKind kind, Skate skate) => new PushShown(kind, skate, PushPhase.Push, 1);

        Transform Bone(HumanBodyBones bone) => _goalie.Bone(bone);

        /// <summary>The foot bones by world X: [0] the one with the smaller X (Skate.Left), [1] the larger (Skate.Right).</summary>
        Transform[] FeetByX()
        {
            Transform l = Bone(HumanBodyBones.LeftFoot), r = Bone(HumanBodyBones.RightFoot);
            return l.position.x < r.position.x ? new[] { l, r } : new[] { r, l };
        }

        /// <summary>The skate's toe direction on the ice: the foot's own axis that points most toward the shooter (-Z) in the Ready Stance, as a local axis, so it can be measured turned.</summary>
        static Vector3 ToeAxis(Transform foot)
        {
            Vector3 best = Vector3.forward;
            float most = float.MinValue;
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                float dot = Vector3.Dot(foot.TransformDirection(axis), Vector3.back);
                if (dot > most) { most = dot; best = axis; }
            }
            return best;
        }

        static Vector3 OnIce(Vector3 v) => new Vector3(v.x, 0f, v.z).normalized;

        [Test]
        public void The_far_side_skate_pushes_out_for_a_move_left_and_a_move_right([Values(Skate.Right, Skate.Left)] Skate skate)
        {
            Transform[] feet = FeetByX();
            Vector3[] before = { feet[0].position, feet[1].position };
            int push = skate == Skate.Right ? 1 : 0, other = 1 - push;
            float outward = skate == Skate.Right ? 1f : -1f;

            _goalie.ShowReaction(0f);
            _goalie.ShowPush(Shown(PushKind.Shuffle, skate), new PushPoseMoment(1, 0));

            Assert.GreaterOrEqual((feet[push].position.x - before[push].x) * outward, GoalieSkatePush.PushOutMetres * 0.9f,
                skate + " skate pushes out along X, away from the move");
            Assert.Less(Vector3.Distance(feet[other].position, before[other]), 0.01f, "the other skate stays where it was");
            Assert.AreEqual(before[0].y, feet[0].position.y, 0.001f, "the smaller-X skate stays on the ice");
            Assert.AreEqual(before[1].y, feet[1].position.y, 0.001f, "the larger-X skate stays on the ice");
        }

        // ---- #130: the push must read from across the rink. Fixed numbers (not the constants), so a tuned-down constant fails here. ----

        static float KneeAngle(Transform upper, Transform lower, Transform foot)
            => Vector3.Angle(upper.position - lower.position, foot.position - lower.position);

        [Test]
        public void A_full_push_takes_the_push_skate_at_least_3_times_the_old_0_12_m_out([Values(Skate.Right, Skate.Left)] Skate skate)
        {
            Transform[] feet = FeetByX();
            int push = skate == Skate.Right ? 1 : 0;
            float outward = skate == Skate.Right ? 1f : -1f, was = feet[push].position.x;
            _goalie.ShowReaction(0f);
            _goalie.ShowPush(Shown(PushKind.Shuffle, skate), new PushPoseMoment(1, 0));
            Assert.GreaterOrEqual((feet[push].position.x - was) * outward, 0.36f, skate + " skate out (m)");
        }

        [Test]
        public void A_full_push_bends_the_lead_knee_and_lowers_and_leans_the_hips_into_the_move([Values(Skate.Right, Skate.Left)] Skate skate)
        {
            Transform[] feet = FeetByX();
            int push = skate == Skate.Right ? 1 : 0;
            bool leadIsLeftBone = feet[1 - push] == Bone(HumanBodyBones.LeftFoot);
            Transform up = Bone(leadIsLeftBone ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg), low = Bone(leadIsLeftBone ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            float kneeBefore = KneeAngle(up, low, feet[1 - push]);
            Vector3 hipsBefore = Bone(HumanBodyBones.Hips).position;
            float toward = skate == Skate.Right ? -1f : 1f;

            _goalie.ShowReaction(0f);
            _goalie.ShowPush(Shown(PushKind.Shuffle, skate), new PushPoseMoment(1, 0));

            Assert.LessOrEqual(KneeAngle(up, low, feet[1 - push]), kneeBefore - 8f, "the lead knee bends at least 8 degrees more");
            Vector3 hips = Bone(HumanBodyBones.Hips).position;
            Assert.GreaterOrEqual((hips.x - hipsBefore.x) * toward, 0.06f, "the hips lean into the move");
            Assert.GreaterOrEqual(hipsBefore.y - hips.y, 0.03f, "the hips sit lower");
            Assert.AreEqual(Bone(HumanBodyBones.LeftFoot).position.y, Bone(HumanBodyBones.RightFoot).position.y, 0.01f, "both skates on the ice");
        }

        [Test]
        public void A_full_T_push_turns_the_lead_skate_at_least_45_degrees()
        {
            Transform[] feet = FeetByX();
            Transform lead = feet[0]; // Skate.Right pushes, the smaller-X skate leads
            Vector3 toe = ToeAxis(lead), before = OnIce(lead.TransformDirection(toe));
            _goalie.ShowReaction(0f);
            _goalie.ShowPush(Shown(PushKind.TPush, Skate.Right), new PushPoseMoment(0, 1));
            Assert.GreaterOrEqual(Mathf.Abs(Vector3.SignedAngle(before, OnIce(lead.TransformDirection(toe)), Vector3.up)), 45f);
        }

        [Test]
        public void Changing_the_push_side_fades_the_old_skate_out_with_no_leg_snap()
        {
            var t = new PushPoseTracker();
            var right = new PushShown(PushKind.Shuffle, Skate.Right, PushPhase.Push, 1);
            var left = new PushShown(PushKind.Shuffle, Skate.Left, PushPhase.Push, 1);
            for (int i = 0; i < 20; i++) t.Step(right, 1.0 / 60);
            Transform[] feet = FeetByX();
            _goalie.ShowReaction(0f);
            _goalie.ShowPush(right, t.Now);
            Vector3 old = feet[1].position;
            t.Step(left, 1.0 / 60);
            _goalie.ShowReaction(0f);
            _goalie.ShowPush(left, t.Now);
            Assert.Less(Vector3.Distance(old, feet[1].position), 0.1f, "the old pushing skate does not snap back on the first frame");
        }

        [Test]
        public void The_skate_is_picked_by_its_world_X_when_he_faces_the_other_way([Values(Skate.Right, Skate.Left)] Skate skate)
        {
            _goalie.ShowReaction(0f, 180f); // turned right round: his bone names swap sides in the world
            Transform[] feet = FeetByX();
            Vector3[] before = { feet[0].position, feet[1].position };
            int push = skate == Skate.Right ? 1 : 0, other = 1 - push;
            float outward = skate == Skate.Right ? 1f : -1f;

            _goalie.ShowPush(Shown(PushKind.Shuffle, skate), new PushPoseMoment(1, 0));

            Assert.Greater((feet[push].position.x - before[push].x) * outward, GoalieSkatePush.PushOutMetres * 0.5f, "the foot with the " + (push == 1 ? "larger" : "smaller") + " world X pushes");
            Assert.Less(Vector3.Distance(feet[other].position, before[other]), 0.01f, "the other skate stays");
        }

        [Test]
        public void A_shuffle_leaves_the_lead_skate_square([Values(Skate.Right, Skate.Left)] Skate skate)
        {
            Transform[] feet = FeetByX();
            Quaternion[] before = { feet[0].rotation, feet[1].rotation };

            _goalie.ShowReaction(0f);
            _goalie.ShowPush(Shown(PushKind.Shuffle, skate), new PushPoseMoment(1, 0));

            Assert.Less(Quaternion.Angle(before[0], feet[0].rotation), 0.5f, "the smaller-X skate keeps its turn");
            Assert.Less(Quaternion.Angle(before[1], feet[1].rotation), 0.5f, "the larger-X skate keeps its turn");
        }

        [Test]
        public void A_T_push_turns_the_lead_skate_toward_the_move([Values(Skate.Right, Skate.Left)] Skate skate)
        {
            Transform[] feet = FeetByX();
            Transform lead = skate == Skate.Right ? feet[0] : feet[1];
            Vector3 toe = ToeAxis(lead);
            Vector3 before = OnIce(lead.TransformDirection(toe));
            float toward = skate == Skate.Right ? -1f : 1f; // a move left is toward -X

            _goalie.ShowReaction(0f);
            _goalie.ShowPush(Shown(PushKind.TPush, skate), new PushPoseMoment(0, 1));
            Vector3 turned = OnIce(lead.TransformDirection(toe));
            float angle = Vector3.SignedAngle(before, turned, Vector3.up);
            Assert.AreEqual(GoalieSkatePush.LeadTurnDegrees, Mathf.Abs(angle), 1f, "turned by LeadTurnDegrees");
            Assert.AreEqual(toward, Mathf.Sign(turned.x - before.x), "the toe turns toward the move");

            _goalie.ShowReaction(0f);
            _goalie.ShowPush(Shown(PushKind.TPush, skate), new PushPoseMoment(0, 0));
            Assert.Less(Vector3.Angle(before, OnIce(lead.TransformDirection(toe))), 0.5f, "back at the Ready Stance at LeadTurn 0");
        }

        [Test]
        public void A_legs_only_push_never_moves_the_body_and_a_full_one_moves_the_hips_but_never_the_Root()
        {
            // #130: the push lowers and leans the hips (bodyShare 1); a Save Reaction's push (bodyShare 0) is legs only; neither moves the Root.
            Vector3 rootAt0 = _goalie.Root.transform.position, hips0 = Bone(HumanBodyBones.Hips).position;
            _goalie.ShowPush(Shown(PushKind.TPush, Skate.Right), new PushPoseMoment(1, 1), 1f);
            Assert.Less(Vector3.Distance(rootAt0, _goalie.Root.transform.position), 1e-5f, "Root position with the body lean");
            Assert.Greater(Vector3.Distance(hips0, Bone(HumanBodyBones.Hips).position), 0.05f, "the full push moves the hips");
            _goalie.ShowReaction(0f);
            HumanBodyBones[] kept = { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest };
            Vector3 rootAt = _goalie.Root.transform.position;
            Quaternion rootTurn = _goalie.Root.transform.rotation;
            Vector3 modelAt = _goalie.Animator.transform.position;
            var at = new Vector3[kept.Length];
            for (int i = 0; i < kept.Length; i++) at[i] = Bone(kept[i]).position;

            foreach (PushKind kind in new[] { PushKind.Shuffle, PushKind.TPush })
            foreach (Skate skate in new[] { Skate.Left, Skate.Right })
            foreach (double pushOut in new[] { 0.0, 0.5, 1.0 })
            foreach (double leadTurn in new[] { 0.0, 0.5, 1.0 })
            {
                _goalie.ShowReaction(0f);
                _goalie.ShowPush(Shown(kind, skate), new PushPoseMoment(pushOut, leadTurn), 0f);
                string label = $"{kind} {skate} out {pushOut} turn {leadTurn}: ";
                Assert.Less(Vector3.Distance(rootAt, _goalie.Root.transform.position), 1e-5f, label + "Root position");
                Assert.Less(Quaternion.Angle(rootTurn, _goalie.Root.transform.rotation), 1e-3f, label + "Root rotation");
                Assert.Less(Vector3.Distance(modelAt, _goalie.Animator.transform.position), 1e-5f, label + "model position");
                for (int i = 0; i < kept.Length; i++)
                    Assert.Less(Vector3.Distance(at[i], Bone(kept[i]).position), 1e-5f, label + kept[i]);
            }
        }

        static HumanBodyBones[] AllBones()
        {
            var bones = new System.Collections.Generic.List<HumanBodyBones>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++) bones.Add((HumanBodyBones)i);
            return bones.ToArray();
        }

        [Test]
        public void Without_a_push_nothing_changes()
        {
            HumanBodyBones[] all = AllBones();
            var before = new Quaternion[all.Length];
            for (int i = 0; i < all.Length; i++) before[i] = Bone(all[i]) != null ? Bone(all[i]).rotation : Quaternion.identity;

            _goalie.ShowPush(PushShown.None, new PushPoseMoment(0, 0));
            _goalie.ShowPush(Shown(PushKind.TPush, Skate.Right), new PushPoseMoment(0, 0));

            for (int i = 0; i < all.Length; i++)
                if (Bone(all[i]) != null)
                    Assert.Less(Quaternion.Angle(before[i], Bone(all[i]).rotation), 1e-3f, all[i] + " keeps its rotation");
        }

        [Test]
        public void A_push_after_a_Save_Reaction_pose_keeps_the_hands_where_they_were()
        {
            _goalie.Spot = new Vector3(0f, 0f, GoalieCharacter.StandZ);
            _goalie.React(SaveReaction.Glove, new Vector3(3f, 1.15f, -12f), new Vector3(0.9f, 0.9f, 0f), 0f, true);
            _goalie.ShowReaction(1f);
            Vector3 left = Bone(HumanBodyBones.LeftHand).position, right = Bone(HumanBodyBones.RightHand).position;

            _goalie.ShowPush(Shown(PushKind.TPush, Skate.Right), new PushPoseMoment(1, 1), 0f); // legs only, as GoalieView shows a Save Reaction's push

            Assert.Less(Vector3.Distance(left, Bone(HumanBodyBones.LeftHand).position), 1e-5f, "the glove hand stays");
            Assert.Less(Vector3.Distance(right, Bone(HumanBodyBones.RightHand).position), 1e-5f, "the stick hand stays");
        }

        [Test]
        public void The_reaction_slide_is_0_before_any_React_and_more_than_10_cm_for_a_far_glove_aim()
        {
            Assert.AreEqual(0f, _goalie.ReactionSlideMetres, "before any React");

            _goalie.Spot = new Vector3(0f, 0f, GoalieCharacter.StandZ);
            _goalie.React(SaveReaction.Glove, new Vector3(3f, 1.15f, -12f), new Vector3(1.5f, 0.5f, 0f), 0f, true);

            Assert.Greater(Mathf.Abs(_goalie.ReactionSlideMetres), 0.1f, "slide for a far glove-side aim");
        }

        [Test]
        public void With_no_goalie_model_the_push_and_the_slide_do_nothing()
        {
            var empty = new GoalieCharacter(_rink.transform, name => null);
            empty.ShowPush(Shown(PushKind.TPush, Skate.Left), new PushPoseMoment(1, 1));
            Assert.AreEqual(0f, empty.ReactionSlideMetres);
        }
    }
}
