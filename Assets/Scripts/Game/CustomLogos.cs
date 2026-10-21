using System;
using System.Collections.Generic;
using System.IO;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The custom logos on the Game side (#226): the picked pictures kept in the save folder's BrandArt folder (one PNG per id),
    /// Unity's image reader behind IPictureCodec, the file box for the Editor, and the made colour panel as a texture.
    /// Which ids are custom is Save Data's list (SaveData.CustomLogos); the made panels are made again at load, not saved.
    /// </summary>
    public static class CustomLogos
    {
        /// <summary>%persistentDataPath%/BrandArt. In the web build this is the browser's stored files (the web writer is #228).</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "BrandArt");

        public static readonly ILogoFiles Files = new DiskFiles();
        public static readonly IPictureCodec Codec = new UnityCodec();

        /// <summary>The file box: the Editor's open-file box in the Editor, the browser's in the web build, the Windows open-file box in the Windows player (#229); none elsewhere.</summary>
        public static IPicturePicker Picker { get; } = Make(ChooseKind(
#if UNITY_EDITOR
            true,
#else
            false,
#endif
#if UNITY_WEBGL
            true,
#else
            false,
#endif
#if UNITY_STANDALONE_WIN
            true));
#else
            false));
#endif

        /// <summary>The Editor wins over the others (the Editor on a web or Windows target still has its own file box).</summary>
        public static PickerKind ChooseKind(bool inEditor, bool onWeb, bool onWindows = false) =>
            inEditor ? PickerKind.Editor : onWeb ? PickerKind.Web : onWindows ? PickerKind.Windows : PickerKind.None;

        public static IPicturePicker Make(PickerKind kind)
        {
            switch (kind)
            {
                case PickerKind.Web: return new WebPicturePicker();
                case PickerKind.Windows: return new WindowsPicturePicker(); // compiles everywhere; only its call into comdlg32 is Windows-player-only (#229)
#if UNITY_EDITOR
                case PickerKind.Editor: return new EditorPicturePicker();
#endif
                default: return null;
            }
        }

        static readonly Dictionary<(string id, string spot), Texture2D> Made = new Dictionary<(string id, string spot), Texture2D>();
        static readonly Dictionary<string, RgbaPicture> Logos = new Dictionary<string, RgbaPicture>();

        /// <summary>The colour panel made from a brand's saved picture, or null when the brand has no custom logo (or its file is unreadable).</summary>
        public static Texture2D Panel(string id) => Spot(id, "panel");

        /// <summary>The picture made from a brand's saved logo for one spot ("panel", "panel-soft", "round", "ribbon", "centre", #227); null: no custom logo, or its file is unreadable.</summary>
        public static Texture2D Spot(string id, string spot)
        {
            if (!SaveStore.Current.IsLogoCustom(id)) return null;
            if (Made.TryGetValue((id, spot), out Texture2D made) && made != null) return made;
            RgbaPicture logo = Logo(id);
            if (logo == null) return null;
            RgbaPicture art;
            switch (spot)
            {
                case "panel": art = LogoArt.MakePanel(logo); break;
                case "panel-soft": art = LogoArt.MakeSoftPanel(logo); break;
                case "round": art = LogoArt.MakeRound(logo); break;
                case "ribbon": art = LogoArt.MakeRibbon(logo); break;
                case "centre": art = LogoArt.MakeCentre(logo); break;
                case "game-logo": art = LogoArt.MakeGameLogo(logo); break; // the GAME LOGO row (#230)
                default: return null;
            }
            Texture2D texture = ToTexture(art, "custom-" + id + "-" + spot, spot == "ribbon");
            Made[(id, spot)] = texture;
            return texture;
        }

        /// <summary>The made logo (cropped, white box cleared) for a saved picture, kept until Forget; null when unreadable.</summary>
        static RgbaPicture Logo(string id)
        {
            if (Logos.TryGetValue(id, out RgbaPicture kept)) return kept;
            byte[] file = Files.Read(id);
            RgbaPicture logo = null;
            if (file != null && Codec.TryDecode(file, out int w, out int h, out byte[] rgba))
                logo = LogoArt.MakeLogo(new RgbaPicture(w, h, rgba));
            if (logo == null)
            {
                Debug.LogWarning("Custom logo '" + id + "' could not be read; using the default art.");
                return null;
            }
            Logos[id] = logo;
            return logo;
        }

        /// <summary>Forgets the made pictures for id (the pictures a Round in play already holds stay).</summary>
        public static void Forget(string id)
        {
            Logos.Remove(id);
            foreach (string spot in new[] { "panel", "panel-soft", "round", "ribbon", "centre", "game-logo" }) Made.Remove((id, spot));
        }

        static Texture2D ToTexture(RgbaPicture picture, string name, bool ribbon = false)
        {
            // Pictures here start at the top row; Unity textures start at the bottom row.
            int stride = picture.Width * 4;
            var flipped = new byte[picture.Rgba.Length];
            for (int y = 0; y < picture.Height; y++)
                Buffer.BlockCopy(picture.Rgba, y * stride, flipped, (picture.Height - 1 - y) * stride, stride);
            var texture = new Texture2D(picture.Width, picture.Height, TextureFormat.RGBA32, !ribbon) { name = name };
            texture.SetPixelData(flipped, 0);
            // The Ribbon strip tiles along the boards, drawn as the shipped strips are: repeat, point filter, no mips.
            texture.wrapMode = ribbon ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            texture.filterMode = ribbon ? FilterMode.Point : FilterMode.Trilinear;
            texture.anisoLevel = ribbon ? 1 : 4;
            texture.Apply(!ribbon, true);
            return texture;
        }

        sealed class DiskFiles : ILogoFiles
        {
            static string PathOf(string id) => Path.Combine(Folder, id + ".png");

            static bool Safe(string id) => !string.IsNullOrEmpty(id) && id == Path.GetFileName(id);

            public bool Write(string id, byte[] png)
            {
                if (!Safe(id)) return false;
                try
                {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllBytes(PathOf(id), png);
                    WebFlush.Request(); // web build: copy to the browser's stored files (#228)
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Could not save the logo picture for '" + id + "': " + e.Message);
                    return false;
                }
            }

            public void Delete(string id)
            {
                try { if (Safe(id) && File.Exists(PathOf(id))) { File.Delete(PathOf(id)); WebFlush.Request(); } }
                catch (Exception e) { Debug.LogWarning("Could not delete the logo picture for '" + id + "': " + e.Message); }
            }

            public bool Exists(string id) => Safe(id) && File.Exists(PathOf(id));

            public byte[] Read(string id)
            {
                try { return Exists(id) ? File.ReadAllBytes(PathOf(id)) : null; }
                catch (Exception) { return null; }
            }
        }

        sealed class UnityCodec : IPictureCodec
        {
            public bool TryDecode(byte[] file, out int width, out int height, out byte[] rgba)
            {
                width = height = 0;
                rgba = null;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!ImageConversion.LoadImage(texture, file, false)) return false;
                    width = texture.width;
                    height = texture.height;
                    Color32[] pixels = texture.GetPixels32(); // bottom row first
                    rgba = new byte[width * height * 4];
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            Color32 c = pixels[(height - 1 - y) * width + x];
                            int i = (y * width + x) * 4;
                            rgba[i] = c.r; rgba[i + 1] = c.g; rgba[i + 2] = c.b; rgba[i + 3] = c.a;
                        }
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            public byte[] EncodePng(int width, int height, byte[] rgba)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    var pixels = new Color32[width * height];
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            int i = (y * width + x) * 4;
                            pixels[(height - 1 - y) * width + x] = new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
                        }
                    texture.SetPixels32(pixels);
                    return ImageConversion.EncodeToPNG(texture);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>The Editor's open-file box for CHANGE LOGO (#226): PNG or JPG; Cancel calls back with null.</summary>
    sealed class EditorPicturePicker : IPicturePicker
    {
        public void Ask(Action<byte[]> done)
        {
            string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters("Pick a logo picture", "", new[] { "Pictures", "png,jpg,jpeg", "All files", "*" });
            if (string.IsNullOrEmpty(path)) { done(null); return; }
            byte[] bytes = new byte[0]; // unreadable: not null (that is Cancel), so the menu says "not a picture"
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception e) { Debug.LogWarning("Could not read " + path + ": " + e.Message); }
            done(bytes);
        }
    }
#endif
}
