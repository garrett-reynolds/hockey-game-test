using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The skin's roof banners (#149, moved in #165): small flat banners that hang along the Ribbon Board ring, each side of the Video
    /// Board, standing just above the Ribbon Board (a fan's head reaches the ribbon's top, so a lower banner would cover fans). They hang
    /// in front of the upper stands, which have no fans behind the board and banners (CrowdSeats, RinkLayout.UpperCutOutHalfWidth).
    /// Each hangs from a dark top bar and two thin wires up to the roof (WiresPart). They sit clear of the Video Board's frame,
    /// face the shooter, and never move. The art (Assets/Resources/Ads) is the skin's RoofBanners
    /// list, left to right; the League Skin's list is the League's award and retired jersey banners (BannerArt).
    /// One atlas for the 6 banner materials was not built (#152): the frame time with the banners and wires on and off differs by less than
    /// the run-to-run noise (+0.03 ms in #149, -0.14 ms with the wires in #152; rounds spread about 0.5 ms), so an atlas buys nothing measurable.
    /// </summary>
    public static class RoofBanners
    {
        public const string Part = "Roof Banners";
        /// <summary>The wires and top bars the banners hang from (#152), a sibling of Part under the rink.</summary>
        public const string WiresPart = "Roof Banner Wires";
        /// <summary>The top bar's thickness and the wires' thickness (metres); the wire colour is a dark grey so it reads as steel, not as art.</summary>
        const float BarThickness = 0.12f, WireThickness = 0.03f;
        static readonly Color WireColour = new Color(0.06f, 0.06f, 0.07f);
        /// <summary>One banner's size (metres) and the gap between banners along the ring.</summary>
        public const float Width = 1.3f, Length = 2.16f, Gap = 0.3f;
        /// <summary>How far a banner's foot stands above the Ribbon Board's top (metres).</summary>
        public const float AboveRibbon = 0.1f;
        /// <summary>Where the banners face (z, on the middle line): the shooter's side of the net.</summary>
        public const float FaceZ = -9f;

        /// <summary>How far in front of the Ribbon Board's face the banners hang, and the clear gap beside the Video Board's frame (metres).</summary>
        public const float RibbonGap = 0.4f, BoardGap = 0.4f;

        /// <summary>Adds the skin's banners under rink (null: the League Skin's); returns null when there are none.</summary>
        public static GameObject Build(Transform rink, TeamSkin skin = null)
        {
            string[] art = TeamSkin.OrLeague(skin).RoofBanners;
            if (art == null || art.Length == 0) return null;

            var root = new GameObject(Part);
            root.transform.SetParent(rink, false);
            var wires = new GameObject(WiresPart);
            wires.transform.SetParent(rink, false);
            Material unlit = Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + ArenaLighting.UnlitMaterial);
            var wire = new Material(unlit) { name = "Roof Banner Wire", color = WireColour };
            // Round the near end's ribbon ring, just in front of the Ribbon Board, from beside the Video Board outward.
            float radius = (float)RinkLayout.CornerRadius + (float)RinkLayout.RibbonOffset - RibbonGap;
            float firstEdge = Mathf.Asin(((float)RinkLayout.VideoBoardWidth / 2f + (float)RinkLayout.VideoBoardFrameMargin + BoardGap - (float)RinkLayout.CornerX) / radius);
            float step = (Width + Gap) / radius, half = Width / 2f / radius;
            float y = (float)(RinkLayout.RibbonBottom + RinkLayout.RibbonHeight) + AboveRibbon + Length / 2f;
            int left = (art.Length + 1) / 2; // the first half hang on the left, left to right: the outermost first
            for (int i = 0; i < art.Length; i++)
            {
                bool onLeft = i < left;
                int slot = onLeft ? left - 1 - i : i - left; // 0 is the one nearest the middle
                float angle = firstEdge + half + step * slot, side = onLeft ? -1f : 1f;
                var at = new Vector3(side * ((float)RinkLayout.CornerX + radius * Mathf.Sin(angle)), y,
                    (float)RinkLayout.NearCornerZ + radius * Mathf.Cos(angle));
                Vector3 toward = new Vector3(0f, 0f, FaceZ) - new Vector3(at.x, 0f, at.z);

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Roof Banner " + i + " " + art[i];
                quad.transform.SetParent(root.transform, false);
                // A quad shows its face to local -z, so look away from the shooter.
                quad.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-toward.normalized));
                quad.transform.localScale = new Vector3(Width, Length, 1f);
                SceneObjects.DestroySafely(quad.GetComponent<Collider>());
                var renderer = quad.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = new Material(unlit)
                {
                    name = "Roof Banner",
                    mainTexture = Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + art[i]),
                    color = Color.white
                };
                Hang(wires.transform, quad.transform, i, wire);
            }
            return root;
        }

        /// <summary>A top bar across the banner's top and two thin wires from its ends up to the roof, so the banner hangs and does not float.</summary>
        static void Hang(Transform wires, Transform banner, int index, Material wire)
        {
            Vector3 top = banner.position + Vector3.up * (Length / 2f);
            float reach = (float)RinkLayout.RoofHeight - top.y;
            Bar(wires, "Roof Banner Bar " + index, top + Vector3.up * (BarThickness / 2f), new Vector3(Width + 0.1f, BarThickness, 0.05f), banner.rotation, wire);
            for (int end = 0; end < 2; end++)
            {
                Vector3 across = banner.right * ((end == 0 ? -1f : 1f) * (Width / 2f - 0.1f));
                Bar(wires, "Roof Banner Wire " + index + (end == 0 ? "a" : "b"), top + across + Vector3.up * (reach / 2f),
                    new Vector3(WireThickness, reach, WireThickness), banner.rotation, wire);
            }
        }

        static void Bar(Transform parent, string name, Vector3 centre, Vector3 scale, Quaternion rotation, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.SetPositionAndRotation(centre, rotation);
            cube.transform.localScale = scale;
            SceneObjects.DestroySafely(cube.GetComponent<Collider>());
            var renderer = cube.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
        }
    }
}
