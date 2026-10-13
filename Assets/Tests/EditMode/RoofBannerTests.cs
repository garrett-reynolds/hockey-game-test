using System.Linq;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>#149: the League Skin hangs award and retired jersey banners, where the shooter's view sees them.</summary>
    public class RoofBannerTests
    {
        const string AwardPrefix = "banner-award-", JerseyPrefix = "banner-jersey-";

        [Test]
        public void The_League_Skin_has_award_and_retired_jersey_banners()
        {
            string[] art = TeamSkin.League.RoofBanners;
            Assert.GreaterOrEqual(art.Length, 6, "at least 6 banners");
            Assert.GreaterOrEqual(art.Count(a => a.StartsWith(AwardPrefix)), 3, "3 awards");
            Assert.GreaterOrEqual(art.Count(a => a.StartsWith(JerseyPrefix)), 3, "3 retired jerseys");
            Assert.AreEqual(art.Length, art.Distinct().Count(), "no banner twice");
        }

        [Test]
        public void The_banner_font_is_whole_and_covers_every_banner_word()
        {
            foreach (BannerArt.Banner banner in BannerArt.League)
                foreach (BannerArt.Line line in banner.Lines)
                    foreach (char c in line.Text)
                    {
                        Assert.IsTrue(BannerFont.Has(c), "the font has '" + c + "' (" + banner.Name + ")");
                        Assert.AreEqual(BannerFont.Rows, BannerFont.RowsOf(c).Length, "glyph '" + c + "' has 7 rows");
                        foreach (string row in BannerFont.RowsOf(c))
                            Assert.AreEqual(BannerFont.Columns, row.Length, "glyph '" + c + "' row " + row + " is 5 cells wide");
                        Assert.LessOrEqual(line.Text.Length * 6 * line.Scale - line.Scale, BannerArt.Width - 2 * 30, line.Text + " fits inside the trim");
                    }
        }

        [Test]
        public void Each_League_banner_art_loads()
        {
            Assert.IsNotEmpty(TeamSkin.League.RoofBanners, "the League Skin has banners to load");
            foreach (string name in TeamSkin.League.RoofBanners)
            {
                var texture = Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + name);
                Assert.IsNotNull(texture, name + " loads from Assets/Resources/" + RinkAds.ArtFolder);
                Assert.Greater(texture.height, texture.width, name + " is tall, like a hanging banner");
            }
        }

        [Test]
        public void The_League_Skin_builds_its_banners_where_the_shooter_sees_them()
        {
            Camera camera = null;
            Transform rink = null, row = null;
            try
            {
                rink = RinkBuilder.Build(out camera);
                row = rink.Find(RoofBanners.Part);
                Assert.IsNotNull(row, "the League Skin builds a banner row");
                Assert.AreEqual(TeamSkin.League.RoofBanners.Length, row.childCount, "one banner for each art");

                Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, 0f, 0f);
                camera.aspect = 16f / 9f;

                int seen = 0;
                foreach (Renderer banner in row.GetComponentsInChildren<Renderer>())
                {
                    Bounds b = banner.bounds;
                    Assert.IsNotNull(banner.sharedMaterial.mainTexture, banner.name + " has its art");
                    Assert.Less(b.max.y, (float)RinkLayout.RoofHeight, banner.name + " hangs under the roof");
                    float nearestX = Mathf.Min(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x));
                    Assert.IsTrue(b.min.x * b.max.x > 0f, banner.name + " is wholly on one side of the middle");
                    Assert.GreaterOrEqual(nearestX, (float)RinkLayout.VideoBoardWidth / 2f, banner.name + " is clear of the Video Board's width (bounds " + b + ")");
                    Assert.GreaterOrEqual(nearestX, (float)RinkLayout.CalmAreaHalfWidth, banner.name + " is outside the Calm Area strip");
                    Assert.Greater(b.min.z, 0f, banner.name + " is behind the near net, not on the ice");
                    // Round the end corner the lower stands rise with the offset from the boards; the banner's foot must clear them.
                    double offset = System.Math.Sqrt(System.Math.Pow(System.Math.Abs(b.center.x) - RinkLayout.CornerX, 2) +
                        System.Math.Pow(b.center.z - RinkLayout.NearCornerZ, 2)) - RinkLayout.CornerRadius;
                    double stands = RinkLayout.StandsFrontHeight + RinkLayout.LowerRise * (offset - RinkLayout.StandsFront) / RinkLayout.RowDepth;
                    Assert.GreaterOrEqual(b.min.y, stands + 0.5, banner.name + " hangs clear of the seats (stands " + stands.ToString("0.0") + " m high there)");
                    Assert.IsTrue(WhollyInView(camera, b), banner.name + " is wholly in the shooter's view, not cut off by the frame");
                    seen++;
                }
                Assert.GreaterOrEqual(seen, 6, "at least 6 banners are in the shooter's view");
            }
            finally
            {
                if (rink != null) RinkTeardown.Destroy(rink.gameObject);
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void Each_banner_hangs_from_a_top_bar_and_two_dark_wires_up_to_the_roof()
        {
            Camera camera = null;
            Transform rink = null;
            try
            {
                rink = RinkBuilder.Build(out camera);
                Transform row = rink.Find(RoofBanners.Part), wires = rink.Find(RoofBanners.WiresPart);
                Assert.IsNotNull(wires, "the banners' wires are built");
                Assert.AreEqual(3 * row.childCount, wires.childCount, "a top bar and two wires for each banner");
                Renderer[] hardware = wires.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in hardware)
                    Assert.Less(r.sharedMaterial.color.maxColorComponent, 0.25f, r.name + " is dark, so it reads as a wire and not as art");
                foreach (Renderer banner in row.GetComponentsInChildren<Renderer>())
                {
                    Bounds b = banner.bounds;
                    int bars = 0, reaching = 0;
                    foreach (Renderer h in hardware)
                    {
                        Bounds w = h.bounds;
                        bool above = Mathf.Abs(w.center.x - b.center.x) <= b.extents.x + 0.05f && Mathf.Abs(w.center.z - b.center.z) <= b.extents.z + 0.2f &&
                                     w.min.y <= b.max.y + 0.05f && w.max.y >= b.max.y - 0.05f;
                        if (!above) continue;
                        if (w.size.y < 0.3f && w.size.x + w.size.z >= b.size.x + b.size.z - 0.5f) bars++;   // a short bar across the banner's top
                        else if (w.size.x < 0.1f && w.size.z < 0.1f && w.max.y >= (float)RinkLayout.RoofHeight - 0.3f) reaching++; // a thin wire to the roof
                    }
                    Assert.AreEqual(1, bars, banner.name + " has a top bar");
                    Assert.AreEqual(2, reaching, banner.name + " has two wires up to the roof");
                }
            }
            finally
            {
                if (rink != null) RinkTeardown.Destroy(rink.gameObject);
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void No_banner_covers_a_Ribbon_Board_panel_in_the_shooters_view()
        {
            Camera camera = null;
            Transform rink = null;
            try
            {
                rink = RinkBuilder.Build(out camera);
                Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, 0f, 0f);
                camera.aspect = 16f / 9f;
                Renderer[] panels = rink.Find("Ribbon Boards").GetComponentsInChildren<Renderer>()
                    .Where(r => r.name.StartsWith("Ribbon Panel")).ToArray();
                Assert.IsNotEmpty(panels, "the Ribbon Board has panels");
                foreach (Renderer banner in rink.Find(RoofBanners.Part).GetComponentsInChildren<Renderer>())
                    foreach (string covered in CoveredPanels(camera, banner.bounds, panels))
                        Assert.Fail(banner.name + " covers " + covered + " in the shooter's view");
            }
            finally
            {
                if (rink != null) RinkTeardown.Destroy(rink.gameObject);
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void A_box_in_front_of_a_panel_covers_it_and_a_box_beside_it_does_not()
        {
            var camera = new GameObject("Test camera").AddComponent<Camera>();
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try
            {
                camera.aspect = 16f / 9f;
                camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                panel.transform.position = new Vector3(0f, 0f, 20f);
                Renderer[] panels = { panel.GetComponent<Renderer>() };
                Assert.AreEqual(1, CoveredPanels(camera, new Bounds(new Vector3(0f, 0f, 10f), new Vector3(2f, 2f, 0.1f)), panels).Count(), "in front");
                Assert.AreEqual(0, CoveredPanels(camera, new Bounds(new Vector3(0f, 8f, 10f), new Vector3(2f, 2f, 0.1f)), panels).Count(), "above it on the screen");
            }
            finally
            {
                Object.DestroyImmediate(panel);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        /// <summary>The panels (wholly in front of the camera) whose screen rectangle shares area with the screen rectangle of box.</summary>
        static System.Collections.Generic.IEnumerable<string> CoveredPanels(Camera camera, Bounds box, Renderer[] panels)
        {
            Rect banner = ScreenRect(camera, box, out bool bannerFront);
            if (!bannerFront) yield break;
            foreach (Renderer panel in panels)
            {
                Rect other = ScreenRect(camera, panel.bounds, out bool front);
                if (front && banner.Overlaps(other)) yield return panel.name;
            }
        }

        static Rect ScreenRect(Camera camera, Bounds b, out bool inFront)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue), max = new Vector3(float.MinValue, float.MinValue);
            inFront = true;
            for (int i = 0; i < 8; i++)
            {
                Vector3 e = b.extents;
                Vector3 v = camera.WorldToViewportPoint(b.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
                if (v.z <= 0f) inFront = false;
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [Test]
        public void The_baked_banner_PNGs_match_what_BannerArt_makes()
        {
            foreach (BannerArt.Banner banner in BannerArt.League)
            {
                string path = System.IO.Path.Combine(Application.dataPath, "Resources", RinkAds.ArtFolder, banner.Name + ".png");
                Assert.IsTrue(System.IO.File.Exists(path), banner.Name + ".png is baked (Hockey Game > Banner Art > Make)");
                Texture2D made = BannerArt.Make(banner);
                try
                {
                    Assert.IsTrue(SamePixels(System.IO.File.ReadAllBytes(path), made),
                        banner.Name + ".png has drifted from BannerArt (run Hockey Game > Banner Art > Make)");
                }
                finally { Object.DestroyImmediate(made); }
            }
        }

        [Test]
        public void The_drift_check_sees_a_changed_banner()
        {
            BannerArt.Banner a = BannerArt.League[0], b = BannerArt.League[1];
            Texture2D made = BannerArt.Make(a), other = BannerArt.Make(b);
            try
            {
                byte[] right = made.EncodeToPNG(), wrong = other.EncodeToPNG();
                Assert.IsTrue(SamePixels(right, made), "its own PNG matches");
                Assert.IsFalse(SamePixels(wrong, made), "another banner's PNG does not match");
            }
            finally { Object.DestroyImmediate(made); Object.DestroyImmediate(other); }
        }

        /// <summary>True when the PNG decodes to exactly the pixels of made (the importer may re-encode the file, so bytes are not compared).</summary>
        static bool SamePixels(byte[] png, Texture2D made)
        {
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!decoded.LoadImage(png) || decoded.width != made.width || decoded.height != made.height) return false;
                Color32[] x = decoded.GetPixels32(), y = made.GetPixels32();
                for (int i = 0; i < x.Length; i++)
                    if (x[i].r != y[i].r || x[i].g != y[i].g || x[i].b != y[i].b || x[i].a != y[i].a) return false;
                return true;
            }
            finally { Object.DestroyImmediate(decoded); }
        }

        [Test]
        public void Tearing_the_rink_down_frees_every_material_and_texture_it_made()
        {
            // Only objects the build made count: project assets the build loads (Glass, Ice...) stay in memory by design.
            int before = Made<Material>();
            int texturesBefore = MadeTextures();
            int meshesBefore = MadeMeshes();
            RinkBuilder.Build(out Camera camera);
            int built = Made<Material>();
            int texturesBuilt = MadeTextures();
            int meshesBuilt = MadeMeshes();
            RinkTeardown.Destroy(GameObject.Find("Rink"));
            Object.DestroyImmediate(camera.gameObject);
            Assert.Greater(built, before, "the rink makes materials");
            Assert.Greater(meshesBuilt, meshesBefore, "the rink makes meshes");
            Assert.AreEqual(meshesBefore, MadeMeshes(), "no mesh is left after the teardown");
            Assert.Greater(texturesBuilt, texturesBefore, "the rink makes textures");
            Assert.AreEqual(before, Made<Material>(), "no material is left after the teardown");
            Assert.AreEqual(texturesBefore, MadeTextures(), "no texture is left after the teardown");
        }

        /// <summary>How many live objects of type T are not project assets (made at runtime).</summary>
        static int Made<T>() where T : Object => Resources.FindObjectsOfTypeAll<T>().Count(o => !UnityEditor.AssetDatabase.Contains(o));

        /// <summary>Textures made at runtime, not counting RenderTextures: the render pipeline makes those (shadow maps) while it draws, not the rink build.</summary>
        static int MadeTextures() => Resources.FindObjectsOfTypeAll<Texture>().Count(t => !(t is RenderTexture) && !UnityEditor.AssetDatabase.Contains(t));

        /// <summary>Meshes made at runtime (not project assets or built-ins).</summary>
        static int MadeMeshes() => Resources.FindObjectsOfTypeAll<Mesh>().Count(m => !UnityEditor.EditorUtility.IsPersistent(m));

        [Test]
        public void Tearing_the_rink_down_frees_the_texture_a_Net_cloth_made()
        {
            int meshesBefore = MadeMeshes();
            int texturesBefore = MadeTextures();
            int materialsBefore = Made<Material>();
            var rink = new GameObject("Rink");
            new NetCloth(rink.transform); // builds "Net holes" and a material under the rink, and is never Disposed here
            Assert.Greater(MadeTextures(), texturesBefore, "the net makes a texture");
            RinkTeardown.Destroy(rink);
            Assert.AreEqual(texturesBefore, MadeTextures(), "its texture is freed with the rink");
            Assert.AreEqual(materialsBefore, Made<Material>(), "and its material");
            Assert.AreEqual(meshesBefore, MadeMeshes(), "and its mesh");
        }

        [Test]
        public void Tearing_the_rink_down_leaves_a_project_asset_texture_alone()
        {
            Texture2D asset = Resources.Load<Texture2D>("Ads/banner-award-goalie");
            Assert.IsNotNull(asset, "a project texture to share");
            var rink = new GameObject("Rink");
            var part = GameObject.CreatePrimitive(PrimitiveType.Quad);
            part.transform.SetParent(rink.transform);
            part.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = asset };
            Texture builtIn = Texture2D.whiteTexture; // built-in, not made by the build
            part.GetComponent<Renderer>().sharedMaterials = new[] { part.GetComponent<Renderer>().sharedMaterial, new Material(Shader.Find("Sprites/Default")) { mainTexture = builtIn } };
            Mesh builtInMesh = part.GetComponent<MeshFilter>().sharedMesh;
            RinkTeardown.Destroy(rink);
            Assert.IsTrue(asset != null, "the shared texture survives the teardown");
            Assert.IsTrue(builtIn != null, "a built-in texture on a made material survives the teardown");
            Assert.IsTrue(builtInMesh != null, "a built-in mesh survives the teardown");
        }

        [Test]
        public void A_banner_cut_off_by_the_frame_is_not_wholly_in_view()
        {
            var camera = new GameObject("Test camera").AddComponent<Camera>();
            try
            {
                camera.aspect = 16f / 9f;
                camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // looks along +z
                Assert.IsTrue(WhollyInView(camera, new Bounds(new Vector3(0f, 0f, 20f), Vector3.one)), "a small box in the middle is in view");
                Assert.IsFalse(WhollyInView(camera, new Bounds(new Vector3(0f, 10f, 20f), new Vector3(2f, 6f, 0.1f))), "its middle is in the frame but its top is above it");
                Assert.IsFalse(WhollyInView(camera, new Bounds(new Vector3(19f, 0f, 20f), new Vector3(4f, 2f, 0.1f))), "its middle is in the frame but its side is past it");
                Assert.IsFalse(WhollyInView(camera, new Bounds(new Vector3(0f, 0f, -20f), Vector3.one)), "behind the camera");
            }
            finally { Object.DestroyImmediate(camera.gameObject); }
        }

        /// <summary>True when all 8 corners of b are in front of the camera and inside its frame.</summary>
        static bool WhollyInView(Camera camera, Bounds b)
        {
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 v = camera.WorldToViewportPoint(corner);
                if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return false;
            }
            return true;
        }
    }
}
