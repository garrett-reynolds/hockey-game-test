using HockeyGame;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class RinkIceTests
    {
        GameObject _rink;

        [SetUp]
        public void MakeRink() => _rink = new GameObject("Rink");

        [TearDown]
        public void RemoveRink() => Object.DestroyImmediate(_rink);

        static Bounds WorldBounds(GameObject ice)
        {
            Renderer[] renderers = ice.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        static void AssertCoversEverySkateIn(GameObject ice)
        {
            Bounds bounds = WorldBounds(ice);
            const float margin = 5f; // the shooter sees ice around and ahead of them
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            {
                for (int i = 0; i <= 20; i++)
                {
                    Breakaway.SkatePoint(side, i / 20.0, out double x, out double distance);
                    var p = new Vector3((float)x, 0f, (float)-distance);
                    Assert.That(p.x - margin, Is.GreaterThanOrEqualTo(bounds.min.x), side + " skate-in leaves the ice on the left");
                    Assert.That(p.x + margin, Is.LessThanOrEqualTo(bounds.max.x), side + " skate-in leaves the ice on the right");
                    Assert.That(p.z - margin, Is.GreaterThanOrEqualTo(bounds.min.z), side + " skate-in starts off the ice");
                }
            }
            Assert.That(bounds.max.z, Is.GreaterThanOrEqualTo(margin), "ice runs on behind the net");
        }

        [Test]
        public void With_no_ice_model_todays_plain_ice_covers_the_skate_in_from_every_attack_side()
        {
            GameObject ice = RinkIce.Build(_rink.transform, name => null);
            Assert.AreEqual("Ice", ice.name);
            AssertCoversEverySkateIn(ice);
            Assert.AreEqual(0f, WorldBounds(ice).max.y, 0.001f, "the ice top is at y = 0");
        }

        static GameObject Part(GameObject ice, string name)
        {
            foreach (Transform t in ice.GetComponentsInChildren<Transform>())
                if (t.name == name && t.GetComponent<Renderer>() != null) return t.gameObject;
            Assert.Fail("the ice model has no part named " + name);
            return null;
        }

        [Test]
        public void The_ice_model_covers_the_skate_in_from_every_attack_side_with_the_ice_top_at_y_0()
        {
            GameObject ice = RinkIce.Build(_rink.transform);
            AssertCoversEverySkateIn(ice);
            Bounds sheet = Part(ice, "Ice").GetComponent<Renderer>().bounds;
            Assert.AreEqual(0f, sheet.max.y, 0.001f);
            Assert.AreEqual(RinkIce.Size, sheet.size.x, 0.01f);
            Assert.AreEqual(RinkIce.Size, sheet.size.z, 0.01f);
        }

        [Test]
        public void The_goal_line_runs_through_the_net_face_and_the_crease_sits_in_front_of_the_net()
        {
            GameObject ice = RinkIce.Build(_rink.transform);
            const float netHalfWidth = 1.83f / 2f;

            Bounds goalLine = Part(ice, "GoalLine").GetComponent<Renderer>().bounds;
            Assert.AreEqual(0f, goalLine.center.z, 0.01f, "goal line is on the Net Face plane");
            Assert.That(goalLine.size.z, Is.InRange(0.04f, 0.06f), "2 in wide");
            Assert.That(goalLine.min.x, Is.LessThan(-netHalfWidth));
            Assert.That(goalLine.max.x, Is.GreaterThan(netHalfWidth));
            Assert.That(goalLine.min.y, Is.GreaterThan(0f), "painted on top of the ice");

            Bounds crease = Part(ice, "Crease").GetComponent<Renderer>().bounds;
            Assert.AreEqual(0f, crease.center.x, 0.01f, "crease is centred on the net");
            Assert.AreEqual(0f, crease.max.z, 0.01f, "crease starts at the goal line");
            Assert.AreEqual(-1.83f, crease.min.z, 0.02f, "crease reaches 6 ft out, on the shooter's side");
            Assert.AreEqual(2.44f, crease.size.x, 0.02f, "crease is 8 ft wide");

            Bounds blueLine = Part(ice, "BlueLine").GetComponent<Renderer>().bounds;
            Assert.AreEqual(-19.66f, blueLine.center.z, 0.05f, "blue line is 64 ft out from the goal line");
            Assert.AreEqual(0.305f, blueLine.size.z, 0.01f, "12 in wide");
        }

        [Test]
        public void Every_ice_material_is_URP_Lit_and_the_ice_sheet_has_its_scuff_texture()
        {
            GameObject ice = RinkIce.Build(_rink.transform);
            foreach (Renderer r in ice.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.sharedMaterials)
                {
                    Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name, r.name + " / " + m.name);
                    Debug.Log($"ice material {m.name}: colour {m.color} smoothness {m.GetFloat("_Smoothness")} texture {m.mainTexture}");
                }
            Assert.IsNotNull(Part(ice, "Ice").GetComponent<Renderer>().sharedMaterial.mainTexture);
        }
    }
}
