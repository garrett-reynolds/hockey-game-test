using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Keeps a fast puck from being pushed by something it never touched (#114). The puck's ContinuousSpeculative collision
    /// (#103) makes contacts a step ahead: a puck flying a few centimetres past a post's or a box's edge gets a contact
    /// pointing at it, and the solver pushes it off as if it would hit ("ghost push"). Before each physics step this sweeps
    /// the puck's own hit shapes along the step's travel (on the main thread) and keeps the colliders it really reaches; while
    /// the step runs, Unity's contact modification ignores the puck's foreseen contacts with anything else, so they give no push.
    /// Contacts already touching (within Slop) are always kept. The goalie's gear moves (KeepContactsWith), so the sweep also
    /// meets it as it will be: the puck swept relative to the way it moves this step (GoalieHitShapes tells GearMoves before
    /// the step, and the sweep is done again then). A foreseen gear contact the puck does not reach is ignored too, unless the puck really meets
    /// something this step (a puck turned off a post or one piece of gear may meet another piece the sweep did not foresee). Works for any collider the puck meets: the
    /// posts, crossbar, catch, boards and glass, and the goalie's gear (which it always keeps: KeepContactsWith). Dispose when done: it stops listening.
    /// </summary>
    public sealed class PuckReach : IDisposable
    {
        /// <summary>A foreseen contact this close (metres) or closer is kept whatever the sweep says: the puck is as good as touching.</summary>
        public const float Slop = 0.002f;
        /// <summary>The sweep reaches this much further (metres) than the step's travel: for the puck's spin and tip during the step.</summary>
        public const float Margin = 0.01f;
        /// <summary>The sweep reaches this much further (metres) for the gear: it does not move in a straight line during the step.</summary>
        public const float GearMargin = 0.015f;
        /// <summary>How far apart (metres) the puck's places are tried against a moving piece of gear.</summary>
        const float SamplePitch = 0.02f;
        /// <summary>Something moving slower than this (m/s, rad/s) counts as still: the sweep saw it where it will be.</summary>
        const float Still = 1e-3f;

        readonly Rigidbody _puck;
        readonly HashSet<EntityId> _ownShapes = new HashSet<EntityId>();
        // Written on the main thread between physics steps (LookAhead); only read by OnModify, which runs during the step on any thread.
        readonly HashSet<EntityId> _reached = new HashSet<EntityId>();
        readonly HashSet<EntityId> _reachedGear = new HashSet<EntityId>(); // the gear the puck reaches, swept relative to its move
        static readonly List<PuckReach> Live = new List<PuckReach>();
        Vector3 _travel; // the step's travel, as LookAhead worked it out
        bool _looking;
        // Goalie gear hit shapes (#111): written on the main thread between steps (GoalieHitShapes), only read by OnModify.
        static readonly HashSet<EntityId> Gear = new HashSet<EntityId>();
        // The same, for the sweep (main thread only): each piece with how far it moves in a step (its middle, and a point of it by turning).
        static readonly Dictionary<Collider, (Vector3 shift, float swing)> GearShapes = new Dictionary<Collider, (Vector3, float)>();

        /// <summary>
        /// Treats this collider as goalie gear (a hit shape, #111): its contacts are kept whenever the puck really meets gear in the
        /// step. A puck deflected off one piece in a step is turned onto another in the same step, which the sweep did not
        /// foresee: a piece then let it through (2 of 102 Shots).
        /// </summary>
        public static void KeepContactsWith(Collider gear)
        {
            Gear.Add(gear.GetEntityId());
            if (!GearShapes.ContainsKey(gear)) GearShapes.Add(gear, (Vector3.zero, 0f));
        }

        /// <summary>
        /// Tells the look-ahead how far a piece of gear moves in the step about to run: shift (its middle, metres) and swing (how
        /// far a point of it moves because it turns). Until the next call the look-ahead takes the next step to move as far.
        /// </summary>
        public static void GearMoves(Collider gear, Vector3 shift, float swing)
        {
            if (GearShapes.ContainsKey(gear)) GearShapes[gear] = (shift, swing);
        }

        /// <summary>Call once every piece of gear has told GearMoves for the step about to run: each look-ahead sweeps the gear again with its true move.</summary>
        public static void GearMoved()
        {
            // Domain reload is off: a Shot never disposed leaves a PuckReach whose puck is gone. Drop it, never throw on it.
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                if (Live[i]._puck == null) Live[i].Dispose();
                else Live[i].SweepGear();
            }
        }

        /// <summary>Undoes KeepContactsWith.</summary>
        public static void ForgetContactsWith(Collider gear)
        {
            Gear.Remove(gear.GetEntityId());
            GearShapes.Remove(gear);
            foreach (Collider dead in new List<Collider>(GearShapes.Keys)) if (dead == null) GearShapes.Remove(dead);
        }

        /// <summary>Filters the contacts of puck's hit shapes (each must have hasModifiableContacts on, as PhysicsPuck.Make does).</summary>
        public PuckReach(Rigidbody puck)
        {
            _puck = puck;
            foreach (Collider shape in puck.GetComponentsInChildren<Collider>(true)) _ownShapes.Add(shape.GetEntityId());
            Physics.ContactModifyEvent += OnModify;
            Live.Add(this);
        }

        /// <summary>
        /// Call before each physics step of stepSeconds (after the launch, and after each step for the next): sweeps the puck
        /// from where it is along this step's travel (its velocity with this step's gravity) and keeps what it reaches.
        /// </summary>
        public void LookAhead(float stepSeconds)
        {
            _reached.Clear();
            _reachedGear.Clear();
            _looking = _puck.gameObject.activeInHierarchy;
            if (!_looking) return;
            Vector3 travel = (_puck.linearVelocity + Physics.gravity * stepSeconds) * stepSeconds;
            _travel = travel;
            if (travel.sqrMagnitude < 1e-12f) return;
            foreach (RaycastHit hit in _puck.SweepTestAll(travel.normalized, travel.magnitude + Margin, QueryTriggerInteraction.Ignore))
                _reached.Add(hit.collider.GetEntityId());
            SweepGear();
        }

        /// <summary>
        /// The gear moves during the step, so the first sweep (which saw it where it stood) is not enough. In the gear's own
        /// frame the puck moves by the step's travel less the gear's move (GearMoves): keeps every piece that line passes
        /// within the puck's radius of, GearMargin, and how far a point of the piece moves by turning. Taken at points
        /// SamplePitch apart (half of it more), as the sweep tests of the physics cannot be widened for a turning piece.
        /// </summary>
        void SweepGear()
        {
            _reachedGear.Clear();
            if (!_looking) return;
            Vector3 start = _puck.position;
            foreach (var piece in GearShapes)
            {
                Collider gear = piece.Key;
                if (gear == null || !gear.gameObject.activeInHierarchy) continue;
                Vector3 relative = _travel - piece.Value.shift;
                float near = PhysicsPuck.Radius + Margin + GearMargin + piece.Value.swing + SamplePitch / 2f;
                Bounds box = gear.bounds;
                box.Expand(2f * near);
                Bounds path = new Bounds(start, Vector3.zero);
                path.Encapsulate(start + relative);
                if (!path.Intersects(box)) continue; // far off
                int samples = Mathf.CeilToInt(relative.magnitude / SamplePitch);
                for (int i = 0; i <= samples; i++)
                {
                    Vector3 point = start + relative * (samples == 0 ? 0f : i / (float)samples);
                    if ((gear.ClosestPoint(point) - point).sqrMagnitude <= near * near) { _reachedGear.Add(gear.GetEntityId()); break; }
                }
            }
        }

        // Runs during the physics step, on any thread: no Unity objects here, only the plain data kept by LookAhead.
        void OnModify(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
        {
            if (!_looking) return;
            // Does the puck really meet something this step (so a piece of gear may be met after a turn)?
            bool meets = false;
            for (int k = 0; k < pairs.Length && !meets; k++)
            {
                ModifiableContactPair pair = pairs[k];
                bool puckFirst = _ownShapes.Contains(pair.colliderEntityId);
                if (!puckFirst && !_ownShapes.Contains(pair.otherColliderEntityId)) continue;
                EntityId other = puckFirst ? pair.otherColliderEntityId : pair.colliderEntityId;
                if (_reached.Contains(other) || _reachedGear.Contains(other)) meets = true;
                for (int i = 0; i < pair.contactCount && !meets; i++) meets = pair.GetSeparation(i) <= Slop;
            }
            for (int k = 0; k < pairs.Length; k++)
            {
                ModifiableContactPair pair = pairs[k];
                bool puckFirst = _ownShapes.Contains(pair.colliderEntityId);
                if (!puckFirst && !_ownShapes.Contains(pair.otherColliderEntityId)) continue;
                EntityId other = puckFirst ? pair.otherColliderEntityId : pair.colliderEntityId;
                Vector3 otherVelocity = puckFirst ? pair.otherBodyVelocity : pair.bodyVelocity;
                Vector3 otherSpin = puckFirst ? pair.otherBodyAngularVelocity : pair.bodyAngularVelocity;
                bool gear = Gear.Contains(other);
                if (_reached.Contains(other) || _reachedGear.Contains(other) || gear && meets) continue;
                if (!gear && (otherVelocity.sqrMagnitude > Still * Still || otherSpin.sqrMagnitude > Still * Still)) continue;
                for (int i = 0; i < pair.contactCount; i++)
                    if (pair.GetSeparation(i) > Slop) pair.IgnoreContact(i);
            }
        }

        public void Dispose()
        {
            Physics.ContactModifyEvent -= OnModify;
            Live.Remove(this);
        }
    }
}
