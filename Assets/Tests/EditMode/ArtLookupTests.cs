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

        // #226: a brand with a saved custom logo gives the made colour panel; the fake stands for the made panel.
        System.Func<string, Texture2D> _customWas;
        Texture2D _fake;

        [SetUp]
        public void KeepCustom() { _customWas = ArtLookup.CustomPanel; }

        [TearDown]
        public void PutCustomBack()
        {
            ArtLookup.CustomPanel = _customWas;
            foreach (Brands.Brand b in Brands.All) ArtLookup.Clear(b.Id);
            if (_fake != null) Object.DestroyImmediate(_fake);
        }

        static Brands.Brand BrandWithRun()
        {
            foreach (Brands.Brand b in Brands.All) if (RinkAds.SequenceLength(b) >= 3) return b;
            Assert.Fail("no brand has a run");
            return null;
        }

        [Test]
        public void A_custom_logo_gives_the_made_panel_for_the_colour_panel_and_the_run_logo_panels_only()
        {
            Brands.Brand b = BrandWithRun(), other = Brands.All[0] == b ? Brands.All[1] : Brands.All[0];
            int length = RinkAds.SequenceLength(b);
            _fake = new Texture2D(4, 4);
            ArtLookup.CustomPanel = id => id == b.Id ? _fake : null;
            ArtLookup.Clear(b.Id); ArtLookup.Clear(other.Id);

            Assert.AreSame(_fake, ArtLookup.Panel(b, false), "colour panel");
            Assert.AreSame(_fake, ArtLookup.RunPanel(b, 0, false), "run logo panel 0");
            Assert.AreSame(_fake, ArtLookup.RunPanel(b, 1, false), "run logo panel 1");
            for (int n = 2; n < length; n++)
                Assert.AreSame(Shipped(RinkAds.SequenceArt(b, n, false)), ArtLookup.RunPanel(b, n, false), "tagline panel " + n + " stays shipped");
            Assert.AreSame(Shipped(RinkAds.PanelArt(b, true)), ArtLookup.Panel(b, true), "the soft panel is #227");
            Assert.AreSame(Shipped(RinkAds.PanelArt(other, false)), ArtLookup.Panel(other, false), "another brand keeps shipped art");
            Assert.AreEqual(length, RinkAds.SequenceLength(b), "the run keeps its length");
        }

        // #227: the soft panel, the soft run logo panels, the round logo, the Ribbon strip and the centre art are made too.
        System.Func<string, string, Texture2D> _spotWas;
        readonly System.Collections.Generic.Dictionary<string, Texture2D> _fakes = new System.Collections.Generic.Dictionary<string, Texture2D>();

        [SetUp]
        public void KeepSpot() { _spotWas = ArtLookup.CustomSpot; }

        [TearDown]
        public void PutSpotBack()
        {
            ArtLookup.CustomSpot = _spotWas;
            foreach (Texture2D t in _fakes.Values) Object.DestroyImmediate(t);
            _fakes.Clear();
        }

        void FakeSpots(string customId)
        {
            foreach (string spot in new[] { "panel-soft", "round", "ribbon", "centre" }) _fakes[spot] = new Texture2D(4, 4) { name = spot };
            ArtLookup.CustomSpot = (id, spot) => id == customId && _fakes.ContainsKey(spot) ? _fakes[spot] : null;
        }

        [Test]
        public void A_custom_logo_gives_the_made_soft_panel_soft_run_logo_panels_round_ribbon_and_centre()
        {
            Brands.Brand b = BrandWithRun(), other = Brands.All[0] == b ? Brands.All[1] : Brands.All[0];
            int length = RinkAds.SequenceLength(b);
            FakeSpots(b.Id);
            ArtLookup.Clear(b.Id); ArtLookup.Clear(other.Id);

            Assert.AreSame(_fakes["panel-soft"], ArtLookup.Panel(b, true), "soft panel");
            Assert.AreSame(_fakes["panel-soft"], ArtLookup.RunPanel(b, 0, true), "soft run logo panel 0");
            Assert.AreSame(_fakes["panel-soft"], ArtLookup.RunPanel(b, 1, true), "soft run logo panel 1");
            for (int n = 2; n < length; n++)
                Assert.AreSame(Shipped(RinkAds.SequenceArt(b, n, true)), ArtLookup.RunPanel(b, n, true), "soft tagline panel " + n + " stays shipped");
            Assert.AreSame(_fakes["round"], ArtLookup.Round(b), "round");
            Assert.AreSame(_fakes["ribbon"], ArtLookup.Ribbon(b), "ribbon");
            Assert.AreSame(_fakes["centre"], ArtLookup.Centre(b), "centre");
            Assert.AreEqual(length, RinkAds.SequenceLength(b), "the run keeps its length");

            Assert.AreSame(Shipped(RinkAds.PanelArt(other, true)), ArtLookup.Panel(other, true), "another brand keeps its soft art");
            Assert.AreSame(Shipped(RinkAds.PanelArt(other, false)), ArtLookup.Round(other), "another brand keeps its round art");
            Assert.AreSame(Shipped("ribbon-" + other.Id), ArtLookup.Ribbon(other), "another brand keeps its ribbon");
            Assert.AreSame(Shipped(RinkAds.CentreArt(other)), ArtLookup.Centre(other), "another brand keeps its centre art");
            Assert.AreSame(Shipped(RinkAds.LeafsCentreArt), ArtLookup.CentreIce(), "the Leafs centre-ice spot does not change");
        }

        [Test]
        public void Clearing_the_id_drops_every_made_picture_and_the_shipped_art_comes_back()
        {
            Brands.Brand b = BrandWithRun();
            ArtLookup.Clear(b.Id);
            FakeSpots(null);
            Texture2D soft = ArtLookup.Panel(b, true), round = ArtLookup.Round(b), ribbon = ArtLookup.Ribbon(b), centre = ArtLookup.Centre(b), run = ArtLookup.RunPanel(b, 0, true);
            FakeSpots(b.Id);
            Assert.AreSame(soft, ArtLookup.Panel(b, true), "the cache holds the old art until SAVE clears it");
            ArtLookup.Clear(b.Id);
            Assert.AreSame(_fakes["panel-soft"], ArtLookup.Panel(b, true));
            Assert.AreSame(_fakes["panel-soft"], ArtLookup.RunPanel(b, 0, true));
            Assert.AreSame(_fakes["round"], ArtLookup.Round(b));
            Assert.AreSame(_fakes["ribbon"], ArtLookup.Ribbon(b));
            Assert.AreSame(_fakes["centre"], ArtLookup.Centre(b));
            ArtLookup.CustomSpot = (id, spot) => null;
            ArtLookup.Clear(b.Id);
            Assert.AreSame(soft, ArtLookup.Panel(b, true));
            Assert.AreSame(round, ArtLookup.Round(b));
            Assert.AreSame(ribbon, ArtLookup.Ribbon(b));
            Assert.AreSame(centre, ArtLookup.Centre(b));
            Assert.AreSame(run, ArtLookup.RunPanel(b, 0, true));
        }

        [Test]
        public void Nothing_changes_until_the_id_is_cleared_and_clearing_counts_a_new_version()
        {
            Brands.Brand b = BrandWithRun();
            ArtLookup.Clear(b.Id);
            Texture2D shipped = ArtLookup.Panel(b, false);
            _fake = new Texture2D(4, 4);
            ArtLookup.CustomPanel = id => id == b.Id ? _fake : null;
            Assert.AreSame(shipped, ArtLookup.Panel(b, false), "before SAVE clears it, the cache holds the old art");
            int version = ArtLookup.Version;
            ArtLookup.Clear(b.Id);
            Assert.AreEqual(version + 1, ArtLookup.Version);
            Assert.AreSame(_fake, ArtLookup.Panel(b, false));
            ArtLookup.CustomPanel = id => null;
            ArtLookup.Clear(b.Id);
            Assert.AreSame(shipped, ArtLookup.Panel(b, false), "USE DEFAULT and SAVE bring the shipped art back");
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
