using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The character pose for each Goalie Pose, laid on top of the ready stance. Each limb is aimed in the shooter's
    /// frame: up, toward the shooter, and toward the shooter's right. Each Right pose is written in a mirrored
    /// frame, so the limb on the post side swaps (blocker at the left post, catcher at the right); it is tuned on its own.
    /// Hands keep their gear to the shooter: the blocker board and the catcher pocket face the shooter.
    /// The shapes read as the Goalie Pose table from the shooter: Open Zones clear, Covered Zones mostly covered (see GoalieCoverageTests).
    /// </summary>
    internal static class GoaliePoseLimbs
    {
        /// <summary>
        /// One pose: limb directions as (toward the shooter's right, up, toward the shooter). "Right" and "Left" are the
        /// shooter's: the Right arm is the goalie's catcher hand (the blocker in a mirrored pose). Hips: where the hips go
        /// from the stand point, in metres (right, unused, toward the shooter).
        /// </summary>
        internal struct Shape
        {
            public Vector3 Spine, Neck;
            public Vector3 RightThigh, RightShin, LeftThigh, LeftShin;
            public Vector3 RightUpperArm, RightForearm, RightFingers, LeftUpperArm, LeftForearm, LeftFingers;
            public Vector3 Hips;
        }

        static Vector3 V(float right, float up, float forward) => new Vector3(right, up, forward);

        /// <summary>
        /// One shape per Goalie Pose, in GoaliePose order, hand-designed to read as real goalie positions (#33). The Right shapes
        /// are written in a mirrored frame (as the Left ones), but kept separate: the post-side hand is the blocker on the left and
        /// the catcher on the right, so each side is posed on its own. Blocker fingers point down (rolled in or out a little), so
        /// the stick, held across the palm, reaches the ice; the arms are only ~0.49 m long, so the hands stay near the body.
        /// </summary>
        internal static readonly Shape[] Shapes =
        {
            new Shape // LeftPostHug: crouched against the left post, post pad upright on the post, blocker low in front with the stick on the ice, catcher out in front
            {
                Spine = V(-0.25f, 0.7f, 0.7f), Neck = V(0.15f, 1f, 0.15f),
                LeftThigh = V(-0.7f, -0.15f, 0.7f), LeftShin = V(-0.2f, -0.85f, -0.45f),
                RightThigh = V(0.75f, -0.15f, 0.45f), RightShin = V(0.3f, -0.85f, -0.3f),
                LeftUpperArm = V(0.059f, -0.238f, 0.049f), LeftForearm = V(0.166f, -0.158f, 0.071f), LeftFingers = V(0.342f, -0.94f, 0f),
                RightUpperArm = V(0.219f, -0.116f, 0.031f), RightForearm = V(-0.034f, -0.2f, 0.129f), RightFingers = V(0.1f, 0.9f, 0.2f),
                Hips = V(-0.38f, 0f, 0f),
            },
            new Shape // LeftButterfly: knees down leaning into the left post, pads flat across the ice, blocker and stick at the post, catcher low and out
            {
                Spine = V(-0.55f, 0.8f, 0.5f), Neck = V(0.3f, 1f, 0.05f),
                LeftThigh = V(-0.38f, -0.76f, 0.4f), LeftShin = V(-1f, -0.05f, -0.4f),
                RightThigh = V(0.8f, -0.6f, 0.25f), RightShin = V(1f, -0.05f, -0.5f),
                LeftUpperArm = V(-0.174f, -0.17f, 0.06f), LeftForearm = V(0.074f, -0.18f, 0.14f), LeftFingers = V(0.492f, -0.853f, -0.174f),
                RightUpperArm = V(0.192f, -0.149f, 0.058f), RightForearm = V(0.164f, -0.161f, 0.071f), RightFingers = V(0.85f, 0.45f, 0.2f),
                Hips = V(-0.56f, 0f, -0.08f),
            },
            new Shape // MiddleButterfly: knees down, pads flared flat on the ice, chest up, hands low and out, stick paddle across the five-hole
            {
                Spine = V(0f, 0.85f, 0.5f), Neck = V(0f, 1f, 0.05f),
                LeftThigh = V(-0.35f, -0.85f, 0.4f), LeftShin = V(-1f, -0.05f, -0.4f),
                RightThigh = V(0.35f, -0.85f, 0.4f), RightShin = V(1f, -0.05f, -0.4f),
                LeftUpperArm = V(-0.161f, -0.188f, 0.035f), LeftForearm = V(0.041f, -0.212f, 0.105f), LeftFingers = V(0.492f, -0.853f, -0.174f),
                RightUpperArm = V(0.134f, -0.181f, 0.108f), RightForearm = V(0.102f, -0.186f, 0.112f), RightFingers = V(0.15f, 0.95f, 0.2f),
                Hips = V(0f, 0f, -0.08f),
            },
            new Shape // MiddleStandUp: upright wide stance, catcher up and out, blocker up at the side with the stick held near-vertical to the ice
            {
                Spine = V(0f, 0.9f, 0.4f), Neck = V(0f, 1f, 0.05f),
                LeftThigh = V(-0.55f, -0.6f, 0.5f), LeftShin = V(-0.1f, -0.95f, -0.2f),
                RightThigh = V(0.55f, -0.6f, 0.5f), RightShin = V(0.1f, -0.95f, -0.2f),
                LeftUpperArm = V(-0.15f, -0.177f, 0.093f), LeftForearm = V(-0.119f, -0.18f, 0.105f), LeftFingers = V(-0.766f, -0.643f, 0f),
                RightUpperArm = V(0.187f, -0.111f, 0.124f), RightForearm = V(0.159f, -0.111f, 0.142f), RightFingers = V(0.2f, 0.95f, 0.15f),
                Hips = V(0f, 0f, 0f),
            },
            new Shape // RightPostHug: LeftPostHug in the mirrored frame; catcher up on the post, blocker and stick in front of the five-hole
            {
                Spine = V(-0.25f, 0.7f, 0.7f), Neck = V(0.15f, 1f, 0.15f),
                LeftThigh = V(-0.7f, -0.15f, 0.7f), LeftShin = V(-0.2f, -0.85f, -0.45f),
                RightThigh = V(0.7f, -0.15f, 0.45f), RightShin = V(0.25f, -0.85f, -0.3f),
                LeftUpperArm = V(0.238f, -0.072f, 0.024f), LeftForearm = V(-0.158f, 0.042f, 0.176f), LeftFingers = V(-0.1f, 0.9f, 0.2f),
                RightUpperArm = V(-0.065f, -0.23f, 0.072f), RightForearm = V(0.195f, -0.1f, 0.098f), RightFingers = V(0.087f, -0.996f, 0f),
                Hips = V(-0.38f, 0f, 0f),
            },
            new Shape // RightButterfly: LeftButterfly in the mirrored frame; catcher high on the post, blocker low on the far side, stick on the ice
            {
                Spine = V(-0.5f, 0.72f, 0.6f), Neck = V(0.3f, 1f, 0.05f),
                LeftThigh = V(-0.42f, -0.73f, 0.4f), LeftShin = V(-1f, -0.05f, -0.4f),
                RightThigh = V(0.8f, -0.6f, 0.25f), RightShin = V(1f, -0.05f, -0.5f),
                LeftUpperArm = V(0.172f, 0.063f, 0.17f), LeftForearm = V(-0.222f, 0.087f, 0.03f), LeftFingers = V(-0.05f, 0.95f, 0.2f),
                RightUpperArm = V(0.077f, -0.232f, 0.051f), RightForearm = V(0.101f, -0.211f, 0.055f), RightFingers = V(-0.557f, -0.663f, -0.5f),
                Hips = V(-0.52f, 0f, -0.08f),
            },
        };

        /// <summary>Poses the limbs and returns where the hips go: metres from the stand point, in the Root frame.</summary>
        public static Vector3 Apply(GoalieCharacter character, GoaliePose pose)
        {
            AttackSide side = GoaliePoses.SideOf(pose);
            return Apply(character, Shapes[(int)pose], side == AttackSide.Right);
        }

        static Vector3 Apply(GoalieCharacter character, Shape s, bool mirror)
        {
            var g = new Frame(character, mirror);
            g.Torso(g.Dir(s.Spine), g.Dir(s.Neck));
            g.Leg(true, g.Dir(s.RightThigh), g.Dir(s.RightShin));
            g.Leg(false, g.Dir(s.LeftThigh), g.Dir(s.LeftShin));
            g.Arm(true, g.Dir(s.RightUpperArm), g.Dir(s.RightForearm), g.Dir(s.RightFingers));
            g.Arm(false, g.Dir(s.LeftUpperArm), g.Dir(s.LeftForearm), g.Dir(s.LeftFingers));
            return character.Root.transform.InverseTransformDirection(g.Dir(s.Hips));
        }

        /// <summary>The shooter's frame on the character, mirrored left-right for the Right poses.</summary>
        readonly struct Frame
        {
            readonly GoalieCharacter _c;
            readonly bool _mirror;
            public readonly Vector3 Up, Forward, Right;

            public Frame(GoalieCharacter character, bool mirror)
            {
                _c = character;
                _mirror = mirror;
                Quaternion q = character.Root.transform.rotation;
                Up = q * Vector3.up;
                Forward = q * Vector3.back;            // toward the shooter
                Right = q * Vector3.right * (mirror ? -1f : 1f); // shooter's right (the goalie's left), mirrored
            }

            /// <summary>A (right, up, toward the shooter) direction in world space.</summary>
            public Vector3 Dir(Vector3 v) => Right * v.x + Up * v.y + Forward * v.z;

            /// <summary>The goalie's left side is on the shooter's right, unless mirrored.</summary>
            bool GoalieLeft(bool shooterRight) => shooterRight != _mirror;

            public void Torso(Vector3 spine, Vector3 neck)
            {
                _c.Aim(HumanBodyBones.Spine, HumanBodyBones.Neck, spine);
                _c.Aim(HumanBodyBones.Neck, HumanBodyBones.Head, neck);
            }

            /// <summary>Aims the leg on the shooter's right (or left): thigh hip-to-knee, shin knee-to-ankle.</summary>
            public void Leg(bool shooterRight, Vector3 thigh, Vector3 shin) => Leg(shooterRight, thigh, shin, Forward);

            /// <summary>As Leg, and turns the pad about the shin so its front faces padFront.</summary>
            public void Leg(bool shooterRight, Vector3 thigh, Vector3 shin, Vector3 padFront)
            {
                bool l = GoalieLeft(shooterRight);
                _c.Aim(l ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg, l ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, thigh);
                _c.Aim(l ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, l ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, shin);
                _c.FacePad(l, padFront);
            }

            /// <summary>Aims the arm on the shooter's right (or left) and turns its hand so the gear faces the shooter.</summary>
            public void Arm(bool shooterRight, Vector3 upper, Vector3 fore, Vector3 fingers)
            {
                bool l = GoalieLeft(shooterRight);
                _c.Aim(l ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, l ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, upper);
                _c.Aim(l ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, l ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, fore);
                // Blocker back-of-hand and catcher palm both face the shooter with the thumb along fingers x forward.
                _c.Orient(l ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, fingers, Vector3.Cross(fingers, Forward));
            }
        }
    }
}
