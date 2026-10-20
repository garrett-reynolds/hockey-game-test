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
    /// "All-Star Hockey" mark); the brands are fixed.
    /// </summary>
    public static class RinkAds
    {
        public const string Part = "Ads", BoardAdsPart = "Board Ads", IceLogosPart = "Ice Logos";
        /// <summary>The art's folder under Resources.</summary>
        public const string ArtFolder = "Ads";

        /// <summary>A brand's panel art: its logo in colour (also laid on the ice), or soft grey for the Calm Area.</summary>
        public static string PanelArt(Brands.Brand brand, bool soft) => "panel-" + brand.Id + (soft ? "-soft" : "");
        /// <summary>A brand's round logo, for the ice by the neutral-zone dots.</summary>
        public static string RoundArt(Brands.Brand brand) => "round-" + brand.Id;

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
                string art = PanelArt(brand, slots[i].CalmArea);
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
                Texture2D texture = brand == null ? centreIceLogo
                    : Art(spot.Kind == RinkLayout.IceLogoKind.NeutralZoneRound ? RoundArt(brand) : PanelArt(brand, false));
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

        /// <summary>A white URP Lit material showing the art (a copy of the ad panel asset, so the build keeps the variant).</summary>
        static Material Opaque(Texture2D texture, float smoothness)
        {
            var m = new Material(Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + ArenaLighting.AdPanelMaterial)) { name = "Ad Panel" };
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
