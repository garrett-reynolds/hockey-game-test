using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class ModelFilesTests
    {
        [Test]
        public void A_model_that_is_present_is_used_instead_of_the_placeholder()
        {
            var real = new GameObject("real puck");
            var model = ModelFiles.Load(ModelFiles.Puck, () => new GameObject("placeholder puck"), name => name == "puck" ? real : null);
            Assert.AreSame(real, model);
            Object.DestroyImmediate(model);
        }

        [Test]
        public void With_no_model_the_placeholder_is_used()
        {
            var model = ModelFiles.Load(ModelFiles.Puck, () => new GameObject("placeholder puck"), name => null);
            Assert.AreEqual("placeholder puck", model.name);
            Object.DestroyImmediate(model);
        }

        [Test]
        public void A_name_with_no_file_in_Resources_Models_falls_back_to_the_placeholder()
        {
            var model = ModelFiles.Load("no-such-model-file", () => new GameObject("placeholder net"));
            Assert.IsNotNull(model);
            Assert.AreEqual("placeholder net", model.name);
            Object.DestroyImmediate(model);
        }
    }
}
