using UnityEngine;

namespace HockeyGame
{
    /// <summary>The materials that draw board text from the font's atlas. Unity's built-in font material ignores depth and draws both
    /// faces, so words showed through boards and mirrored from behind. These are depth-tested and front-face only (shader
    /// Hockey Game/LED Board, font-atlas path). Make is the Ribbon Boards' (smooth text until #122 gives them the LED look);
    /// MakeCoverage is for the Video Board and Centre Board, whose words VideoBoards draws into a RenderTexture. The builder that makes one frees it.</summary>
    public static class BoardWordsMaterial
    {
        /// <summary>Smooth text in the text's own colour (the Ribbon Boards; cellMetres 0 until #122).</summary>
        public static Material Make(Font font, double cellMetres)
        {
            var words = LedBoardMaterial.PlainText("Board Words", cellMetres, false);
            Follow(words, font);
            return words;
        }

        /// <summary>White text with the font's alpha, for drawing words as coverage into a RenderTexture.</summary>
        public static Material MakeCoverage(Font font)
        {
            var words = LedBoardMaterial.PlainText("Board Words Coverage", 0.0, true);
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
