using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// Materials for the LED boards (shader Hockey Game/LED Board): the Video Board and Centre Board now, the Ribbon Boards in #122.
    /// A screen's picture and its words (drawn into a RenderTexture, then shown as a picture) share one grid of square cells with a
    /// dark gap, anchored at the screen's corner so the edge cells are whole. The builder that makes a material frees it.
    /// </summary>
    public static class LedBoardMaterial
    {
        public const string ShaderPath = "Lighting/LedBoard", ShaderName = "Hockey Game/LED Board";
        /// <summary>The share of a cell's side taken by the gap (set on every material; the one source).</summary>
        public const float GapShare = 0.2f;
        /// <summary>The colour in the gap (set on every material; the one source). A Color is read as sRGB: 0.02 is 0.00155 linear.</summary>
        public static readonly Color GapColour = new Color(0.02f, 0.02f, 0.02f);
        /// <summary>
        /// A words cell is lit when the words cover at least this share of it. Chosen between the two things it must tell apart, at
        /// WordsPixelsPerCell = 8 (a Headline's capitals are about 13 cells tall, its bold strokes about 2 cells wide):
        /// a round letter's overshoot past the cap line or baseline is about 2 per cent of the capital's height, so about a quarter of a
        /// cell, and would light a stray row at any lower threshold; half a stroke is the most a threshold can ask before a thin bar
        /// that crosses a cell loses it. 0.5 is in the middle and is the usual "more than half lit" rule.
        /// </summary>
        public const float WordsLitAbove = 0.5f;
        /// <summary>The words' RenderTexture holds this many pixels (a power of two, so mips average exactly one cell) across each cell.</summary>
        public const int WordsPixelsPerCell = 8;

        static readonly int CellSize = Shader.PropertyToID("_CellSize");
        static readonly int GapShareId = Shader.PropertyToID("_GapShare");
        static readonly int GapColourId = Shader.PropertyToID("_GapColour");
        static readonly int GridOrigin = Shader.PropertyToID("_GridOrigin");
        static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        static readonly int LitAbove = Shader.PropertyToID("_LitAbove");
        static readonly int CellMip = Shader.PropertyToID("_CellMip");
        static readonly int Backdrop = Shader.PropertyToID("_Backdrop");
        static readonly int FontAtlas = Shader.PropertyToID("_FontAtlas");
        static readonly int CoverageOut = Shader.PropertyToID("_CoverageOut");

        /// <summary>A screen's material: shows its colour (times a picture, if one is set as mainTexture). A cell of 0 m means no LED look.</summary>
        public static Material Screen(string name, double cellMetres, Color colour)
        {
            var screen = Make(name, cellMetres);
            screen.color = colour;
            screen.renderQueue = (int)RenderQueue.Geometry;
            return screen;
        }

        /// <summary>
        /// A screen's material for words drawn into coverage (white on black, WordsPixelsPerCell pixels across each cell, with mips):
        /// cells with words show colour, the others backdrop.
        /// </summary>
        public static Material WordsOnScreen(string name, double cellMetres, Texture coverage, Color words, Color backdrop)
        {
            var screen = Screen(name, cellMetres, words);
            screen.mainTexture = coverage;
            screen.SetFloat(LitAbove, WordsLitAbove);
            screen.SetFloat(CellMip, Mathf.Log(WordsPixelsPerCell, 2f));
            screen.SetColor(Backdrop, backdrop);
            return screen;
        }

        /// <summary>The colour of lit cells and of the others on a words material from WordsOnScreen.</summary>
        public static void SetWordsColours(Material m, Color words, Color backdrop)
        {
            m.color = words;
            m.SetColor(Backdrop, backdrop);
        }

        /// <summary>
        /// The material that draws smooth text from the font's atlas (mainTexture): depth-tested, front faces only, drawn after the
        /// screens. The text is always smooth (the Ribbon Boards' plain look until #122); cellMetres is only recorded on the material.
        /// coverageOut: write white with the text's alpha (for a coverage RenderTexture) instead of the text's colour.
        /// </summary>
        public static Material PlainText(string name, double cellMetres, bool coverageOut)
        {
            var text = Make(name, cellMetres);
            text.SetFloat(FontAtlas, 1f);
            text.SetFloat(CoverageOut, coverageOut ? 1f : 0f);
            // Smooth text needs blending; the LED look is opaque.
            text.SetFloat(SrcBlend, (float)BlendMode.SrcAlpha);
            text.SetFloat(DstBlend, (float)BlendMode.OneMinusSrcAlpha);
            text.renderQueue = (int)RenderQueue.AlphaTest;
            return text;
        }

        /// <summary>Starts the renderer's cell grid at worldOrigin (the screen's lower-left corner).</summary>
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
