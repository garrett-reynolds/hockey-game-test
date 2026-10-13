using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// Materials for the LED boards (shader Hockey Game/LED Board): the Video Board and Centre Board now, the Ribbon Boards in #122.
    /// A screen's picture and its words share one grid of square cells with a dark gap, anchored at the screen's corner
    /// so the edge cells are whole and the words' cells line up with the picture's. The builder that makes a material frees it.
    /// </summary>
    public static class LedBoardMaterial
    {
        public const string ShaderPath = "Lighting/LedBoard", ShaderName = "Hockey Game/LED Board";
        /// <summary>The share of a cell's side taken by the gap (set on every material; the one source).</summary>
        public const float GapShare = 0.2f;
        /// <summary>The colour in the gap (set on every material; the one source). A Color is read as sRGB: 0.02 is 0.00155 linear.</summary>
        public static readonly Color GapColour = new Color(0.02f, 0.02f, 0.02f);

        const string WordsKeyword = "_LED_WORDS";
        static readonly int CellSize = Shader.PropertyToID("_CellSize");
        static readonly int GapShareId = Shader.PropertyToID("_GapShare");
        static readonly int GapColourId = Shader.PropertyToID("_GapColour");
        static readonly int GridOrigin = Shader.PropertyToID("_GridOrigin");
        static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlend = Shader.PropertyToID("_DstBlend");

        /// <summary>A screen's material: shows its colour (times a picture, if one is set as mainTexture). A cell of 0 m means no LED look.</summary>
        public static Material Screen(string name, double cellMetres, Color colour)
        {
            var screen = Make(name, cellMetres);
            screen.color = colour;
            screen.renderQueue = (int)RenderQueue.Geometry;
            return screen;
        }

        /// <summary>The material for board words (the font's atlas as mainTexture): depth-tested, front faces only, drawn after the screens.</summary>
        public static Material Words(string name, double cellMetres)
        {
            var words = Make(name, cellMetres);
            words.EnableKeyword(WordsKeyword);
            // A cell of 0 m draws smooth text, which needs blending; the LED look is opaque.
            words.SetFloat(SrcBlend, (float)BlendMode.SrcAlpha);
            words.SetFloat(DstBlend, (float)BlendMode.OneMinusSrcAlpha);
            words.renderQueue = (int)RenderQueue.AlphaTest;
            return words;
        }

        /// <summary>Starts the renderer's cell grid at worldOrigin (the screen's lower-left corner), for the screen and each of its words objects.</summary>
        public static void Anchor(Renderer renderer, Vector3 worldOrigin)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetVector(GridOrigin, worldOrigin);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>The cell size the material was made with, in metres (0: no LED look).</summary>
        public static float CellOf(Material m) => m.GetFloat(CellSize);

        static Material Make(string name, double cellMetres)
        {
            var m = new Material(Resources.Load<Shader>(ShaderPath)) { name = name };
            m.SetFloat(CellSize, (float)cellMetres);
            m.SetFloat(GapShareId, GapShare);
            m.SetColor(GapColourId, GapColour);
            return m;
        }
    }
}
