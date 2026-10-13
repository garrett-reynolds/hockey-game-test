using System;
using System.Collections.Generic;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The goalie's hit shapes for the physics puck (#111, spec #107): a box for each piece of gear, two turned boxes for the
    /// stick, and capsules on the body's bones. Each is on its own unparented kinematic Rigidbody (ContinuousSpeculative), not
    /// on the bone, so physics sees the gear move. The owner of the physics step poses the goalie for the end of the step, calls
    /// Move, and then simulates: the hit shapes then reach the gear's pose with a true speed over the step. The look never
    /// depends on them. Dispose when done.
    /// </summary>
    public sealed class GoalieHitShapes : IDisposable
    {
        public const string RootName = "Goalie Hit Shapes";

        /// <summary>No gear box is thinner than this (metres): a thin one would let a fast puck skip through in a step.</summary>
        public const float MinDepth = 0.1f;

        // Capsules on the bones: (from, to, radius).
        static readonly (HumanBodyBones from, HumanBodyBones to, float radius)[] Bones =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Chest, 0.18f), (HumanBodyBones.Chest, HumanBodyBones.Neck, 0.2f),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.08f), (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.07f),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.08f), (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.07f),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.1f), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.1f),
        };

        /// <summary>The stick's blade is the part further than this (metres) from the shaft's line.</summary>
        const float BladeFrom = 0.07f;
        const float MinStickDepth = 0.04f;

        /// <summary>One hit shape's body, and where it sits in its gear's rigid frame (position and turn, no scale).</summary>
        sealed class Follower
        {
            public Transform Follow;
            public Rigidbody Body;
            public Collider Collider;
            public Vector3 Offset;
            public Quaternion Turn;
            public Vector3 Position => Follow.position + Follow.rotation * Offset;
            public Quaternion Rotation => Follow.rotation * Turn;
        }

        readonly List<Follower> _followers = new List<Follower>();
        readonly List<Collider> _colliders = new List<Collider>();
        readonly GameObject _root;

        /// <summary>The hit shapes' colliders, for a test to tell them apart from the rest.</summary>
        public IReadOnlyList<Collider> Colliders => _colliders;

        /// <summary>
        /// Builds the hit shapes for every piece of gear (not the Visor) and the body under parent, as the goalie is now, then
        /// puts each on its gear. minDepth: the thinnest a gear box may be; only a test's control run changes it.
        /// </summary>
        public GoalieHitShapes(GoalieCharacter goalie, Transform parent, float minDepth = MinDepth)
        {
            _root = new GameObject(RootName);
            _root.transform.SetParent(parent, false);
            foreach (MeshRenderer renderer in goalie.Root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string piece = GearPiece.PieceOf(renderer.name);
                if (piece == null || piece == GearPiece.Visor) continue;
                // The mesh in the piece's own frame: its position and turn, no scale.
                Matrix4x4 toOwn = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one).inverse * renderer.transform.localToWorldMatrix;
                Vector3[] points = renderer.GetComponent<MeshFilter>().sharedMesh.vertices;
                for (int i = 0; i < points.Length; i++) points[i] = toOwn.MultiplyPoint3x4(points[i]);
                PhysicsMaterial material = ShotColliders.GearOf(piece);
                if (piece == GearPiece.Stick) AddStick(renderer.transform, points, material);
                else
                {
                    Bounds bounds = BoundsOf(points);
                    Box(piece, renderer.transform, bounds.center, Quaternion.identity, Vector3.Max(bounds.size, Vector3.one * minDepth), material);
                }
            }
            foreach (var (from, to, radius) in Bones)
            {
                Transform a = goalie.Bone(from), b = goalie.Bone(to);
                if (a == null || b == null) continue;
                Vector3 along = Quaternion.Inverse(a.rotation) * (b.position - a.position); // the bone's own frame
                // The body turned so its up runs from a to b carries the capsule.
                GameObject body = NewBody(GearPiece.Body, a, along / 2f, Quaternion.FromToRotation(Vector3.up, along));
                var capsule = body.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = radius;
                capsule.height = along.magnitude + 2f * radius;
                Add(capsule, ShotColliders.GearBody);
            }
        }

        /// <summary>Hit shapes are only there while the goalie is shown.</summary>
        public void SetActive(bool active)
        {
            if (_root.activeSelf == active) return;
            _root.SetActive(active);
            if (active) Snap();
        }

        /// <summary>
        /// Call once the goalie is posed for the end of the physics step, before it is simulated: moves each hit shape to its gear,
        /// and tells PuckReach how far each moves this step (a kinematic body's velocity is not there for its look-ahead).
        /// </summary>
        public void Move()
        {
            if (!_root.activeInHierarchy) return;
            foreach (Follower f in _followers)
            {
                if (f.Collider == null) f.Collider = f.Body.GetComponent<Collider>();
                Vector3 target = f.Position;
                Quaternion turn = f.Rotation;
                // How far a point of the shape may travel because of the turn: the angle times the shape's reach from its middle.
                float swing = Quaternion.Angle(f.Body.rotation, turn) * Mathf.Deg2Rad * f.Collider.bounds.extents.magnitude;
                PuckReach.GearMoves(f.Collider, target - f.Body.position, swing);
                f.Body.MovePosition(target);
                f.Body.MoveRotation(turn);
            }
            PuckReach.GearMoved();
        }

        /// <summary>Puts each hit shape on its gear at once, with no speed (a test's start, or the goalie shown again).</summary>
        public void Snap()
        {
            foreach (Follower f in _followers) f.Body.transform.SetPositionAndRotation(f.Position, f.Rotation);
            Physics.SyncTransforms();
        }

        /// <summary>The name of the body (and object) that carries a gear piece's hit shape.</summary>
        public static string BodyName(string piece) => "Hit " + piece;

        public void Dispose()
        {
            foreach (Collider c in _colliders) if (c != null) PuckReach.ForgetContactsWith(c);
            SceneObjects.DestroySafely(_root);
        }

        /// <summary>A kinematic body whose own origin is the middle of its one hit shape, which follows `follow` at that offset and turn.</summary>
        GameObject NewBody(string piece, Transform follow, Vector3 offset, Quaternion turn)
        {
            var go = new GameObject(BodyName(piece));
            go.transform.SetParent(_root.transform, false);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.None;
            var f = new Follower { Follow = follow, Body = body, Offset = offset, Turn = turn };
            go.transform.SetPositionAndRotation(f.Position, f.Rotation);
            _followers.Add(f);
            return go;
        }

        void Box(string piece, Transform follow, Vector3 offset, Quaternion turn, Vector3 size, PhysicsMaterial material)
        {
            var box = NewBody(piece, follow, offset, turn).AddComponent<BoxCollider>();
            box.size = size;
            Add(box, material);
        }

        void Add(Collider collider, PhysicsMaterial material)
        {
            collider.sharedMaterial = material;
            collider.providesContacts = true;
            _colliders.Add(collider);
            PuckReach.KeepContactsWith(collider);
        }

        /// <summary>
        /// The stick is an L lying on a slant in its own frame, so straight boxes round it would be far too big. Two turned boxes
        /// instead: the shaft (with the paddle) along the mesh's longest direction, and the blade (the points further than
        /// BladeFrom from the shaft's line).
        /// </summary>
        void AddStick(Transform follow, Vector3[] points, PhysicsMaterial material)
        {
            Vector3 centre = Centre(points);
            Vector3 axis = Spread(points, centre, Vector3.zero);
            var shaft = new List<Vector3>();
            var blade = new List<Vector3>();
            foreach (Vector3 p in points)
            {
                Vector3 d = p - centre;
                ((d - axis * Vector3.Dot(d, axis)).magnitude > BladeFrom ? blade : shaft).Add(p);
            }
            TurnedBox(follow, shaft, axis, material);
            if (blade.Count > 0) TurnedBox(follow, blade, Spread(blade, Centre(blade), Vector3.zero), material);
        }

        /// <summary>A box turned to fit points: first along `along`, then their widest spread square to it.</summary>
        void TurnedBox(Transform follow, List<Vector3> points, Vector3 along, PhysicsMaterial material)
        {
            Vector3 centre = Centre(points);
            Vector3 wide = Spread(points, centre, along);
            Quaternion turn = Quaternion.LookRotation(Vector3.Cross(along, wide).normalized, wide); // local x along, y wide, z thin
            Quaternion back = Quaternion.Inverse(turn);
            Vector3 min = Vector3.one * float.MaxValue, max = -min;
            foreach (Vector3 p in points)
            {
                Vector3 q = back * (p - centre);
                min = Vector3.Min(min, q);
                max = Vector3.Max(max, q);
            }
            Box(GearPiece.Stick, follow, centre + turn * ((min + max) / 2f), turn, Vector3.Max(max - min, Vector3.one * MinStickDepth), material);
        }

        static Vector3 Centre(IList<Vector3> points)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 p in points) sum += p;
            return sum / Mathf.Max(1, points.Count);
        }

        /// <summary>The direction the points spread most (power iteration), square to `without` when that is given (not zero).</summary>
        static Vector3 Spread(IList<Vector3> points, Vector3 centre, Vector3 without)
        {
            Vector3 dir = new Vector3(0.3f, 0.5f, 0.8f).normalized;
            if (without != Vector3.zero) dir = Vector3.Cross(without, dir).normalized;
            for (int i = 0; i < 30; i++)
            {
                Vector3 next = Vector3.zero;
                foreach (Vector3 p in points)
                {
                    Vector3 d = p - centre;
                    if (without != Vector3.zero) d -= without * Vector3.Dot(d, without);
                    next += d * Vector3.Dot(d, dir);
                }
                if (without != Vector3.zero) next -= without * Vector3.Dot(next, without);
                if (next.sqrMagnitude < 1e-12f) break;
                dir = next.normalized;
            }
            return dir;
        }

        static Bounds BoundsOf(IList<Vector3> points)
        {
            if (points.Count == 0) return new Bounds();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 p in points) bounds.Encapsulate(p);
            return bounds;
        }
    }
}
