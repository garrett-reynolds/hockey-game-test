using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The boards and glass around the rink, from the boards model (Assets/Resources/Models/boards.obj) when there is one.
    /// With no model the rink stays open, as it was before. NHL sizes, lined up with the ice markings: 85 ft wide, end
    /// boards 11 ft behind the goal line (the Net Face plane). Behind the shooter the rink closes just inside the ice
    /// sheet, past the furthest skate-in start; the shooter never looks that way.
    /// The model's parts named "Glass" are made see-through here (OBJ materials import opaque).
    /// </summary>
    public static class RinkBoards
    {
        /// <summary>Inside face of the side boards: x = ±HalfWidth (metres).</summary>
        public const float HalfWidth = 12.95f;
        /// <summary>Inside face of the end boards behind the net (metres).</summary>
        public const float EndZ = 3.35f;
        /// <summary>Inside face of the boards behind the shooter (metres).</summary>
        public const float NearZ = -29.6f;
        /// <summary>Thickness of the boards (metres); the model's outer box is the inside outline plus this all round.</summary>
        public const float Thickness = 0.2f;
        /// <summary>Top of the highest glass, behind the net (metres above the ice).</summary>
        public const float GlassTop = 3.5f;

        /// <summary>Name of the see-through parts in the boards model.</summary>
        public const string GlassPart = "Glass";

        const float GlassAlpha = 0.15f;

        /// <summary>The model's outer box in rink space: the boards' outside faces, from the ice to the top of the glass.</summary>
        public static Bounds Box
        {
            get
            {
                var min = new Vector3(-HalfWidth - Thickness, 0f, NearZ - Thickness);
                var max = new Vector3(HalfWidth + Thickness, GlassTop, EndZ + Thickness);
                return new Bounds((min + max) / 2f, max - min);
            }
        }

        /// <summary>Adds the boards under rink. findFile is for tests; by default the model comes from Assets/Resources/Models.</summary>
        public static GameObject Build(Transform rink, Func<string, GameObject> findFile = null)
        {
            var boards = new GameObject("Boards");
            boards.transform.SetParent(rink, false);
            boards.transform.localPosition = Box.center;

            GameObject model = ModelFiles.Load(ModelFiles.Boards, () => new GameObject("No Boards Model"), findFile);
            model.transform.SetParent(boards.transform, false);
            ModelFiles.FitTo(model.transform, Box.size);

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                if (r.name == GlassPart) MakeGlass(r);
            return boards;
        }

        static void MakeGlass(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var m = new Material(renderer.sharedMaterial);
            Color c = m.color;
            c.a = GlassAlpha;
            m.color = c;
            m.SetFloat("_Surface", 1f); // URP Lit: transparent, alpha blended
            m.SetFloat("_Blend", 0f);
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
            renderer.sharedMaterial = m;
        }
    }
}
