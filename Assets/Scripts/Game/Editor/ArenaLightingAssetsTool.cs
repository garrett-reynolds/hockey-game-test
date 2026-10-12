using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HockeyGame.Editor
{
    /// <summary>
    /// Recreates the arena lighting assets in Assets/Resources/Lighting: the post-processing profile and the material
    /// templates the game copies at runtime. They are assets (not made in code) so a player build keeps the bloom,
    /// tonemapping, transparent glass, glow and unlit shader variants the game uses.
    /// </summary>
    public static class ArenaLightingAssetsTool
    {
        const string Folder = "Assets/Resources/" + ArenaLighting.AssetFolder;

        [MenuItem("Hockey Game/Build Arena Lighting Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            BuildProfile();
            Save(GlassTemplate(), ArenaLighting.GlassMaterial);
            Save(GlowTemplate(), ArenaLighting.GlowMaterial);
            Save(new Material(Shader.Find("Universal Render Pipeline/Unlit")), ArenaLighting.UnlitMaterial);
            BuildEasyGlow();
            BuildIce();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Recreates only the ice template (RinkIce.IceMaterial): URP Lit with the smoothness map (Models/ice-smoothness,
        /// Tools/make_ice_wear.py) that makes the Calm Area matte. RinkIce copies it and adds the markings texture.
        /// </summary>
        public static void BuildIce()
        {
            Directory.CreateDirectory(Folder);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Models/ice-smoothness.png"));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", RinkIce.Gloss);
            Save(m, RinkIce.IceMaterial);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Recreates only the Easy mode glow template (Assets/Shaders/EasyGlow.shader).</summary>
        public static void BuildEasyGlow()
        {
            Directory.CreateDirectory(Folder);
            Save(new Material(Shader.Find(GoalieView.EasyGlowShader)), ArenaLighting.EasyGlowMaterial);
            AssetDatabase.SaveAssets();
        }

        static void BuildProfile()
        {
            string path = Folder + "/" + ArenaLighting.PostProcessingProfile + ".asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.3f);   // above the lit ice, so only the light banks, highlights and goal lamp glow
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.5f);     // a tight glow that doesn't spread over the next zone
            bloom.highQualityFiltering.Override(false);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.4f);

            var colour = profile.Add<ColorAdjustments>();
            colour.postExposure.Override(0.2f);
            colour.contrast.Override(8f);

            foreach (VolumeComponent c in profile.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
        }

        static Material GlassTemplate()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetFloat("_Surface", 1f); // transparent, alpha blended
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 0f); // reflections fade with the glass, so it stays see-through
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Smoothness", 0.9f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = (int)RenderQueue.Transparent;
            m.color = new Color(1f, 1f, 1f, 0.15f);
            return m;
        }

        static Material GlowTemplate()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive; // URP keeps _EMISSION only on an "emissive" material; nothing is baked
            return m;
        }

        static void Save(Material material, string name)
        {
            string path = Folder + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
        }
    }
}
