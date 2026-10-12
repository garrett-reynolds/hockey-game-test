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
            public Brand(string id, string name) { Id = id; Name = name; }
            public override string ToString() => Name;
        }

        public static readonly Brand[] All =
        {
            new Brand("northvale-bank", "Northvale Bank"),
            new Brand("glacierlink", "GlacierLink"),
            new Brand("brawnys-lager", "Brawny's Lager"),
            new Brand("kestrel-motors", "Kestrel Motors"),
            new Brand("aurora-air", "Aurora Air"),
            new Brand("shieldcross", "Shieldcross"),
            new Brand("volt-rush", "Volt Rush"),
            new Brand("big-timber-burgers", "Big Timber Burgers"),
            new Brand("peakside-energy", "Peakside Energy"),
            new Brand("frostbyte-pcs", "FrostByte PCs"),
        };

        /// <summary>The brand on ad panel slot i (RinkLayout.AdPanelSlots): the brands in turn, all round the boards.</summary>
        public static Brand ForPanel(int slot) => All[slot % All.Length];

        /// <summary>The brand on ice logo spot i (RinkLayout.IceLogoSpots); null for the centre-ice logo, which is the skin's.</summary>
        public static Brand ForIceLogo(int spot) =>
            RinkLayout.IceLogoSpots[spot].Kind == RinkLayout.IceLogoKind.CentreIce ? null : All[IceLogoBrand[spot]];

        /// <summary>Index into All for each ice logo spot, in RinkLayout.IceLogoSpots order (the centre-ice entry is unused).</summary>
        static readonly int[] IceLogoBrand = { -1, 0, 1, 4, 3, 6, 9, 5, 7 };
    }
}
