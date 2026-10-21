using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>
    /// The real brands on the board ads, ice logos and LED ads (#223: the 43 logos Garrett picked; CIBC taken out). Each Round, one brand
    /// takes over the dasher boards, the Ribbon Boards and the ice logos (Current); a BrandDeck deals them with no repeats
    /// until the pool is used up, shuffled again each game load. The Calm Area keeps its soft grey art. No Team Skin
    /// changes the brands. Each brand's art is named after its Id, the logo's SVG name and its row in the sources list
    /// beside the art (Assets/Resources/Ads/sources.csv, with each logo's source URL): keep the ids in step with both.
    /// </summary>
    public static class Brands
    {
        public sealed class Brand
        {
            /// <summary>The name the art files are named after, e.g. "tim-hortons".</summary>
            public readonly string Id;
            public readonly string Name;
            /// <summary>The brand's words as the LED ads show them (the Video Board ad slides), e.g. "Tim Hortons".</summary>
            public readonly string AdWords;
            /// <summary>The brand's hockey line (Garrett approved them all, 2026-10-20), e.g. "ALWAYS FRESH. ALWAYS HOCKEY.".</summary>
            public readonly string Tagline;
            /// <summary>The LED ads' background and words colours: the brand's own colours, kept off the reserved colours.</summary>
            public readonly ArenaColour AdBack, AdFore;
            public Brand(string id, string name, string adWords, string tagline, ArenaColour adBack, ArenaColour adFore)
            {
                Id = id;
                Name = name;
                AdWords = adWords;
                Tagline = tagline;
                AdBack = adBack;
                AdFore = adFore;
            }
            /// <summary>The brand colour (the tagline's script words): the more colourful of AdBack and AdFore, AdBack on a tie.</summary>
            public ArenaColour Colour => Chroma(AdFore) > Chroma(AdBack) ? AdFore : AdBack;
            /// <summary>The tagline's first words (black on the boards, white on the LEDs) and last words (brand colour).</summary>
            public string TaglineFirst => SplitTagline(Tagline).First;
            public string TaglineLast => SplitTagline(Tagline).Last;
            public override string ToString() => Name;
        }

        static int Chroma(ArenaColour c) => System.Math.Max(c.R, System.Math.Max(c.G, c.B)) - System.Math.Min(c.R, System.Math.Min(c.G, c.B));

        static ArenaColour C(byte r, byte g, byte b) => new ArenaColour(r, g, b);
        static readonly ArenaColour White = C(255, 255, 255), Dark = C(40, 40, 45);

        /// <summary>The pool: all 43 brands, in the sources list's order.</summary>
        public static readonly Brand[] All =
        {
            new Brand("scotiabank", "Scotiabank", "Scotiabank", "BANK ON THE TOP SHELF", C(215, 15, 30), White),
            new Brand("td", "TD Bank", "TD BANK", "COMFORTABLE IN THE CREASE", C(0, 138, 0), White),
            new Brand("bmo", "BMO", "BMO", "HERE TO HELP YOU SCORE", C(0, 121, 193), White),
            new Brand("rogers", "Rogers", "ROGERS", "THE FASTEST NETWORK ON THE ICE", C(218, 41, 28), White),
            new Brand("bell", "Bell", "Bell", "LET'S TALK HOCKEY", C(0, 102, 164), White),
            new Brand("telus", "Telus", "TELUS", "THE FUTURE IS FRIENDLY. THE FORECHECK ISN'T.", C(75, 40, 109), C(102, 204, 0)),
            new Brand("molson-canadian", "Molson Canadian", "MOLSON CANADIAN", "MADE FROM CANADA. MADE FOR HOCKEY.", C(200, 16, 46), White),
            new Brand("coors-light", "Coors Light", "COORS LIGHT", "AS COLD AS THE ICE", C(180, 185, 190), C(196, 18, 48)),
            new Brand("budweiser", "Budweiser", "Budweiser", "THE KING OF THE CREASE", C(200, 16, 46), White),
            new Brand("pepsi", "Pepsi", "PEPSI", "THIRSTY FOR OVERTIME", C(0, 48, 135), White),
            new Brand("coca-cola", "Coca-Cola", "Coca-Cola", "OPEN HAPPINESS. OPEN NET.", C(220, 10, 30), White),
            new Brand("gatorade", "Gatorade", "GATORADE", "IS IT IN YOU? IS IT IN THE NET?", C(245, 120, 0), Dark),
            new Brand("red-bull", "Red Bull", "Red Bull", "GIVES YOUR SLAPSHOT WIIINGS", C(0, 30, 80), C(219, 10, 64)),
            new Brand("tim-hortons", "Tim Hortons", "Tim Hortons", "ALWAYS FRESH. ALWAYS HOCKEY.", C(200, 16, 46), White),
            new Brand("mcdonalds", "McDonald's", "McDonald's", "I'M LOVIN' THIS POWER PLAY", C(218, 41, 28), C(255, 199, 44)),
            new Brand("subway", "Subway", "SUBWAY", "BUILD YOUR LINEUP", C(0, 140, 21), C(255, 194, 0)),
            new Brand("dominos", "Domino's", "Domino's", "DELIVERING HAT TRICKS", C(0, 100, 145), White),
            new Brand("honda", "Honda", "HONDA", "THE POWER OF DREAMS. THE POWER PLAY.", C(204, 0, 0), White),
            new Brand("toyota", "Toyota", "TOYOTA", "LET'S GO PLACES. LIKE THE TOP CORNER.", C(215, 10, 30), White),
            new Brand("ford", "Ford", "Ford", "BUILT FORD TOUGH. BUILT FOR THE BOARDS.", C(0, 52, 120), White),
            new Brand("chevrolet", "Chevrolet", "CHEVROLET", "FIND NEW ROADS TO THE NET", Dark, C(210, 165, 70)),
            new Brand("hyundai", "Hyundai", "HYUNDAI", "NEW THINKING. NEW SCORING CHANCES.", C(0, 44, 95), White),
            new Brand("air-canada", "Air Canada", "AIR CANADA", "FLY TO THE TOP SHELF", Dark, C(240, 20, 40)),
            new Brand("westjet", "WestJet", "WESTJET", "WE'VE GOT YOUR BACKCHECK", C(0, 40, 85), C(0, 170, 165)),
            new Brand("bauer", "Bauer", "BAUER", "BUILT FOR THE PERFECT RELEASE", Dark, C(0, 160, 225)),
            new Brand("ccm", "CCM", "CCM", "MADE FOR THE BIG MOMENT", C(220, 30, 40), White),
            new Brand("warrior", "Warrior", "WARRIOR", "SHOOT LIKE A WARRIOR", Dark, White),
            new Brand("esso", "Esso", "Esso", "FUEL FOR THE FINAL SHIFT", White, C(225, 25, 45)),
            new Brand("petro-canada", "Petro-Canada", "PETRO-CANADA", "FUELLING CANADA'S GAME", C(230, 30, 55), White),
            new Brand("geico", "GEICO", "GEICO", "15 MINUTES COULD SAVE YOU A SHUTOUT", White, C(0, 50, 120)),
            new Brand("sap", "SAP", "SAP", "RUN THE PLAY BETTER", C(0, 143, 211), White),
            new Brand("amazon", "Amazon", "amazon", "DELIVERING GOALS FROM A TO Z", C(35, 47, 62), C(255, 153, 0)),
            new Brand("canadian-tire", "Canadian Tire", "CANADIAN TIRE", "GEAR UP FOR THE THIRD PERIOD", C(213, 43, 30), White),
            new Brand("sport-chek", "Sport Chek", "SPORT CHEK", "ALL IN ON HOCKEY", Dark, C(230, 40, 50)),
            new Brand("enterprise", "Enterprise", "enterprise", "WE'LL PICK YOU UP AFTER THE GAME", C(22, 155, 98), White),
            new Brand("shell", "Shell", "Shell", "GO WELL. GO FIVE-HOLE.", C(251, 206, 7), C(221, 29, 33)),
            new Brand("kia", "Kia", "KIA", "MOVEMENT THAT INSPIRES. TOE DRAGS TOO.", C(25, 40, 55), White),
            new Brand("rbc", "RBC", "RBC Royal Bank", "INVESTING IN EVERY SHIFT", C(0, 93, 170), C(255, 210, 0)),
            new Brand("michelob-ultra", "Michelob Ultra", "MICHELOB ULTRA", "SUPERIOR LIGHT. SUPERIOR SNIPE.", C(0, 40, 95), C(200, 205, 210)),
            new Brand("monster-energy", "Monster Energy", "MONSTER ENERGY", "UNLEASH THE BEAST ON THE BREAKAWAY", Dark, C(149, 213, 0)),
            new Brand("pizza-pizza", "Pizza Pizza", "Pizza Pizza", "GRAB A SLICE OF THE ACTION", C(230, 40, 40), White),
            new Brand("labatt-blue", "Labatt Blue", "LABATT BLUE", "SEE THE BLUE LINE? GRAB A BLUE.", C(0, 40, 130), White),
            new Brand("discover", "Discover", "DISCOVER", "DISCOVER THE FIVE-HOLE", Dark, C(255, 110, 0)),
        };

        /// <summary>
        /// Splits a tagline into its first words (black) and last words (brand colour, script): after the first sentence when
        /// it has two ("ALWAYS FRESH." / "ALWAYS HOCKEY."), else after half its words, rounded down (at least one).
        /// </summary>
        public static (string First, string Last) SplitTagline(string tagline)
        {
            string[] words = tagline.Split(' ');
            int split = words.Length / 2;
            for (int i = 0; i + 1 < words.Length; i++)
                if (words[i].EndsWith(".") || words[i].EndsWith("?")) { split = i + 1; break; }
            if (split < 1) split = 1;
            return (string.Join(" ", words, 0, split), string.Join(" ", words, split, words.Length - split));
        }

        static BrandDeck _deck;
        static System.Random _random;

        /// <summary>This Round's brand (the start screen shows the load's first one): on all boards and the ribbon.</summary>
        public static Brand Current { get; private set; } = All[0];

        /// <summary>This Round's four ice brands (PickIce): centre ice, beside the centre circle on -x, on +x, the neutral-zone rounds.</summary>
        public static IReadOnlyList<Brand> Ice { get; private set; } = new[] { All[0], All[1], All[2], All[3] };

        /// <summary>A new game load: a new deck shuffled with random, and its first brand for the start screen.</summary>
        public static void NewLoad(System.Random random)
        {
            _random = random;
            _deck = new BrandDeck(_pool, random);
            Current = _deck.Next();
            Ice = PickIce(_pool, random);
        }

        /// <summary>A Round starts: the deck's next brand and four new ice brands (ScreenFlow calls it). Without a load yet, deals a new deck first.</summary>
        public static void NextRound()
        {
            if (_deck == null) { _random = new System.Random(); _deck = new BrandDeck(_pool, _random); }
            Current = _deck.Next();
            Ice = PickIce(_pool, _random);
        }

        /// <summary>Four different brands from pool for the ice (Garrett, #223): they need not match the Round's brand. A pool
        /// of fewer than four (brands unticked in Settings) repeats brands once each is used.</summary>
        public static Brand[] PickIce(IReadOnlyList<Brand> pool, System.Random random)
        {
            var picked = new Brand[4];
            var used = new HashSet<int>();
            for (int k = 0; k < 4; k++)
            {
                if (used.Count == pool.Count) used.Clear();
                int i;
                do i = random.Next(pool.Count); while (!used.Add(i));
                picked[k] = pool[i];
            }
            return picked;
        }

        /// <summary>The pool brands left after the Settings page's unticked ones (off: their ids, comma-separated); never empty.</summary>
        public static IReadOnlyList<Brand> Pool(string off)
        {
            var offIds = new HashSet<string>((off ?? "").Split(','));
            var pool = new List<Brand>();
            foreach (Brand b in All) if (!offIds.Contains(b.Id)) pool.Add(b);
            return pool.Count > 0 ? pool : (IReadOnlyList<Brand>)All;
        }

        static IReadOnlyList<Brand> _pool = All;
        static string _off = "";

        /// <summary>
        /// Takes the Settings page's unticked brands out of the rotation (off: their ids, comma-separated): the game load and
        /// each Save call it. A new deck from the pool left; this Round's brand and ice brands change only if one was turned off.
        /// </summary>
        public static void UseOff(string off)
        {
            off = off ?? "";
            if (off == _off) return;
            _off = off;
            _pool = Pool(off);
            if (_random == null) _random = new System.Random();
            _deck = new BrandDeck(_pool, _random);
            if (!Contains(_pool, Current)) Current = _deck.Next();
            foreach (Brand b in Ice)
                if (!Contains(_pool, b)) { Ice = PickIce(_pool, _random); break; }
        }

        static bool Contains(IReadOnlyList<Brand> pool, Brand brand)
        {
            foreach (Brand b in pool) if (b == brand) return true;
            return false;
        }

        /// <summary>Shows these four ice brands (tests put the old ones back with it).</summary>
        public static void UseIce(IReadOnlyList<Brand> ice) => Ice = ice;

        /// <summary>Shows brand (tests put the old one back with it).</summary>
        public static void Use(Brand brand) => Current = brand;

        /// <summary>True for a brand's words as the LED ads show them.</summary>
        public static bool IsAdWords(string text)
        {
            foreach (Brand b in All)
                if (b.AdWords == text) return true;
            return false;
        }

        /// <summary>The brand on ad panel slot i (RinkLayout.AdPanelSlots): this Round's, all round the boards.</summary>
        public static Brand ForPanel(int slot) => Current;

        /// <summary>The brand on ice logo spot i (RinkLayout.IceLogoSpots). Centre ice is null: it always shows the Toronto Maple
        /// Leafs logo, not a pool brand. The four spots beside the centre circle show this Round's four ice brands, in spot order.</summary>
        public static Brand ForIceLogo(int spot)
        {
            if (RinkLayout.IceLogoSpots[spot].Kind == RinkLayout.IceLogoKind.CentreIce) return null;
            int beside = 0;
            for (int i = 0; i < spot; i++)
                if (RinkLayout.IceLogoSpots[i].Kind == RinkLayout.IceLogoKind.BesideCentreCircle) beside++;
            return Ice[beside];
        }
    }

    /// <summary>
    /// Deals brands one at a time with no repeats until the pool is used up, then shuffles the pool again (never starting the
    /// new pass with the brand that ended the last one). The same random gives the same order.
    /// </summary>
    public sealed class BrandDeck
    {
        readonly Brands.Brand[] _order;
        readonly System.Random _random;
        int _next;
        Brands.Brand _last;

        public BrandDeck(System.Collections.Generic.IReadOnlyList<Brands.Brand> pool, System.Random random)
        {
            _random = random ?? throw new System.ArgumentNullException(nameof(random));
            _order = new Brands.Brand[pool.Count];
            for (int i = 0; i < pool.Count; i++) _order[i] = pool[i];
            _next = _order.Length; // shuffles on the first Next
        }

        /// <summary>The next brand.</summary>
        public Brands.Brand Next()
        {
            if (_next >= _order.Length)
            {
                for (int i = _order.Length - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    (_order[i], _order[j]) = (_order[j], _order[i]);
                }
                if (_order.Length > 1 && _order[0] == _last)
                {
                    int j = 1 + _random.Next(_order.Length - 1);
                    (_order[0], _order[j]) = (_order[j], _order[0]);
                }
                _next = 0;
            }
            _last = _order[_next++];
            return _last;
        }
    }
}
