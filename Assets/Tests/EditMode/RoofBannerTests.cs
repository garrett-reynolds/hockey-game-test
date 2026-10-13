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
            Transform rink = null;
            try
            {
                rink = RinkBuilder.Build(out camera);
                Transform row = rink.Find(RoofBanners.Part);
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
                    Assert.GreaterOrEqual(nearestX, (float)RinkLayout.VideoBoardWidth / 2f, banner.name + " is clear of the Video Board's width");
                    Assert.GreaterOrEqual(nearestX, (float)RinkLayout.CalmAreaHalfWidth, banner.name + " is outside the Calm Area strip");
                    Assert.Greater(b.min.z, 0f, banner.name + " is behind the near net, not on the ice");
                    // Round the end corner the lower stands rise with the offset from the boards; the banner's foot must clear them.
                    double offset = System.Math.Sqrt(System.Math.Pow(System.Math.Abs(b.center.x) - RinkLayout.CornerX, 2) +
                        System.Math.Pow(b.center.z - RinkLayout.NearCornerZ, 2)) - RinkLayout.CornerRadius;
                    double stands = RinkLayout.StandsFrontHeight + RinkLayout.LowerRise * (offset - RinkLayout.StandsFront) / RinkLayout.RowDepth;
                    Assert.GreaterOrEqual(b.min.y, stands + 0.5, banner.name + " hangs clear of the seats (stands " + stands.ToString("0.0") + " m high there)");
                    Vector3 v = camera.WorldToViewportPoint(b.center);
                    if (v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f) seen++;
                }
                Assert.GreaterOrEqual(seen, 6, "at least 6 banners are in the shooter's view");
            }
            finally
            {
                if (rink != null) Object.DestroyImmediate(rink.gameObject);
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
