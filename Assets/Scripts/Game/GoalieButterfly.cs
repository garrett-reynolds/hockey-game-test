using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The butterfly the Pads Save Reaction drops into, laid on top of the Ready Stance: knees down, pads flared flat on the
    /// ice, hands low and out. Each limb is aimed in the shooter's frame: toward the shooter's right, up, and toward the
    /// shooter. The shooter's right arm and leg are the goalie's left (the catcher side). The legs and arms are the #33
    /// MiddleButterfly pose; the chest, neck and wrists keep their Ready Stance bend, because the model's face and hands are
    /// skinned partly to the bones above them and come out of the mask and gloves when those bends change.
    /// </summary>
    internal static class GoalieButterfly
    {
        static Vector3 V(float right, float up, float forward) => new Vector3(right, up, forward);

        static readonly Vector3 LeftThigh = V(-0.35f, -0.85f, 0.4f), LeftShin = V(-1f, -0.05f, -0.4f);
        static readonly Vector3 RightThigh = V(0.35f, -0.85f, 0.4f), RightShin = V(1f, -0.05f, -0.4f);
        static readonly Vector3 LeftUpperArm = V(-0.161f, -0.188f, 0.035f), LeftForearm = V(0.041f, -0.212f, 0.105f);
        static readonly Vector3 RightUpperArm = V(0.134f, -0.181f, 0.108f), RightForearm = V(0.102f, -0.186f, 0.112f);
        static readonly Vector3 Hips = V(0f, 0f, -0.08f);

        /// <summary>Poses the limbs and returns where the hips go: metres from the stand point, in the Root frame.</summary>
        public static Vector3 Apply(GoalieCharacter c)
        {
            Quaternion q = c.Root.transform.rotation;
            Vector3 up = q * Vector3.up, forward = q * Vector3.back, right = q * Vector3.right; // right: the shooter's right, the goalie's left
            Vector3 Dir(Vector3 v) => right * v.x + up * v.y + forward * v.z;

            Leg(c, true, Dir(RightThigh), Dir(RightShin), forward);
            Leg(c, false, Dir(LeftThigh), Dir(LeftShin), forward);
            Arm(c, true, Dir(RightUpperArm), Dir(RightForearm));
            Arm(c, false, Dir(LeftUpperArm), Dir(LeftForearm));
            return c.Root.transform.InverseTransformDirection(Dir(Hips));
        }

        /// <summary>Aims a leg (the goalie's left is on the shooter's right) and turns its pad front to the shooter.</summary>
        static void Leg(GoalieCharacter c, bool goalieLeft, Vector3 thigh, Vector3 shin, Vector3 forward)
        {
            c.Aim(goalieLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg, goalieLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, thigh);
            c.Aim(goalieLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, goalieLeft ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, shin);
            c.FacePad(goalieLeft, forward);
        }

        /// <summary>Aims an arm (upper arm, then forearm); the hand keeps its Ready Stance bend at the wrist.</summary>
        static void Arm(GoalieCharacter c, bool goalieLeft, Vector3 upper, Vector3 fore)
        {
            c.Aim(goalieLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, goalieLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, upper);
            c.Aim(goalieLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, goalieLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, fore);
        }
    }
}
