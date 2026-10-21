using System;
using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>The one place the Ads art is loaded by name: each picture is found once per brand and spot, then reused.</summary>
    public static class ArtLookup
    {
        public const string CentreIceId = "centre-ice", GameLogoId = "game-logo";

        /// <summary>How many times art was loaded from Resources (the cache hits do not count).</summary>
        public static int Loads { get; private set; }

        /// <summary>The made colour panel for a brand id that has a saved custom logo, else null (#226). Tests put a fake here.</summary>
        public static Func<string, Texture2D> CustomPanel = CustomLogos.Panel;

        /// <summary>The made picture for a brand id with a saved custom logo at one spot ("panel-soft", "round", "ribbon", "centre"), else null (#227). Tests put a fake here.</summary>
        public static Func<string, string, Texture2D> CustomSpot = CustomLogos.Spot;

        /// <summary>Counts each Clear, so a screen can tell the art changed since it last built.</summary>
        public static int Version { get; private set; }

        static readonly Dictionary<(string id, string spot), Texture2D> Cache = new Dictionary<(string id, string spot), Texture2D>();

        /// <summary>A brand's panel: its logo in colour, or the soft grey version for the Calm Area.</summary>
        public static Texture2D Panel(Brands.Brand brand, bool soft) =>
            Get(brand.Id, soft ? "panel-soft" : "panel", () => (soft ? CustomSpot(brand.Id, "panel-soft") : CustomPanel(brand.Id)) ?? Load(RinkAds.PanelArt(brand, soft)));

        /// <summary>The run's panels 0 and 1 are the brand's logo (look A: logo, logo, tagline); the rest are taglines and stay shipped art.</summary>
        public const int RunLogoPanels = 2;

        /// <summary>Panel n of a brand's board run, in colour or soft. A custom logo (#226) takes the colour logo panels of an existing run.</summary>
        public static Texture2D RunPanel(Brands.Brand brand, int n, bool soft) =>
            Get(brand.Id, "run-" + n + (soft ? "-soft" : ""), () =>
            {
                Texture2D shipped = Load(RinkAds.SequenceArt(brand, n, soft));
                if (shipped == null || n >= RunLogoPanels) return shipped; // no run panel here: the run keeps its length
                return (soft ? CustomSpot(brand.Id, "panel-soft") : CustomPanel(brand.Id)) ?? shipped;
            });

        /// <summary>A brand's round ice logo: the made round logo for a custom logo (#227), else the colour panel for now.</summary>
        public static Texture2D Round(Brands.Brand brand) =>
            Get(brand.Id, "round", () => CustomSpot(brand.Id, "round") ?? Load(RinkAds.PanelArt(brand, false)));

        /// <summary>A brand's Ribbon strip (a strip that tiles), else its colour panel. A custom logo (#227) gives a made strip.</summary>
        public static Texture2D Ribbon(Brands.Brand brand) =>
            Get(brand.Id, "ribbon", () => CustomSpot(brand.Id, "ribbon") ?? Load("ribbon-" + brand.Id) ?? Load(RinkAds.PanelArt(brand, false)));

        /// <summary>A brand's big centre-ice logo. A custom logo (#227) gives made art, only for a brand that ships centre art.</summary>
        public static Texture2D Centre(Brands.Brand brand) =>
            Get(brand.Id, "centre", () =>
            {
                Texture2D shipped = Load(RinkAds.CentreArt(brand));
                return shipped == null ? null : CustomSpot(brand.Id, "centre") ?? shipped;
            });

        /// <summary>The centre-ice logo: the Leafs art, else the skin's own (null: the League Skin's).</summary>
        public static Texture2D CentreIce(TeamSkin skin = null)
        {
            string art = TeamSkin.OrLeague(skin).CentreIceLogo;
            return Get(CentreIceId, art, () => Load(RinkAds.LeafsCentreArt) ?? Load(art));
        }

        /// <summary>The saved game logo; none: the start screen draws its own logo, and a later ticket gives it a saved picture.</summary>
        public static Texture2D GameLogo() => null;

        /// <summary>Forgets every picture cached under id, so the next ask loads it afresh.</summary>
        public static void Clear(string id)
        {
            Version++;
            var keys = new List<(string id, string spot)>();
            foreach ((string id, string spot) key in Cache.Keys)
                if (key.id == id) keys.Add(key);
            foreach ((string id, string spot) key in keys) Cache.Remove(key);
        }

        static Texture2D Get(string id, string spot, Func<Texture2D> load)
        {
            if (Cache.TryGetValue((id, spot), out Texture2D cached) && cached != null) return cached;
            Texture2D texture = load();
            Cache[(id, spot)] = texture;
            return texture;
        }

        static Texture2D Load(string name)
        {
            Loads++;
            return Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + name);
        }
    }
}
