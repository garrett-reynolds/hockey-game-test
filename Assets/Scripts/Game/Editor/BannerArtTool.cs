using System.IO;
using UnityEditor;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>Writes the League Skin's banner art (BannerArt.League) as PNGs into Assets/Resources/Ads, where the skin finds it.</summary>
    public static class BannerArtTool
    {
        [MenuItem("Hockey Game/Banner Art/Make")]
        public static void Make()
        {
            foreach (BannerArt.Banner banner in BannerArt.League)
            {
                Texture2D texture = BannerArt.Make(banner);
                string path = "Assets/Resources/" + RinkAds.ArtFolder + "/" + banner.Name + ".png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                Debug.Log("Banner art: " + path);
            }
            AssetDatabase.Refresh();
        }
    }
}
