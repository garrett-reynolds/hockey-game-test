using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class GoalieCharacterTests
    {
        static readonly string[] GearNames =
            { "Left Pad", "Right Pad", "Blocker", "Catcher", "Stick", "Mask", "Left Skate", "Right Skate" };
        static readonly string[] ClothesNames = { "Jersey", "Pants", "Socks" };

        static GameObject LoadModel()
        {
            var prefab = Resources.Load<GameObject>(ModelFiles.Folder + "/" + ModelFiles.GoalieDressing);
            Assert.IsNotNull(prefab, "Assets/Resources/Models/goalie-dressing.fbx is missing");
            return Object.Instantiate(prefab);
        }

        [Test]
        public void The_goalie_model_imports_as_a_valid_humanoid_with_its_real_bones_mapped()
        {
            var model = LoadModel();
            try
            {
                var animator = model.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator, "no Animator: the model is not imported as Humanoid");
                Assert.IsNotNull(animator.avatar, "no avatar");
                Assert.IsTrue(animator.avatar.isValid, "avatar is not valid");
                Assert.IsTrue(animator.avatar.isHuman, "avatar is not humanoid");
                Assert.IsTrue(animator.isHuman);

                // The rig has a "root" bone under the pelvis's parent; the pelvis must be the Hips.
                var expected = new (HumanBodyBones bone, string name)[]
                {
                    (HumanBodyBones.Hips, "pelvis"), (HumanBodyBones.Spine, "spine_01"), (HumanBodyBones.Chest, "spine_02"),
                    (HumanBodyBones.UpperChest, "spine_03"), (HumanBodyBones.Neck, "neck_01"), (HumanBodyBones.Head, "Head"),
                    (HumanBodyBones.LeftShoulder, "clavicle_l"), (HumanBodyBones.RightShoulder, "clavicle_r"),
                    (HumanBodyBones.LeftUpperArm, "upperarm_l"), (HumanBodyBones.RightUpperArm, "upperarm_r"),
                    (HumanBodyBones.LeftLowerArm, "lowerarm_l"), (HumanBodyBones.RightLowerArm, "lowerarm_r"),
                    (HumanBodyBones.LeftHand, "hand_l"), (HumanBodyBones.RightHand, "hand_r"),
                    (HumanBodyBones.LeftUpperLeg, "thigh_l"), (HumanBodyBones.RightUpperLeg, "thigh_r"),
                    (HumanBodyBones.LeftLowerLeg, "calf_l"), (HumanBodyBones.RightLowerLeg, "calf_r"),
                    (HumanBodyBones.LeftFoot, "foot_l"), (HumanBodyBones.RightFoot, "foot_r"),
                };
                foreach (var (bone, name) in expected)
                {
                    Transform t = animator.GetBoneTransform(bone);
                    Assert.IsNotNull(t, bone + " is not mapped");
                    Assert.AreEqual(name, t.name, bone + " is mapped to the wrong bone");
                }
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void The_goalie_model_is_life_size_about_1_8_m_from_skate_blades_to_mask_top()
        {
            var model = LoadModel();
            try
            {
                Bounds all = WorldBounds(model.GetComponentsInChildren<Renderer>(true));
                Assert.That(all.size.y, Is.InRange(1.75f, 1.95f), "standing height in T-pose, skates and mask included");
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void The_whole_goalie_stays_under_80k_triangles()
        {
            var model = LoadModel();
            try
            {
                int triangles = 0;
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
                    for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3;
                }
                Assert.Less(triangles, 80000);
                Assert.Greater(triangles, 10000, "a lifelike goalie, not a placeholder");
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_parent); }

        [Test]
        public void With_no_goalie_model_there_is_no_character_and_showing_it_does_nothing()
        {
            var character = new GoalieCharacter(_parent.transform, name => null);
            Assert.IsFalse(character.Present);
            Assert.IsNull(character.Animator);
            Assert.IsNull(character.Bone(HumanBodyBones.Head));
            character.Show(true);
            Assert.AreEqual(0, _parent.GetComponentsInChildren<Renderer>(true).Length, "nothing is drawn without the model");
        }

        [Test]
        public void The_character_is_hidden_until_shown_and_its_rig_is_reachable_for_posing()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assert.IsTrue(character.Present);
            Assert.IsTrue(character.Animator.isHuman);
            Assert.AreEqual("calf_l", character.Bone(HumanBodyBones.LeftLowerLeg).name);
            Assert.IsFalse(character.Root.activeInHierarchy, "hidden by default");
            character.Show(true);
            Assert.IsTrue(character.Root.activeInHierarchy);
            character.Show(false);
            Assert.IsFalse(character.Root.activeInHierarchy);
        }

        [Test]
        public void The_character_wears_jersey_pants_socks_and_all_the_gear()
        {
            var character = new GoalieCharacter(_parent.transform);
            foreach (string clothes in ClothesNames)
                Assert.IsNotNull(Find(character.Root.transform, clothes)?.GetComponent<SkinnedMeshRenderer>(), "no " + clothes);
            foreach (string gear in GearNames)
            {
                Assert.IsNotNull(Find(character.Root.transform, "Gear " + gear)?.GetComponent<Renderer>(), "no " + gear);
                int count = 0;
                foreach (Transform t in character.Root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Gear " + gear) count++;
                Assert.AreEqual(1, count, "one " + gear + ", not a second copy");
            }
        }

        [Test]
        public void The_jersey_is_in_the_blue_team_colour()
        {
            var character = new GoalieCharacter(_parent.transform);
            Material jersey = Find(character.Root.transform, "Jersey").GetComponent<Renderer>().sharedMaterial;
            Assert.IsNull(jersey.mainTexture, "plain cloth colour, not a repainted skin texture");
            Assert.Greater(jersey.color.b, jersey.color.r + 0.3f, "blue");
        }

        [Test]
        public void The_character_keeps_its_life_size()
        {
            var character = new GoalieCharacter(_parent.transform);
            float leg = Vector3.Distance(character.Bone(HumanBodyBones.LeftUpperLeg).position, character.Bone(HumanBodyBones.LeftLowerLeg).position)
                + Vector3.Distance(character.Bone(HumanBodyBones.LeftLowerLeg).position, character.Bone(HumanBodyBones.LeftFoot).position);
            Assert.That(leg, Is.InRange(0.8f, 0.95f), "hip to ankle of a 1.8 m man");
        }

        [Test]
        public void Every_material_is_URP_and_the_skin_mask_and_skates_keep_their_textures()
        {
            var character = new GoalieCharacter(_parent.transform);
            foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                {
                    Assert.IsNotNull(m, r.name + " has an empty material slot");
                    StringAssert.StartsWith("Universal Render Pipeline/", m.shader.name, r.name + " would draw pink");
                }
            foreach (string textured in new[] { "Skin", "Gear Mask", "Gear Left Skate", "Gear Right Skate" })
                Assert.IsNotNull(Find(character.Root.transform, textured).GetComponent<Renderer>().sharedMaterial.mainTexture, textured + " lost its texture");
        }

        [Test]
        public void In_the_ready_stance_the_goalie_stands_on_the_ice_in_front_of_the_net_and_about_fills_its_height()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            Bounds skates = WorldBounds(Gear(character, "Left Skate"), Gear(character, "Right Skate"));
            Assert.AreEqual(0f, skates.min.y, 0.02f, "skate blades on the ice");
            Bounds body = character.BodyBounds();
            // A 1.8 m goalie crouched in the ready stance is about 1.25-1.5 m tall: just over the 1.22 m crossbar.
            Bounds mask = WorldBounds(Gear(character, "Mask"));
            Assert.That(mask.max.y, Is.InRange(1.25f, 1.5f), "a crouched lifelike goalie about fills the 1.22 m net");
            Assert.AreEqual(0f, body.center.x, 0.1f, "centred on the net");
            Assert.Less(body.max.z, -0.13f, "in front of the Net Face and the goalie blocks (z -0.13 to -0.03)");
            Assert.Less(WorldBounds(Gear(character, "Left Pad"), Gear(character, "Right Pad")).min.y, 0.12f, "pads reach down to the skates");
            Bounds stick = WorldBounds(Gear(character, "Stick"));
            Assert.That(stick.min.y, Is.InRange(-0.03f, 0.08f), "stick blade down on the ice");
        }

        [Test]
        public void The_goalie_faces_the_shooter()
        {
            var character = new GoalieCharacter(_parent.transform);
            foreach (var (skate, foot) in new[] { ("Left Skate", HumanBodyBones.LeftFoot), ("Right Skate", HumanBodyBones.RightFoot) })
                Assert.Less(WorldBounds(Gear(character, skate)).center.z, character.Bone(foot).position.z - 0.02f, skate + " toe points at the shooter (-z)");
            // The goalie's left hand (catcher) is on the shooter's right (+x), as for a goalie facing you.
            Assert.Greater(character.Bone(HumanBodyBones.LeftHand).position.x, character.Bone(HumanBodyBones.RightHand).position.x);
        }

        [Test]
        public void The_ready_stance_bends_the_knees_and_hips()
        {
            var character = new GoalieCharacter(_parent.transform);
            foreach (var (upper, lower, foot) in new[]
            {
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            })
            {
                Vector3 hip = character.Bone(upper).position, knee = character.Bone(lower).position, ankle = character.Bone(foot).position;
                Assert.Greater(Vector3.Angle(knee - hip, ankle - knee), 20f, lower + " is not bent");
                Assert.Less(knee.z, hip.z - 0.03f, lower + " should be forward of the hip");
            }
        }

        [Test]
        public void Each_piece_of_gear_follows_every_joint_above_it()
        {
            var character = new GoalieCharacter(_parent.transform);
            HumanBodyBones[] rightArm = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand };
            var cases = new (string gear, HumanBodyBones[] joints)[]
            {
                ("Left Pad", new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg }),
                ("Right Pad", new[] { HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg }),
                ("Left Skate", new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot }),
                ("Right Skate", new[] { HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot }),
                ("Blocker", rightArm),
                ("Stick", rightArm),
                ("Catcher", new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand }),
                ("Mask", new[] { HumanBodyBones.Neck, HumanBodyBones.Head }),
            };
            foreach (var (gearName, joints) in cases)
            {
                Transform gear = Find(character.Root.transform, "Gear " + gearName);
                Assert.IsNotNull(gear, "no " + gearName);
                foreach (HumanBodyBones joint in joints)
                {
                    Transform bone = character.Bone(joint);
                    Quaternion before = gear.rotation, jointBefore = bone.localRotation;
                    bone.localRotation *= Quaternion.Euler(25f, 0f, 0f);
                    Assert.Greater(Quaternion.Angle(before, gear.rotation), 20f, gearName + " does not follow " + joint);
                    bone.localRotation = jointBefore;
                }
            }
        }

        [Test]
        public void The_clothes_bend_with_the_joints_under_them()
        {
            var character = new GoalieCharacter(_parent.transform);
            foreach (var (clothes, joint) in new[] { ("Jersey", HumanBodyBones.LeftLowerArm), ("Pants", HumanBodyBones.LeftUpperLeg), ("Socks", HumanBodyBones.LeftLowerLeg) })
            {
                var skin = Find(character.Root.transform, clothes).GetComponent<SkinnedMeshRenderer>();
                Bounds before = WorldBounds(skin);
                Transform bone = character.Bone(joint);
                Quaternion jointBefore = bone.localRotation;
                bone.localRotation *= Quaternion.Euler(60f, 0f, 0f);
                Bounds after = WorldBounds(skin);
                bone.localRotation = jointBefore;
                Assert.Greater((after.min - before.min).magnitude + (after.max - before.max).magnitude, 0.05f, clothes + " does not bend with " + joint);
            }
        }

        static Renderer Gear(GoalieCharacter character, string name) =>
            Find(character.Root.transform, "Gear " + name).GetComponent<Renderer>();

        /// <summary>World bounds of the renderers' actual vertices (skinned meshes as posed now).</summary>
        static Bounds WorldBounds(params Renderer[] renderers)
        {
            var points = new List<Vector3>();
            var baked = new Mesh();
            foreach (Renderer r in renderers)
            {
                if (r is SkinnedMeshRenderer s)
                {
                    s.BakeMesh(baked, true);
                    Matrix4x4 m = Matrix4x4.TRS(s.transform.position, s.transform.rotation, Vector3.one);
                    foreach (Vector3 v in baked.vertices) points.Add(m.MultiplyPoint3x4(v));
                }
                else
                {
                    Matrix4x4 m = r.transform.localToWorldMatrix;
                    foreach (Vector3 v in r.GetComponent<MeshFilter>().sharedMesh.vertices) points.Add(m.MultiplyPoint3x4(v));
                }
            }
            Object.DestroyImmediate(baked);
            Assert.IsNotEmpty(points, "no vertices");
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 p in points) bounds.Encapsulate(p);
            return bounds;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
