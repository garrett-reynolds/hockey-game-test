using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The skin's roof banners (#149): tall flat banners that hang round the near end's corners, each side of the Video Board,
    /// about as high as the Ribbon Board. They hang there because that is what the shooter's camera sees: it looks level at
    /// the net, so the roof (30 m) and the far end behind the shooter are never in view (the old far-end row showed in no view the
    /// shooter has), and a banner hung against the stands' back is buried in the rising seats. They sit outside the Calm Area strip and clear of the Video Board's width,
    /// face the shooter, and never move. The art (Assets/Resources/Ads) is the skin's RoofBanners list, left to right; the League
    /// Skin's list is the League's award and retired jersey banners (BannerArt).
    /// </summary>
    public static class RoofBanners
    {
        public const string Part = "Roof Banners";
        /// <summary>One banner's size (metres) and the gap between banners along the ring.</summary>
        public const float Width = 2.16f, Length = 3.6f, Gap = 0.3f;
        /// <summary>The height of a banner's top (metres): low enough that the whole banner is in the shooter's view.</summary>
        public const float Top = 9.2f;
        /// <summary>
        /// How far out from the boards the banners hang (metres), along the ring round the end corners. The lower stands rise
        /// with the offset (StandsFrontHeight + LowerRise per RowDepth), so a banner hung farther out would be buried in the
        /// seats: at this offset the stands are about 5.5 m high and the banner's foot (Top - Length) clears them.
        /// </summary>
        public const float Offset = 8f;
        /// <summary>The angle round the end corner at which the first banner's near edge starts: its edge is then clear of the Video Board's width.</summary>
        public const float FirstEdgeAngle = 0.2f;
        /// <summary>Where the banners face (z, on the middle line): the shooter's side of the net.</summary>
        public const float FaceZ = -9f;

        /// <summary>Adds the skin's banners under rink (null: the League Skin's); returns null when there are none.</summary>
        public static GameObject Build(Transform rink, TeamSkin skin = null)
        {
            string[] art = TeamSkin.OrLeague(skin).RoofBanners;
            if (art == null || art.Length == 0) return null;

            var root = new GameObject(Part);
            root.transform.SetParent(rink, false);
            Material unlit = Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + ArenaLighting.UnlitMaterial);
            float radius = (float)RinkLayout.CornerRadius + Offset;
            float step = (Width + Gap) / radius, half = Width / 2f / radius;
            float y = Top - Length / 2f;
            int left = (art.Length + 1) / 2; // the first half hang on the left, left to right: the outermost first
            for (int i = 0; i < art.Length; i++)
            {
                bool onLeft = i < left;
                int slot = onLeft ? left - 1 - i : i - left; // 0 is the one nearest the middle
                float angle = FirstEdgeAngle + half + step * slot, side = onLeft ? -1f : 1f;
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
            }
            return root;
        }
    }
}
