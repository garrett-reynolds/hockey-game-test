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
        public void The_ten_made_up_brands_are_fixed()
        {
            CollectionAssert.AreEqual(new[]
            {
                "Northvale Bank", "GlacierLink", "Brawny's Lager", "Kestrel Motors", "Aurora Air",
                "Shieldcross", "Volt Rush", "Big Timber Burgers", "Peakside Energy", "FrostByte PCs",
            }, Brands.All.Select(b => b.Name).ToArray());
            Assert.AreEqual(Brands.All.Length, Brands.All.Select(b => b.Id).Distinct().Count(), "one art name per brand");
        }

        [Test]
        public void Every_brand_shows_on_the_boards_and_every_ice_logo_but_centre_ice_has_a_brand()
        {
            var onBoards = Enumerable.Range(0, RinkLayout.AdPanelSlots.Length).Select(Brands.ForPanel).Distinct();
            Assert.AreEqual(Brands.All.Length, onBoards.Count());
            for (int i = 0; i < RinkLayout.IceLogoSpots.Length; i++)
            {
                bool centre = RinkLayout.IceLogoSpots[i].Kind == RinkLayout.IceLogoKind.CentreIce;
                Assert.AreEqual(centre, Brands.ForIceLogo(i) == null, RinkLayout.IceLogoSpots[i].ToString());
            }
        }

        [Test]
        public void Every_brand_has_its_art()
        {
            foreach (Brands.Brand b in Brands.All)
                foreach (string art in new[] { RinkAds.PanelArt(b, false), RinkAds.PanelArt(b, true), RinkAds.RoundArt(b) })
                    Assert.IsNotNull(Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + art), art);
            Assert.IsNotNull(Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + TeamSkin.League.CentreIceLogo));

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
                Assert.AreEqual(RinkAds.PanelArt(Brands.ForPanel(i), slot.CalmArea), Art(panels[i]), slot.ToString());
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
        public void Soft_panels_are_white_with_low_contrast_grey_logos_and_strong_panels_are_coloured()
        {
            foreach (Brands.Brand b in Brands.All)
            {
                Color32[] soft = Pixels(RinkAds.PanelArt(b, true));
                float lightest = soft.Max(Luma), darkest = soft.Min(Luma);
                Assert.That(lightest, Is.GreaterThan(0.85f), b.Name + ": a white panel");
                Assert.That(lightest - darkest, Is.InRange(0.02f, 0.1f), b.Name + ": a logo, but low contrast");
                Assert.That(soft.Max(Saturation), Is.LessThan(0.06f), b.Name + ": grey, no colour");

                Color32[] strong = Pixels(RinkAds.PanelArt(b, false));
                Assert.That(strong.Max(Luma) - strong.Min(Luma), Is.GreaterThan(0.3f), b.Name + ": a strong logo");
                Assert.That(strong.Max(Saturation), Is.GreaterThan(0.25f), b.Name + ": in colour");
            }
        }

        // ---------- the ice logos ----------

        [Test]
        public void Ice_logos_lie_flat_on_their_spots_and_read_from_the_Replay_view_behind_the_near_net()
        {
            GameObject ads = RinkAds.Build(_rink.transform);
            Renderer[] logos = IceLogos(ads);
            Assert.AreEqual(RinkLayout.IceLogoSpots.Length, logos.Length);

            var replay = new GameObject("Replay View").AddComponent<Camera>();
            replay.transform.SetParent(_rink.transform);
            replay.aspect = 16f / 9f;
            replay.transform.position = new Vector3(0f, 2.3f, (float)RinkLayout.NearEndBoardsZ - 0.6f);
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
                if (spot.Kind == RinkLayout.IceLogoKind.CentreIce) Assert.AreEqual(TeamSkin.League.CentreIceLogo, art);
                else if (spot.Kind == RinkLayout.IceLogoKind.NeutralZoneRound) Assert.AreEqual(RinkAds.RoundArt(Brands.ForIceLogo(i)), art);
                else Assert.AreEqual(RinkAds.PanelArt(Brands.ForIceLogo(i), false), art);
            }
        }

        [Test]
        public void The_centre_ice_logo_can_be_swapped_and_the_brands_stay()
        {
            var skin = Object.Instantiate(TeamSkin.League);
            skin.CentreIceLogo = "panel-shieldcross-soft"; // any art no ice logo uses
            try
            {
                GameObject ads = RinkAds.Build(_rink.transform, skin);
                Renderer[] logos = IceLogos(ads);
                for (int i = 0; i < logos.Length; i++)
                {
                    bool centre = RinkLayout.IceLogoSpots[i].Kind == RinkLayout.IceLogoKind.CentreIce;
                    Assert.AreEqual(centre, Art(logos[i]) == skin.CentreIceLogo, RinkLayout.IceLogoSpots[i].ToString());
                }
            }
            finally { Object.DestroyImmediate(skin); }
        }
    }
}
