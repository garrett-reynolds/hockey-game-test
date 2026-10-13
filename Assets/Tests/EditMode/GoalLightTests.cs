using System.Linq;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class GoalLightTests
    {
        const float CrossbarTop = 1.22f + 0.08f; // Net Face height plus the crossbar

        Transform _rink;

        [SetUp]
        public void SetUp() => _rink = new GameObject("Test Rink").transform;

        [TearDown]
        public void TearDown() => RinkTeardown.Destroy(_rink.gameObject);

        [Test]
        public void The_goal_light_model_sits_above_the_net_and_its_dome_lights_up_on_a_goal()
        {
            var light = new GoalLight(_rink);
            Renderer dome = NetBuilderTests.Child(_rink, "Dome")?.GetComponent<Renderer>();
            Assert.IsNotNull(dome, "the goal light model was not found");

            Assert.Greater(NetBuilderTests.WorldBounds(_rink).min.y, CrossbarTop, "above the crossbar");
            light.SetOn(true);
            Assert.Greater(dome.sharedMaterial.color.r, 0.9f, "dome is bright red when on");
            Assert.IsTrue(Glow().enabled, "glow on");
            light.SetOn(false);
            Assert.Less(dome.sharedMaterial.color.r, 0.5f, "dome is dark when off");
            Assert.IsFalse(Glow().enabled, "glow off");
        }

        [Test]
        public void With_no_goal_light_model_the_box_light_comes_back_and_still_lights_up()
        {
            var light = new GoalLight(_rink, name => null);
            Assert.IsNull(NetBuilderTests.Child(_rink, "Dome"));
            Renderer box = _rink.GetComponentsInChildren<Renderer>(true).Single();

            Assert.Greater(NetBuilderTests.WorldBounds(_rink).min.y, CrossbarTop, "above the crossbar");
            light.SetOn(true);
            Assert.Greater(box.sharedMaterial.color.r, 0.9f);
            Assert.IsTrue(Glow().enabled);
            light.SetOn(false);
            Assert.Less(box.sharedMaterial.color.r, 0.5f);
        }

        [Test]
        public void Every_part_of_the_goal_light_uses_a_URP_material()
        {
            new GoalLight(_rink);
            foreach (Renderer r in _rink.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    StringAssert.StartsWith("Universal Render Pipeline/", m.shader.name, r.name);
        }

        Light Glow() => _rink.GetComponentsInChildren<Light>(true).Single();
    }
}
