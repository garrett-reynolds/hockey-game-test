using System.IO;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Renders the start screen at 1920 x 1080 for the #140 pictures: the rink's first wide shot, crowd off and audio muted (not a
    /// crowd picture), the Shootout logo where the game draws it, and the other start-screen lines in LED-font stand-in text (an
    /// EditMode render cannot draw IMGUI). Writes _notes\t140-after.png when that folder exists.
    /// </summary>
    public class StartLogoPictureTests
    {
        const int W = 1920, H = 1080;

        [Test]
        public void The_start_screen_picture_shows_the_logo_over_the_rink_and_clear_of_the_text()
        {
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
                Color32[] before = texture.GetPixels32();
                Color32[] px = (Color32[])before.Clone();

                logo = StartLogoArt.Make();
                Rect area = StartScreenLayout.Logo(W, H);
                DrawLogo(px, logo, area);
                Text(px, "BEST SCORE: 0 / 6", StartScreenLayout.BestScore(W, H), 4, false);
                Text(px, "SHOOT OR PRESS ANY KEY", StartScreenLayout.Prompt(W, H), 4, false);
                Text(px, "NO ELECTRONICS FOUND", StartScreenLayout.UsbStatus(W, H), 4, false);
                Text(px, "F2: SETTINGS", StartScreenLayout.SettingsHint(W, H), 2, true);
                Text(px, "TEST SHOTS: OFF  (F1 ON)", StartScreenLayout.TestShots(W, H), 2, true);

                // The logo changed the picture inside its area and nowhere under the prompt or the F2 hint.
                Assert.Greater(Changed(before, px, area), area.width * area.height * 0.3, "the logo is on the picture");
                Rect prompt = StartScreenLayout.Prompt(W, H);
                Assert.IsFalse(area.Overlaps(prompt));

                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../_notes"));
                if (Directory.Exists(folder))
                {
                    texture.SetPixels32(px);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, "t140-after.png"), texture.EncodeToPNG());
                }
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

        static int Changed(Color32[] a, Color32[] b, Rect area)
        {
            int n = 0;
            for (int y = (int)area.yMin; y < (int)area.yMax; y++)
                for (int x = (int)area.xMin; x < (int)area.xMax; x++)
                {
                    int i = (H - 1 - y) * W + x;
                    if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b) n++;
                }
            return n;
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
