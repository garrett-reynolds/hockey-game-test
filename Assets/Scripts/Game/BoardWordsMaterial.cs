using UnityEngine;

namespace HockeyGame
{
    /// <summary>The material for board words (Video Board, Centre Board, Ribbon Board). Unity's built-in font material ignores
    /// depth and draws both faces, so words showed through boards and mirrored from behind. This one is depth-tested and front-face
    /// only (shader Hockey Game/LED Board, words mode), reads the font's atlas and draws in cells of cellMetres (0: smooth text).
    /// The builder that makes it frees it.</summary>
    public static class BoardWordsMaterial
    {
        public static Material Make(Font font, double cellMetres)
        {
            var words = LedBoardMaterial.Words("Board Words", cellMetres);
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
