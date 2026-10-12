using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The Team Skin's roof banners: flat banners hung in a row high over the far end stands, behind the shooter, facing
    /// into the rink, so they show in the Replay and never in the Calm Area. The art is in Assets/Resources/Ads. The League
    /// Skin has none, so the rink gets nothing.
    /// </summary>
    public static class RoofBanners
    {
        public const string Part = "Roof Banners";
        /// <summary>One banner's size (metres), the gap between banners, and how far its top hangs below the roof.</summary>
        public const float Width = 1.5f, Length = 3f, Gap = 1f, BelowRoof = 4f;
        /// <summary>How far behind the far end boards the row hangs (metres), over the far stands.</summary>
        public const float BehindFarBoards = 6f;

        /// <summary>Adds the skin's banners under rink (null: the League Skin's, which has none); returns null when there are none.</summary>
        public static GameObject Build(Transform rink, TeamSkin skin = null)
        {
            string[] art = TeamSkin.OrLeague(skin).RoofBanners;
            if (art == null || art.Length == 0) return null;

            var root = new GameObject(Part);
            root.transform.SetParent(rink, false);
            Material unlit = Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + ArenaLighting.UnlitMaterial);
            float z = (float)RinkLayout.FarEndBoardsZ - BehindFarBoards;
            float y = (float)RinkLayout.RoofHeight - BelowRoof - Length / 2f;
            float step = Width + Gap, left = -step * (art.Length - 1) / 2f;
            for (int i = 0; i < art.Length; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Roof Banner " + i + " " + art[i];
                quad.transform.SetParent(root.transform, false);
                // A quad shows its face to local -z: facing back down the rink (+z), toward the near net.
                quad.transform.SetPositionAndRotation(new Vector3(left + step * i, y, z), Quaternion.LookRotation(Vector3.back));
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
