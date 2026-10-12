using System.Linq;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// NHL-style arena lighting: a bright, cool, even ice under rows of glowing light banks, with darker stands so the
    /// rink "pops". A main light almost straight down plus two shadowed spots over the crease give soft, short shadows
    /// under the goalie and net; three spots along the skate-in brighten the ice. A cubemap of the arena, rendered once at
    /// start, is the scene's reflection: the glossy ice mirrors the light banks, boards and net. Post-processing
    /// (tonemapping, bloom, vignette) and the material templates come from assets in Assets/Resources/Lighting, so a
    /// player build keeps their shaders.
    /// Cost: 6 realtime lights (Forward+), 3 of them with soft shadows, and one 128 px cubemap rendered at start.
    /// </summary>
    public static class ArenaLighting
    {
        /// <summary>The dark arena behind the rink: the camera background and the fog colour.</summary>
        public static readonly Color Backdrop = new Color(0.1f, 0.12f, 0.16f);

        /// <summary>Folder under Assets/Resources holding the lighting assets (Hockey Game > Build Arena Lighting Assets).</summary>
        public const string AssetFolder = "Lighting";
        /// <summary>Post-processing profile asset: tonemapping, bloom, vignette.</summary>
        public const string PostProcessingProfile = "ArenaPostProcessing";
        /// <summary>Material templates the game copies at runtime, kept as assets so the build keeps their shader variants.</summary>
        public const string GlassMaterial = "Glass", GlowMaterial = "Glow", UnlitMaterial = "Unlit";
        /// <summary>Easy mode glow material template (shader Hockey Game/Easy Glow, see GoalieView).</summary>
        public const string EasyGlowMaterial = "EasyGlow";
        /// <summary>Name of each glowing overhead light bank under the roof.</summary>
        public const string LightBank = "Light Bank";

        /// <summary>Cool arena white, about 6,000 K.</summary>
        static readonly Color ArenaWhite = new Color(0.93f, 0.97f, 1f);
        /// <summary>HDR glow of a light bank: well above the bloom threshold.</summary>
        const float BankGlow = 16f;
        const float BankHeight = 20f;
        static readonly Vector3 BankSize = new Vector3(5f, 0.3f, 1.4f);
        /// <summary>How far a light bank keeps clear of the Centre Board's box (metres).</summary>
        public const float CentreBoardClearance = 0.4f;

        /// <summary>Adds the lights under rink and sets the scene's ambient light and fog. Returns the main light.</summary>
        public static Light Build(Transform rink)
        {
            var main = new GameObject("Arena Light").AddComponent<Light>();
            main.transform.SetParent(rink, false);
            main.type = LightType.Directional;
            main.transform.rotation = Quaternion.Euler(78f, -10f, 0f); // nearly overhead, a little from the shooter's side
            main.color = ArenaWhite;
            main.intensity = 0.8f;
            main.shadows = LightShadows.Soft;
            main.shadowStrength = 0.45f;

            // Two banks over the crease, left and right: two soft, short shadows under the goalie and net.
            var crease = new Vector3(0f, 0f, -0.5f);
            foreach (float x in new[] { -7f, 7f })
            {
                Light spot = Spot(rink, "Crease Light", new Vector3(x, 16f, -6f), crease, 50f, 30f, 110f);
                spot.shadows = LightShadows.Soft;
                spot.shadowStrength = 0.5f;
            }

            // Along the skate-in: brighter pools and highlights on the glossy ice. No shadows (cost).
            foreach (float z in new[] { -8f, -16f, -24f })
                Spot(rink, "Overhead Light", new Vector3(0f, 16f, z), new Vector3(0f, 0f, z), 110f, 30f, 70f);

            AddLightBanks(rink);
            AddReflections(rink);

            // Ambient: bright from above (the ice), dim from the sides and below (the stands).
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.49f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.17f, 0.18f, 0.21f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.11f, 0.13f);

            SetFog();

            var post = new GameObject("Arena Post-processing").AddComponent<Volume>();
            post.transform.SetParent(rink, false);
            post.isGlobal = true;
            post.sharedProfile = Resources.Load<VolumeProfile>(AssetFolder + "/" + PostProcessingProfile);
            return main;
        }

        /// <summary>
        /// Linear fog that fades the far arena into the backdrop. The Rink scene saves the same fog (RinkSceneTool), so a
        /// player build keeps the linear fog shaders.
        /// </summary>
        public static void SetFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Backdrop;
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 90f;
        }

        /// <summary>A glowing URP Lit material (a copy of the Glow asset, so the build keeps the emission variant).</summary>
        public static Material Glow(Color baseColour, Color emission)
        {
            var m = new Material(Resources.Load<Material>(AssetFolder + "/" + GlowMaterial));
            m.color = baseColour;
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        static Light Spot(Transform rink, string name, Vector3 position, Vector3 target, float spotAngle, float range, float intensity)
        {
            var spot = new GameObject(name).AddComponent<Light>();
            spot.transform.SetParent(rink, false);
            spot.type = LightType.Spot;
            spot.transform.position = position;
            spot.transform.rotation = Quaternion.LookRotation(target - position);
            spot.color = ArenaWhite;
            spot.spotAngle = spotAngle;
            spot.innerSpotAngle = spotAngle * 0.5f;
            spot.range = range;
            spot.intensity = intensity;
            spot.shadows = LightShadows.None;
            return spot;
        }

        /// <summary>
        /// A bank's z, moved out along the rink past the Centre Board (with CentreBoardClearance) when its footprint would
        /// fall under the board's box; the board hangs at the banks' height, so the bank keeps lighting the ice beside it.
        /// </summary>
        static float ClearOfCentreBoard(float x, float z)
        {
            float half = (float)RinkLayout.CentreBoardScreenWidth / 2f, centre = (float)RinkLayout.CentreBoard.Z;
            bool under = Mathf.Abs(x) - BankSize.x / 2f < half + CentreBoardClearance
                         && Mathf.Abs(z - centre) - BankSize.z / 2f < half + CentreBoardClearance;
            if (!under) return z;
            return centre + Mathf.Sign(z - centre) * (half + CentreBoardClearance + BankSize.z / 2f);
        }

        /// <summary>Rows of glowing light banks under the roof: seen when looking up, mirrored in the ice.</summary>
        static void AddLightBanks(Transform rink)
        {
            var banks = new GameObject("Light Banks").transform;
            banks.SetParent(rink, false);
            Material glow = Glow(new Color(0.2f, 0.2f, 0.2f), ArenaWhite * BankGlow);
            // A rig over the whole bowl, not just the ice: from the shooter's eye, the far banks are what the ice mirrors.
            // The near half's rows, and their mirror over the far half of the full rink.
            float[] near = { -26f, -18f, -10f, -2f, 6f, 14f, 22f };
            foreach (float z in near.Concat(near.Select(z => (float)RinkLayout.Mirror(z))))
                foreach (float x in new[] { -12f, -4f, 4f, 12f })
                {
                    GameObject bank = RinkBuilder.Cube(banks, LightBank, new Vector3(x, BankHeight, ClearOfCentreBoard(x, z)), BankSize, Color.white);
                    var r = bank.GetComponent<Renderer>();
                    r.sharedMaterial = glow;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
        }

        /// <summary>
        /// What glossy surfaces (the ice, the glass) reflect: a cubemap of the arena seen from over the skate-in (ReflectionEyeZ) at eye
        /// height, rendered once, here, by a temporary camera, and used as the scene's reflection. It holds the stands,
        /// light banks, boards and net but not the goalie or puck (they move). Rendered in code, so it needs neither baked
        /// lighting nor Realtime Reflection Probes on in the Quality settings.
        /// </summary>
        /// <summary>
        /// Where along the rink the reflection cubemap is seen from: over the skate-in, the middle of the half rink the
        /// shooter sees (the cubemap was taken here before the rink grew to full length, so the Calm Area keeps its look).
        /// </summary>
        public const float ReflectionEyeZ = -13.125f;

        static void AddReflections(Transform rink)
        {
            var cube = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGBHalf)
            {
                dimension = TextureDimension.Cube,
                useMipMap = true,      // blurrier mips for rougher surfaces
                autoGenerateMips = true,
                name = "Arena Reflections",
            };
            var camera = new GameObject("Reflection Camera").AddComponent<Camera>();
            camera.transform.SetParent(rink, false);
            camera.transform.position = new Vector3(0f, 1.5f, ReflectionEyeZ);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Backdrop;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.enabled = false;
            camera.RenderToCubemap(cube);
            SceneObjects.DestroySafely(camera.gameObject);

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
        }
    }
}
