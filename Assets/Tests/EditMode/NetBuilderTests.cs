using System.Linq;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class NetBuilderTests
    {
        // Independent of the code: the Net Face is 1.83 m wide and 1.22 m high, the net is 0.6 m deep, posts 0.08 m thick.
        const float HalfWidth = 0.915f;
        const float Height = 1.22f;
        const float Depth = 0.6f;
        const float Post = 0.08f;
        const float Tolerance = 0.003f;

        Transform _rink;

        [SetUp]
        public void SetUp() => _rink = new GameObject("Test Rink").transform;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rink.gameObject);

        [Test]
        public void The_net_model_opening_matches_the_Net_Face_with_the_back_at_the_net_depth()
        {
            GameObject net = NetBuilder.Build(_rink);

            Assert.IsNull(Child(net.transform, "Back"), "the net model was not found, so the box net was built");
            AssertOpeningMatchesTheNetFace(net.transform);
            Bounds netting = WorldBounds(Part(net.transform, NetBuilder.NettingName));
            Assert.AreEqual(Depth, netting.max.z, Tolerance, "back of the netting");
            Assert.GreaterOrEqual(netting.min.z, 0f, "netting starts behind the Net Face");
            Bounds all = WorldBounds(net.transform);
            Assert.AreEqual(0f, all.min.y, Tolerance, "net sits on the ice");
            Assert.AreEqual(Depth, all.max.z, Tolerance, "nothing behind the net depth");
        }

        [Test]
        public void With_no_net_model_the_box_net_comes_back_around_the_same_opening()
        {
            GameObject net = NetBuilder.Build(_rink, name => null);

            Assert.IsNotNull(Child(net.transform, "Back"), "box net back");
            AssertOpeningMatchesTheNetFace(net.transform);
            Assert.IsNotNull(NetBuilder.FindNetting(_rink), "the box netting still shakes on a goal");
        }

        [Test]
        public void Every_part_of_the_net_model_uses_a_URP_material()
        {
            GameObject net = NetBuilder.Build(_rink);

            foreach (Renderer r in net.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    StringAssert.StartsWith("Universal Render Pipeline/", m.shader.name, r.name);
        }

        [Test]
        public void Any_net_model_dropped_in_is_fitted_from_the_post_fronts_to_the_net_back()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = new Vector3(5f, 3f, 2f);
            cube.transform.position = new Vector3(7f, 1f, -4f);

            GameObject net = NetBuilder.Build(_rink, name => name == "net" ? cube : null);

            Bounds b = WorldBounds(net.transform);
            Assert.AreEqual(-HalfWidth - Post, b.min.x, Tolerance);
            Assert.AreEqual(HalfWidth + Post, b.max.x, Tolerance);
            Assert.AreEqual(0f, b.min.y, Tolerance);
            Assert.AreEqual(Height + Post, b.max.y, Tolerance);
            Assert.AreEqual(-Post / 2f, b.min.z, Tolerance);
            Assert.AreEqual(Depth, b.max.z, Tolerance);
        }

        static void AssertOpeningMatchesTheNetFace(Transform net)
        {
            Bounds left = WorldBounds(Part(net, "LeftPost"));
            Bounds right = WorldBounds(Part(net, "RightPost"));
            if (left.center.x > right.center.x) { Bounds t = left; left = right; right = t; } // a model may mirror left and right
            Bounds bar = WorldBounds(Part(net, "Crossbar"));

            Assert.AreEqual(-HalfWidth, left.max.x, Tolerance, "inside of the left post");
            Assert.AreEqual(HalfWidth, right.min.x, Tolerance, "inside of the right post");
            Assert.AreEqual(Height, bar.min.y, Tolerance, "underside of the crossbar");
            Assert.AreEqual(0f, left.min.y, Tolerance, "left post stands on the ice");
            Assert.AreEqual(0f, left.center.z, Tolerance, "posts stand on the Net Face plane");
            Assert.AreEqual(0f, right.center.z, Tolerance, "posts stand on the Net Face plane");
            Assert.AreEqual(0f, bar.center.z, Tolerance, "crossbar is on the Net Face plane");
            Assert.AreEqual(Post, left.size.x, Tolerance, "post thickness");
        }

        internal static Transform Child(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static Transform Part(Transform root, string name)
        {
            Transform part = Child(root, name);
            Assert.IsNotNull(part, name + " not found in: " +
                string.Join(", ", root.GetComponentsInChildren<Transform>(true).Select(t => t.name)));
            return part;
        }

        internal static Bounds WorldBounds(Transform root)
        {
            Assert.IsNotNull(root, "part not found");
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers, root.name + " has no renderers");
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
