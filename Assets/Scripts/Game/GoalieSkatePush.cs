using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The skate push (#126, made clear in #130) laid on top of the goalie character's pose: the skate on the far side of the move
    /// drives out wide and back along the ice (a push off the edge), the hips sit lower and lean into the move with both skates kept
    /// on the ice (so the lead knee bends and the push leg extends), and on a T-push the lead skate turns toward the move first. The
    /// Root is left alone. The pose numbers (how far out, how far turned) come from PushPoseTracker; a push that has just changed
    /// skate also carries the old skate's fading pose.
    /// </summary>
    public static class GoalieSkatePush
    {
        /// <summary>
        /// The pushing skate plants this far out along the rink's X, away from the move, and this far back away from the shooter
        /// (metres), at full push; the lead skate turns this far on a T-push (degrees).
        /// Band (#130 fix round 1, GoalieHitShapesTests ..._push_held_on, 45 m/s Still shots): out/back 0.30/0.065 to 0.40/0.086 and
        /// 0.37/0.10 all pass; only 0.40/0.10 fails (one Shot ends sunk in the tilted Right Pad at t 0.067, a tunnelling edge case
        /// that is not fixed here). So 0.37 / 0.07 keeps 3 cm from the failing out and 3 cm from the failing back. Do not raise
        /// either without running that test (and the strip, which must still show the push).
        /// </summary>
        public const float PushOutMetres = 0.37f, PushBackMetres = 0.07f, LeadTurnDegrees = 50f;

        /// <summary>At full push the hips sit this far lower and lean this far toward the move (metres).</summary>
        public const float DropMetres = 0.06f, LeanMetres = 0.10f;

        /// <summary>A Save Reaction's push keeps its #126 size (#130: the reaction is not changed): this far out and back (metres), no body lean.</summary>
        public const float ReactionOutMetres = 0.12f, ReactionBackMetres = 0.05f;

        /// <summary>When a Save Reaction starts (or ends) with a push out, the push blends between its full and its reaction size over this long (seconds).</summary>
        public const float BlendSeconds = 0.15f;

        /// <summary>
        /// Adds the push to the pose shown. s: which skate pushes (Skate.Right pushes for a move toward -X, Skate.Left toward +X);
        /// the foot is picked by its world X (the goalie's LeftFoot bone is the +X foot, but his facing may swap it). m: how far
        /// out and how far turned, 0..1; all 0 changes nothing. model: the character's model, moved with the hips. bodyShare: 1 the
        /// full push (hips lower and lean, full reach); 0 a Save Reaction's push (legs only, the #126 size); between, a blend (GoalieView
        /// blends it over BlendSeconds when a Save Reaction starts mid-push, so nothing jumps).
        /// </summary>
        public static void Apply(GoalieCharacter c, Transform model, PushShown s, PushPoseMoment m, float bodyShare = 1f)
        {
            bodyShare = Mathf.Clamp01(bodyShare);
            bool body = bodyShare > 0f;
            float pushOut = Mathf.Clamp01((float)m.PushOut), lead = Mathf.Clamp01((float)m.LeadTurn);
            float fadeOut = Mathf.Clamp01((float)m.FadeOut), fadeTurn = Mathf.Clamp01((float)m.FadeTurn);
            if (pushOut <= 0f && lead <= 0f && fadeOut <= 0f && fadeTurn <= 0f) return;
            Transform lf = c.Bone(HumanBodyBones.LeftFoot), rf = c.Bone(HumanBodyBones.RightFoot);
            Transform lk = c.Bone(HumanBodyBones.LeftLowerLeg), rk = c.Bone(HumanBodyBones.RightLowerLeg);
            if (lf == null || rf == null || lk == null || rk == null) return;
            bool leftIsPlusX = lf.position.x >= rf.position.x;
            Transform rink = c.Root.transform.parent;
            Vector3 across = rink != null ? rink.right : Vector3.right, back = rink != null ? rink.forward : Vector3.forward; // the shooter is at -Z
            float outMetres = Mathf.Lerp(ReactionOutMetres, PushOutMetres, bodyShare), backMetres = Mathf.Lerp(ReactionBackMetres, PushBackMetres, bodyShare);

            // Per foot (index 0 the left bone, 1 the right bone): how far it goes out along X (signed, metres) and back, and its turn (degrees).
            var outBy = new float[2];
            var backBy = new float[2];
            var turn = new float[2];
            float signedOut = 0f;
            AddPush(s.Skate, pushOut, lead);
            AddPush(m.FadeSkate, fadeOut, fadeTurn);

            void AddPush(Skate skate, float p, float l)
            {
                if (p <= 0f && l <= 0f) return;
                int pusher = (skate == Skate.Right) == leftIsPlusX ? 0 : 1; // Skate.Right is the foot with the larger world X
                float away = skate == Skate.Right ? 1f : -1f; // out along +X for a move toward -X
                outBy[pusher] += away * outMetres * p;
                backBy[pusher] += backMetres * p;
                turn[1 - pusher] += away * LeadTurnDegrees * l; // the lead skate turns about the vertical, toe toward the move
                signedOut += away * p;
            }

            Transform[] feet = { lf, rf };
            Transform[] knees = { lk, rk };
            HumanBodyBones[] uppers = { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg };
            HumanBodyBones[] lowers = { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };

            // The body goes first (the feet are planted again after it): lower, and over toward the move (against the pushing skate's way).
            Vector3 bodyMove = Vector3.zero;
            if (body)
            {
                float down = Mathf.Max(pushOut, fadeOut);
                bodyMove = (across * (-signedOut * LeanMetres) + Vector3.down * (down * DropMetres)) * bodyShare;
            }
            var at = new Vector3[2];
            var rot = new Quaternion[2];
            var kneeAt = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                Vector3 shift = across * outBy[i] + back * backBy[i];
                at[i] = feet[i].position + shift;
                rot[i] = Quaternion.AngleAxis(turn[i], Vector3.up) * feet[i].rotation;
                kneeAt[i] = knees[i].position + shift * 0.5f + bodyMove * 0.5f;
            }
            if (body) model.position += bodyMove;
            for (int i = 0; i < 2; i++)
                GoalieStanceLife.Plant(c, uppers[i], lowers[i], feet[i], at[i], rot[i], kneeAt[i]);
        }
    }
}
