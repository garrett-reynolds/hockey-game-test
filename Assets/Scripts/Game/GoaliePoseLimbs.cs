using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The character pose for each Goalie Pose, laid on top of the ready stance. Each limb is aimed in the shooter's
    /// frame: up, toward the shooter, and toward the shooter's right. Left and Middle poses are written out; each Right
    /// pose is its Left pose mirrored, so the limb on the post side swaps (blocker at the left post, catcher at the right).
    /// Hands keep their gear to the shooter: the blocker board and the catcher pocket face the shooter.
    /// The shapes are tuned so the posed goalie, seen from the shooter, matches the Goalie Pose table (see GoalieCoverageTests).
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

        /// <summary>Shapes for LeftPostHug, LeftButterfly, MiddleButterfly and MiddleStandUp; the Right poses mirror the Left ones.</summary>
        internal static readonly Shape[] Shapes =
        {
            new Shape // LeftPostHug: standing on the post leg, the far pad down across the ice, blocker high on the post
            {
                Spine = V(-0.15f, 0.9f, 0.4f), Neck = V(0f, 1f, 0f),
                LeftThigh = V(-0.25f, -0.7f, 0.5f), LeftShin = V(-0.1f, -0.95f, -0.1f),
                RightThigh = V(0.4f, -0.75f, 0.4f), RightShin = V(0.95f, -0.05f, -0.25f),
                LeftUpperArm = V(-0.8f, 0.2f, 0.3f), LeftForearm = V(-0.1f, 0.9f, 0.3f), LeftFingers = V(-0.1f, 0.9f, 0.3f),
                RightUpperArm = V(0.6f, -0.5f, 0.4f), RightForearm = V(0.5f, 0.1f, 0.6f), RightFingers = V(0f, 1f, 0f),
                Hips = V(-0.4f, 0f, 0f),
            },
            new Shape // LeftButterfly: down at the left post, the far pad stretched to the far post, blocker high
            {
                Spine = V(-0.35f, 0.9f, 0.3f), Neck = V(-0.2f, 1f, 0f),
                LeftThigh = V(-0.1f, -0.85f, 0.4f), LeftShin = V(-0.95f, -0.08f, -0.3f),
                RightThigh = V(0.75f, -0.6f, 0.3f), RightShin = V(1f, -0.05f, -0.15f),
                LeftUpperArm = V(-0.85f, 0.1f, 0.3f), LeftForearm = V(-0.1f, 0.95f, 0.2f), LeftFingers = V(-0.1f, 0.95f, 0.2f),
                RightUpperArm = V(0.5f, -0.7f, 0.4f), RightForearm = V(0.6f, -0.1f, 0.6f), RightFingers = V(0.3f, 0.8f, 0f),
                Hips = V(-0.3f, 0f, 0f),
            },
            new Shape // MiddleButterfly: knees down, pads flared flat across the ice, upright chest, hands out at the sides
            {
                Spine = V(0f, 1f, 0.15f), Neck = V(0f, 1f, 0f),
                LeftThigh = V(-0.08f, -0.85f, 0.45f), LeftShin = V(-1f, -0.1f, -0.25f),
                RightThigh = V(0.08f, -0.85f, 0.45f), RightShin = V(1f, -0.1f, -0.25f),
                LeftUpperArm = V(-0.1f, -0.8f, 0.55f), LeftForearm = V(-0.35f, -0.1f, 0.5f), LeftFingers = V(-0.2f, -0.8f, 0.3f),
                RightUpperArm = V(0.1f, -0.8f, 0.55f), RightForearm = V(0.35f, 0.2f, 0.5f), RightFingers = V(0.1f, 0.9f, 0.1f),
                Hips = V(0f, 0f, -0.1f),
            },
            new Shape // MiddleStandUp: tall wide stance, knees out, catcher and blocker out wide, five-hole open
            {
                Spine = V(0f, 0.65f, 0.7f), Neck = V(0f, 1f, 0.15f),
                LeftThigh = V(-1.05f, -0.2f, 0.5f), LeftShin = V(-0.15f, -0.95f, 0.05f),
                RightThigh = V(0.85f, -0.4f, 0.2f), RightShin = V(-0.05f, -1f, 0f),
                LeftUpperArm = V(-0.95f, 0.05f, 0.25f), LeftForearm = V(-0.5f, -0.1f, 0.5f), LeftFingers = V(-0.5f, -0.7f, 0.2f),
                RightUpperArm = V(0.95f, -0.25f, 0.2f), RightForearm = V(0.5f, 0.3f, 0.55f), RightFingers = V(0.45f, 0.9f, 0f),
                Hips = V(0f, 0f, -0.05f),
            },
        };

        /// <summary>Poses the limbs and returns where the hips go: metres from the stand point, in the Root frame.</summary>
        public static Vector3 Apply(GoalieCharacter character, GoaliePose pose)
        {
            AttackSide side = GoaliePoses.SideOf(pose);
            int shape = side == AttackSide.Middle ? (int)pose : (int)pose % GoaliePoses.PosesPerSide;
            return Apply(character, Shapes[shape], side == AttackSide.Right);
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
