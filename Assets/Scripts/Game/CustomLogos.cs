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

        /// <summary>The file box: the Editor's open-file box in the Editor; none elsewhere until the web and Windows boxes exist (#228, #229).</summary>
        public static IPicturePicker Picker =>
#if UNITY_EDITOR
            EditorPicker;
        static readonly IPicturePicker EditorPicker = new EditorPicturePicker();
#else
            null;
#endif

        static readonly Dictionary<string, Texture2D> Made = new Dictionary<string, Texture2D>();

        /// <summary>The colour panel made from a brand's saved picture, or null when the brand has no custom logo (or its file is unreadable).</summary>
        public static Texture2D Panel(string id)
        {
            if (!SaveStore.Current.IsLogoCustom(id)) return null;
            if (Made.TryGetValue(id, out Texture2D made) && made != null) return made;
            byte[] file = Files.Read(id);
            RgbaPicture logo = null;
            if (file != null && Codec.TryDecode(file, out int w, out int h, out byte[] rgba))
                logo = LogoArt.MakeLogo(new RgbaPicture(w, h, rgba));
            if (logo == null)
            {
                Debug.LogWarning("Custom logo '" + id + "' could not be read; using the default art.");
                return null;
            }
            Texture2D texture = ToTexture(LogoArt.MakePanel(logo), "custom-" + id);
            Made[id] = texture;
            return texture;
        }

        /// <summary>Forgets the made panel for id (the pictures a Round in play already holds stay).</summary>
        public static void Forget(string id) => Made.Remove(id);

        static Texture2D ToTexture(RgbaPicture picture, string name)
        {
            // Pictures here start at the top row; Unity textures start at the bottom row.
            int stride = picture.Width * 4;
            var flipped = new byte[picture.Rgba.Length];
            for (int y = 0; y < picture.Height; y++)
                Buffer.BlockCopy(picture.Rgba, y * stride, flipped, (picture.Height - 1 - y) * stride, stride);
            var texture = new Texture2D(picture.Width, picture.Height, TextureFormat.RGBA32, true) { name = name };
            texture.SetPixelData(flipped, 0);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            texture.Apply(true, true);
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
                try { if (Safe(id) && File.Exists(PathOf(id))) File.Delete(PathOf(id)); }
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
