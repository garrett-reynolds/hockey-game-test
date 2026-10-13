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
        public void RemoveRink() => RinkTeardown.Destroy(_rink);

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
        static Color32 IceColourAt(GameObject ice, float x, float z) =>
            ColourAt(Part(ice, "Ice"), "Assets/Resources/Models/ice-markings.jpg", x, z);

        /// <summary>The colour (0..255) of an image file at a point on the ice, read through a flat part's mesh UVs.</summary>
        static Color32 ColourAt(GameObject sheet, string file, float x, float z)
        {
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

            Texture2D tex = Image(file);
            return tex.GetPixel(Mathf.FloorToInt(u * tex.width), Mathf.FloorToInt(t * tex.height));
        }

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> Images = new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>An image file, read from disk once per test run.</summary>
        static Texture2D Image(string file)
        {
            if (!Images.TryGetValue(file, out Texture2D tex))
            {
                tex = new Texture2D(2, 2);
                tex.LoadImage(System.IO.File.ReadAllBytes(file));
                Images[file] = tex;
            }
            return tex;
        }

        [OneTimeTearDown]
        public void ForgetImages()
        {
            foreach (Texture2D tex in Images.Values) Object.DestroyImmediate(tex);
            Images.Clear();
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
        public void The_ice_shows_the_full_rink_and_the_far_end_mirrors_the_near_end()
        {
            GameObject ice = RinkIce.Build(_rink.transform);
            float far = (float)RinkLayout.FarGoalLineZ, centre = (float)RinkLayout.CentreLineZ;

            Assert.IsTrue(IsRed(IceColourAt(ice, 5f, far)), "far goal line");
            Assert.IsTrue(IsCreaseBlue(IceColourAt(ice, 0f, far + 0.9f)), "far crease, in front of the far net");
            Assert.IsTrue(IsIce(IceColourAt(ice, 0f, far - 0.5f)), "no crease behind the far goal line");
            Assert.IsTrue(IsBlueLine(IceColourAt(ice, 0f, (float)RinkLayout.FarBlueLineZ)), "far blue line");
            Assert.IsTrue(IsRed(IceColourAt(ice, 2f, centre)), "centre red line");
            Assert.IsTrue(IsBlueLine(IceColourAt(ice, 0f, centre + 4.57f)), "the centre circle is blue");
            Assert.IsTrue(IsIce(IceColourAt(ice, 0f, centre + 3f)), "plain ice inside the centre circle");

            // The trapezoid behind each net: 6.71 m wide at the goal line, 8.53 m at the end boards.
            float TrapezoidX(float behind) => (float)(RinkLayout.TrapezoidGoalLineWidth / 2 +
                (RinkLayout.TrapezoidEndBoardsWidth - RinkLayout.TrapezoidGoalLineWidth) / 2 * behind / RinkLayout.GoalLineFromEnd);
            foreach (float goal in new[] { 0f, far })
            {
                float back = goal == 0f ? 1f : -1f; // toward this end's boards
                Assert.IsTrue(IsRed(IceColourAt(ice, TrapezoidX(0.1f), goal + back * 0.1f)), "trapezoid line by the goal line");
                Assert.IsTrue(IsRed(IceColourAt(ice, -TrapezoidX(3.3f), goal + back * 3.3f)), "trapezoid line by the end boards");
                Assert.IsTrue(IsIce(IceColourAt(ice, TrapezoidX(0.1f) + 0.3f, goal + back * 0.1f)), "outside the trapezoid");
            }
            // End-zone faceoff circles at both ends.
            foreach (float spot in new[] { (float)RinkLayout.NearEndZoneDotZ, (float)RinkLayout.Mirror(RinkLayout.NearEndZoneDotZ) })
                Assert.IsTrue(IsRed(IceColourAt(ice, 6.71f, spot)), "end-zone spot");
        }

        [Test]
        public void The_ice_sheet_is_centred_on_the_centre_line_and_runs_past_both_end_boards()
        {
            foreach (GameObject ice in new[] { RinkIce.Build(_rink.transform), RinkIce.Build(_rink.transform, name => null) })
            {
                Bounds b = WorldBounds(ice);
                Assert.AreEqual((float)RinkLayout.CentreLineZ, b.center.z, 0.01f, ice.name);
                Assert.That(b.min.z, Is.LessThan((float)RinkLayout.FarEndBoardsZ - 0.2f), "past the far end boards");
                Assert.That(b.max.z, Is.GreaterThan((float)RinkLayout.NearEndBoardsZ + 0.2f), "past the near end boards");
            }
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
        public void The_ice_is_TV_bright_white_with_a_soft_sheen()
        {
            Material m = Part(RinkIce.Build(_rink.transform), "Ice").GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual(0.7f, m.GetFloat("_Smoothness"), 0.02f, "soft sheen: it shows the arena, not a mirror");
            Color tint = m.color;
            Assert.AreEqual(0.88f, tint.r, 0.01f);
            Assert.AreEqual(0.91f, tint.g, 0.01f);
            Assert.AreEqual(0.95f, tint.b, 0.01f);
            Assert.AreEqual("ice-markings", m.mainTexture.name, "still the rink lines");
        }

        [Test]
        public void The_Calm_Area_ice_is_matte_with_a_soft_edge_and_the_rest_keeps_the_ice_gloss()
        {
            GameObject sheet = Part(RinkIce.Build(_rink.transform), "Ice");
            Material m = sheet.GetComponent<Renderer>().sharedMaterial;
            Assert.IsTrue(m.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), "smoothness comes from the map");
            Assert.AreEqual("ice-smoothness", m.GetTexture("_MetallicGlossMap").name);

            float Gloss(float x, float z) => ColourAt(sheet, "Assets/Resources/Models/ice-smoothness.png", x, z).a / 255f;
            float halfWidth = (float)RinkLayout.CalmAreaHalfWidth, front = (float)RinkLayout.CalmAreaFrontZ;
            Assert.AreEqual(0.3f, Gloss(0f, 1f), 0.02f, "behind the net: about 30% of the ice gloss");
            Assert.AreEqual(0.3f, Gloss(3.5f, -1f), 0.02f, "beside the crease, inside the Calm Area");
            Assert.AreEqual(0.3f, Gloss(0f, 3f), 0.02f, "by the end boards");
            Assert.AreEqual(1f, Gloss(0f, -10f), 0.01f, "the slot keeps the full gloss");
            Assert.AreEqual(1f, Gloss(8f, 1f), 0.01f, "beside the Calm Area keeps the full gloss");
            Assert.AreEqual(1f, Gloss(0f, (float)RinkLayout.FarGoalLineZ), 0.01f, "the far end keeps the full gloss");
            Assert.AreEqual(0.65f, Gloss(halfWidth, 1f), 0.08f, "a soft edge: half way on the Calm Area's side");
            Assert.AreEqual(0.65f, Gloss(0f, front), 0.08f, "a soft edge: half way at the crease front");
            Assert.That(Gloss(halfWidth + 0.15f, 1f), Is.GreaterThan(Gloss(halfWidth - 0.15f, 1f)), "the edge ramps, not steps");
        }

        [Test]
        public void Faint_skate_marks_and_snow_lie_over_the_ice_under_the_ice_logos_but_none_in_the_Calm_Area()
        {
            GameObject ice = RinkIce.Build(_rink.transform);
            GameObject wear = Part(ice, RinkIce.WearPart);
            Renderer r = wear.GetComponent<Renderer>();
            Material m = r.sharedMaterial;
            Assert.AreEqual("ice-wear", m.mainTexture.name);
            Assert.That(m.GetFloat("_Smoothness"), Is.LessThan(RinkIce.Gloss), "rougher than the ice");
            Assert.That(m.renderQueue, Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent - 100), "see-through");
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode);

            GameObject ads = RinkAds.Build(_rink.transform);
            Renderer logo = ads.transform.Find(RinkAds.IceLogosPart).GetComponentInChildren<Renderer>();
            Assert.That(m.renderQueue, Is.LessThan(logo.sharedMaterial.renderQueue), "drawn before the ice logos, so under them");
            Assert.That(r.bounds.min.y, Is.GreaterThan(0f), "over the ice");
            Assert.That(r.bounds.max.y, Is.LessThan(logo.bounds.min.y), "under the ice logos");
            Assert.AreEqual((float)RinkLayout.HalfWidth, r.bounds.max.x, 0.01f, "out to the side boards");
            Assert.AreEqual((float)RinkLayout.NearEndBoardsZ, r.bounds.max.z, 0.01f, "to the near end boards");
            Assert.AreEqual((float)RinkLayout.FarEndBoardsZ, r.bounds.min.z, 0.01f, "to the far end boards");

            float Wear(float x, float z) => ColourAt(wear, "Assets/Resources/Models/ice-wear.png", x, z).a / 255f;
            float halfWidth = (float)RinkLayout.CalmAreaHalfWidth, front = (float)RinkLayout.CalmAreaFrontZ;
            float calmMax = 0f;
            for (float x = -halfWidth; x <= halfWidth; x += 0.1f)
                for (float z = front; z < (float)RinkLayout.NearEndBoardsZ; z += 0.1f)
                    calmMax = Mathf.Max(calmMax, Wear(x, z));
            Assert.AreEqual(0f, calmMax, "no marks or snow in the Calm Area");

            int snow = 0, marks = 0;
            for (float z = -50f; z < -5f; z += 0.25f)
            {
                if (Wear((float)RinkLayout.HalfWidth - 0.3f, z) > 0.3f) snow++;
                for (float x = -6f; x <= 6f; x += 0.25f)
                    if (Wear(x, z) > 0f) marks++;
            }
            Assert.That(snow, Is.GreaterThan(20), "snow piled along the boards");
            Assert.That(marks, Is.GreaterThan(200), "skate marks all over the middle of the ice");
        }

        [Test]
        public void Plain_ice_has_no_skate_marks()
        {
            GameObject ice = RinkIce.Build(_rink.transform, name => null);
            foreach (Transform t in ice.GetComponentsInChildren<Transform>())
                Assert.AreNotEqual(RinkIce.WearPart, t.name);
        }
    }
}
