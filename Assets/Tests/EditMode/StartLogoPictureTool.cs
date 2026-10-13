using System.IO;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Renders the start screen at 1920 x 1080 for the #140 pictures: the rink's first wide shot, crowd off and audio muted (not a
    /// crowd picture), the Shootout logo where the game draws it, and the other start-screen lines in LED-font stand-in text (an
    /// EditMode render cannot draw IMGUI). A picture maker, not a check (StartLogoTests checks the layout): it runs only on
    /// request (--include_explicit) and writes _notes\t140-after.png only when that folder exists.
    /// </summary>
    public class StartLogoPictureTool
    {
        const int W = 1920, H = 1080;

        [Test, Category("Slow"), Explicit("Slow: picture maker for #140, runs only in the full run (--include_explicit true)")]
        public void Take_the_start_screen_picture()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../_notes"));
            if (!Directory.Exists(folder)) Assert.Ignore("No _notes folder: the picture is only made in the dev checkout");
            bool asyncShaders = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            bool muted = UnityEditor.EditorUtility.audioMasterMute;
            UnityEditor.EditorUtility.audioMasterMute = true;
            RenderTexture target = null;
            Camera camera = null;
            Texture2D logo = null, texture = null;
            try
            {
                RinkBuilder.Build(out camera);
                GameObject.Find("Rink").GetComponentInChildren<Crowd>().Drawn = false; // crowd off: not a crowd picture
                new StartShotsView(camera, 60f).Step(true, 0.5f);
                target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                camera.targetTexture = target;
                camera.aspect = W / (float)H;
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                texture = new Texture2D(W, H, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                RenderTexture.active = previous;
                Color32[] px = texture.GetPixels32();

                logo = StartLogoArt.Make();
                DrawLogo(px, logo, StartScreenLayout.Logo(W, H));
                Strip(px, StartScreenLayout.TextStrip(W, H));
                Text(px, "BEST SCORE: 0 / 6", StartScreenLayout.BestScore(W, H), 4, false);
                Text(px, "SHOOT OR PRESS ANY KEY", StartScreenLayout.Prompt(W, H), 4, false);
                Text(px, "NO ELECTRONICS FOUND", StartScreenLayout.UsbStatus(W, H), 4, false);
                Text(px, "TEST SHOTS: OFF  (F1 ON)", StartScreenLayout.TestShots(W, H), 2, true);

                texture.SetPixels32(px);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(folder, "t140-after.png"), texture.EncodeToPNG());
            }
            finally
            {
                UnityEditor.ShaderUtil.allowAsyncCompilation = asyncShaders;
                UnityEditor.EditorUtility.audioMasterMute = muted;
                if (camera != null) camera.targetTexture = null;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (logo != null) Object.DestroyImmediate(logo);
                if (texture != null) Object.DestroyImmediate(texture);
                Object.DestroyImmediate(GameObject.Find("Rink"));
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
            }
        }

        // The dark see-through strip with its gold top edge, as RoundDriver draws it.
        static void Strip(Color32[] px, Rect r)
        {
            Color back = StartLogoArt.StripBack;
            for (int y = (int)r.yMin; y < (int)r.yMax; y++)
                for (int x = (int)r.xMin; x < (int)r.xMax; x++)
                {
                    int i = (H - 1 - y) * W + x;
                    if (y < (int)r.yMin + StartLogoArt.StripEdgeHeight) { px[i] = StartLogoArt.Gold; continue; }
                    px[i] = new Color32((byte)(back.r * 255 * back.a + px[i].r * (1 - back.a)), (byte)(back.g * 255 * back.a + px[i].g * (1 - back.a)),
                                        (byte)(back.b * 255 * back.a + px[i].b * (1 - back.a)), 255);
                }
        }

        // Alpha-blends the logo into the picture at area (top-left origin).
        static void DrawLogo(Color32[] px, Texture2D logo, Rect area)
        {
            for (int y = (int)area.yMin; y < (int)area.yMax; y++)
                for (int x = (int)area.xMin; x < (int)area.xMax; x++)
                {
                    Color c = logo.GetPixelBilinear((x - area.x + 0.5f) / area.width, 1f - (y - area.y + 0.5f) / area.height);
                    int i = (H - 1 - y) * W + x;
                    px[i] = new Color32((byte)(c.r * 255 * c.a + px[i].r * (1 - c.a)), (byte)(c.g * 255 * c.a + px[i].g * (1 - c.a)),
                                        (byte)(c.b * 255 * c.a + px[i].b * (1 - c.a)), 255);
                }
        }

        static void Text(Color32[] px, string s, Rect r, int scale, bool left)
        {
            int width = LedFont.LineWidth(s) * scale;
            int x0 = left ? (int)r.x : (int)(r.x + (r.width - width) / 2f);
            int top = (int)(r.y + (r.height - LedFont.CapHeight * scale) / 2f);
            foreach (char c in s)
            {
                for (int col = 0; col < LedFont.WidthOf(c); col++)
                    for (int row = 0; row < LedFont.CapHeight; row++)
                    {
                        if (!LedFont.IsLit(c, col, row)) continue;
                        for (int dy = 0; dy < scale; dy++)
                            for (int dx = 0; dx < scale; dx++)
                            {
                                int x = x0 + col * scale + dx, y = H - 1 - (top + (LedFont.CapHeight - 1 - row) * scale + dy);
                                if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = new Color32(255, 255, 255, 255);
                            }
                    }
                x0 += LedFont.AdvanceOf(c) * scale;
            }
        }
    }
}
