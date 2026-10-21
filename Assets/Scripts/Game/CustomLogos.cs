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

        /// <summary>Raised when the browser could not store a saved picture: the id, and whether an older picture was put back (#224).</summary>
        public static event Action<string, bool> WriteFailed;

        /// <summary>Whether id has a custom logo: Save Data's list. Tests put a fake here (they never read the real save file).</summary>
        public static Func<string, bool> IsCustom = id => SaveStore.Current.IsLogoCustom(id);

        /// <summary>The saved picture files. Tests put a fake here.</summary>
        public static ILogoFiles Files { get; set; } = new DiskFiles();
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

        // Made textures that Forget let go of. A Round in play may still show one, so they are destroyed later (ReleaseRetired), not at SAVE (#224).
        static readonly List<Texture2D> Retired = new List<Texture2D>();
        static int _retiredFrame = -1;

        /// <summary>How many made textures exist now: the kept ones and the retired ones not yet destroyed. A test counts it.</summary>
        public static int LiveTextures => Made.Count + Retired.Count;

        /// <summary>The colour panel made from a brand's saved picture, or null when the brand has no custom logo (or its file is unreadable).</summary>
        public static Texture2D Panel(string id) => Spot(id, LogoSpots.Panel);

        /// <summary>The picture made from a brand's saved logo for one spot (a LogoSpots name, #227); null: no custom logo, or its file is unreadable.</summary>
        public static Texture2D Spot(string id, string spot)
        {
            if (!IsCustom(id)) return null;
            if (Made.TryGetValue((id, spot), out Texture2D made) && made != null) return made;
            RgbaPicture art;
            if (spot == LogoSpots.CentrePicked)
            {
                RgbaPicture picked = Picked(id);
                art = picked == null ? null : LogoArt.MakeCentrePicked(picked); // the CENTRE ICE row: the picture as picked (#224)
            }
            else
            {
                RgbaPicture logo = Logo(id);
                if (logo == null) return null;
                switch (spot)
                {
                    case LogoSpots.Panel: art = LogoArt.MakePanel(logo); break;
                    case LogoSpots.PanelSoft: art = LogoArt.MakeSoftPanel(logo); break;
                    case LogoSpots.Round: art = LogoArt.MakeRound(logo); break;
                    case LogoSpots.Ribbon: art = LogoArt.MakeRibbon(logo); break;
                    case LogoSpots.Centre: art = LogoArt.MakeCentre(logo); break;
                    case LogoSpots.GameLogo: art = LogoArt.MakeGameLogo(logo); break; // the GAME LOGO row (#230)
                    default: return null;
                }
            }
            if (art == null) return null;
            Texture2D texture = ToTexture(art, "custom-" + id + "-" + spot, spot == LogoSpots.Ribbon);
            Made[(id, spot)] = texture;
            return texture;
        }

        /// <summary>The saved picture as picked, decoded and nothing else (the made logo crops and clears it); null when unreadable.</summary>
        static RgbaPicture Picked(string id)
        {
            byte[] file = Files.Read(id);
            if (file != null && Codec.TryDecode(file, out int w, out int h, out byte[] rgba)) return new RgbaPicture(w, h, rgba);
            Debug.LogWarning("Custom logo '" + id + "' could not be read; using the default art.");
            return null;
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

        /// <summary>
        /// Forgets the made pictures for id. A Round in play may still show them, so they are not destroyed here: they wait in
        /// Retired until ReleaseRetired (#224).
        /// </summary>
        public static void Forget(string id)
        {
            Logos.Remove(id);
            foreach (string spot in LogoSpots.All)
                if (Made.TryGetValue((id, spot), out Texture2D texture))
                {
                    Made.Remove((id, spot));
                    if (texture != null) Retired.Add(texture);
                    _retiredFrame = Time.frameCount;
                }
        }

        /// <summary>
        /// Destroys the forgotten textures. The Round calls it each frame after it has taken the new art, so by a later frame nothing
        /// shows an old one. now: destroy at once (tests).
        /// </summary>
        public static void ReleaseRetired(bool now = false)
        {
            if (Retired.Count == 0 || (!now && Time.frameCount <= _retiredFrame)) return;
            foreach (Texture2D texture in Retired) UnityEngine.Object.DestroyImmediate(texture);
            Retired.Clear();
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
                    byte[] old = Read(id); // kept in memory so a failed browser store can put the old art back (#224)
                    File.WriteAllBytes(PathOf(id), png);
                    // Web build: copy to the browser's stored files (#228). The copy ends later; if it fails the old picture comes back.
                    WebFlush.Request(ok => { if (!ok) KeepOld(id, old); });
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Could not save the logo picture for '" + id + "': " + e.Message);
                    return false;
                }
            }

            // The browser could not store the new picture: the old one (or none) is put back and the page is told (WriteFailed).
            static void KeepOld(string id, byte[] old)
            {
                try
                {
                    if (old != null) File.WriteAllBytes(PathOf(id), old);
                    else if (File.Exists(PathOf(id))) File.Delete(PathOf(id));
                }
                catch (Exception e) { Debug.LogWarning("Could not put the old logo picture back for '" + id + "': " + e.Message); }
                WriteFailed?.Invoke(id, old != null);
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

    /// <summary>The names of the spots a custom logo is made for (the keys of CustomLogos.Spot and ArtLookup's cache).</summary>
    public static class LogoSpots
    {
        public const string Panel = "panel", PanelSoft = "panel-soft", Round = "round", Ribbon = "ribbon", Centre = "centre";
        public const string CentrePicked = "centre-ice"; // the CENTRE ICE row's picture as picked (#224)
        public const string GameLogo = "game-logo";     // the GAME LOGO row (#230)

        public static readonly string[] All = { Panel, PanelSoft, Round, Ribbon, Centre, CentrePicked, GameLogo };
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
