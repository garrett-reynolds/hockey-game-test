using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The skate push (#126) laid on top of the goalie character's pose: the skate on the far side of the move plants out and
    /// back along the ice (a push off the edge), and on a T-push the lead skate turns toward the move first. Leg and foot bones
    /// only: the Root, the model position, the hips and every other bone are left alone, and both skates stay on the ice. The
    /// two numbers (how far out, how far turned) come from PushPoseTracker.
    /// </summary>
    public static class GoalieSkatePush
    {
        /// <summary>The pushing skate plants this far out along the rink's X, away from the move, and this far back away from the shooter (metres), at full push; the lead skate turns this far on a T-push (degrees).</summary>
        public const float PushOutMetres = 0.12f, PushBackMetres = 0.05f, LeadTurnDegrees = 35f;

        /// <summary>
        /// Adds the push to the pose shown. s: which skate pushes (Skate.Right pushes for a move toward -X, Skate.Left toward +X);
        /// the foot is picked by its world X (the goalie's LeftFoot bone is the +X foot, but his facing may swap it). m: how far
        /// out and how far turned, 0..1; both 0 changes nothing.
        /// </summary>
        public static void Apply(GoalieCharacter c, PushShown s, PushPoseMoment m)
        {
            float pushOut = Mathf.Clamp01((float)m.PushOut), lead = Mathf.Clamp01((float)m.LeadTurn);
            if (pushOut <= 0f && lead <= 0f) return;
            Transform lf = c.Bone(HumanBodyBones.LeftFoot), rf = c.Bone(HumanBodyBones.RightFoot);
            if (lf == null || rf == null) return;
            bool leftIsPlusX = lf.position.x >= rf.position.x;
            // Skate.Right is the foot with the larger world X.
            bool pushIsLeftBone = (s.Skate == Skate.Right) == leftIsPlusX;
            Transform push = pushIsLeftBone ? lf : rf, other = pushIsLeftBone ? rf : lf;
            float away = s.Skate == Skate.Right ? 1f : -1f; // out along +X for a move toward -X

            // The lead skate turns first, about the vertical, toe toward the move (-X turns the toe from -Z toward -X with +degrees).
            if (lead > 0f) other.rotation = Quaternion.AngleAxis(away * LeadTurnDegrees * lead, Vector3.up) * other.rotation;

            if (pushOut <= 0f) return;
            Transform rink = c.Root.transform.parent;
            Vector3 across = rink != null ? rink.right : Vector3.right, back = rink != null ? rink.forward : Vector3.forward; // the shooter is at -Z
            Vector3 shift = across * (away * PushOutMetres * pushOut) + back * (PushBackMetres * pushOut);
            HumanBodyBones upper = pushIsLeftBone ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
            HumanBodyBones lower = pushIsLeftBone ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
            Transform knee = c.Bone(lower);
            if (knee == null) return;
            // On the ice: the target keeps the foot's own height.
            GoalieStanceLife.Plant(c, upper, lower, push, push.position + shift, push.rotation, knee.position + shift * 0.5f);
        }
    }
}
