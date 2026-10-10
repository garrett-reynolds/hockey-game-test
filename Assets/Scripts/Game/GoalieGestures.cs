using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// What the goalie character's head and body do once the puck has got there (#43, AfterShot), laid on top of the pose
    /// shown: the head turning to watch the puck (on a GOAL! the shoulders too) and the gestures, an Annoyed Gesture
    /// (Slump or StickSlap) or Relief. Each is a few bends and turns of the spine, neck and stick arm, in the goalie's frame.
    /// </summary>
    internal static class GoalieGestures
    {
        /// <summary>The most the head turns to watch up and down (degrees).</summary>
        const float WatchUp = 30f, WatchDown = 50f;
        /// <summary>A point further round than this (degrees from square) is behind: looked at over the shoulder on its side.</summary>
        const float BehindDegrees = 120f;

        /// <summary>Slump: the spine bends forward this far over progress 0..SlumpSpineTo (degrees).</summary>
        const float SlumpSpineDegrees = 18f, SlumpSpineTo = 0.35f;
        /// <summary>Slump: the head drops this far over SlumpNeckFrom..SlumpNeckTo (degrees).</summary>
        const float SlumpNeckDegrees = 28f, SlumpNeckFrom = 0.1f, SlumpNeckTo = 0.45f;
        /// <summary>Slump: the head shakes (ShakeCount shakes of up to ShakeDegrees, dying away) over ShakeFrom..ShakeFrom + ShakeLength.</summary>
        const float ShakeDegrees = 14f, ShakeFrom = 0.4f, ShakeLength = 0.45f, ShakeCount = 2f;

        /// <summary>Stick slap: the stick arm lifts up to LiftedBy, holds to SlamFrom, is slammed down by SlamTo, bounces off the ice (BounceShare of the lift) to BounceTo.</summary>
        const float LiftedBy = 0.4f, SlamFrom = 0.48f, SlamTo = 0.56f, BounceTo = 0.66f, BounceShare = 0.12f;
        /// <summary>Stick slap: where the lifted stick arm points, in the goalie's frame (forward, up, toward his right).</summary>
        const float RaisedForward = 0.75f, RaisedUp = 0.55f, RaisedRight = 0.15f;
        /// <summary>Stick slap: then the spine and head drop this far (degrees) over DropFrom..DropTo.</summary>
        const float SlapSpineDegrees = 8f, SlapNeckDegrees = 22f, DropFrom = 0.6f, DropTo = 0.9f;

        /// <summary>Relief: a breath in (head up ReliefDegrees) by BreathInTo, then out to ExhaleShare of it below where it was, by BreathOutTo.</summary>
        const float ReliefDegrees = 14f, BreathInTo = 0.35f, BreathOutTo = 0.75f, ExhaleShare = 1.6f;

        /// <summary>
        /// Turns the head toward a point, `weight` of the way: at most `most` degrees from square about the vertical (a point
        /// behind is looked at over the shoulder on its side) and WatchUp/WatchDown up and down. spine: the share of the turn the
        /// shoulders take. faceLocal: the way the face looks in the Ready Stance (head-local). The neck turns the head and mask
        /// together; the head never bends on the neck.
        /// </summary>
        public static void Watch(GoalieCharacter c, Vector3 faceLocal, Vector3 point, float weight, float most, float spine)
        {
            Transform neck = c.Bone(HumanBodyBones.Neck), head = c.Bone(HumanBodyBones.Head), back = c.Bone(HumanBodyBones.Spine);
            if (neck == null || head == null) return;
            Vector3 forward = c.Root.transform.rotation * Vector3.back, left = c.Root.transform.rotation * Vector3.right;
            Vector3 to = point - head.position, flat = Vector3.ProjectOnPlane(to, Vector3.up);
            if (to.sqrMagnitude < 1e-6f) return;
            float yaw = flat.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(forward, flat, Vector3.up) : 0f;
            if (Mathf.Abs(yaw) > BehindDegrees)
            {
                // Behind: over the shoulder on the point's side of the hips (steady while the point stays on that side).
                float onLeft = Vector3.Dot(point - c.Bone(HumanBodyBones.Hips).position, left) >= 0f ? 1f : -1f;
                yaw = Mathf.Abs(yaw) * onLeft * Mathf.Sign(Vector3.SignedAngle(forward, left, Vector3.up));
            }
            yaw = Mathf.Clamp(yaw, -most, most);
            float pitch = Mathf.Clamp(Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -WatchDown, WatchUp);
            if (back != null && spine > 0f) back.rotation = Quaternion.AngleAxis(yaw * spine * weight, Vector3.up) * back.rotation;
            Vector3 square = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
            Vector3 want = Vector3.RotateTowards(square, pitch >= 0f ? Vector3.up : Vector3.down, Mathf.Abs(pitch) * Mathf.Deg2Rad, 0f);
            Vector3 face = head.TransformDirection(faceLocal);
            neck.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(face, want), weight) * neck.rotation;
        }

        /// <summary>The gesture at progress p (0..1; 1 is its end pose): spine and neck bends, and for the slap the stick arm.</summary>
        public static void Gesture(GoalieCharacter c, AfterShotGesture gesture, float p)
        {
            Vector3 forward = c.Root.transform.rotation * Vector3.back, left = c.Root.transform.rotation * Vector3.right;
            switch (gesture)
            {
                case AfterShotGesture.Slump:
                    {
                        Bend(c, HumanBodyBones.Spine, forward, SlumpSpineDegrees * Ease(p, 0f, SlumpSpineTo));
                        Bend(c, HumanBodyBones.Neck, forward, SlumpNeckDegrees * Ease(p, SlumpNeckFrom, SlumpNeckTo));
                        float u = Mathf.Clamp01((p - ShakeFrom) / ShakeLength); // the shakes, dying away
                        Turn(c, HumanBodyBones.Neck, ShakeDegrees * Mathf.Sin(u * 2f * ShakeCount * Mathf.PI) * Mathf.Sin(u * Mathf.PI));
                    }
                    break;
                case AfterShotGesture.StickSlap:
                    {
                        // Up, held, slammed down (speeding up), a small bounce off the ice, then the head drops.
                        float lift = p < LiftedBy ? Ease(p, 0f, LiftedBy) : p < SlamFrom ? 1f : p < SlamTo ? 1f - Square((p - SlamFrom) / (SlamTo - SlamFrom))
                            : p < BounceTo ? BounceShare * Mathf.Sin((p - SlamTo) / (BounceTo - SlamTo) * Mathf.PI) : 0f;
                        Transform shoulder = c.Bone(HumanBodyBones.RightUpperArm), hand = c.Bone(HumanBodyBones.RightHand);
                        if (shoulder != null && hand != null && lift > 0f)
                        {
                            Vector3 arm = hand.position - shoulder.position;
                            Vector3 raised = (forward * RaisedForward + Vector3.up * RaisedUp - left * RaisedRight).normalized * arm.magnitude;
                            shoulder.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(arm, raised), lift) * shoulder.rotation;
                        }
                        Bend(c, HumanBodyBones.Spine, forward, SlapSpineDegrees * Ease(p, DropFrom, DropTo));
                        Bend(c, HumanBodyBones.Neck, forward, SlapNeckDegrees * Ease(p, DropFrom, DropTo));
                    }
                    break;
                case AfterShotGesture.Relief:
                    {
                        // A breath in (head up a little), then out (down past where it was), settling a little low.
                        float breath = p < BreathInTo ? Ease(p, 0f, BreathInTo) : 1f - ExhaleShare * Ease(p, BreathInTo, BreathOutTo);
                        Bend(c, HumanBodyBones.Neck, -forward, ReliefDegrees * breath);
                    }
                    break;
            }
        }

        /// <summary>Bends a bone (and all it carries) so its up turns `degrees` toward a direction (negative: away).</summary>
        public static void Bend(GoalieCharacter c, HumanBodyBones bone, Vector3 toward, float degrees)
        {
            Transform t = c.Bone(bone);
            if (t == null || Mathf.Abs(degrees) < 1e-4f) return;
            if (degrees < 0f) { toward = -toward; degrees = -degrees; }
            t.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.RotateTowards(Vector3.up, toward, degrees * Mathf.Deg2Rad, 0f)) * t.rotation;
        }

        /// <summary>Turns a bone (and all it carries) about the vertical.</summary>
        static void Turn(GoalieCharacter c, HumanBodyBones bone, float degrees)
        {
            Transform t = c.Bone(bone);
            if (t != null) t.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * t.rotation;
        }

        static float Ease(float p, float from, float to) => (float)AfterShot.Ramp(p, from, to);

        static float Square(float u) => u * u;
    }
}
