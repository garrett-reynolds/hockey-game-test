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
        public void TearDown() => RinkTeardown.Destroy(_rink.gameObject);

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
        public void The_still_far_net_stands_on_the_far_goal_line_facing_the_near_net()
        {
            const float farGoalLine = -54.26f; // 200 ft rink, goal lines 11 ft from each end, near goal line at z = 0
            foreach (System.Func<string, GameObject> findFile in new System.Func<string, GameObject>[] { null, name => null })
            {
                GameObject far = NetBuilder.BuildFar(_rink, findFile);
                Bounds all = WorldBounds(far.transform);
                Assert.AreEqual(0f, all.min.y, Tolerance, "on the ice");
                Assert.AreEqual(farGoalLine - Depth, all.min.z, Tolerance, "its back toward the far end boards");
                Bounds left = WorldBounds(Part(far.transform, "LeftPost")), right = WorldBounds(Part(far.transform, "RightPost"));
                Assert.AreEqual(0f, (left.center.x + right.center.x) / 2f, Tolerance, "in the middle of the far end");
                Assert.That(left.center.x, Is.GreaterThan(0f), "turned round: its left post is on the shooter's right");
                Assert.AreEqual(farGoalLine, left.center.z, Tolerance, "posts on the far goal line");
                Object.DestroyImmediate(far);
            }
        }

        [Test]
        public void The_netting_that_shakes_on_a_goal_is_always_the_near_nets()
        {
            GameObject far = NetBuilder.BuildFar(_rink);
            GameObject near = NetBuilder.Build(_rink);
            Transform netting = NetBuilder.FindNetting(_rink);
            Assert.IsNotNull(netting);
            Assert.IsTrue(netting.IsChildOf(near.transform), "the near net's netting, never the far net's");
            Assert.IsNotNull(Part(far.transform, NetBuilder.FarNettingName), "the far net keeps its netting, under another name");
        }

        [Test]
        public void With_no_net_model_the_box_net_comes_back_around_the_same_opening()
        {
            GameObject net = NetBuilder.Build(_rink, name => null);

            Assert.IsNotNull(Child(net.transform, "Back"), "box net back");
            AssertOpeningMatchesTheNetFace(net.transform);
            Assert.IsNotNull(NetBuilder.FindNetting(_rink), "the box netting is still found (the cloth netting hides it)");
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
        public void The_supplied_net_has_a_red_frame_white_netting_and_a_skirt_within_the_triangle_budget()
        {
            GameObject net = NetBuilder.Build(_rink);

            foreach (string frame in new[] { "LeftPost", "RightPost", "Crossbar" })
                AssertColour(Part(net.transform, frame), red: true);
            AssertColour(Part(net.transform, NetBuilder.NettingName), red: false);
            AssertColour(Part(net.transform, "Skirt"), red: false);
            int triangles = net.GetComponentsInChildren<MeshFilter>(true).Sum(f => (int)f.sharedMesh.GetIndexCount(0) / 3);
            Assert.Less(triangles, 50000, "whole net, triangles");
            Assert.Greater(triangles, 10000, "the netting is real strands, not a box");
        }

        [Test]
        public void A_net_model_wider_at_the_base_than_the_posts_keeps_its_opening_on_the_Net_Face()
        {
            // The game's net at 1.2 times size (posts 0.096 thick round a 2.196 x 1.464 opening), with a base 3 wide.
            var model = new GameObject("Wide Net");
            Cube(model, "LeftPost", new Vector3(-1.146f, 0.78f, 0f), new Vector3(0.096f, 1.56f, 0.096f));
            Cube(model, "RightPost", new Vector3(1.146f, 0.78f, 0f), new Vector3(0.096f, 1.56f, 0.096f));
            Cube(model, "Crossbar", new Vector3(0f, 1.512f, 0f), new Vector3(2.388f, 0.096f, 0.096f));
            Cube(model, "Skirt", new Vector3(0f, 0.05f, 0.42f), new Vector3(3f, 0.1f, 0.6f));

            GameObject net = NetBuilder.Build(_rink, name => name == "net" ? model : null);

            Bounds left = WorldBounds(Part(net.transform, "LeftPost"));
            Bounds right = WorldBounds(Part(net.transform, "RightPost"));
            Bounds bar = WorldBounds(Part(net.transform, "Crossbar"));
            Assert.AreEqual(-HalfWidth, left.max.x, Tolerance, "inside of the left post");
            Assert.AreEqual(HalfWidth, right.min.x, Tolerance, "inside of the right post");
            Assert.AreEqual(Height, bar.min.y, Tolerance, "underside of the crossbar");
            Assert.AreEqual(0f, left.min.y, Tolerance, "posts on the ice");
            Assert.AreEqual(0f, left.center.z, Tolerance, "posts on the Net Face plane");
            Assert.AreEqual(Depth, WorldBounds(net.transform).max.z, Tolerance, "back of the base at the net depth");
            Assert.Greater(WorldBounds(Part(net.transform, "Skirt")).size.x, 2f * HalfWidth + 2f * Post + 0.1f, "the base still flares past the posts");
        }

        static void Cube(GameObject parent, string name, Vector3 position, Vector3 size)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent.transform, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
        }

        static void AssertColour(Transform part, bool red)
        {
            foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true))
            {
                Color c = r.sharedMaterial.GetColor("_BaseColor");
                if (red)
                {
                    Assert.Greater(c.r, 0.3f, part.name + " is red: " + c);
                    Assert.Less(c.g, c.r * 0.3f, part.name + " is red: " + c);
                    Assert.Less(c.b, c.r * 0.3f, part.name + " is red: " + c);
                }
                else
                {
                    Assert.Greater(Mathf.Min(c.r, Mathf.Min(c.g, c.b)), 0.5f, part.name + " is white or light grey: " + c);
                }
            }
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
