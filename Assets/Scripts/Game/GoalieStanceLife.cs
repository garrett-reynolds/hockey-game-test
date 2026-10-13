using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Stance Life (#44, StanceLife) laid on top of the goalie character's pose: the chest rises with a breath, the hips move
    /// over one skate with the skates kept where they are, and the stick arm lifts the blade for a tap. Also sits him lower
    /// once set, before the Shot (#70, SitLower).
    /// </summary>
    internal static class GoalieStanceLife
    {
        /// <summary>Stance Life at full size: the chest rises this far on a breath in (degrees); the hips move this far over a
        /// skate (metres); a stick tap turns the stick arm up this far (degrees).</summary>
        public const float BreathDegrees = 1.6f, ShiftMetres = 0.018f, TapDegrees = 3.5f;

        /// <summary>
        /// Adds `weight` of Stance Life (0 none, 1 full; more is taken as 1) to the pose shown. model: the character's model,
        /// moved across with the hips. Only up and across, never down: the blade never goes into the ice. Leaves the Root
        /// (facing, spot) alone.
        /// </summary>
        public static void Apply(GoalieCharacter c, Transform model, StanceLifeMoment life, float weight)
        {
            if (weight <= 0f) return;
            weight = Mathf.Min(weight, 1f);
            Vector3 forward = c.Root.transform.rotation * Vector3.back, left = c.Root.transform.rotation * Vector3.right;
            float shift = (float)life.Shift * ShiftMetres * weight;
            if (Mathf.Abs(shift) > 1e-5f)
            {
                Transform lf = c.Bone(HumanBodyBones.LeftFoot), rf = c.Bone(HumanBodyBones.RightFoot);
                Vector3 lp = lf.position, rp = rf.position, lk = c.Bone(HumanBodyBones.LeftLowerLeg).position, rk = c.Bone(HumanBodyBones.RightLowerLeg).position;
                Quaternion lr = lf.rotation, rr = rf.rotation;
                model.position += left * shift;
                Plant(c, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, lf, lp, lr, lk + left * shift);
                Plant(c, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, rf, rp, rr, rk + left * shift);
            }
            Transform chest = c.Bone(HumanBodyBones.Chest) ?? c.Bone(HumanBodyBones.Spine);
            if (chest != null) GoalieGestures.Bend(c, chest == c.Bone(HumanBodyBones.Chest) ? HumanBodyBones.Chest : HumanBodyBones.Spine, -forward, BreathDegrees * (float)life.Breath * weight);
            float tap = TapDegrees * (float)life.Tap * weight;
            Transform shoulder = c.Bone(HumanBodyBones.RightUpperArm), hand = c.Bone(HumanBodyBones.RightHand);
            if (tap > 1e-4f && shoulder != null && hand != null)
            {
                Vector3 axis = Vector3.Cross(hand.position - shoulder.position, Vector3.up);
                if (axis.sqrMagnitude > 1e-8f) shoulder.rotation = Quaternion.AngleAxis(tap, axis.normalized) * shoulder.rotation;
            }
        }

        /// <summary>
        /// Before the Shot (#70): sits the pose shown `metres` lower. model: the character's model, moved down; the feet (and
        /// skates) are planted back where they were on the ice, so the knees bend more.
        /// </summary>
        public static void SitLower(GoalieCharacter c, Transform model, float metres)
        {
            Transform lf = c.Bone(HumanBodyBones.LeftFoot), rf = c.Bone(HumanBodyBones.RightFoot);
            Transform lkb = c.Bone(HumanBodyBones.LeftLowerLeg), rkb = c.Bone(HumanBodyBones.RightLowerLeg);
            if (lf == null || rf == null || lkb == null || rkb == null) return;
            Vector3 lp = lf.position, rp = rf.position, lk = lkb.position, rk = rkb.position;
            Quaternion lr = lf.rotation, rr = rf.rotation;
            model.position += Vector3.down * metres;
            Plant(c, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, lf, lp, lr, lk);
            Plant(c, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, rf, rp, rr, rk);
        }

        /// <summary>Keeps a foot (and its skate) where it was: two-bone IK on the leg, the knee kept in its own plane (`knee`: where it was, moved with the hips), so a tiny shift gives a tiny change.</summary>
        internal static void Plant(GoalieCharacter c, HumanBodyBones upper, HumanBodyBones lower, Transform foot, Vector3 at, Quaternion turn, Vector3 knee)
        {
            Transform u = c.Bone(upper), l = c.Bone(lower);
            if (u == null || l == null) return;
            GoalieCharacter.TwoBone(u, l, l.InverseTransformPoint(foot.position), at, knee);
            foot.rotation = turn;
        }
    }
}
