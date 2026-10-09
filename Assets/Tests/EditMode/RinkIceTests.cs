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

        /// <summary>The ice texture colour (0..255) at a point on the ice, read through the imported mesh's UVs.</summary>
        static Color32 IceColourAt(GameObject ice, float x, float z)
        {
            GameObject sheet = Part(ice, "Ice");
            Mesh mesh = sheet.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v = mesh.vertices;
            Vector2[] uv = mesh.uv;
            int lo = 0, hi = 0; // corners nearest world (-x, -z) and (+x, +z)
            for (int i = 1; i < v.Length; i++)
            {
                Vector3 w = sheet.transform.TransformPoint(v[i]);
                Vector3 wl = sheet.transform.TransformPoint(v[lo]), wh = sheet.transform.TransformPoint(v[hi]);
                if (w.x + w.z < wl.x + wl.z) lo = i;
                if (w.x + w.z > wh.x + wh.z) hi = i;
            }
            Vector3 a = sheet.transform.TransformPoint(v[lo]), b = sheet.transform.TransformPoint(v[hi]);
            float u = Mathf.Lerp(uv[lo].x, uv[hi].x, Mathf.InverseLerp(a.x, b.x, x));
            float t = Mathf.Lerp(uv[lo].y, uv[hi].y, Mathf.InverseLerp(a.z, b.z, z));

            var tex = new Texture2D(2, 2);
            tex.LoadImage(System.IO.File.ReadAllBytes("Assets/Resources/Models/ice-markings.jpg"));
            Color32 c = tex.GetPixel(Mathf.FloorToInt(u * tex.width), Mathf.FloorToInt(t * tex.height));
            Object.DestroyImmediate(tex);
            return c;
        }

        static bool IsRed(Color32 c) => c.r > 150 && c.g < 80 && c.b < 100;
        static bool IsBlueLine(Color32 c) => c.b > 120 && c.r < 60 && c.g < 110;
        static bool IsCreaseBlue(Color32 c) => c.b > 190 && c.r < 160 && c.r > 60 && c.g > 130;
        static bool IsIce(Color32 c) => c.r > 190 && c.g > 190 && c.b > 190;

        [Test]
        public void The_goal_line_runs_through_the_net_face_and_the_crease_sits_in_front_of_the_net()
        {
            GameObject ice = RinkIce.Build(_rink.transform);

            Assert.IsTrue(IsRed(IceColourAt(ice, 5f, 0f)), "goal line on the Net Face plane, beside the net");
            Assert.IsTrue(IsRed(IceColourAt(ice, -5f, 0f)), "goal line on the other side");
            Assert.IsTrue(IsIce(IceColourAt(ice, 5f, -0.15f)), "ice just in front of the goal line");
            Assert.IsTrue(IsIce(IceColourAt(ice, 0f, 0.5f)), "no crease behind the goal line, inside the net");

            Assert.IsTrue(IsCreaseBlue(IceColourAt(ice, 0f, -0.9f)), "crease in front of the middle of the net");
            Assert.IsTrue(IsCreaseBlue(IceColourAt(ice, 1.1f, -0.5f)), "crease runs past the right post (8 ft wide, net 6 ft)");
            Assert.IsTrue(IsCreaseBlue(IceColourAt(ice, -1.1f, -0.5f)), "crease runs past the left post");
            Assert.IsTrue(IsIce(IceColourAt(ice, 1.4f, -0.5f)), "crease ends 4 ft from the middle");
            Assert.IsTrue(IsCreaseBlue(IceColourAt(ice, 0f, -1.7f)), "crease reaches about 6 ft out");
            Assert.IsTrue(IsIce(IceColourAt(ice, 0f, -2.0f)), "ice beyond the crease");

            Assert.IsTrue(IsBlueLine(IceColourAt(ice, 0f, -19.66f)), "blue line 64 ft out from the goal line");
            Assert.IsTrue(IsIce(IceColourAt(ice, 0f, -10f)), "plain ice in the slot");
        }

        [Test]
        public void The_ice_is_URP_Lit_with_a_full_size_mipmapped_markings_texture()
        {
            GameObject ice = RinkIce.Build(_rink.transform);
            foreach (Renderer r in ice.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.sharedMaterials)
                    Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name, r.name + " / " + m.name);

            var tex = Part(ice, "Ice").GetComponent<Renderer>().sharedMaterial.mainTexture as Texture2D;
            Assert.IsNotNull(tex);
            Assert.AreEqual("ice-markings", tex.name);
            Assert.AreEqual(4096, tex.width, "1.5 cm per pixel so the 2 in lines stay sharp up close");
            Assert.That(tex.mipmapCount, Is.GreaterThan(1), "far lines fade instead of breaking up");
            Assert.That(tex.anisoLevel, Is.GreaterThanOrEqualTo(8), "lines stay readable at the skater's low angle");
        }
        [Test]
        public void The_ice_is_glossy_so_it_shows_the_arena_lights_and_the_net()
        {
            foreach (GameObject ice in new[] { RinkIce.Build(_rink.transform), RinkIce.Build(_rink.transform, name => null) })
                foreach (Renderer r in ice.GetComponentsInChildren<Renderer>())
                    Assert.That(r.sharedMaterial.GetFloat("_Smoothness"), Is.GreaterThanOrEqualTo(0.8f), r.name);
        }
    }
}
