using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame
{
    /// <summary>
    /// The rigged goalie character: a life-size Humanoid model (Assets/Resources/Models/goalie-dressing.fbx, built by
    /// Tools/make_lifelike_goalie.py) wearing jersey, pants and socks that bend with its skin, and gear (pads, blocker,
    /// catcher, stick, mask, skates) parented to its bones in the file, so the gear follows every joint.
    /// It stands at the net in a neutral ready stance and is hidden until shown. It decides nothing: the GoalieView blocks
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
                Destroy(model);
                return;
            }
            Animator = animator;
            animator.runtimeAnimatorController = null; // bones are posed from code
            model.transform.SetParent(Root.transform, false);
            _body = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _skates = Array.FindAll(model.GetComponentsInChildren<Renderer>(true), r => r.name == "Gear Left Skate" || r.name == "Gear Right Skate");

            FaceTheShooter(model.transform);
            CaptureRest();
            ReadyStance();
            StandOnIce(model.transform);
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
            Destroy(mesh);
            return bounds;
        }

        /// <summary>
        /// Neutral ready stance: wide stance with the knees bent over the skates, hips back, chest leaning in, head up to
        /// the shooter, skates flat on the ice. Blocker hand low at the side holding the stick: back of the hand (the
        /// blocker board) to the shooter, thumb up, so the stick runs down to the ice. Catcher out at the side, palm (the
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
            // Blocker (right hand): low at the side, board to the shooter, stick down to the ice in front of the pads.
            Aim(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, -up * 0.6f - left * 0.45f + forward * 0.35f);
            Aim(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, forward * 0.9f - left * 0.15f - up * 0.05f);
            Orient(HumanBodyBones.RightHand, left * 0.8f + forward * 0.3f, up - forward * 0.5f); // stick slants down and forward
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

        static void Destroy(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
