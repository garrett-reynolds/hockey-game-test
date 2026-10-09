using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class GoalieCharacterTests
    {
        [Test]
        public void The_goalie_model_imports_as_a_valid_humanoid_with_its_real_bones_mapped()
        {
            var prefab = Resources.Load<GameObject>(ModelFiles.Folder + "/" + ModelFiles.GoalieDressing);
            Assert.IsNotNull(prefab, "Assets/Resources/Models/goalie-dressing.fbx is missing");
            var model = Object.Instantiate(prefab);
            try
            {
                var animator = model.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator, "no Animator: the model is not imported as Humanoid");
                Assert.IsNotNull(animator.avatar, "no avatar");
                Assert.IsTrue(animator.avatar.isValid, "avatar is not valid");
                Assert.IsTrue(animator.avatar.isHuman, "avatar is not humanoid");
                Assert.IsTrue(animator.isHuman);

                // The Kenney rig also has HipsCtrl, LeftFootIK, LeftKneeCtrl... The real bones must be the mapped ones.
                var expected = new (HumanBodyBones bone, string name)[]
                {
                    (HumanBodyBones.Hips, "Hips"), (HumanBodyBones.Spine, "Spine"), (HumanBodyBones.Chest, "Chest"),
                    (HumanBodyBones.Neck, "Neck"), (HumanBodyBones.Head, "Head"),
                    (HumanBodyBones.LeftShoulder, "LeftShoulder"), (HumanBodyBones.RightShoulder, "RightShoulder"),
                    (HumanBodyBones.LeftUpperArm, "LeftArm"), (HumanBodyBones.RightUpperArm, "RightArm"),
                    (HumanBodyBones.LeftLowerArm, "LeftForeArm"), (HumanBodyBones.RightLowerArm, "RightForeArm"),
                    (HumanBodyBones.LeftHand, "LeftHand"), (HumanBodyBones.RightHand, "RightHand"),
                    (HumanBodyBones.LeftUpperLeg, "LeftUpLeg"), (HumanBodyBones.RightUpperLeg, "RightUpLeg"),
                    (HumanBodyBones.LeftLowerLeg, "LeftLeg"), (HumanBodyBones.RightLowerLeg, "RightLeg"),
                    (HumanBodyBones.LeftFoot, "LeftFoot"), (HumanBodyBones.RightFoot, "RightFoot"),
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
            Assert.AreEqual("LeftLeg", character.Bone(HumanBodyBones.LeftLowerLeg).name);
            Assert.IsFalse(character.Root.activeInHierarchy, "hidden by default");
            character.Show(true);
            Assert.IsTrue(character.Root.activeInHierarchy);
            character.Show(false);
            Assert.IsFalse(character.Root.activeInHierarchy);
        }

        [Test]
        public void The_character_wears_the_goalie_jersey_skin_and_every_material_is_URP()
        {
            var character = new GoalieCharacter(_parent.transform);
            var body = character.Root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual("goalie-skin", body.sharedMaterial.mainTexture.name);
            foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    StringAssert.StartsWith("Universal Render Pipeline/", m.shader.name, r.name + " would draw pink");
        }

        [Test]
        public void In_the_ready_stance_the_goalie_stands_on_the_ice_in_front_of_the_net_and_about_fills_its_height()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            Bounds body = character.BodyBounds();
            Assert.AreEqual(0f, body.min.y, 0.05f, "skates on the ice");
            Assert.That(body.max.y, Is.InRange(1.0f, 1.35f), "a crouched goalie about fills the 1.22 m net");
            Assert.AreEqual(0f, body.center.x, 0.1f, "centred on the net");
            Assert.Less(body.max.z, -0.13f, "in front of the Net Face and the goalie blocks (z -0.13 to -0.03)");
        }

        [Test]
        public void The_goalie_faces_the_shooter()
        {
            var character = new GoalieCharacter(_parent.transform);
            foreach (var (foot, toes) in new[] { (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes), (HumanBodyBones.RightFoot, HumanBodyBones.RightToes) })
                Assert.Less(character.Bone(toes).position.z, character.Bone(foot).position.z - 0.02f, "toes point at the shooter (-z)");
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
            var cases = new (string gear, HumanBodyBones[] joints)[]
            {
                ("Left Pad", new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg }),
                ("Right Pad", new[] { HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg }),
                ("Blocker", new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand }),
                ("Catcher", new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand }),
                ("Mask", new[] { HumanBodyBones.Neck, HumanBodyBones.Head }),
            };
            foreach (var (gearName, joints) in cases)
            {
                Transform gear = Find(character.Root.transform, "Gear " + gearName);
                Assert.IsNotNull(gear, "no " + gearName);
                Assert.IsNotNull(gear.GetComponentInChildren<Renderer>(), gearName + " draws nothing");
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
        public void With_no_gear_models_simple_shapes_stand_in_for_the_gear()
        {
            var character = new GoalieCharacter(_parent.transform,
                name => name == ModelFiles.GoalieDressing ? Object.Instantiate(Resources.Load<GameObject>(ModelFiles.Folder + "/" + name)) : null);
            foreach (string gearName in new[] { "Left Pad", "Right Pad", "Blocker", "Catcher", "Mask" })
                Assert.IsNotNull(Find(character.Root.transform, "Gear " + gearName).GetComponentInChildren<Renderer>(), gearName);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
