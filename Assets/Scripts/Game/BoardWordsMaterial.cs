using UnityEngine;

namespace HockeyGame
{
    /// <summary>The material that draws the Ribbon Boards' text from the font's atlas. Unity's built-in font material ignores depth and
    /// draws both faces, so words showed through boards and mirrored from behind. This one is depth-tested and front-face only (shader
    /// Hockey Game/LED Board, font-atlas path) until #122 gives the Ribbon Boards the LED look. The Video Board and Centre Board do not
    /// use it: they draw the LED font (LedFont). The builder that makes one frees it.</summary>
    public static class BoardWordsMaterial
    {
        /// <summary>Smooth text in the text's own colour (the Ribbon Boards; cellMetres 0 until #122).</summary>
        public static Material Make(Font font, double cellMetres)
        {
            var words = LedBoardMaterial.PlainText("Board Words", cellMetres);
            Follow(words, font);
            return words;
        }

        /// <summary>Points the material at the font's atlas as it is now (the atlas grows when new letters are asked for).</summary>
        public static void Follow(Material words, Font font)
        {
            Texture atlas = font.material.mainTexture;
            if (words.mainTexture != atlas) words.mainTexture = atlas;
        }
    }
}
