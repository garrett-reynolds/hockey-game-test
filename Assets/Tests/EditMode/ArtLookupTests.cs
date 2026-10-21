using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class ArtLookupTests
    {
        static Texture2D Shipped(string name) => Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + name);

        [Test]
        public void Gives_the_shipped_art_for_each_spot_of_a_brand()
        {
            foreach (Brands.Brand b in Brands.All)
            {
                Assert.AreSame(Shipped(RinkAds.PanelArt(b, false)), ArtLookup.Panel(b, false), b.Id + " colour panel");
                Assert.AreSame(Shipped(RinkAds.PanelArt(b, true)), ArtLookup.Panel(b, true), b.Id + " soft panel");
                Assert.AreSame(Shipped(RinkAds.PanelArt(b, false)), ArtLookup.Round(b), b.Id + " round ice logo: the colour panel until the round art exists");
                Assert.AreSame(Shipped(RinkAds.CentreArt(b)), ArtLookup.Centre(b), b.Id + " centre art");
                for (int n = 0; n < 4; n++)
                {
                    Assert.AreSame(Shipped(RinkAds.SequenceArt(b, n, false)), ArtLookup.RunPanel(b, n, false), b.Id + " run panel " + n);
                    Assert.AreSame(Shipped(RinkAds.SequenceArt(b, n, true)), ArtLookup.RunPanel(b, n, true), b.Id + " soft run panel " + n);
                }
            }
        }

        [Test]
        public void The_ribbon_strip_falls_back_to_the_colour_panel_when_a_brand_has_no_ribbon_art()
        {
            foreach (Brands.Brand b in Brands.All)
            {
                Texture2D strip = Shipped("ribbon-" + b.Id);
                if (strip != null) Assert.AreSame(strip, ArtLookup.Ribbon(b), b.Id);
                else Assert.AreSame(Shipped(RinkAds.PanelArt(b, false)), ArtLookup.Ribbon(b), b.Id + " falls back");
            }
        }

        [Test]
        public void Centre_ice_is_the_Leafs_art_and_the_game_logo_is_none()
        {
            Assert.IsNotNull(ArtLookup.CentreIce(), "centre ice has art");
            Assert.AreSame(Shipped(RinkAds.LeafsCentreArt), ArtLookup.CentreIce(), "centre ice");
            Assert.IsNull(ArtLookup.GameLogo(), "none: the start screen draws its own logo");
        }

        [Test]
        public void Asking_again_reuses_the_cache_and_clearing_one_id_reloads_only_that_id()
        {
            Brands.Brand a = Brands.All[0], b = Brands.All[1];
            ArtLookup.Clear(a.Id); ArtLookup.Clear(b.Id);
            ArtLookup.Panel(a, false); ArtLookup.Panel(b, false);
            int loaded = ArtLookup.Loads;

            ArtLookup.Panel(a, false); ArtLookup.Panel(b, false);
            Assert.AreEqual(loaded, ArtLookup.Loads, "the second ask comes from the cache");

            ArtLookup.Clear(a.Id);
            ArtLookup.Panel(b, false);
            Assert.AreEqual(loaded, ArtLookup.Loads, "another id stays cached");
            ArtLookup.Panel(a, false);
            Assert.AreEqual(loaded + 1, ArtLookup.Loads, "the cleared id loads again");
        }
    }
}
