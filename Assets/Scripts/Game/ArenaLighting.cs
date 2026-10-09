using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// Arena lighting: a bright, cool overhead main light from the shooter's side (soft shadows ground the goalie
    /// and puck on the white ice), a few overhead spots over the skate-in, ambient light from above, and fog that fades
    /// the far ice into the dark stands. At most three spots, so the goal light glow still lights the ice in URP Forward.
    /// </summary>
    public static class ArenaLighting
    {
        /// <summary>The dark arena behind the rink: the camera background and the fog colour.</summary>
        public static readonly Color Backdrop = new Color(0.1f, 0.12f, 0.16f);

        static readonly Color ArenaWhite = new Color(0.96f, 0.98f, 1f);

        /// <summary>Adds the lights under rink and sets the scene's ambient light and fog. Returns the main light.</summary>
        public static Light Build(Transform rink)
        {
            var main = new GameObject("Arena Light").AddComponent<Light>();
            main.transform.SetParent(rink, false);
            main.type = LightType.Directional;
            main.transform.rotation = Quaternion.Euler(62f, -15f, 0f); // high, from behind the shooter toward the net
            main.color = ArenaWhite;
            main.intensity = 0.85f;
            main.shadows = LightShadows.Soft;
            main.shadowStrength = 0.55f;

            // Overhead spots along the skate-in: brighter pools on the ice toward the net.
            float[] spotZ = { -3f, -11f, -19f };
            foreach (float z in spotZ)
            {
                var spot = new GameObject("Overhead Light").AddComponent<Light>();
                spot.transform.SetParent(rink, false);
                spot.type = LightType.Spot;
                spot.transform.position = new Vector3(0f, 14f, z);
                spot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                spot.color = ArenaWhite;
                spot.spotAngle = 110f;
                spot.innerSpotAngle = 60f;
                spot.range = 30f;
                spot.intensity = 18f;
                spot.shadows = LightShadows.None;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.4f, 0.42f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.27f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.17f, 0.19f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Backdrop;
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 90f;
            return main;
        }
    }
}
