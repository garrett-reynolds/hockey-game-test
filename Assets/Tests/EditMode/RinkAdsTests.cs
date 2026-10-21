using System.Collections.Generic;
using System.IO;
using System.Linq;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class RinkAdsTests
    {
        GameObject _rink;

        [SetUp]
        public void MakeRink() => _rink = new GameObject("Rink");

        [TearDown]
        public void RemoveRink() => RinkTeardown.Destroy(_rink);

        static Renderer[] Panels(GameObject ads) => ads.transform.Find(RinkAds.BoardAdsPart).GetComponentsInChildren<Renderer>();
        static Renderer[] IceLogos(GameObject ads) => ads.transform.Find(RinkAds.IceLogosPart).GetComponentsInChildren<Renderer>();
        static string Art(Renderer r) => r.sharedMaterial.mainTexture == null ? null : r.sharedMaterial.mainTexture.name;

        /// <summary>The world point where the renderer's mesh shows texture point (u, v), from the triangle that holds it.</summary>
        static Vector3 AtUv(Renderer r, Vector2 uv)
        {
            Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v = mesh.vertices;
            Vector2[] t = mesh.uv;
            int[] tri = mesh.triangles;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector2 a = t[tri[i]], b = t[tri[i + 1]], c = t[tri[i + 2]];
                float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(d) < 1e-9f) continue;
                float wa = ((b.y - c.y) * (uv.x - c.x) + (c.x - b.x) * (uv.y - c.y)) / d;
                float wb = ((c.y - a.y) * (uv.x - c.x) + (a.x - c.x) * (uv.y - c.y)) / d;
                float wc = 1f - wa - wb;
                if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f) continue;
                return r.transform.TransformPoint(v[tri[i]] * wa + v[tri[i + 1]] * wb + v[tri[i + 2]] * wc);
            }
            Assert.Fail($"{r.name} does not show texture point {uv}");
            return default;
        }

        // ---------- the brands ----------

        [Test]
        public void The_pool_is_the_43_picked_real_brands_one_per_row_of_the_sources_list()
        {
            Assert.AreEqual(43, Brands.All.Length, "the 44 picked, CIBC taken out (Garrett, #223)");
            Assert.AreEqual(Brands.All.Length, Brands.All.Select(b => b.Id).Distinct().Count(), "one art name per brand");
            // The sources list beside the art (#223) keeps each logo's source URL: one row per brand, same ids, same order.
            var sources = Resources.Load<TextAsset>(RinkAds.ArtFolder + "/sources");
            Assert.IsNotNull(sources, "Ads/sources.csv");
            string[] rows = sources.text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Skip(1).ToArray();
            CollectionAssert.AreEqual(Brands.All.Select(b => b.Id).ToArray(), rows.Select(l => l.Split(',')[0]).ToArray());
            foreach (string row in rows) StringAssert.Contains("http", row, "a source URL");
        }

        [Test]
        public void Brand_LED_colours_keep_off_the_reserved_colours()
        {
            foreach (Brands.Brand b in Brands.All)
            {
                Assert.IsTrue(ReservedColours.IsSafe(b.AdBack), b.Name + " background " + b.AdBack);
                Assert.IsTrue(ReservedColours.IsSafe(b.AdFore), b.Name + " words " + b.AdFore);
            }
        }

        [Test]
        public void One_brand_shows_on_every_board_panel_and_the_ice_shows_the_Leafs_and_four_ice_brands()
        {
            Brands.Brand before = Brands.Current;
            IReadOnlyList<Brands.Brand> iceBefore = Brands.Ice;
            try
            {
                Brands.Use(Brands.All[5]);
                Brands.Brand[] ice = { Brands.All[7], Brands.All[8], Brands.All[9], Brands.All[10] };
                Brands.UseIce(ice);
                for (int i = 0; i < RinkLayout.AdPanelSlots.Length; i++) Assert.AreSame(Brands.All[5], Brands.ForPanel(i), "panel " + i);
                var beside = new List<Brands.Brand>();
                for (int i = 0; i < RinkLayout.IceLogoSpots.Length; i++)
                {
                    RinkLayout.IceLogoSpot spot = RinkLayout.IceLogoSpots[i];
                    if (spot.Kind == RinkLayout.IceLogoKind.CentreIce) Assert.IsNull(Brands.ForIceLogo(i), "centre ice: the Leafs, not a pool brand");
                    else beside.Add(Brands.ForIceLogo(i));
                }
                CollectionAssert.AreEqual(ice, beside, "the four spots beside the centre circle: the four ice brands, one each");
            }
            finally { Brands.Use(before); Brands.UseIce(iceBefore); }
        }

        [Test]
        public void Neighbouring_board_slots_show_the_brands_run_in_order_and_it_repeats()
        {
            foreach (Brands.Brand brand in Brands.All)
            {
                int run = RinkAds.SequenceLength(brand);
                Assert.That(run, Is.InRange(4, 7), brand.Id + ": logo, logo, then the tagline");
                int slots = RinkLayout.AdPanelSlots.Length;
                int[] n = Enumerable.Range(0, slots).Select(i =>
                {
                    string art = RinkAds.SlotArt(brand, i, false);
                    return int.Parse(art.Substring(("panel-" + brand.Id + "-").Length));
                }).ToArray();
                Assert.IsTrue(n.Contains(0) && n.Contains(run - 1), brand.Id + ": the whole run shows");
                for (int i = 0; i + 1 < slots; i++)
                {
                    int left = SlotToRight(i) ? n[i] : n[i + 1], right = SlotToRight(i) ? n[i + 1] : n[i];
                    Assert.AreEqual((left + 1) % run, right, brand.Id + ": slot " + i + " and its neighbour read on");
                }
                for (int i = 0; i < slots; i++)
                    Assert.AreEqual(RinkAds.SlotArt(brand, i, false) + "-soft", RinkAds.SlotArt(brand, i, true), "the Calm Area's soft version");
            }
        }

        // Whether slot i + 1 stands to the right of slot i, seen from the ice.
        static bool SlotToRight(int i)
        {
            RinkLayout.AdPanelSlot a = RinkLayout.AdPanelSlots[i], b = RinkLayout.AdPanelSlots[i + 1];
            var facing = new Vector3(-(float)a.InX, 0f, -(float)a.InZ);
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            return Vector3.Dot(new Vector3((float)(b.X - a.X), 0f, (float)(b.Z - a.Z)), right) > 0f;
        }

        [Test]
        public void A_round_change_puts_the_new_brand_on_the_boards_and_ice_and_the_Calm_Area_stays_soft()
        {
            Brands.Brand before = Brands.Current;
            try
            {
                foreach (Brands.Brand brand in new[] { Brands.All[0], Brands.All[1] })
                {
                    Brands.Use(brand);
                    RinkAds.Rebuild(_rink.transform);
                    Assert.AreEqual(1, _rink.transform.Cast<Transform>().Count(t => t.name == RinkAds.Part), "one Ads part, the old one gone");
                    GameObject ads = _rink.transform.Find(RinkAds.Part).gameObject;
                    Renderer[] panels = Panels(ads);
                    for (int i = 0; i < panels.Length; i++)
                    {
                        RinkLayout.AdPanelSlot slot = RinkLayout.AdPanelSlots[i];
                        Assert.AreEqual(RinkAds.SlotArt(brand, i, slot.CalmArea), Art(panels[i]), brand.Id + " " + slot);
                        StringAssert.StartsWith("panel-" + brand.Id + "-", Art(panels[i]));
                        if (slot.CalmArea)
                            Assert.That(panels[i].sharedMaterial.GetFloat("_Smoothness"), Is.LessThanOrEqualTo(0.05f), "the Calm Area stays matte soft grey");
                    }
                    Renderer[] logos = IceLogos(ads);
                    Assert.AreEqual(RinkLayout.IceLogoSpots.Length, logos.Length);
                    for (int i = 0; i < logos.Length; i++)
                    {
                        if (RinkLayout.IceLogoSpots[i].Kind == RinkLayout.IceLogoKind.CentreIce)
                            Assert.AreEqual(RinkAds.LeafsCentreArt, Art(logos[i]), "centre ice: the Leafs every Round");
                        else Assert.AreEqual(RinkAds.PanelArt(Brands.ForIceLogo(i), false), Art(logos[i]), "ice logo " + i);
                    }
                }
            }
            finally { Brands.Use(before); }
        }

        [Test]
        public void Every_brand_has_its_art()
        {
            foreach (Brands.Brand b in Brands.All)
                foreach (string art in new[] { RinkAds.PanelArt(b, false), RinkAds.PanelArt(b, true) })
                    Assert.IsNotNull(Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + art), art);
            Assert.IsNotNull(Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + TeamSkin.League.CentreIceLogo));
            // The Ribbon tile: one logo then the tagline (looped: logo, words, logo, words), one pixel per LED, 24 LEDs tall.
            foreach (Brands.Brand b in Brands.All)
            {
                Texture2D ribbon = Resources.Load<Texture2D>(RinkAds.ArtFolder + "/ribbon-" + b.Id);
                Assert.IsNotNull(ribbon, "ribbon-" + b.Id);
                Assert.AreSame(ribbon, RibbonBoards.PictureOf(b), b.Id);
                Assert.AreEqual(24, ribbon.height, "ribbon-" + b.Id + ": one pixel per LED, 24 tall");
                Assert.Greater(ribbon.width, 4 * ribbon.height, "ribbon-" + b.Id + ": a logo and its tagline");
            }

            // The panel art keeps its shape when imported (no power-of-two scaling), so its logo fits the narrowest panel.
            double narrowest = RinkLayout.AdPanelSlots.Min(s => s.Width), band = RinkLayout.AdBandTop - RinkLayout.AdBandBottom;
            foreach (Brands.Brand b in Brands.All)
            {
                Texture2D panel = Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + RinkAds.PanelArt(b, false));
                Assert.That(band * panel.width / panel.height, Is.LessThan(narrowest), b.Name);
            }
        }

        // ---------- the board ads ----------

        [Test]
        public void Ad_panels_fill_the_whole_board_run_in_the_ad_band_and_never_on_the_glass()
        {
            GameObject ads = RinkAds.Build(_rink.transform);
            Renderer[] panels = Panels(ads);
            Assert.AreEqual(RinkLayout.AdPanelSlots.Length, panels.Length, "one panel per slot");

            float along = 0f;
            for (int i = 0; i < panels.Length; i++)
            {
                RinkLayout.AdPanelSlot slot = RinkLayout.AdPanelSlots[i];
                Vector3[] v = panels[i].GetComponent<MeshFilter>().sharedMesh.vertices.Select(panels[i].transform.TransformPoint).ToArray();
                Assert.That(v.Min(p => p.y), Is.EqualTo((float)RinkLayout.AdBandBottom).Within(1e-4f), slot.ToString());
                Assert.That(v.Max(p => p.y), Is.EqualTo((float)RinkLayout.AdBandTop).Within(1e-4f), "under the rail, nothing on the glass: " + slot);
                foreach (Vector3 p in v) // on the board face, just off it
                {
                    Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(RinkBoards.HalfWidth), slot.ToString());
                    Assert.That(p.z, Is.InRange(RinkBoards.FarZ, RinkBoards.EndZ), slot.ToString());
                }
                // Its two ends are on (just off) the slot's ends, so side by side the panels run the whole way round.
                RinkLayout.BoardPoint from = RinkLayout.OnBoards(slot.FromS), to = RinkLayout.OnBoards(slot.ToS);
                Assert.That(v.Min(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2((float)from.X, (float)from.Z))), Is.LessThan(0.05f), slot.ToString());
                Assert.That(v.Min(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2((float)to.X, (float)to.Z))), Is.LessThan(0.05f), slot.ToString());
                along += (float)slot.Width;
            }
            Assert.AreEqual((float)RinkLayout.BoardsPerimeter, along, 0.01f);
        }

        [Test]
        public void Ad_panels_face_into_the_rink_and_read_left_to_right_from_the_ice()
        {
            GameObject ads = RinkAds.Build(_rink.transform);
            Renderer[] panels = Panels(ads);
            for (int i = 0; i < panels.Length; i++)
            {
                RinkLayout.AdPanelSlot slot = RinkLayout.AdPanelSlots[i];
                var inward = new Vector3((float)slot.InX, 0f, (float)slot.InZ);
                Mesh mesh = panels[i].GetComponent<MeshFilter>().sharedMesh;
                int[] t = mesh.triangles;
                Vector3[] v = mesh.vertices;
                for (int k = 0; k < t.Length; k += 3) // Unity draws a triangle's front where its corners run clockwise
                {
                    Vector3 front = Vector3.Cross(v[t[k + 1]] - v[t[k]], v[t[k + 2]] - v[t[k]]).normalized;
                    Assert.That(Vector3.Dot(front, inward), Is.GreaterThan(0.9f), slot.ToString());
                }
                // Seen from the ice (looking along -inward), the texture's left edge is on the viewer's left.
                Vector3 viewerRight = Vector3.Cross(Vector3.up, -inward);
                Vector3 left = AtUv(panels[i], new Vector2(0.02f, 0.5f)), right = AtUv(panels[i], new Vector2(0.98f, 0.5f));
                Assert.That(Vector3.Dot(right - left, viewerRight), Is.GreaterThan(0.5f), slot.ToString());
                Assert.That(AtUv(panels[i], new Vector2(0.5f, 0.9f)).y, Is.GreaterThan(AtUv(panels[i], new Vector2(0.5f, 0.1f)).y), "upright: " + slot);
            }
        }

        [Test]
        public void Calm_Area_panels_use_the_soft_grey_style_and_every_other_panel_its_brand_colours()
        {
            GameObject ads = RinkAds.Build(_rink.transform);
            Renderer[] panels = Panels(ads);
            for (int i = 0; i < panels.Length; i++)
            {
                RinkLayout.AdPanelSlot slot = RinkLayout.AdPanelSlots[i];
                Assert.AreEqual(RinkAds.SlotArt(Brands.ForPanel(i), i, slot.CalmArea), Art(panels[i]), slot.ToString());
                Assert.AreEqual(slot.CalmArea, Art(panels[i]).EndsWith("-soft"), slot.ToString());
                if (slot.CalmArea) Assert.That(panels[i].sharedMaterial.GetFloat("_Smoothness"), Is.LessThanOrEqualTo(0.05f), "matte, no glare");
            }
        }

        static Color32[] Pixels(string art)
        {
            var texture = new Texture2D(2, 2);
            Assert.IsTrue(texture.LoadImage(File.ReadAllBytes("Assets/Resources/" + RinkAds.ArtFolder + "/" + art + ".png")), art);
            Color32[] pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);
            return pixels;
        }

        static float Luma(Color32 c) => (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
        static float Saturation(Color32 c) => (Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b)) / 255f;

        [Test]
        public void Soft_panels_are_white_with_low_contrast_grey_logos_and_strong_panels_stand_out()
        {
            // Real logos (#223) can be all black (Kia, Warrior) or a light strong colour on white (McDonald's yellow):
            // a strong panel stands out by its contrast or by its colour.
            foreach (Brands.Brand b in Brands.All)
            {
                Color32[] soft = Pixels(RinkAds.PanelArt(b, true));
                float lightest = soft.Max(Luma), darkest = soft.Min(Luma);
                Assert.That(lightest, Is.GreaterThan(0.85f), b.Name + ": a white panel");
                Assert.That(lightest - darkest, Is.InRange(0.02f, 0.1f), b.Name + ": a logo, but low contrast");
                Assert.That(soft.Max(Saturation), Is.LessThan(0.06f), b.Name + ": grey, no colour");

                Color32[] strong = Pixels(RinkAds.PanelArt(b, false));
                float contrast = strong.Max(Luma) - strong.Min(Luma), colour = strong.Max(Saturation);
                Assert.IsTrue(contrast > 0.3f || colour > 0.5f, $"{b.Name}: a strong logo (contrast {contrast:0.00}, colour {colour:0.00})");
            }
        }

        // ---------- the ice logos ----------

        [Test]
        public void Ice_logos_lie_flat_on_their_spots_and_all_read_from_the_side_like_a_TV_view()
        {
            GameObject ads = RinkAds.Build(_rink.transform);
            Renderer[] logos = IceLogos(ads);
            Assert.AreEqual(RinkLayout.IceLogoSpots.Length, logos.Length);

            // Garrett (#223): all five (the Leafs and the four brands) turned the same way, upright from the -x side of the rink
            // (the start screen's "Pan along the side" camera), as a TV camera sees centre ice.
            var replay = new GameObject("Side View").AddComponent<Camera>();
            replay.transform.SetParent(_rink.transform);
            replay.aspect = 16f / 9f;
            replay.transform.position = new Vector3(-11.5f, 9f, (float)RinkLayout.CentreLineZ);
            replay.transform.LookAt(new Vector3(0f, 0f, (float)RinkLayout.CentreLineZ));

            for (int i = 0; i < logos.Length; i++)
            {
                RinkLayout.IceLogoSpot spot = RinkLayout.IceLogoSpots[i];
                Bounds b = logos[i].bounds;
                Assert.AreEqual((float)spot.X, b.center.x, 0.01f, spot.ToString());
                Assert.AreEqual((float)spot.Z, b.center.z, 0.01f, spot.ToString());
                Assert.That(b.size.y, Is.LessThan(1e-3f), "flat: " + spot);
                Assert.That(b.center.y, Is.InRange(0.001f, 0.02f), "just over the ice: " + spot);
                Assert.That(b.size.x, Is.LessThanOrEqualTo((float)spot.Width + 1e-3f), spot.ToString());
                Assert.That(b.size.z, Is.LessThanOrEqualTo((float)spot.Height + 1e-3f), spot.ToString());

                Vector3 left = replay.WorldToViewportPoint(AtUv(logos[i], new Vector2(0.1f, 0.5f)));
                Vector3 right = replay.WorldToViewportPoint(AtUv(logos[i], new Vector2(0.9f, 0.5f)));
                Vector3 bottom = replay.WorldToViewportPoint(AtUv(logos[i], new Vector2(0.5f, 0.1f)));
                Vector3 top = replay.WorldToViewportPoint(AtUv(logos[i], new Vector2(0.5f, 0.9f)));
                Assert.That(right.x, Is.GreaterThan(left.x), "not mirrored: " + spot);
                Assert.That(top.y, Is.GreaterThan(bottom.y), "not upside down: " + spot);

                string art = Art(logos[i]);
                if (spot.Kind == RinkLayout.IceLogoKind.CentreIce) Assert.AreEqual(RinkAds.LeafsCentreArt, art);
                else Assert.AreEqual(RinkAds.PanelArt(Brands.ForIceLogo(i), false), art);
            }
        }

        [Test]
        public void The_centre_ice_logo_is_the_Leafs_every_Round_big_and_centred_with_its_shape_kept()
        {
            Brands.Brand before = Brands.Current;
            try
            {
                foreach (Brands.Brand brand in new[] { Brands.All[0], Brands.All[19] })
                {
                    Brands.Use(brand);
                    Renderer[] logos = IceLogos(RinkAds.Rebuild(_rink.transform));
                    Assert.AreEqual(5, logos.Length, "the Leafs and four beside the centre circle");
                    int c = System.Array.FindIndex(RinkLayout.IceLogoSpots, s => s.Kind == RinkLayout.IceLogoKind.CentreIce);
                    RinkLayout.IceLogoSpot spot = RinkLayout.IceLogoSpots[c];
                    Texture tex = logos[c].sharedMaterial.mainTexture;
                    Assert.AreEqual(RinkAds.LeafsCentreArt, tex.name, brand.Id + " Round: still the Leafs");
                    Bounds b = logos[c].bounds;
                    Assert.AreEqual((float)spot.X, b.center.x, 0.01f, "centred");
                    Assert.AreEqual((float)spot.Z, b.center.z, 0.01f, "centred");
                    Assert.That(Mathf.Max(b.size.x, b.size.z), Is.GreaterThan(0.95f * (float)System.Math.Min(spot.Width, spot.Height)), "big: it fills the spot one way");
                    float shape = b.size.x / b.size.z, art = tex.width / (float)tex.height;
                    Assert.That(Mathf.Min(Mathf.Abs(shape - art) / art, Mathf.Abs(1f / shape - art) / art), Is.LessThan(0.02f), brand.Id + ": the art's shape kept");
                }
            }
            finally { Brands.Use(before); }
        }
    }
}
