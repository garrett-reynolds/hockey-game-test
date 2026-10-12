namespace HockeyGame.Core
{
    /// <summary>
    /// The 10 made-up brands on the board ads and ice logos, and which brand goes where. Fixed: no Team Skin changes them.
    /// Each brand's art (Tools/make_ads.py) is named after its Id: keep the ids in step with that script.
    /// </summary>
    public static class Brands
    {
        public sealed class Brand
        {
            /// <summary>The name the art files are named after, e.g. "northvale-bank".</summary>
            public readonly string Id;
            public readonly string Name;
            /// <summary>The brand's words as the LED ads show them (Ribbon Boards and Video Board ad slides), e.g. "NORTHVALE BANK".</summary>
            public readonly string AdWords;
            /// <summary>The LED ads' background and words colours, from the #77 prototype.</summary>
            public readonly ArenaColour AdBack, AdFore;
            public Brand(string id, string name, string adWords, ArenaColour adBack, ArenaColour adFore)
            {
                Id = id;
                Name = name;
                AdWords = adWords;
                AdBack = adBack;
                AdFore = adFore;
            }
            public override string ToString() => Name;
        }

        static readonly ArenaColour White = new ArenaColour(255, 255, 255);

        /// <summary>The 10 brands, in the order the ads cycle through them.</summary>
        public static readonly Brand[] All =
        {
            new Brand("northvale-bank", "Northvale Bank", "NORTHVALE BANK", new ArenaColour(12, 35, 80), White),
            new Brand("glacierlink", "GlacierLink", "GlacierLink", White, new ArenaColour(110, 40, 170)),
            new Brand("brawnys-lager", "Brawny's Lager", "BRAWNY'S LAGER", new ArenaColour(200, 150, 30), new ArenaColour(60, 20, 10)),
            new Brand("kestrel-motors", "Kestrel Motors", "KESTREL MOTORS", new ArenaColour(30, 30, 35), new ArenaColour(200, 205, 210)),
            new Brand("aurora-air", "Aurora Air", "Aurora Air", White, new ArenaColour(0, 130, 140)),
            new Brand("shieldcross", "Shieldcross", "SHIELDCROSS", new ArenaColour(0, 90, 60), White),
            new Brand("volt-rush", "Volt Rush", "VOLT RUSH", new ArenaColour(20, 20, 20), new ArenaColour(170, 230, 0)),
            new Brand("big-timber-burgers", "Big Timber Burgers", "BIG TIMBER BURGERS", new ArenaColour(235, 110, 20), White),
            new Brand("peakside-energy", "Peakside Energy", "PEAKSIDE ENERGY", White, new ArenaColour(0, 80, 170)),
            new Brand("frostbyte-pcs", "FrostByte PCs", "FrostByte PCs", new ArenaColour(0, 170, 220), White),
        };

        /// <summary>True for a brand's words as the LED ads show them.</summary>
        public static bool IsAdWords(string text)
        {
            foreach (Brand b in All)
                if (b.AdWords == text) return true;
            return false;
        }

        /// <summary>The brand on ad panel slot i (RinkLayout.AdPanelSlots): the brands in turn, all round the boards.</summary>
        public static Brand ForPanel(int slot) => All[slot % All.Length];

        /// <summary>The brand on ice logo spot i (RinkLayout.IceLogoSpots); null for the centre-ice logo, which is the skin's.</summary>
        public static Brand ForIceLogo(int spot) =>
            RinkLayout.IceLogoSpots[spot].Kind == RinkLayout.IceLogoKind.CentreIce ? null : All[IceLogoBrand[spot]];

        /// <summary>Index into All for each ice logo spot, in RinkLayout.IceLogoSpots order (the centre-ice entry is unused).</summary>
        static readonly int[] IceLogoBrand = { -1, 0, 1, 4, 3, 6, 9, 5, 7 };
    }
}
