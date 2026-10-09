using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame
{
    /// <summary>
    /// The rigged goalie character: a Humanoid model (Assets/Resources/Models/goalie-dressing) in a goalie jersey skin,
    /// with pads, blocker, catcher and mask parented to its bones so they follow every joint. It stands at the net in a
    /// neutral ready stance and is hidden until shown. It decides nothing: the GoalieView blocks stay the gameplay look.
    /// To pose it, use Animator (Humanoid, no controller, so nothing overwrites the bones) or Bone(...).
    /// With no model file, Present is false and the character draws nothing.
    /// </summary>
    public sealed class GoalieCharacter
    {
        /// <summary>Where the goalie stands: centred on the net, this far in front of the Net Face (metres, -z is toward the shooter).</summary>
        public const float StandZ = -0.4f;
        /// <summary>Top of the head when standing straight, metres. The ready stance crouches to about the 1.22 m net height.</summary>
        const float StandingHeight = 1.3f;
        const string SkinTexture = "goalie-skin";
        static readonly Color GearWhite = new Color(0.92f, 0.92f, 0.9f);

        SkinnedMeshRenderer[] _body = new SkinnedMeshRenderer[0];

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
            Skin();

            FaceTheShooter(model.transform);
            ScaleTo(model.transform, StandingHeight);
            ReadyStance();
            Dress(findFile);
            StandOnIce(model.transform);
        }

        /// <summary>The bone for a humanoid body part, or null when there is no model.</summary>
        public Transform Bone(HumanBodyBones bone) => Animator != null ? Animator.GetBoneTransform(bone) : null;

        public void Show(bool visible)
        {
            if (Root.activeSelf != visible) Root.SetActive(visible);
        }

        /// <summary>World-space bounds of the body as posed now (gear not included).</summary>
        public Bounds BodyBounds()
        {
            bool any = false;
            var bounds = new Bounds();
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer body in _body)
            {
                body.BakeMesh(mesh);
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
        /// Neutral ready stance: knees bent over the toes, hips back, chest leaning in, head up, catcher and blocker
        /// out in front at the sides. Each limb is aimed in the goalie's own frame (forward = toward the shooter), so it
        /// can be re-applied from any pose. #27 can pose the rig the same way (Aim) or through the Humanoid Animator.
        /// </summary>
        public void ReadyStance()
        {
            if (!Present) return;
            Transform frame = Root.transform;
            Vector3 forward = frame.rotation * Vector3.back, up = frame.up, left = frame.rotation * Vector3.right; // the goalie's own left

            Aim(HumanBodyBones.Spine, HumanBodyBones.Neck, up * 0.9f + forward * 0.35f);
            Aim(HumanBodyBones.Neck, HumanBodyBones.Head, up + forward * 0.1f);
            foreach (float side in new[] { 1f, -1f })
            {
                bool l = side > 0f;
                Vector3 outward = left * side;
                Aim(l ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg, l ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                    -up * 0.4f + forward * 0.9f + outward * 0.25f);
                Aim(l ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, l ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
                    -up * 0.9f - forward * 0.3f + outward * 0.1f);
                Aim(l ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, l ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes,
                    forward - up * 0.15f + outward * 0.15f);
                Aim(l ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, l ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                    -up * 0.7f + outward * 0.55f + forward * 0.35f);
                Aim(l ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, l ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
                    forward * 0.8f + outward * 0.45f - up * 0.15f);
            }
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

        void Skin()
        {
            var texture = Resources.Load<Texture2D>(ModelFiles.Folder + "/" + SkinTexture);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (SkinnedMeshRenderer body in _body)
            {
                var material = new Material(lit != null ? lit : body.sharedMaterial.shader) { name = "Goalie Skin" };
                if (texture != null) material.mainTexture = texture; // _BaseMap in URP Lit
                material.color = Color.white;
                body.sharedMaterial = material;
            }
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

        void ScaleTo(Transform model, float height)
        {
            Bounds b = BodyBounds();
            if (b.size.y <= 0f) return;
            model.localScale *= height / b.size.y;
        }

        void StandOnIce(Transform model)
        {
            Bounds b = BodyBounds();
            Vector3 hips = Bone(HumanBodyBones.Hips).position;
            Vector3 root = Root.transform.position;
            model.position += new Vector3(root.x - hips.x, root.y - b.min.y, 0f);
        }

        // ---- gear: placed in the ready stance (facing the shooter), then parented to bones so any later pose carries it ----

        void Dress(Func<string, GameObject> findFile)
        {
            Vector3 forward = Vector3.back; // toward the shooter
            foreach (var (side, upper, lower, foot) in new[]
            {
                ("Left", HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
                ("Right", HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            })
            {
                Vector3 knee = Bone(lower).position, ankle = Bone(foot).position;
                float shin = Vector3.Distance(knee, ankle);
                Vector3 up = (knee - ankle).normalized;
                Vector3 centre = Vector3.Lerp(knee, ankle, 0.6f) + forward * (0.25f * shin);
                Attach(side + " Pad", ModelFiles.GoaliePad, findFile, PrimitiveType.Cube, Bone(lower),
                    centre, Quaternion.LookRotation(Vector3.ProjectOnPlane(-forward, up), up), new Vector3(0.8f, 1.75f, 0.38f) * shin);
            }

            float forearm = Vector3.Distance(Bone(HumanBodyBones.RightLowerArm).position, Bone(HumanBodyBones.RightHand).position);
            Transform rightHand = Bone(HumanBodyBones.RightHand), leftHand = Bone(HumanBodyBones.LeftHand);
            Attach("Blocker", ModelFiles.GoalieBlocker, findFile, PrimitiveType.Cube, rightHand,
                rightHand.position + forward * (0.1f * forearm) + Vector3.left * (0.3f * forearm), Quaternion.LookRotation(-forward, Vector3.up),
                new Vector3(0.95f, 1.6f, 0.2f) * forearm);
            Attach("Catcher", ModelFiles.SaveGlove, findFile, PrimitiveType.Sphere, leftHand,
                leftHand.position + forward * (0.1f * forearm) + Vector3.right * (0.25f * forearm), Quaternion.LookRotation(-forward, Vector3.up),
                new Vector3(1.3f, 1.35f, 0.5f) * forearm);

            // The mask is a helmet shell over the front of the head, its cage just in front of the face.
            Bounds head = HeadBounds();
            float maskDepth = head.size.z * 0.8f;
            Attach("Mask", ModelFiles.GoalieMask, findFile, PrimitiveType.Sphere, Bone(HumanBodyBones.Head),
                head.center + forward * (head.size.z * 0.5f + 0.015f - maskDepth / 2f), Quaternion.LookRotation(-forward, Vector3.up),
                new Vector3(head.size.x * 1.2f, head.size.y * 1.05f, maskDepth));
        }

        void Attach(string name, string fileName, Func<string, GameObject> findFile, PrimitiveType placeholder,
            Transform bone, Vector3 position, Quaternion rotation, Vector3 size)
        {
            var pivot = new GameObject("Gear " + name);
            pivot.transform.SetPositionAndRotation(position, rotation);
            GameObject piece = ModelFiles.Load(fileName, () =>
            {
                var shape = GameObject.CreatePrimitive(placeholder);
                shape.name = name + " Placeholder";
                RinkBuilder.Paint(shape, GearWhite);
                return shape;
            }, findFile);
            piece.transform.SetParent(pivot.transform, false);
            ModelFiles.FitTo(piece.transform, size);
            ModelFiles.RemoveColliders(piece);
            pivot.transform.SetParent(bone, true);
        }

        /// <summary>World bounds of the vertices mostly weighted to the head bone, in the rest pose.</summary>
        Bounds HeadBounds()
        {
            Transform headBone = Bone(HumanBodyBones.Head);
            bool any = false;
            var bounds = new Bounds(headBone.position, Vector3.zero);
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer body in _body)
            {
                int head = Array.IndexOf(body.bones, headBone);
                if (head < 0 || body.sharedMesh == null) continue;
                BoneWeight[] weights = body.sharedMesh.boneWeights;
                body.BakeMesh(mesh);
                Vector3[] vertices = mesh.vertices;
                Matrix4x4 toWorld = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                for (int i = 0; i < vertices.Length && i < weights.Length; i++)
                {
                    BoneWeight w = weights[i];
                    float onHead = (w.boneIndex0 == head ? w.weight0 : 0f) + (w.boneIndex1 == head ? w.weight1 : 0f)
                        + (w.boneIndex2 == head ? w.weight2 : 0f) + (w.boneIndex3 == head ? w.weight3 : 0f);
                    if (onHead < 0.5f) continue;
                    Vector3 p = toWorld.MultiplyPoint3x4(vertices[i]);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            Destroy(mesh);
            return bounds;
        }

        static void Destroy(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
