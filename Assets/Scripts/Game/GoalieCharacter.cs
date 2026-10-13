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
    /// It stands at the net in a neutral Ready Stance and is hidden until shown. On the Shot it moves into the Save Reaction the
    /// Round's race gives (React), with the reaction's gear reaching the puck's line (see docs/adr/0003). With no model file, Present is false and the
    /// character draws nothing.
    /// </summary>
    public sealed class GoalieCharacter
    {
        /// <summary>Where the goalie stands until he is placed (Spot): centred on the net, this far in front of the Net Face (metres, -z is toward the shooter).</summary>
        public const float StandZ = -0.4f;
        /// <summary>The furthest the goalie pushes across the crease to reach a Shot (metres, either way).</summary>
        public const float MaxSlide = 0.6f;

        SkinnedMeshRenderer[] _body = new SkinnedMeshRenderer[0];
        Mesh[] _bodyAll = new Mesh[0], _bodyHidden = new Mesh[0]; // per _body renderer: the model's mesh, and a copy without the bare hand and feet
        int[][] _bodyTriangles = new int[0][];                    // per _body renderer: the model's triangles (reach reads these, whatever is drawn)
        Renderer[] _skates = new Renderer[0];
        Quaternion _restLeftFoot = Quaternion.identity, _restRightFoot = Quaternion.identity; // in the Root frame
        Vector3 _leftFingers = Vector3.forward, _leftThumb = Vector3.up, _rightFingers = Vector3.forward, _rightThumb = Vector3.up; // hand-local
        Transform _model;
        Transform[] _bones = new Transform[0];   // every mapped humanoid bone
        Snapshot _rest, _ready, _butterfly;      // T-pose as imported; the Ready Stance; the butterfly the Pads reaction drops into
        Snapshot _reaction;                      // what ShowReaction(1, facing) shows: the last React, or the Ready Stance before any
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
            HideBareParts(model);
            _gear = Array.FindAll(model.GetComponentsInChildren<MeshRenderer>(true), r => GearPiece.PieceOf(r.name) != null);
            _skates = Array.FindAll(model.GetComponentsInChildren<Renderer>(true), r => r.name == GearPiece.ObjectName(GearPiece.LeftSkate) || r.name == GearPiece.ObjectName(GearPiece.RightSkate));

            _model = model.transform;
            FaceTheShooter(model.transform);
            ScaleGear();
            CaptureRest();
            ReadyStance();
            StandOnIce(model.transform);
            _ready = Take();
            _butterfly = MakeButterfly();
            _reaction = _ready;
            Put(_ready);
            Transform head = Bone(HumanBodyBones.Head);
            if (head != null) _faceLocal = head.InverseTransformDirection(Root.transform.rotation * Vector3.back);
        }

        /// <summary>
        /// Moves the character from the Ready Stance (set 0) into the reaction React built (set 1; the Ready Stance before any
        /// React). The same reaction and set always give the same look, whatever was shown before. facing: a body turn (degrees
        /// about the vertical, + toward the shooter's left; see GoalieTurn) about the goalie's own spot, the vertical through its
        /// hips. Does nothing without a model.
        /// </summary>
        public void ShowReaction(float set, float facing = 0f)
        {
            if (!Present) return;
            set = Mathf.Clamp01(set);
            for (int i = 0; i < _bones.Length; i++)
                _bones[i].localRotation = Quaternion.Slerp(_ready.Rotations[i], _reaction.Rotations[i], set);
            _model.localPosition = Vector3.Lerp(_ready.ModelPosition, _reaction.ModelPosition, set);
            PlaceRoot(facing);
        }

        /// <summary>Turns the Root about the hips' vertical: the hips stay on their spot in the crease.</summary>
        void PlaceRoot(float facing)
        {
            Transform root = Root.transform;
            Vector3 hips = root.InverseTransformPoint(Bone(HumanBodyBones.Hips).position);
            hips.y = 0f;
            Quaternion turn = Quaternion.AngleAxis(facing, Vector3.up);
            root.localRotation = turn;
            root.localPosition = _spot + hips - turn * hips;
        }

        /// <summary>Where the goalie stands in the crease (rink space, on the ice): centred on the net, StandZ out, until Spot moves it. MeasureReach moves it for a while.</summary>
        Vector3 _spot = new Vector3(0f, 0f, StandZ);

        /// <summary>
        /// Where the goalie stands in the crease (rink space, on the ice; y is dropped): the spot he turns about. He plays
        /// the angle by moving it (BreakawayView). Shown, and used by the next React, from the next ShowReaction.
        /// </summary>
        public Vector3 Spot
        {
            get => _spot;
            set => _spot = new Vector3(value.x, 0f, value.z);
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

        /// <summary>Builds the butterfly from the T-pose: Ready Stance first, then the butterfly's limbs, then back on the ice.</summary>
        Snapshot MakeButterfly()
        {
            Put(_rest);
            ReadyStance();
            Vector3 shift = GoalieButterfly.Apply(this);
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

        // ---- Save Reaction ------------------------------------------------------------------------------------------------

        /// <summary>A piece of gear measured once in its own rigid frame (position and turn, with its scale baked in).</summary>
        sealed class GearShape
        {
            public Transform Transform;
            public readonly GoalieShape Shape = new GoalieShape();
            public Bounds Local;
            public Vector3 Point; // where the piece reaches for the puck: its centre (the stick: the middle of its blade)
        }

        Dictionary<string, GearShape> _gearShapes;          // measured by Prepare
        List<(Transform bone, GoalieShape shape)> _readyBody, _butterflyBody; // the body and clothes in each base, in pieces carried by their bones
        readonly List<(GoalieShape shape, Matrix4x4 toWorld)> _placed = new List<(GoalieShape, Matrix4x4)>(); // where React left them
        readonly Dictionary<string, Matrix4x4> _placedGear = new Dictionary<string, Matrix4x4>();
        readonly GoalieShape _scratch = new GoalieShape(); // Placed()'s, reused so a Shot frame makes no new ones
        readonly string[] _onePiece = new string[1];            // OnePiece()'s

        /// <summary>
        /// The piece of gear (GearPiece) the last React put on the puck's line: Catcher, Blocker, a pad, Stick or Body. Null
        /// before any React and after a reach that keeps off the puck (a Miss, or a Goal no gear can reach).
        /// </summary>
        public string ReactionPiece { get; private set; }

        static Matrix4x4 Rigid(Transform t) => Matrix4x4.TRS(t.position, t.rotation, Vector3.one);

        /// <summary>
        /// Measures the gear and the body once (the Shot then costs no baking or mesh copying, only placing). Call while the
        /// character is shown, for example at the Round start; React does it itself if it has not been done.
        /// </summary>
        public void Prepare()
        {
            if (!Present || _gearShapes != null) return;
            Snapshot shown = Take();
            Put(_ready);
            _gearShapes = new Dictionary<string, GearShape>();
            foreach (MeshRenderer r in _gear)
            {
                string piece = GearPiece.PieceOf(r.name);
                Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                Matrix4x4 toRigid = Rigid(r.transform).inverse * r.transform.localToWorldMatrix;
                Vector3[] local = ToWorld(mesh.vertices, toRigid);
                var gear = new GearShape { Transform = r.transform, Local = BoundsOf(local) };
                gear.Shape.AddMesh(local, mesh.triangles, piece);
                gear.Point = piece == GearPiece.Stick ? Blade(local, Rigid(r.transform)) : gear.Local.center;
                _gearShapes[piece] = gear;
            }
            if (_gearShapes.ContainsKey(GearPiece.Stick)) _bladeY = Lowest(GearPiece.Stick);
            _readyBody = BakeBody(_ready);
            _butterflyBody = BakeBody(_butterfly);
            Put(shown);
        }

        /// <summary>The middle of the stick blade (stick-local): the stick's lowest few centimetres in the Ready Stance.</summary>
        static Vector3 Blade(Vector3[] local, Matrix4x4 toWorld)
        {
            float lowest = float.MaxValue;
            foreach (Vector3 v in local) lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(v).y);
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Vector3 v in local)
                if (toWorld.MultiplyPoint3x4(v).y < lowest + 0.04f) { sum += v; n++; }
            return sum / n;
        }

        static Bounds BoundsOf(Vector3[] points)
        {
            var b = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 p in points) b.Encapsulate(p);
            return b;
        }

        /// <summary>
        /// The body and clothes baked in a base pose, split by the bone that moves each triangle most (its first corner's
        /// heaviest skin weight) and kept in that bone's rigid frame. Placed by the bones as a reaction leaves them, an arm
        /// reaching for the puck takes its sleeve along (rigid per bone: only the bend at each joint is approximate).
        /// </summary>
        List<(Transform bone, GoalieShape shape)> BakeBody(Snapshot pose)
        {
            Put(pose);
            var groups = new Dictionary<Transform, (Matrix4x4 toBone, List<Vector3> corners)>();
            var baked = new Mesh();
            for (int part = 0; part < _body.Length; part++)
            {
                SkinnedMeshRenderer body = _body[part];
                body.BakeMesh(baked, true);
                Vector3[] vertices = baked.vertices;
                int[] triangles = _bodyTriangles[part]; // the model's, not the drawn mesh's (hidden hands and feet still reach)
                BoneWeight[] weights = body.sharedMesh.boneWeights;
                Transform[] bones = body.bones;
                Matrix4x4 toWorld = Rigid(body.transform);
                bool skin = body.name == BareSkin;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Transform bone = weights.Length == vertices.Length ? bones[weights[triangles[i]].boneIndex0] : null;
                    if (bone == null) bone = _model;
                    if (skin && InHand(bone)) continue; // the bare hands are inside the catcher and blocker
                    if (!groups.TryGetValue(bone, out var group))
                    {
                        group = (Rigid(bone).inverse * toWorld, new List<Vector3>());
                        groups[bone] = group;
                    }
                    for (int k = 0; k < 3; k++) group.corners.Add(group.toBone.MultiplyPoint3x4(vertices[triangles[i + k]]));
                }
            }
            SceneObjects.DestroySafely(baked);
            var parts = new List<(Transform, GoalieShape)>();
            foreach (var kv in groups)
            {
                var shape = new GoalieShape();
                var order = new int[kv.Value.corners.Count];
                for (int i = 0; i < order.Length; i++) order[i] = i;
                shape.AddMesh(kv.Value.corners.ToArray(), order, GearPiece.Body);
                parts.Add((kv.Key, shape));
            }
            return parts;
        }

        /// <summary>The skinned mesh of the bare skin (Tools/make_lifelike_goalie.py), whose hands sit inside the gloves.</summary>
        const string BareSkin = "Skin";

        /// <summary>Holds the hidden-parts mesh copies on the model so they are destroyed with the goalie.</summary>
        [ExecuteAlways]
        sealed class MeshOwner : MonoBehaviour
        {
            public Mesh[] Meshes = new Mesh[0];
            void OnDestroy() { foreach (Mesh m in Meshes) if (m != null) SceneObjects.DestroySafely(m); }
        }

        /// <summary>
        /// Gives each skinned renderer a mesh copy without the triangles that poke through the gear: the LEFT bare hand (in the
        /// catcher; the right hand grips the stick inside the blocker and stays) and the feet (the socks' foot share, inside the
        /// skates). Chosen by bone weights, so it holds in every pose. Reach reads the original triangles (_bodyTriangles).
        /// </summary>
        void HideBareParts(GameObject model)
        {
            int count = _body.Length;
            _bodyAll = new Mesh[count];
            _bodyHidden = new Mesh[count];
            _bodyTriangles = new int[count][];
            var owned = new List<Mesh>();
            Transform leftHand = Bone(HumanBodyBones.LeftHand), leftFoot = Bone(HumanBodyBones.LeftFoot), rightFoot = Bone(HumanBodyBones.RightFoot);
            for (int part = 0; part < count; part++)
            {
                SkinnedMeshRenderer body = _body[part];
                Mesh all = body.sharedMesh;
                _bodyAll[part] = _bodyHidden[part] = all;
                if (all == null) { _bodyTriangles[part] = new int[0]; continue; }
                _bodyTriangles[part] = all.triangles;
                BoneWeight[] weights = all.boneWeights;
                if (weights.Length != all.vertexCount) continue;
                Transform[] bones = body.bones;
                var handBone = new bool[bones.Length];
                var footBone = new bool[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    handBone[i] = bones[i] != null && leftHand != null && bones[i].IsChildOf(leftHand);
                    footBone[i] = bones[i] != null && ((leftFoot != null && bones[i].IsChildOf(leftFoot)) || (rightFoot != null && bones[i].IsChildOf(rightFoot)));
                }
                bool skin = body.name == BareSkin;
                Mesh copy = UnityEngine.Object.Instantiate(all);
                copy.name = all.name + " (hands and feet hidden)";
                int removed = 0;
                for (int sub = 0; sub < all.subMeshCount; sub++)
                {
                    int[] triangles = all.GetTriangles(sub);
                    var kept = new List<int>(triangles.Length);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        if (HiddenUnderGear(skin, handBone, footBone, weights,triangles[i], triangles[i + 1], triangles[i + 2])) { removed++; continue; }
                        kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]);
                    }
                    copy.SetTriangles(kept, sub);
                }
                if (removed == 0) { SceneObjects.DestroySafely(copy); continue; }
                _bodyHidden[part] = copy;
                owned.Add(copy);
                body.sharedMesh = copy;
            }
            model.AddComponent<MeshOwner>().Meshes = owned.ToArray();
        }

        /// <summary>
        /// Whether a triangle of the renderer is a bare left hand (the bare skin's, by its first corner's heaviest bone, as InHand
        /// is used for reach) or a foot (any corner with at least half its weight on a foot bone or toe).
        /// </summary>
        static bool HiddenUnderGear(bool skin, bool[] handBone, bool[] footBone, BoneWeight[] weights, int a, int b, int c)
        {
            if (skin && handBone[weights[a].boneIndex0]) return true;
            return FootShare(footBone, weights[a]) >= 0.5f || FootShare(footBone, weights[b]) >= 0.5f || FootShare(footBone, weights[c]) >= 0.5f;
        }

        static float FootShare(bool[] footBone, BoneWeight w)
        {
            float foot = 0f;
            if (footBone[w.boneIndex0]) foot += w.weight0;
            if (footBone[w.boneIndex1]) foot += w.weight1;
            if (footBone[w.boneIndex2]) foot += w.weight2;
            if (footBone[w.boneIndex3]) foot += w.weight3;
            return foot;
        }

        /// <summary>
        /// Draws the model's own meshes (true: the bare left hand and the feet show) or the copies without them (false, the default:
        /// they never poke through the gear). Reach and hit shapes are the same either way.
        /// </summary>
        public void ShowUnderGear(bool show)
        {
            for (int part = 0; part < _body.Length; part++)
                _body[part].sharedMesh = show ? _bodyAll[part] : _bodyHidden[part];
        }

        bool InHand(Transform bone)
        {
            Transform left = Bone(HumanBodyBones.LeftHand), right = Bone(HumanBodyBones.RightHand);
            return bone.IsChildOf(left) || bone.IsChildOf(right);
        }

        /// <summary>
        /// Builds what the goalie moves into from the Ready Stance on the Shot (shown by ShowReaction(set, facing)), at the facing it
        /// ends at. The puck flies from `from` to `to` (world space). touch (a Save): the reaction's gear (ReactionPiece) reaches
        /// the puck's line: the catcher or blocker by arm (two-bone IK), the stick blade along the ice, a pad dropped into the
        /// butterfly (standing for a puck a little higher), or the body pushed across in front of it; pushing across the crease (up to
        /// MaxSlide) when the arm alone cannot reach. touch also serves a Goal the race timed some gear for: the same reach,
        /// shown getting there too late (#63). Not touch (a Miss, or a Goal no gear can reach): a hand reach that stops short and
        /// keeps the whole goalie off the puck's line. The same arguments always give the same reaction.
        /// </summary>
        public void React(SaveReaction reaction, Vector3 from, Vector3 to, float facing, bool touch)
        {
            if (!Present) return;
            Prepare();
            Snapshot shown = Take();
            Transform root = Root.transform;
            Vector3 rootPosition = root.localPosition;
            Quaternion rootRotation = root.localRotation;

            if (touch) Reach(reaction, from, to, facing);
            else Beaten(reaction, from, to, facing);
            _line = (from, to, facing, touch ? (SaveReaction?)null : reaction);
            _late = null; // built when the puck has got past (AfterShot), not on the Shot frame

            Put(shown);
            root.localPosition = rootPosition;
            root.localRotation = rootRotation;
        }

        /// <summary>
        /// Measures a Save Reaction for the race (Reach in time) without keeping or showing it: the goalie in the Ready Stance
        /// standing at spot (rink space, on the ice) at this facing, the puck flying from `from` to `to`. inTheWay: his gear or
        /// body already meets the puck's line in the Ready Stance. True when the reaction puts gear on the puck's line;
        /// gearMetres: how far that gear travels from the Ready Stance (the body: the hips); pushMetres: how far the whole goalie
        /// pushes across. Everything shown and the last React are left as they were.
        /// </summary>
        public bool MeasureReach(SaveReaction reaction, Vector3 spot, Vector3 from, Vector3 to, float facing,
            out float gearMetres, out float pushMetres, out bool inTheWay)
        {
            gearMetres = pushMetres = 0f;
            inTheWay = false;
            if (!Present) return false;
            float gear = 0f, push = 0f;
            bool blocked = false;
            bool reaches = InReadyStanceAt(spot, facing, () =>
            {
                blocked = Touches(from, to);
                Vector3 readyHips = Bone(HumanBodyBones.Hips).position;
                var start = new Dictionary<string, Vector3>();
                foreach (var kv in _gearShapes) start[kv.Key] = GearPoint(kv.Key);

                Reach(reaction, from, to, facing);
                string piece = ReactionPiece;
                bool touches = piece != null && Touches(from, to, OnePiece(piece)); // its own gear, not whatever else ended up on the line
                push = Mathf.Abs(_model.localPosition.x - _ready.ModelPosition.x);
                gear = piece != null && start.TryGetValue(piece, out Vector3 was)
                    ? Vector3.Distance(was, GearPoint(piece))
                    : Vector3.Distance(readyHips, Bone(HumanBodyBones.Hips).position);
                return touches;
            });
            gearMetres = gear;
            pushMetres = push;
            inTheWay = blocked;
            return reaches;
        }

        /// <summary>
        /// For the Gaps: whether a puck flying from `from` to each point of `to` meets his gear or body already, in the Ready
        /// Stance standing at spot (rink space, on the ice) at this facing: MeasureReach's inTheWay, for many points at once.
        /// Without a model nothing is in the way. Everything shown and the last React are left as they were.
        /// </summary>
        public void MeasureInTheWay(Vector3 spot, float facing, Vector3 from, Vector3[] to, bool[] inTheWay)
        {
            if (!Present)
            {
                for (int i = 0; i < inTheWay.Length; i++) inTheWay[i] = false;
                return;
            }
            InReadyStanceAt(spot, facing, () =>
            {
                for (int i = 0; i < to.Length; i++) inTheWay[i] = Touches(from, to[i]);
                return true;
            });
        }

        /// <summary>
        /// Runs measure with the goalie in the Ready Stance standing at spot at this facing (his gear and body placed there), then
        /// puts back everything shown and the last React.
        /// </summary>
        T InReadyStanceAt<T>(Vector3 spot, float facing, Func<T> measure)
        {
            Prepare();
            Snapshot shown = Take(), kept = _reaction;
            string keptPiece = ReactionPiece;
            var keptPlaced = new List<(GoalieShape, Matrix4x4)>(_placed);
            var keptPlacedGear = new Dictionary<string, Matrix4x4>(_placedGear);
            Transform root = Root.transform;
            Vector3 rootPosition = root.localPosition, keptSpot = _spot;
            Quaternion rootRotation = root.localRotation;
            try
            {
                _spot = new Vector3(spot.x, 0f, spot.z);
                Put(_ready);
                PlaceRoot(facing);
                Finish(null, _readyBody);
                return measure();
            }
            finally
            {
                _spot = keptSpot;
                _reaction = kept;
                ReactionPiece = keptPiece;
                _placed.Clear();
                _placed.AddRange(keptPlaced);
                _placedGear.Clear();
                foreach (var kv in keptPlacedGear) _placedGear[kv.Key] = kv.Value;
                Put(shown);
                root.localPosition = rootPosition;
                root.localRotation = rootRotation;
            }
        }

        void Reach(SaveReaction reaction, Vector3 from, Vector3 to, float facing)
        {
            bool butterfly = reaction == SaveReaction.Pads && LineAt(from, to, _spot.z - 0.25f).y < ButterflyBelow; // at the pads' depth
            Put(butterfly ? _butterfly : _ready);
            PlaceRoot(facing);
            string piece;
            switch (reaction)
            {
                case SaveReaction.Glove:
                case SaveReaction.Blocker: piece = GearPiece.HandOf(reaction); ReachHand(reaction == SaveReaction.Glove, piece, OnLine(from, to, piece), facing, true); break;
                case SaveReaction.Stick: piece = GearPiece.Stick; ReachStick(from, to, facing); break;
                case SaveReaction.Pads: piece = ReachPad(from, to, facing, butterfly); break;
                default: piece = GearPiece.Body; ReachBody(from, to, facing); break;
            }
            var body = butterfly ? _butterflyBody : _readyBody;
            Look(piece == GearPiece.Body ? LineAt(from, to, Bone(HumanBodyBones.Chest).position.z) : OnLine(from, to, piece));
            Finish(piece, body);
            if (SaveReactions.IsHand(reaction)) ReachPastPads(reaction == SaveReaction.Glove, piece, from, to, facing, body);
            if (reaction != SaveReaction.Pads) return;
            // A pad save with the stick or blocker in front of the pad on the puck's line: the blade goes further to the far
            // side, then the blocker comes up and out of the way (the pad stays put, so where the puck meets it is found once).
            Vector3 near = NearGoalie(from, to);
            if (!Placed().Cast(near, to, GoalieShape.LinePuckThickness / 2f, out Vector3 onPad, out _,OnePiece(piece))) return;
            bool left = piece == GearPiece.LeftPad;
            for (int i = 1; i < FiveHoleBlades.Length + BlockerOut.Length && InFront(near, onPad); i++)
            {
                PlaceBlade(left, FiveHoleBlades[Mathf.Min(i, FiveHoleBlades.Length - 1)]);
                if (i >= FiveHoleBlades.Length)
                    ReachHand(false, GearPiece.Blocker, Bone(HumanBodyBones.RightUpperArm).position + BlockerOut[i - FiveHoleBlades.Length], facing, false);
                Finish(piece, body);
            }
        }

        /// <summary>How much further toward the shooter a hand reaches, along the puck's line, when a pad is in front of it (metres).</summary>
        static readonly float[] ReachForward = { 0.08f, 0.16f, 0.24f };

        /// <summary>Higher than the pads' tops in the Ready Stance (metres), with room to spare.</summary>
        const float PadTop = 1.0f;

        /// <summary>The gear that can stand in front of a hand on the puck's line: the tall pads of the stance, and the stick.</summary>
        static readonly string[] HandRivals = { GearPiece.LeftPad, GearPiece.RightPad, GearPiece.Stick };

        /// <summary>
        /// A glove or blocker save low enough that a pad of the stance stands in front of the hand on the puck's line: the
        /// hand reaches further out along the line, toward the shooter, until it is first.
        /// </summary>
        void ReachPastPads(bool goalieLeft, string piece, Vector3 from, Vector3 to, float facing, List<(Transform bone, GoalieShape shape)> body)
        {
            Vector3 near = NearGoalie(from, to);
            float depth = GearPoint(piece).z;
            if (LineAt(from, to, depth).y > PadTop) return; // above every pad of the stance
            for (int i = 0; i < ReachForward.Length; i++)
            {
                if (!Placed().Cast(near, to, GoalieShape.LinePuckThickness / 2f, out Vector3 onHand, out _,OnePiece(piece))) return;
                if (!InFront(near, onHand, HandRivals)) return;
                ReachHand(goalieLeft, piece, LineAt(from, to, depth - ReachForward[i]), facing, true);
                Finish(piece, body);
            }
        }

        /// <summary>Where the blocker hand goes, from its shoulder, to take the stick out of a pad save's way (first try first).</summary>
        static readonly Vector3[] BlockerOut = { new Vector3(-0.25f, -0.1f, -0.3f), new Vector3(-0.3f, 0.1f, 0.05f), new Vector3(-0.05f, -0.25f, 0.1f) };

        /// <summary>True when one of the rivals (by default the stick, blocker or a skate) is on the puck's line before `onGear`.</summary>
        bool InFront(Vector3 near, Vector3 onGear, string[] rivals = null)
        {
            return Placed().Cast(near, onGear - (onGear - near).normalized * 0.002f, GoalieShape.LinePuckThickness / 2f, out _, out _, rivals ?? PadRivals);
        }

        /// <summary>The gear that can stand in front of a pad on the puck's line in a pad save.</summary>
        static readonly string[] PadRivals = { GearPiece.Stick, GearPiece.Blocker, GearPiece.LeftSkate, GearPiece.RightSkate };

        /// <summary>How far a beaten hand gets from where it stood toward the puck's line (0: no reach), and how far the goalie may push away from it.</summary>
        static readonly float[] BeatenReach = { 0.5f, 0.25f, 0f };
        static readonly float[] BeatenSlides = { 0f, 0.2f, 0.4f, MaxSlide, BeatenMaxSlide };

        /// <summary>A beaten goalie may push this far out of the puck's way (metres; a save reaches at most MaxSlide).</summary>
        const float BeatenMaxSlide = 0.8f;
        static readonly float[] NoGaps = { 0f };
        readonly float[] _sides = new float[2]; // BeatenPass: away from the puck's side first, then toward it
        static readonly float[] NoPush = { 1f };

        void Beaten(SaveReaction reaction, Vector3 from, Vector3 to, float facing)
        {
            // First the first reach whose gear and body boxes the puck passes clear of (quick, smallest push first); failing
            // that, the first that truly misses the puck (exact, slower: largest push first and only a few tries); failing both,
            // the last one tried. The beaten hand carries on only once the puck has gone by (AfterShot).
            if (BeatenPass(reaction, from, to, facing, true)) return;
            BeatenPass(reaction, from, to, facing, false);
        }

        /// <summary>The most reaches the exact pass tries (each is five casts), so a Shot frame stays a few milliseconds.</summary>
        const int ExactTries = 3;

        bool BeatenPass(SaveReaction reaction, Vector3 from, Vector3 to, float facing, bool quick)
        {
            int tries = 0;
            bool hand = SaveReactions.IsHand(reaction);
            string piece = GearPiece.HandOf(reaction);
            Put(_ready);
            PlaceRoot(facing);
            float away = Bone(HumanBodyBones.Hips).position.x >= to.x ? 1f : -1f; // push away from the puck's side
            _sides[0] = away; _sides[1] = -away;
            for (int i = 0; i < BeatenSlides.Length; i++)
            {
                float slide = BeatenSlides[quick ? i : BeatenSlides.Length - 1 - i];
                foreach (float sign in slide == 0f ? NoPush : _sides)
                foreach (float share in hand ? BeatenReach : NoGaps)
                {
                    Put(_ready);
                    PlaceRoot(facing);
                    Slide(sign * slide, facing, BeatenMaxSlide);
                    Vector3 target = OnLine(from, to, piece), stood = GearPoint(piece);
                    if (share > 0f) ReachHand(reaction == SaveReaction.Glove, piece, Vector3.Lerp(stood, target, share), facing, false);
                    Look(target);
                    Finish(null, _readyBody);
                    if (quick ? !MayTouch(from, to) : !Touches(from, to)) return true;
                    if (!quick && ++tries >= ExactTries) return false;
                }
            }
            return false;
        }

        /// <summary>Cautious and quick: false only when the puck surely passes clear of every gear and body box of the reaction.</summary>
        bool MayTouch(Vector3 from, Vector3 to)
        {
            return Placed().NearAnyMesh(NearGoalie(from, to), to, GoalieShape.LinePuckDiameter / 2f);
        }

        /// <summary>
        /// Where the puck's line comes within reach of the goalie (1.3 m in front of where it stands): the rest of the line
        /// cannot meet it, so a cast from here costs a fraction of one from the shooter.
        /// </summary>
        /// <summary>The puck's middle and its edges as it flies face on (offsets from its centre).</summary>
        static readonly Vector3[] PuckEdges =
        {
            Vector3.zero, Vector3.left * (GoalieShape.LinePuckDiameter / 2f), Vector3.right * (GoalieShape.LinePuckDiameter / 2f),
            Vector3.up * (GoalieShape.LinePuckDiameter / 2f), Vector3.down * (GoalieShape.LinePuckDiameter / 2f),
        };

        /// <summary>The reaction's gear and body as placed now (AddReaction), in one GoalieShape reused for every check.</summary>
        GoalieShape Placed()
        {
            _scratch.Clear();
            AddReaction(_scratch);
            return _scratch;
        }

        /// <summary>Just this piece, for a cast (one array reused for every check).</summary>
        string[] OnePiece(string piece)
        {
            _onePiece[0] = piece;
            return _onePiece;
        }

        internal static Vector3 NearGoalie(Vector3 from, Vector3 to) => from.z < StandZ - 1.3f ? LineAt(from, to, StandZ - 1.3f) : from;

        /// <summary>
        /// True when the puck, flying from `from` to `to`, would meet any of the last reaction's gear or body: its middle or
        /// any edge (it flies face on, GoalieShape.LinePuckDiameter across).
        /// </summary>
        public bool Touches(Vector3 from, Vector3 to) => Touches(from, to, null);

        /// <summary>As Touches, only for these pieces (GearPiece; the body and clothes are GearPiece.Body); null: everything.</summary>
        bool Touches(Vector3 from, Vector3 to, string[] pieces)
        {
            GoalieShape obstacles = Placed();
            from = NearGoalie(from, to);
            foreach (Vector3 edge in PuckEdges)
                if (obstacles.Cast(from + edge, to + edge, GoalieShape.LinePuckThickness / 2f, out _, out _, pieces)) return true;
            return false;
        }

        void Finish(string piece, List<(Transform bone, GoalieShape shape)> body)
        {
            _reaction = Take();
            ReactionPiece = piece;
            _placed.Clear();
            _placedGear.Clear();
            foreach (var kv in _gearShapes)
            {
                Matrix4x4 m = Rigid(kv.Value.Transform);
                _placed.Add((kv.Value.Shape, m));
                _placedGear[kv.Key] = m;
            }
            foreach (var (bone, shape) in body) _placed.Add((shape, Rigid(bone)));
        }

        /// <summary>The point on the puck's line at the depth of a piece's reach point as it stands now.</summary>
        Vector3 OnLine(Vector3 from, Vector3 to, string piece) => LineAt(from, to, GearPoint(piece).z);

        Vector3 GearPoint(string piece)
        {
            GearShape gear = _gearShapes[piece];
            return Rigid(gear.Transform).MultiplyPoint3x4(gear.Point);
        }

        static Vector3 LineAt(Vector3 from, Vector3 to, float z)
        {
            float t = Mathf.Approximately(to.z, from.z) ? 1f : Mathf.Clamp01((z - from.z) / (to.z - from.z));
            return Vector3.Lerp(from, to, t);
        }

        /// <summary>Pushes the whole goalie dx metres along x (across the crease), keeping its facing about its new spot.</summary>
        void Slide(float dx, float facing, float most = MaxSlide)
        {
            _model.localPosition += new Vector3(Mathf.Clamp(dx, -most, most), 0f, 0f);
            PlaceRoot(facing);
        }

        /// <summary>How far out from its shoulder a reaching hand stays, on its own side (metres).</summary>
        const float MinOutward = 0.05f;

        /// <summary>
        /// Reaches a hand's gear point to target with two-bone IK on the arm. The wrist keeps its bend from the stance (the
        /// hand's skin and its glove stay together) and the forearm turns about its own length so the gear keeps facing the
        /// shooter as it did in the stance (the catcher pocket and the blocker board open to the shooter). slide: push across
        /// the crease when the arm alone cannot reach, or to keep the hand out on its own side rather than across the face.
        /// </summary>
        void ReachHand(bool goalieLeft, string piece, Vector3 target, float facing, bool slide)
        {
            if (piece == GearPiece.Stick) { ReachStickHand(target, facing, slide); return; }
            Transform shoulder = Bone(goalieLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Transform elbow = Bone(goalieLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform hand = Bone(goalieLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Vector3 forward = Root.transform.rotation * Vector3.back;
            Vector3 point = elbow.InverseTransformPoint(GearPoint(piece)); // carried by the forearm: the wrist does not bend
            Vector3 face = elbow.InverseTransformDirection(forward);       // the way the gear faces now, in the forearm's frame
            float reach = 0.98f * (Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, elbow.TransformPoint(point)));
            if (slide)
            {
                float side = goalieLeft ? 1f : -1f; // the goalie's left is +x, toward the shooter's right
                float outward = (target.x - shoulder.position.x) * side;
                if (outward < MinOutward) Slide(-(MinOutward - outward) * side, facing);
                Vector3 d = target - shoulder.position;
                if (d.magnitude > reach)
                {
                    float keep = Mathf.Sqrt(Mathf.Max(0f, reach * reach - d.y * d.y - d.z * d.z));
                    Slide(d.x - Mathf.Sign(d.x) * keep, facing);
                }
            }
            // The elbow bends down, out and a little back toward the net, never toward the shooter in front of the glove.
            Vector3 pole = shoulder.position + Vector3.down * 0.4f - forward * 0.2f + Vector3.right * ((goalieLeft ? 1f : -1f) * 0.15f);
            for (int i = 0; i < 3; i++)
            {
                TwoBone(shoulder, elbow, point, target, pole);
                Twist(elbow, hand, face, forward);
            }
            TwoBone(shoulder, elbow, point, target, pole);
        }


        /// <summary>
        /// Moves the stick, blade first, by reaching the blocker hand: the hand keeps its turn (not its wrist bend), so the
        /// stick keeps its angle and its blade stays flat. Behind the pads the bent wrist does not show.
        /// </summary>
        void ReachStickHand(Vector3 target, float facing, bool slide)
        {
            Transform shoulder = Bone(HumanBodyBones.RightUpperArm), elbow = Bone(HumanBodyBones.RightLowerArm), hand = Bone(HumanBodyBones.RightHand);
            Quaternion turn = hand.rotation;
            Vector3 wrist = target - (GearPoint(GearPiece.Stick) - hand.position);
            float reach = 0.98f * (Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position));
            Vector3 d = wrist - shoulder.position;
            if (slide && d.magnitude > reach)
            {
                float keep = Mathf.Sqrt(Mathf.Max(0f, reach * reach - d.y * d.y - d.z * d.z));
                Slide(d.x - Mathf.Sign(d.x) * keep, facing);
            }
            Vector3 forward = Root.transform.rotation * Vector3.back;
            Vector3 pole = shoulder.position + Vector3.down * 0.4f - forward * 0.2f - Vector3.right * 0.15f;
            TwoBone(shoulder, elbow, elbow.InverseTransformPoint(hand.position), wrist, pole);
            hand.rotation = turn;
        }

        /// <summary>
        /// Two-bone IK: turns upper and lower so `end` (a point carried by lower, in its own frame) lands on target, or as
        /// near as the limb reaches, the middle joint bending toward pole.
        /// </summary>
        internal static void TwoBone(Transform upper, Transform lower, Vector3 end, Vector3 target, Vector3 pole)
        {
            Vector3 a = upper.position;
            float la = Vector3.Distance(a, lower.position), lb = Vector3.Distance(lower.position, lower.TransformPoint(end));
            Vector3 toTarget = target - a;
            if (toTarget.sqrMagnitude < 1e-8f) return;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-4f);
            Vector3 dir = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole - a, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            bend.Normalize();
            float cos = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            Vector3 middle = a + la * (cos * dir + Mathf.Sqrt(1f - cos * cos) * bend);
            upper.rotation = Quaternion.FromToRotation(lower.position - a, middle - a) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(lower.TransformPoint(end) - lower.position, a + dir * d - lower.position) * lower.rotation;
        }

        /// <summary>Turns the forearm about its own length (elbow to wrist) so a direction it carries points as near `want` as it can.</summary>
        static void Twist(Transform forearm, Transform hand, Vector3 carried, Vector3 want)
        {
            Vector3 axis = (hand.position - forearm.position).normalized;
            Vector3 now = Vector3.ProjectOnPlane(forearm.TransformDirection(carried), axis), goal = Vector3.ProjectOnPlane(want, axis);
            if (now.sqrMagnitude < 1e-6f || goal.sqrMagnitude < 1e-6f) return;
            forearm.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(now, goal, axis), axis) * forearm.rotation;
        }

        /// <summary>The stick save: the blade slides along the ice to the puck (the blocker hand keeps its grip and turn).</summary>
        void ReachStick(Vector3 from, Vector3 to, float facing)
        {
            // The puck comes down from the shooter's eye, so where it passes the stick it is still above the blade: the
            // stick moves (blade on the ice, same angle) until the lower shaft is on the puck's line at that height.
            Vector3 blade = GearPoint(GearPiece.Stick);
            Vector3 shaft = (Bone(HumanBodyBones.RightHand).position - blade).normalized; // up the shaft to the grip
            Vector3 target = blade, onShaft = blade;
            for (int i = 0; i < 3; i++)
            {
                onShaft = LineAt(from, to, target.z + ShaftOffset(shaft, LineAt(from, to, target.z).y - blade.y).z);
                target = onShaft - ShaftOffset(shaft, onShaft.y - blade.y);
            }
            // Only the shaft below the blocker hand makes a stick save: a line above the grip is the blocker's, so the stick
            // stays put (#109: from the Shot start on the ice the line can pass a high corner below the knob, above the hand).
            if (onShaft.y > Bone(HumanBodyBones.RightHand).position.y) return;
            ReachHand(false, GearPiece.Stick, target, facing, true);
        }

        /// <summary>From the blade's middle up the shaft to where it is this much higher (none if the shaft is flat).</summary>
        static Vector3 ShaftOffset(Vector3 shaft, float rise) => shaft.y > 0.1f ? shaft * (Mathf.Max(0f, rise) / shaft.y) : Vector3.zero;

        /// <summary>Below this height (metres, on the puck's line at the pads) a pad save is made down in the butterfly, pads flat on the ice; above it standing.</summary>
        public const float ButterflyBelow = 0.28f;

        /// <summary>
        /// The pad save: the pad nearer the puck goes to it. Low (below ButterflyBelow) the goalie drops into the butterfly,
        /// pushed across only as far as it takes to lay that pad under the puck; higher, it stands and pushes across until the
        /// pad's middle is in front of the puck. The stick blade covers the five-hole, a little to the far side, so it is not
        /// in front of the pad. Returns the pad.
        /// </summary>
        string ReachPad(Vector3 from, Vector3 to, float facing, bool butterfly)
        {
            bool goalieLeft = to.x > Bone(HumanBodyBones.Hips).position.x; // the goalie's left is on the shooter's right
            string piece = goalieLeft ? GearPiece.LeftPad : GearPiece.RightPad;
            Vector3 centre = GearPoint(piece);
            Vector3 target = OnLine(from, to, piece);
            if (butterfly)
            {
                // Along the pad, knee to middle is its inner half: a puck there needs no push; past the middle, push to it.
                float knee = Bone(goalieLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg).position.x;
                float lo = Mathf.Min(knee, centre.x), hi = Mathf.Max(knee, centre.x);
                if (target.x < lo || target.x > hi) Slide(target.x - (target.x < lo ? lo : hi), facing);
            }
            else Slide(target.x - centre.x, facing);

            PlaceBlade(goalieLeft, FiveHoleBlades[0]);
            return piece;
        }

        /// <summary>Puts the stick blade's middle this far from the hips, away from the puck's side (the goalie's left or right).</summary>
        void PlaceBlade(bool puckOnGoalieLeft, float offset)
        {
            Vector3 blade = GearPoint(GearPiece.Stick), hips = Bone(HumanBodyBones.Hips).position;
            ReachHand(false, GearPiece.Stick, new Vector3(hips.x - (puckOnGoalieLeft ? 1f : -1f) * offset, blade.y, blade.z), 0f, false);
        }

        /// <summary>How far the stick blade's middle sits from the hips, away from the puck, in a pad save (metres): first try first.</summary>
        static readonly float[] FiveHoleBlades = { 0.1f, 0.25f, 0.4f };

        /// <summary>The body save: pushed across so the chest is in front of the puck.</summary>
        void ReachBody(Vector3 from, Vector3 to, float facing)
        {
            Vector3 target = LineAt(from, to, Bone(HumanBodyBones.Chest).position.z);
            Slide(target.x - Bone(HumanBodyBones.Chest).position.x, facing);
        }

        /// <summary>Turns the neck (head and mask with it) toward a point, at most LookDegrees, so the goalie watches the puck onto its gear.</summary>
        void Look(Vector3 point)
        {
            Transform neck = Bone(HumanBodyBones.Neck), head = Bone(HumanBodyBones.Head);
            if (neck == null || head == null) return;
            Vector3 face = Root.transform.rotation * Vector3.back, toward = point - head.position;
            if (toward.sqrMagnitude < 1e-6f) return;
            neck.rotation = Quaternion.FromToRotation(face, Vector3.RotateTowards(face, toward, LookDegrees * Mathf.Deg2Rad, 0f)) * neck.rotation;
        }

        const float LookDegrees = 20f;

        // ---- After the Shot (#43) ----------------------------------------------------------------------------------------

        Vector3 _faceLocal = Vector3.forward;  // head-local: the way the face looks in the Ready Stance
        (Vector3 from, Vector3 to, float facing, SaveReaction? beaten) _line; // the last React's puck line; beaten: a Goal or Miss reach
        Snapshot _late;                        // a GOAL!: the beaten hand carried on toward where the puck went (built after it has)

        /// <summary>The most the head turns from square to follow the puck (degrees about the vertical; neck alone).</summary>
        public const float WatchDegrees = 75f;
        /// <summary>On a GOAL! the look back into the net turns this far, the shoulders taking LookBackSpine of it.</summary>
        public const float LookBackDegrees = 125f;
        const float LookBackSpine = 0.35f;
        /// <summary>A GOAL!'s late hand gets this share of the way from where the beaten reach left it to the puck's line.</summary>
        const float LateShare = 0.85f;

        /// <summary>
        /// Shows the character as ShowReaction(set, facing), then moves it on through what it does once the puck has got there (#43,
        /// AfterShot): a GOAL!'s late reach and dropped arms; the head (on a GOAL! the shoulders too) turning to `watch`, the
        /// puck or what it looks at; and the gesture (its progress in moment). On a Save or a Miss only the neck turns, so the
        /// gear stays where the rebound was measured. The same arguments always give the same look. Nothing more before the
        /// puck gets there (a zero moment) or with set 0.
        /// </summary>
        public void ShowReaction(float set, float facing, AfterShotGesture gesture, AfterShotMoment moment, Vector3 watch)
        {
            ShowReaction(set, facing);
            if (!Present || set <= 0f) return;
            if (moment.Return > 0)
            {
                // Glides back across the crease to his spot (keeping how low he is: a butterfly comes up in the recovery).
                Vector3 at = _model.localPosition, spot = _ready.ModelPosition;
                float r = (float)moment.Return;
                _model.localPosition = new Vector3(Mathf.Lerp(at.x, spot.x, r), at.y, Mathf.Lerp(at.z, spot.z, r));
                PlaceRoot(facing);
            }
            bool goal = AfterShot.IsGoal(gesture);
            if (goal && (moment.LateReach > 0 || moment.Relax > 0))
            {
                if (_late == null) _late = LateReach();
                float late = (float)moment.LateReach, relax = (float)moment.Relax;
                for (int i = 0; i < _bones.Length; i++)
                    _bones[i].localRotation = Quaternion.Slerp(Quaternion.Slerp(_bones[i].localRotation, _late.Rotations[i], late), _ready.Rotations[i], relax);
                PlaceRoot(facing);
            }
            if (moment.Watch > 0) GoalieGestures.Watch(this, _faceLocal, watch, (float)moment.Watch, goal ? LookBackDegrees : WatchDegrees, goal ? LookBackSpine : 0f);
            if (moment.Gesture > 0) GoalieGestures.Gesture(this, gesture, (float)moment.Gesture);
            if (goal) KeepBladeUp();
            if (gesture == AfterShotGesture.None && moment.FollowThrough > 0) FollowThrough((float)moment.FollowThrough, facing);
            if (moment.Recover > 0)
            {
                // Back up into the Ready Stance: at 1 exactly the Ready Stance on his spot, as ShowReaction(0, facing) shows it.
                float r = (float)moment.Recover;
                for (int i = 0; i < _bones.Length; i++)
                    _bones[i].localRotation = Quaternion.Slerp(_bones[i].localRotation, _ready.Rotations[i], r);
                _model.localPosition = Vector3.Lerp(_model.localPosition, _ready.ModelPosition, r);
                PlaceRoot(facing);
            }
        }

        /// <summary>How far a glove save's glove comes up to show the puck, and a blocker save's blocker follows through out (metres).</summary>
        public const float GloveHoldUp = 0.1f, BlockerFollowThrough = 0.08f;
        /// <summary>Which way the blocker follows through, in the goalie's frame: mostly toward his right, a little up and forward.</summary>
        const float FollowRight = 0.85f, FollowUp = 0.3f, FollowForward = 0.3f;

        /// <summary>
        /// A Save, once the puck is off the gear: the catching glove comes up with the puck in it (GoalieView carries it), the
        /// blocker carries on out toward the corner. Other saves keep their gear still (the rebound was measured on it).
        /// </summary>
        void FollowThrough(float p, float facing)
        {
            if (ReactionPiece == GearPiece.Catcher)
                ReachHand(true, GearPiece.Catcher, GearPoint(GearPiece.Catcher) + Vector3.up * (GloveHoldUp * p), facing, false);
            else if (ReactionPiece == GearPiece.Blocker)
            {
                Vector3 goalieRight = Root.transform.rotation * Vector3.left, forward = Root.transform.rotation * Vector3.back;
                ReachHand(false, GearPiece.Blocker, GearPoint(GearPiece.Blocker) + (goalieRight * FollowRight + Vector3.up * FollowUp + forward * FollowForward).normalized * (BlockerFollowThrough * p), facing, false);
            }
        }

        // ---- Stance Life (#44) -------------------------------------------------------------------------------------------

        /// <summary>
        /// Adds Stance Life (StanceLife) to the pose shown, `weight` of it (0 none, 1 full): the chest rises with a breath, the
        /// hips move over one skate with the skates kept where they are, and the stick arm lifts the blade for a tap
        /// (GoalieStanceLife). Leaves the Root (facing, spot) alone.
        /// </summary>
        public void Live(StanceLifeMoment life, float weight)
        {
            if (Present) GoalieStanceLife.Apply(this, _model, life, weight);
        }

        // ---- Skate push (#126) -------------------------------------------------------------------------------------------

        /// <summary>
        /// Adds the skate push to the pose shown (GoalieSkatePush): the far-side skate plants out and back, and on a T-push the lead
        /// skate turns toward the move. Leg and foot bones only; the body, the Root and the hands are left alone. Does nothing
        /// without a model. Call after ShowReaction (it sets the legs again).
        /// </summary>
        public void ShowPush(PushShown shown, PushPoseMoment moment)
        {
            if (Present) GoalieSkatePush.Apply(this, shown, moment);
        }

        /// <summary>How far the last React slid the body across (metres, + toward the goalie's left in model space): the move the push shows. 0 before any React or without a model.</summary>
        public float ReactionSlideMetres => Present ? _reaction.ModelPosition.x - _ready.ModelPosition.x : 0f;

        // ---- Before the Shot (#70, BeforeTheShot) ------------------------------------------------------------------------

        /// <summary>
        /// Sits the pose shown `metres` lower: the body comes down with the skates kept where they are on the ice (the knees
        /// bend more) and the stick blade kept on it (the stick hand lifts it back, KeepBladeUp). Shown only: the race measures the Ready
        /// Stance itself.
        /// </summary>
        public void SitLower(float metres)
        {
            if (!Present || metres <= 0f) return;
            Prepare();
            GoalieStanceLife.SitLower(this, _model, metres);
            KeepBladeUp();
        }

        /// <summary>Turns the head toward the puck at `point`, `weight` of the way (at most WatchDegrees), the shoulders taking shoulderShare of the turn.</summary>
        public void WatchPuck(Vector3 point, float weight, float shoulderShare)
        {
            if (Present && weight > 0f) GoalieGestures.Watch(this, _faceLocal, point, weight, WatchDegrees, shoulderShare);
        }

        float _bladeY; // the stick's lowest point in the Ready Stance (Prepare)

        /// <summary>The lowest corner of a piece's measured box as it stands now (world y).</summary>
        float Lowest(string piece)
        {
            GearShape gear = _gearShapes[piece];
            Matrix4x4 m = Rigid(gear.Transform);
            Bounds b = gear.Local;
            float lowest = float.MaxValue;
            for (int i = 0; i < 8; i++)
                lowest = Mathf.Min(lowest, m.MultiplyPoint3x4(new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)).y);
            return lowest;
        }

        /// <summary>
        /// A GOAL!'s bend forward (or sitting lower, SitLower) would push the stick blade into the ice: the stick hand lifts it back to its stance height,
        /// keeping its turn (only up, so a lifted stick is left alone).
        /// </summary>
        void KeepBladeUp()
        {
            if (!_gearShapes.ContainsKey(GearPiece.Stick)) return;
            float low = _bladeY - Lowest(GearPiece.Stick);
            if (low > 0.002f) ReachStickHand(GearPoint(GearPiece.Stick) + Vector3.up * low, 0f, false); // the hand keeps its turn: the stick only rises
        }

        /// <summary>The reaction with its beaten hand carried on toward the puck's line, too late (the arm alone: no push).</summary>
        Snapshot LateReach()
        {
            Snapshot shown = Take();
            Put(_reaction);
            PlaceRoot(_line.facing);
            if (_line.beaten is SaveReaction beaten && SaveReactions.IsHand(beaten))
            {
                bool glove = beaten == SaveReaction.Glove;
                string piece = GearPiece.HandOf(beaten);
                ReachHand(glove, piece, Vector3.Lerp(GearPoint(piece), OnLine(_line.from, _line.to, piece), LateShare), _line.facing, false);
            }
            Snapshot late = Take();
            Put(shown);
            return late;
        }

        /// <summary>Adds the reaction's gear and body, as the last React left them, to what a puck can hit (only placing).</summary>
        public void AddReaction(GoalieShape obstacles)
        {
            if (!Present || _placed.Count == 0) return;
            foreach (var (shape, toWorld) in _placed) obstacles.AddPlaced(shape, toWorld);
        }

        /// <summary>World bounds of a piece of gear as the last React left it, or false without one.</summary>
        public bool ReactionGearBounds(string piece, out Bounds bounds)
        {
            bounds = default;
            if (!Present || !_placedGear.TryGetValue(piece, out Matrix4x4 toWorld)) return false;
            Bounds b = _gearShapes[piece].Local;
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
        /// Adds the gear and body as posed now, in world space, to what a puck can hit. Each gear piece is named as in
        /// GearBounds (GearPiece); the body and clothes are GearPiece.Body. Does nothing without a model.
        /// </summary>
        public void AddTo(GoalieShape obstacles)
        {
            if (!Present) return;
            foreach (MeshRenderer r in _gear)
            {
                string piece = GearPiece.PieceOf(r.name);
                Mesh shared = r.GetComponent<MeshFilter>().sharedMesh;
                obstacles.AddMesh(ToWorld(shared.vertices, r.transform.localToWorldMatrix), shared.triangles, piece);
            }
            var mesh = new Mesh();
            for (int part = 0; part < _body.Length; part++)
            {
                SkinnedMeshRenderer body = _body[part];
                body.BakeMesh(mesh, true);
                Matrix4x4 toWorld = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                obstacles.AddMesh(ToWorld(mesh.vertices, toWorld), WithoutBareHands(body, _bodyTriangles[part]), GearPiece.Body);
            }
            SceneObjects.DestroySafely(mesh);
        }

        /// <summary>The triangles, less the bare hands' (inside the catcher and blocker) when this is the bare skin.</summary>
        int[] WithoutBareHands(SkinnedMeshRenderer body, int[] triangles)
        {
            BoneWeight[] weights = body.sharedMesh.boneWeights;
            if (body.name != BareSkin || weights.Length == 0) return triangles;
            var kept = new List<int>(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Transform bone = body.bones[weights[triangles[i]].boneIndex0];
                if (bone != null && InHand(bone)) continue;
                kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]);
            }
            return kept.ToArray();
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
