using System;
using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The board ads and ice logos, from the Rink Layout's ad panel slots and ice logo spots, with the art from
    /// Tools/make_ads.py (Assets/Resources/Ads). One row of white printed panels runs all round the boards in the ad band,
    /// corners included, each with a brand's logo (Brands); panels in the Calm Area's bounds show the soft grey logo, matte.
    /// Nothing goes on the glass. The ice logos lie just over the ice, between the blue lines, and read from behind the
    /// near net looking out (the Replay view). The centre-ice logo is the Team Skin's (the League Skin's is the
    /// "All-Star Hockey" mark); the brands are the game load's set (Brands.Shown), a new random one each load.
    /// </summary>
    public static class RinkAds
    {
        public const string Part = "Ads", BoardAdsPart = "Board Ads", IceLogosPart = "Ice Logos";
        /// <summary>The art's folder under Resources.</summary>
        public const string ArtFolder = "Ads";

        /// <summary>
        /// A new shuffle of the brand deck each game load (#223), before any scene builds the rink: runs on every Play start
        /// (the Editor keeps static fields over Play, no domain reload) and on every page load of the Web build.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewBrandDeckEachLoad()
        {
            Brands.UseOff(SaveStore.Current.BrandsOff); // the brands unticked in Settings stay out (#223)
            Brands.NewLoad(new System.Random());
        }

        /// <summary>Swaps the ads under rink for this Round's brand (Brands.Current): the old Ads part and its meshes and
        /// materials go, a new one is built (each art keeps its own shape, so the meshes are made again).</summary>
        public static GameObject Rebuild(Transform rink, TeamSkin skin = null)
        {
            Transform old = rink.Find(Part);
            if (old != null)
            {
                old.name = Part + " (old)"; // Destroy waits for the frame's end in Play: the new part is the only "Ads" at once
                var made = new HashSet<UnityEngine.Object>();
                foreach (MeshFilter f in old.GetComponentsInChildren<MeshFilter>()) if (f.sharedMesh != null) made.Add(f.sharedMesh);
                foreach (Renderer r in old.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial != null) made.Add(r.sharedMaterial);
                SceneObjects.DestroySafely(old.gameObject);
                foreach (UnityEngine.Object o in made) SceneObjects.DestroySafely(o);
            }
            return Build(rink, skin);
        }

        /// <summary>A brand's big centre-ice logo (the logo only, clear round it); without it, its panel art stands in.</summary>
        public static string CentreArt(Brands.Brand brand) => "centre-" + brand.Id;

        /// <summary>A brand's panel art: its logo in colour (also laid on the ice), or soft grey for the Calm Area.</summary>
        public static string PanelArt(Brands.Brand brand, bool soft) => "panel-" + brand.Id + (soft ? "-soft" : "");
        /// <summary>Panel n of a brand's board run (look A, #223): logo, logo, then the tagline, one whole panel each.</summary>
        public static string SequenceArt(Brands.Brand brand, int n, bool soft) => "panel-" + brand.Id + "-" + n + (soft ? "-soft" : "");

        /// <summary>How many panels the brand's board run has (art panel-&lt;id&gt;-0, -1, ... until one is missing); 0: none.</summary>
        public static int SequenceLength(Brands.Brand brand)
        {
            if (RunLengths.TryGetValue(brand.Id, out int known)) return known;
            int n = 0;
            while (n < 16 && Art(SequenceArt(brand, n, false)) != null) n++;
            RunLengths[brand.Id] = n;
            return n;
        }
        static readonly Dictionary<string, int> RunLengths = new Dictionary<string, int>();

        /// <summary>The art on board slot i: the brand's run laid on neighbouring slots in reading order (left to right from the
        /// ice), repeating round the rink and stopping part-way where it wraps; the Calm Area takes the soft version of the same
        /// panel. A brand with no run shows its one panel on every slot.</summary>
        public static string SlotArt(Brands.Brand brand, int slot, bool soft)
        {
            int run = SequenceLength(brand);
            if (run == 0) return PanelArt(brand, soft);
            int k = SlotsReadForward ? slot : RinkLayout.AdPanelSlots.Length - 1 - slot;
            return SequenceArt(brand, k % run, soft);
        }
        /// <summary>Whether slot i + 1 is to the viewer's right of slot i (the slots' order round the boards).</summary>
        static readonly bool SlotsReadForward = true;

        /// <summary>Centre ice always shows the Toronto Maple Leafs logo, every Round (Garrett, #223); not a pool brand.</summary>
        public const string LeafsCentreArt = "centre-maple-leafs";

        /// <summary>How far the panels stand off the board face (past the 1 cm the boards model's corner chords cut in from the arc), and the ice logos over the ice (metres).</summary>
        const float PanelLift = 0.025f, IceLift = 0.006f;
        /// <summary>Corner panels bend round the arc in steps this long (metres).</summary>
        const float BendStep = 0.25f;
        const float PanelSmoothness = 0.2f, SoftPanelSmoothness = 0f, IceLogoSmoothness = 0.5f;

        /// <summary>Adds the ads under rink, with the skin's centre-ice logo (null: the League Skin's).</summary>
        public static GameObject Build(Transform rink, TeamSkin skin = null)
        {
            Texture2D centreIceLogo = Art(TeamSkin.OrLeague(skin).CentreIceLogo);
            var ads = new GameObject(Part);
            ads.transform.SetParent(rink, false);
            var materials = new Dictionary<string, Material>();

            var boards = new GameObject(BoardAdsPart).transform;
            boards.SetParent(ads.transform, false);
            RinkLayout.AdPanelSlot[] slots = RinkLayout.AdPanelSlots;
            for (int i = 0; i < slots.Length; i++)
            {
                Brands.Brand brand = Brands.ForPanel(i);
                string art = SlotArt(brand, i, slots[i].CalmArea);
                Texture2D texture = Art(art);
                Material m = Shared(materials, art, () => Opaque(texture, slots[i].CalmArea ? SoftPanelSmoothness : PanelSmoothness));
                Piece(boards, $"Ad Panel {i} {brand.Id}", PanelMesh(slots[i], texture), m);
            }

            var ice = new GameObject(IceLogosPart).transform;
            ice.SetParent(ads.transform, false);
            RinkLayout.IceLogoSpot[] spots = RinkLayout.IceLogoSpots;
            for (int i = 0; i < spots.Length; i++)
            {
                RinkLayout.IceLogoSpot spot = spots[i];
                Brands.Brand brand = Brands.ForIceLogo(i);
                Texture2D texture = brand == null ? (Art(LeafsCentreArt) ?? centreIceLogo) : Art(PanelArt(brand, false));
                string key = "ice " + (texture != null ? texture.name : "none");
                Material m = Shared(materials, key, () => SeeThrough(texture));
                Piece(ice, $"Ice Logo {i} {(brand == null ? "centre ice" : brand.Id)}", IceLogoMesh(spot, texture), m);
            }
            return ads;
        }

        static Texture2D Art(string name) => Resources.Load<Texture2D>(ArtFolder + "/" + name);

        static Material Shared(Dictionary<string, Material> materials, string key, Func<Material> make)
        {
            if (!materials.TryGetValue(key, out Material m)) materials[key] = m = make();
            return m;
        }

        /// <summary>
        /// A panel's mesh on the board face, PanelLift in from it, from AdBandBottom to AdBandTop, bending round a corner. The
        /// logo art keeps its shape, centred along the panel; past its ends the panel shows the art's edge (board white).
        /// Seen from the ice, the art reads left to right and upright.
        /// </summary>
        static Mesh PanelMesh(RinkLayout.AdPanelSlot slot, Texture2D texture)
        {
            float bottom = (float)RinkLayout.AdBandBottom, top = (float)RinkLayout.AdBandTop;
            float aspect = texture != null ? (float)texture.width / texture.height : 3f;
            double middle = (slot.FromS + slot.ToS) / 2, artHalf = (top - bottom) * aspect / 2;

            var along = new List<double> { slot.FromS, slot.ToS };
            if (slot.Corner)
                for (double s = slot.FromS + BendStep; s < slot.ToS; s += BendStep) along.Add(s);
            foreach (double s in new[] { middle - artHalf, middle + artHalf })
                if (s > slot.FromS && s < slot.ToS) along.Add(s);
            along.Sort();

            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            foreach (double s in along)
            {
                RinkLayout.BoardPoint b = RinkLayout.OnBoards(s);
                var at = new Vector3((float)(b.X + b.InX * PanelLift), 0f, (float)(b.Z + b.InZ * PanelLift));
                float u = Mathf.Clamp01((float)(0.5 + (s - middle) / (2 * artHalf)));
                vertices.Add(at + Vector3.up * bottom); uv.Add(new Vector2(u, 0f));
                vertices.Add(at + Vector3.up * top); uv.Add(new Vector2(u, 1f));
            }
            var triangles = new List<int>();
            for (int k = 0; k + 1 < along.Count; k++)
            {
                int b0 = 2 * k, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                triangles.AddRange(new[] { b0, t0, t1, b0, t1, b1 }); // clockwise seen from the ice: the front faces in
            }
            return Finish("Ad Panel", vertices, uv, triangles);
        }

        /// <summary>
        /// An ice logo's mesh: flat, IceLift over the ice, as big as fits its spot without changing the art's shape. Its
        /// art's right runs to -x and its top to -z, so it reads from behind the near net looking out.
        /// </summary>
        static Mesh IceLogoMesh(RinkLayout.IceLogoSpot spot, Texture2D texture)
        {
            float aspect = texture != null ? (float)texture.width / texture.height : 1f;
            float w = (float)spot.Width, h = (float)spot.Height;
            if (w / h > aspect) w = h * aspect; else h = w / aspect;
            var c = new Vector3((float)spot.X, IceLift, (float)spot.Z);
            var vertices = new List<Vector3>
            {
                c + new Vector3(w / 2, 0f, h / 2), c + new Vector3(w / 2, 0f, -h / 2),
                c + new Vector3(-w / 2, 0f, -h / 2), c + new Vector3(-w / 2, 0f, h / 2),
            };
            var uv = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            return Finish("Ice Logo", vertices, uv, new List<int> { 0, 1, 2, 0, 2, 3 }); // clockwise from above
        }

        static Mesh Finish(string name, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Piece(Transform parent, string name, Mesh mesh, Material material)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(parent, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = piece.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>A white URP Lit material showing the art (a copy of a primitive's, as RinkBuilder.Paint does).</summary>
        static Material Opaque(Texture2D texture, float smoothness)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var m = new Material(quad.GetComponent<Renderer>().sharedMaterial) { name = "Ad Panel" };
            SceneObjects.DestroySafely(quad);
            m.mainTexture = texture;
            m.color = Color.white;
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// <summary>A see-through URP Lit material showing the art (a copy of the glass asset, so the build keeps the variant).</summary>
        static Material SeeThrough(Texture2D texture)
        {
            var m = new Material(Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + ArenaLighting.GlassMaterial)) { name = "Ice Logo" };
            m.mainTexture = texture;
            m.color = Color.white;
            m.SetFloat("_Smoothness", IceLogoSmoothness);
            return m;
        }
    }
}
