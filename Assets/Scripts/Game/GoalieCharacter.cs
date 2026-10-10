using System;
using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The rigged goalie character: a life-size Humanoid model (Assets/Resources/Models/goalie-dressing.fbx, built by
    /// Tools/make_lifelike_goalie.py) wearing jersey, pants and socks that bend with its skin, and gear (pads, blocker,
    /// catcher, stick, mask, skates) parented to its bones in the file, so the gear follows every joint.
    /// It stands at the net in a neutral Ready Stance and is hidden until shown. It decides nothing: the GoalieView blocks
    /// stay the gameplay look. To pose it, use Animator (Humanoid, no controller, so nothing overwrites the bones) or Bone(...).
    /// With no model file, Present is false and the character draws nothing.
    /// </summary>
    public sealed class GoalieCharacter
    {
        /// <summary>Where the goalie stands: centred on the net, this far in front of the Net Face (metres, -z is toward the shooter).</summary>
        public const float StandZ = -0.4f;

        SkinnedMeshRenderer[] _body = new SkinnedMeshRenderer[0];
        Renderer[] _skates = new Renderer[0];
        Quaternion _restLeftFoot = Quaternion.identity, _restRightFoot = Quaternion.identity; // in the Root frame
        Vector3 _leftFingers = Vector3.forward, _leftThumb = Vector3.up, _rightFingers = Vector3.forward, _rightThumb = Vector3.up; // hand-local
        Transform _model;
        Transform[] _bones = new Transform[0];   // every mapped humanoid bone
        Snapshot _rest, _ready;                  // T-pose as imported; the Ready Stance
        readonly Snapshot[] _poses = new Snapshot[6]; // one per Goalie Pose, fully set
        readonly Shape[] _shapes = new Shape[6];       // one per Goalie Pose, measured on first use (ShapeOf)
        MeshRenderer[] _gear = new MeshRenderer[0];    // every gear piece, in model order

        /// <summary>The object that holds the character; the parent of the model. Inactive while hidden.</summary>
        public GameObject Root { get; }
        /// <summary>The Humanoid Animator, or null when there is no model.</summary>
        public Animator Animator { get; private set; }
        public bool Present => Animator != null;

        public GoalieCharacter(Transform parent, Func<string, GameObject> findFile = null)
        {
            Root = new GameObject("Goalie Character");
            Root.transform.SetParent(parent, false);
            Root.transform.localPosition = new Vector3(0f, 0f, StandZ);
            Build(findFile); // built while active: an inactive Animator has no bones bound
            Root.SetActive(false);
        }

        void Build(Func<string, GameObject> findFile)
        {
            GameObject model = ModelFiles.Load(ModelFiles.GoalieDressing, () => null, findFile);
            if (model == null) return;
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("goalie-dressing is not a Humanoid model; the goalie character is left out.");
                SceneObjects.DestroySafely(model);
                return;
            }
            Animator = animator;
            animator.runtimeAnimatorController = null; // bones are posed from code
            model.transform.SetParent(Root.transform, false);
            _body = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _gear = Array.FindAll(model.GetComponentsInChildren<MeshRenderer>(true), r => GearPiece.PieceOf(r.name) != null);
            _skates = Array.FindAll(model.GetComponentsInChildren<Renderer>(true), r => r.name == GearPiece.ObjectName(GearPiece.LeftSkate) || r.name == GearPiece.ObjectName(GearPiece.RightSkate));

            _model = model.transform;
            FaceTheShooter(model.transform);
            ScaleGear();
            CaptureRest();
            ReadyStance();
            StandOnIce(model.transform);
            _ready = Take();
            foreach (GoaliePose pose in Enum.GetValues(typeof(GoaliePose)))
                _poses[(int)pose] = MakePose(pose);
            Put(_ready);
        }

        /// <summary>
        /// Moves the character from the Ready Stance (set 0) into the Goalie Pose (set 1). The same pose and set always give
        /// the same look, whatever was shown before. facing: a body turn (degrees about the vertical, + toward the shooter's
        /// left; see GoalieTurn) about the goalie's own spot, the vertical through its hips. Does nothing without a model.
        /// </summary>
        public void Pose(GoaliePose pose, float set, float facing = 0f)
        {
            if (!Present) return;
            Snapshot target = _poses[(int)pose];
            set = Mathf.Clamp01(set);
            for (int i = 0; i < _bones.Length; i++)
                _bones[i].localRotation = Quaternion.Slerp(_ready.Rotations[i], target.Rotations[i], set);
            _model.localPosition = Vector3.Lerp(_ready.ModelPosition, target.ModelPosition, set);

            // Turn the Root about the hips' vertical: the hips stay on their spot in the crease.
            Transform root = Root.transform;
            Vector3 hips = root.InverseTransformPoint(Bone(HumanBodyBones.Hips).position);
            hips.y = 0f;
            Quaternion turn = Quaternion.AngleAxis(facing, Vector3.up);
            root.localRotation = turn;
            root.localPosition = new Vector3(0f, 0f, StandZ) + hips - turn * hips;
        }

        /// <summary>
        /// The facing (GoalieTurn) that squares the goalie up to a shooter at this point (rink space: Net Face at z = 0,
        /// the shooter at -z), seen from where the goalie stands: centred on the net, StandZ out.
        /// </summary>
        public static float FacingToward(Vector3 shooter) => (float)GoalieTurn.Facing(shooter.x, StandZ - shooter.z);

        sealed class Snapshot
        {
            public Quaternion[] Rotations;
            public Vector3 ModelPosition;
        }

        Snapshot Take()
        {
            var snap = new Snapshot { Rotations = new Quaternion[_bones.Length], ModelPosition = _model.localPosition };
            for (int i = 0; i < _bones.Length; i++) snap.Rotations[i] = _bones[i].localRotation;
            return snap;
        }

        void Put(Snapshot snap)
        {
            for (int i = 0; i < _bones.Length; i++) _bones[i].localRotation = snap.Rotations[i];
            _model.localPosition = snap.ModelPosition;
        }

        /// <summary>Builds a pose from the T-pose: Ready Stance first, then the pose's own limbs, then back on the ice.</summary>
        Snapshot MakePose(GoaliePose pose)
        {
            Put(_rest);
            ReadyStance();
            Vector3 shift = GoaliePoseLimbs.Apply(this, pose);
            Ground(shift);
            return Take();
        }

        /// <summary>Puts the lowest pad or skate point on the ice and the hips over the stand point plus shift (Root frame).</summary>
        void Ground(Vector3 shift)
        {
            float lowest = float.MaxValue;
            foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) || !(r.name.Contains("Pad") || r.name.Contains("Skate"))) continue;
                Matrix4x4 toWorld = r.transform.localToWorldMatrix;
                foreach (Vector3 v in r.GetComponent<MeshFilter>().sharedMesh.vertices)
                    lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(v).y);
            }
            if (lowest == float.MaxValue) return;
            Vector3 hips = Bone(HumanBodyBones.Hips).position;
            Vector3 target = Root.transform.TransformPoint(new Vector3(shift.x, 0f, 0f));
            _model.position += new Vector3(target.x - hips.x, Root.transform.position.y - lowest, 0f) + Root.transform.TransformVector(new Vector3(0f, 0f, shift.z));
        }

        /// <summary>
        /// Gear sizes (x the size in the model file) across, up and front-to-back as the piece sits in the T-pose facing the
        /// shooter. Kept near real size: pads 28 cm wide (the 11 in limit), blocker and catcher a touch over the file.
        /// Scaled about each piece's centre.
        /// </summary>
        static readonly Dictionary<string, Vector3> GearScale = new Dictionary<string, Vector3>
        {
            { GearPiece.LeftPad, new Vector3(1.12f, 1f, 1f) }, { GearPiece.RightPad, new Vector3(1.12f, 1f, 1f) },
            { GearPiece.Blocker, Vector3.one * 1.05f }, { GearPiece.Catcher, Vector3.one * 1.1f },
        };

        /// <summary>The piece's own axis (0 x, 1 y, 2 z) that lies nearest the world direction, and its sign.</summary>
        static (int axis, float sign) AxisNearest(Transform piece, Vector3 world)
        {
            int best = 0;
            float bestDot = 0f;
            for (int a = 0; a < 3; a++)
            {
                Vector3 local = Vector3.zero;
                local[a] = 1f;
                float d = Vector3.Dot(piece.TransformDirection(local).normalized, world);
                if (Mathf.Abs(d) > Mathf.Abs(bestDot)) { best = a; bestDot = d; }
            }
            return (best, Mathf.Sign(bestDot));
        }

        /// <summary>Scales the gear (call in the T-pose, facing the shooter) and remembers which way each pad's front faces.</summary>
        void ScaleGear()
        {
            Quaternion q = Root.transform.rotation;
            Vector3[] world = { q * Vector3.right, q * Vector3.up, q * Vector3.back };
            foreach (MeshRenderer r in _model.GetComponentsInChildren<MeshRenderer>(true))
            {
                string piece = GearPiece.PieceOf(r.name);
                if (piece == null) continue;
                if (piece == GearPiece.LeftPad || piece == GearPiece.RightPad)
                {
                    var (axis, sign) = AxisNearest(r.transform, world[2]);
                    Vector3 front = Vector3.zero;
                    front[axis] = sign;
                    if (piece == GearPiece.LeftPad) { _leftPad = r.transform; _leftPadFront = front; }
                    else { _rightPad = r.transform; _rightPadFront = front; }
                }
                if (!GearScale.TryGetValue(piece, out Vector3 k)) continue;
                Vector3 scale = Vector3.one;
                for (int w = 0; w < 3; w++) scale[AxisNearest(r.transform, world[w]).axis] = k[w];
                Vector3 centre = r.bounds.center;
                r.transform.localScale = Vector3.Scale(r.transform.localScale, scale);
                r.transform.position += centre - r.bounds.center;
            }
        }

        /// <summary>
        /// Turns a lower leg about the shin so its pad's front faces as near face as it can. The pad front is the side of the pad
        /// axis that faces the shooter in the T-pose.
        /// </summary>
        internal void FacePad(bool goalieLeft, Vector3 face)
        {
            Transform calf = Bone(goalieLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            Transform foot = Bone(goalieLeft ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            Transform pad = goalieLeft ? _leftPad : _rightPad;
            if (calf == null || foot == null || pad == null) return;
            Vector3 shin = (foot.position - calf.position).normalized;
            Vector3 now = Vector3.ProjectOnPlane(pad.TransformDirection(goalieLeft ? _leftPadFront : _rightPadFront), shin);
            Vector3 want = Vector3.ProjectOnPlane(face, shin);
            if (now.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-6f) return;
            calf.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(now, want, shin), shin) * calf.rotation;
        }

        Transform _leftPad, _rightPad;
        Vector3 _leftPadFront, _rightPadFront; // pad-local

        /// <summary>World bounds of a piece of gear (GearPiece) as posed now, or false without it.</summary>
        public bool GearBounds(string piece, out Bounds bounds)
        {
            bounds = default;
            if (!Present) return false;
            foreach (MeshRenderer r in _gear)
                if (r.name == GearPiece.ObjectName(piece)) { bounds = r.bounds; return true; }
            return false;
        }

        /// <summary>
        /// The gear and body of one Goalie Pose fully set, in the Root's own space: the turn to the shooter moves the Root,
        /// not the bones, so one measurement serves every facing.
        /// </summary>
        sealed class Shape
        {
            public Vector3 Hips; // in the Root's space, on the ice plane (y 0): the turn is about the vertical through it
            public readonly PuckObstacles Local = new PuckObstacles(); // the gear and body as AddTo adds them, in the Root's space
            public readonly Dictionary<string, (Matrix4x4 toRoot, Bounds mesh)> Gear = new Dictionary<string, (Matrix4x4, Bounds)>();
        }

        /// <summary>
        /// Measures the pose now if it has not been yet (the Shot then costs no posing or mesh copying). Call while the
        /// character is shown, for example when the Round picks the pose.
        /// </summary>
        public void Prepare(GoaliePose pose)
        {
            if (Present) ShapeOf(pose);
        }

        Shape ShapeOf(GoaliePose pose)
        {
            Shape shape = _shapes[(int)pose];
            if (shape != null) return shape;
            Snapshot shown = Take();
            Transform root = Root.transform;
            Vector3 rootPosition = root.localPosition;
            Quaternion rootRotation = root.localRotation;

            Pose(pose, 1f, 0f);
            shape = new Shape();
            Vector3 hips = root.InverseTransformPoint(Bone(HumanBodyBones.Hips).position);
            shape.Hips = new Vector3(hips.x, 0f, hips.z);
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (MeshRenderer r in _gear)
            {
                string piece = GearPiece.PieceOf(r.name);
                Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                Matrix4x4 local = toRoot * r.transform.localToWorldMatrix;
                shape.Gear[piece] = (local, mesh.bounds);
                shape.Local.AddMesh(ToWorld(mesh.vertices, local), mesh.triangles, PuckMotion.SurfaceOf(piece), piece);
            }
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer body in _body)
            {
                body.BakeMesh(baked, true);
                Matrix4x4 local = toRoot * Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                shape.Local.AddMesh(ToWorld(baked.vertices, local), baked.triangles, PuckSurface.Body, GearPiece.Body);
            }
            SceneObjects.DestroySafely(baked);

            Put(shown);
            root.localPosition = rootPosition;
            root.localRotation = rootRotation;
            _shapes[(int)pose] = shape;
            return shape;
        }

        /// <summary>The Root's local-to-world matrix as Pose(pose, 1, facing) leaves it.</summary>
        Matrix4x4 RootToWorld(Shape shape, float facing)
        {
            Quaternion turn = Quaternion.AngleAxis(facing, Vector3.up);
            Vector3 position = new Vector3(0f, 0f, StandZ) + shape.Hips - turn * shape.Hips;
            Matrix4x4 local = Matrix4x4.TRS(position, turn, Root.transform.localScale);
            Transform parent = Root.transform.parent;
            return parent != null ? parent.localToWorldMatrix * local : local;
        }

        /// <summary>
        /// GearBounds as Pose(pose, 1, facing) would give it, without moving the character (measured once per pose).
        /// </summary>
        public bool PosedGearBounds(GoaliePose pose, float facing, string piece, out Bounds bounds)
        {
            bounds = default;
            if (!Present) return false;
            Shape shape = ShapeOf(pose);
            if (!shape.Gear.TryGetValue(piece, out var gear)) return false;
            Matrix4x4 toWorld = RootToWorld(shape, facing) * gear.toRoot;
            Bounds b = gear.mesh;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = toWorld.MultiplyPoint3x4(new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                if (i == 0) bounds = new Bounds(p, Vector3.zero);
                else bounds.Encapsulate(p);
            }
            return true;
        }

        /// <summary>
        /// AddTo as Pose(pose, 1, facing) would give it, without moving the character: the pose is measured once (Prepare)
        /// and only placed at the facing here, so a Shot costs no posing, baking or mesh copying.
        /// </summary>
        public void AddPosed(PuckObstacles obstacles, GoaliePose pose, float facing)
        {
            if (!Present) return;
            Shape shape = ShapeOf(pose);
            obstacles.AddPlaced(shape.Local, RootToWorld(shape, facing));
        }

        /// <summary>
        /// Adds the gear and body as posed now, in world space, to what a puck can hit. Each gear piece is named as in
        /// GearBounds (GearPiece); the body and clothes are GearPiece.Body. Does nothing without a model.
        /// </summary>
        public void AddTo(PuckObstacles obstacles)
        {
            if (!Present) return;
            foreach (MeshRenderer r in _gear)
            {
                string piece = GearPiece.PieceOf(r.name);
                Mesh shared = r.GetComponent<MeshFilter>().sharedMesh;
                obstacles.AddMesh(ToWorld(shared.vertices, r.transform.localToWorldMatrix), shared.triangles, PuckMotion.SurfaceOf(piece), piece);
            }
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer body in _body)
            {
                body.BakeMesh(mesh, true);
                Matrix4x4 toWorld = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                obstacles.AddMesh(ToWorld(mesh.vertices, toWorld), mesh.triangles, PuckSurface.Body, GearPiece.Body);
            }
            SceneObjects.DestroySafely(mesh);
        }

        static Vector3[] ToWorld(Vector3[] vertices, Matrix4x4 toWorld)
        {
            var world = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) world[i] = toWorld.MultiplyPoint3x4(vertices[i]);
            return world;
        }

        /// <summary>The bone for a humanoid body part, or null when there is no model.</summary>
        public Transform Bone(HumanBodyBones bone) => Animator != null ? Animator.GetBoneTransform(bone) : null;

        public void Show(bool visible)
        {
            if (Root.activeSelf != visible) Root.SetActive(visible);
        }

        /// <summary>World-space bounds of the body and its clothes as posed now (the rigid gear is not included).</summary>
        public Bounds BodyBounds()
        {
            bool any = false;
            var bounds = new Bounds();
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer body in _body)
            {
                body.BakeMesh(mesh, true);
                Matrix4x4 toWorld = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                foreach (Vector3 v in mesh.vertices)
                {
                    Vector3 p = toWorld.MultiplyPoint3x4(v);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            SceneObjects.DestroySafely(mesh);
            return bounds;
        }

        /// <summary>
        /// Neutral Ready Stance: wide stance with the knees bent over the skates, hips back, chest leaning in, head up to
        /// the shooter, skates flat on the ice. Blocker hand low at the side holding the stick: forearm and fingers
        /// pointing down, back of the hand (the blocker board) to the shooter, thumb inward, so the stick (diagonal in
        /// the palm) runs down and in to the ice. Catcher out at the side, palm (the
        /// pocket) to the shooter, fingers up. Each limb is aimed in the goalie's own frame (forward = toward the shooter),
        /// so it can be re-applied from any pose. #27 can pose the rig the same way (Aim, Orient) or through the Animator.
        /// </summary>
        public void ReadyStance()
        {
            if (!Present) return;
            Transform frame = Root.transform;
            Vector3 forward = frame.rotation * Vector3.back, up = frame.up, left = frame.rotation * Vector3.right; // the goalie's own left

            Aim(HumanBodyBones.Spine, HumanBodyBones.Neck, up * 0.6f + forward * 0.8f);
            Aim(HumanBodyBones.Neck, HumanBodyBones.Head, up + forward * 0.2f);
            foreach (float side in new[] { 1f, -1f })
            {
                bool l = side > 0f;
                Vector3 outward = left * side;
                Aim(l ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg, l ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                    -up * 0.35f + forward * 0.85f + outward * 0.4f);
                Aim(l ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, l ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
                    -up * 0.85f - forward * 0.45f + outward * 0.12f);
                // Skates flat on the ice, toes turned out a little.
                Transform foot = Bone(l ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                foot.rotation = frame.rotation * Quaternion.AngleAxis(l ? -12f : 12f, Vector3.up) * (l ? _restLeftFoot : _restRightFoot);
            }
            // Catcher (left hand): out at the side, pocket to the shooter, fingers up.
            Aim(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -up * 0.75f + left * 0.5f + forward * 0.3f);
            Aim(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, forward * 0.75f + left * 0.45f + up * 0.1f);
            Orient(HumanBodyBones.LeftHand, up * 0.9f + forward * 0.2f + left * 0.1f, -left);
            // Blocker (right hand): low at the side, the board (along the forearm and fingers) upright and facing the
            // shooter, thumb inward; the stick, held across the palm on a diagonal, slants down and in to the ice.
            Vector3 blockerForearm = -up * 0.9f + forward * 0.35f;
            Aim(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, -up * 0.85f - left * 0.35f + forward * 0.3f);
            Aim(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, blockerForearm);
            Orient(HumanBodyBones.RightHand, blockerForearm, left);
        }

        /// <summary>
        /// Turns a hand so its fingers point along fingers and its thumb as near thumb as it can (square to the fingers).
        /// </summary>
        public void Orient(HumanBodyBones hand, Vector3 fingers, Vector3 thumb)
        {
            Transform bone = Bone(hand);
            bool l = hand == HumanBodyBones.LeftHand;
            if (bone == null || (hand != HumanBodyBones.LeftHand && hand != HumanBodyBones.RightHand)) return;
            Quaternion rest = Quaternion.LookRotation(l ? _leftFingers : _rightFingers, l ? _leftThumb : _rightThumb);
            bone.rotation = Quaternion.LookRotation(fingers, thumb) * Quaternion.Inverse(rest);
        }

        /// <summary>Remembers, from the rest pose, how the feet sit and which way each hand's fingers and thumb point.</summary>
        void CaptureRest()
        {
            var bones = new List<Transform>();
            for (var b = HumanBodyBones.Hips; b < HumanBodyBones.LastBone; b++)
            {
                Transform t = Bone(b);
                if (t != null && !bones.Contains(t)) bones.Add(t);
            }
            _bones = bones.ToArray();
            _rest = Take();
            Quaternion toFrame = Quaternion.Inverse(Root.transform.rotation);
            _restLeftFoot = toFrame * Bone(HumanBodyBones.LeftFoot).rotation;
            _restRightFoot = toFrame * Bone(HumanBodyBones.RightFoot).rotation;
            (_leftFingers, _leftThumb) = HandAxes(HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftThumbProximal);
            (_rightFingers, _rightThumb) = HandAxes(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightThumbProximal);
        }

        (Vector3 fingers, Vector3 thumb) HandAxes(HumanBodyBones hand, HumanBodyBones middle, HumanBodyBones thumb)
        {
            Transform h = Bone(hand), m = Bone(middle), t = Bone(thumb);
            if (h == null || m == null || t == null) return (Vector3.forward, Vector3.up);
            Vector3 f = h.InverseTransformDirection(m.position - h.position).normalized;
            Vector3 th = h.InverseTransformDirection(t.position - h.position);
            return (f, Vector3.ProjectOnPlane(th, f).normalized);
        }

        /// <summary>Turns a bone so the direction from it to the child bone points along direction (shortest turn).</summary>
        public void Aim(HumanBodyBones bone, HumanBodyBones child, Vector3 direction)
        {
            Transform from = Bone(bone), to = Bone(child);
            if (from == null || to == null || from == to) return;
            Vector3 now = to.position - from.position;
            if (now.sqrMagnitude < 1e-10f || direction.sqrMagnitude < 1e-10f) return;
            from.rotation = Quaternion.FromToRotation(now, direction) * from.rotation;
        }

        /// <summary>Turns the model about Y so its front faces the shooter (-z). Front = from the hips' right x up.</summary>
        void FaceTheShooter(Transform model)
        {
            Vector3 right = Bone(HumanBodyBones.RightUpperLeg).position - Bone(HumanBodyBones.LeftUpperLeg).position;
            Vector3 up = Bone(HumanBodyBones.Head).position - Bone(HumanBodyBones.Hips).position;
            Vector3 forward = Vector3.Cross(right, up);
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-8f) return;
            model.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(forward, Vector3.back, Vector3.up), Vector3.up) * model.rotation;
        }

        /// <summary>Puts the skate blades on the ice (y = 0) and the hips over the stand point.</summary>
        void StandOnIce(Transform model)
        {
            float lowest = float.MaxValue;
            foreach (Renderer skate in _skates)
            {
                Matrix4x4 toWorld = skate.transform.localToWorldMatrix;
                foreach (Vector3 v in skate.GetComponent<MeshFilter>().sharedMesh.vertices)
                    lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(v).y);
            }
            if (lowest == float.MaxValue) lowest = BodyBounds().min.y;
            Vector3 hips = Bone(HumanBodyBones.Hips).position;
            Vector3 root = Root.transform.position;
            model.position += new Vector3(root.x - hips.x, root.y - lowest, 0f);
        }
    }
}
