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
    /// Contacts already touching (within Slop) are always kept, and so are contacts with something moving (the sweep only saw
    /// where it was before the step). Works for any collider the puck meets: the posts, crossbar and catch now, the boards
    /// (#110) and the goalie's gear (#111) later. Dispose when done: it stops listening.
    /// </summary>
    public sealed class PuckReach : IDisposable
    {
        /// <summary>A foreseen contact this close (metres) or closer is kept whatever the sweep says: the puck is as good as touching.</summary>
        public const float Slop = 0.002f;
        /// <summary>The sweep reaches this much further (metres) than the step's travel: for the puck's spin and tip during the step.</summary>
        public const float Margin = 0.01f;
        /// <summary>Something moving slower than this (m/s, rad/s) counts as still: the sweep saw it where it will be.</summary>
        const float Still = 1e-3f;

        readonly Rigidbody _puck;
        readonly HashSet<EntityId> _ownShapes = new HashSet<EntityId>();
        // Written on the main thread between physics steps (LookAhead); only read by OnModify, which runs during the step on any thread.
        readonly HashSet<EntityId> _reached = new HashSet<EntityId>();
        bool _looking;

        /// <summary>Filters the contacts of puck's hit shapes (each must have hasModifiableContacts on, as PhysicsPuck.Make does).</summary>
        public PuckReach(Rigidbody puck)
        {
            _puck = puck;
            foreach (Collider shape in puck.GetComponentsInChildren<Collider>(true)) _ownShapes.Add(shape.GetEntityId());
            Physics.ContactModifyEvent += OnModify;
        }

        /// <summary>
        /// Call before each physics step of stepSeconds (after the launch, and after each step for the next): sweeps the puck
        /// from where it is along this step's travel (its velocity with this step's gravity) and keeps what it reaches.
        /// </summary>
        public void LookAhead(float stepSeconds)
        {
            _reached.Clear();
            _looking = _puck.gameObject.activeInHierarchy;
            if (!_looking) return;
            Vector3 travel = (_puck.linearVelocity + Physics.gravity * stepSeconds) * stepSeconds;
            if (travel.sqrMagnitude < 1e-12f) return;
            foreach (RaycastHit hit in _puck.SweepTestAll(travel.normalized, travel.magnitude + Margin, QueryTriggerInteraction.Ignore))
                _reached.Add(hit.collider.GetEntityId());
        }

        // Runs during the physics step, on any thread: no Unity objects here, only the plain data kept by LookAhead.
        void OnModify(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
        {
            if (!_looking) return;
            for (int k = 0; k < pairs.Length; k++)
            {
                ModifiableContactPair pair = pairs[k];
                bool puckFirst = _ownShapes.Contains(pair.colliderEntityId);
                if (!puckFirst && !_ownShapes.Contains(pair.otherColliderEntityId)) continue;
                EntityId other = puckFirst ? pair.otherColliderEntityId : pair.colliderEntityId;
                Vector3 otherVelocity = puckFirst ? pair.otherBodyVelocity : pair.bodyVelocity;
                Vector3 otherSpin = puckFirst ? pair.otherBodyAngularVelocity : pair.bodyAngularVelocity;
                if (_reached.Contains(other) || otherVelocity.sqrMagnitude > Still * Still || otherSpin.sqrMagnitude > Still * Still) continue;
                for (int i = 0; i < pair.contactCount; i++)
                    if (pair.GetSeparation(i) > Slop) pair.IgnoreContact(i);
            }
        }

        public void Dispose() => Physics.ContactModifyEvent -= OnModify;
    }
}
