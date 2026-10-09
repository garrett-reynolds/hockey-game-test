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
    }
}
