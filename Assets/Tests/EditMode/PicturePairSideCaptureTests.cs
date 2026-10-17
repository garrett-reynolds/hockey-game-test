using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HockeyGame.Core;
using HockeyGame.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    public class PicturePairSideCaptureTests
    {
        [Test, Category("Slow"), Explicit("Real six-view capture: owned native run only")]
        public void One_real_side_keeps_six_views_same_frame_raw_data_and_actual_source_identity()
        {
            string parent = TestContext.CurrentContext.TestDirectory;
            Assert.That(Directory.Exists(parent), Is.True, "loaded test assembly directory must exist");
            string output = Path.Combine(parent, "picture-side-" + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(output) || File.Exists(output), Is.False);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string baseline = Path.Combine(project, PictureTestTool.BaselineFolder);
            var before = Snapshot(baseline);
            Assert.That(before.Count, Is.EqualTo(7), "the original saved baseline must be present");
            var context = new PictureCaptureContext {
                sourceCommit = Encoding.UTF8.GetString(Git(project, "rev-parse HEAD")).Trim(),
                sourceDiffSha256 = Hash(Git(project, "diff --binary HEAD -- Assets Packages ProjectSettings")),
                capturePayloadSha256 = CaptureBundleHash(project)
            };
            Assert.That(context.sourceCommit, Does.Match("^[0-9a-f]{40}$"));
            Assert.That(context.sourceDiffSha256, Does.Match("^[0-9a-f]{64}$"));
            int baselineCount = before.Count;
            try
            {
                string manifestPath = PictureTestTool.CapturePairSide(output, context);
                Assert.That(Path.GetFullPath(manifestPath), Is.EqualTo(Path.Combine(output, "manifest.json")));
                var manifest = JsonUtility.FromJson<PictureSideManifest>(File.ReadAllText(manifestPath));
                Assert.That(manifest.schema, Is.EqualTo("hockey-picture-capture-v1"));
                Assert.That(manifest.sourceCommit, Is.EqualTo(context.sourceCommit));
                Assert.That(manifest.sourceDiffSha256, Is.EqualTo(context.sourceDiffSha256));
                Assert.That(manifest.capturePayloadSha256, Is.EqualTo(context.capturePayloadSha256));
                Assert.That(manifest.width, Is.EqualTo(1280));
                Assert.That(manifest.height, Is.EqualTo(720));
                Assert.That(manifest.frozen.unityVersion, Is.EqualTo(Application.unityVersion));
                Assert.That(manifest.frozen.host, Is.EqualTo(SystemInfo.deviceName));
                Assert.That(manifest.frozen.gpu, Is.EqualTo(SystemInfo.graphicsDeviceName));
                Assert.That(manifest.frozen.gpuDriver, Is.EqualTo(SystemInfo.graphicsDeviceVersion));
                Assert.That(manifest.frozen.graphicsApi, Is.EqualTo(SystemInfo.graphicsDeviceType.ToString()));
                Assert.That(manifest.frozen.colorSpace, Is.EqualTo(QualitySettings.activeColorSpace.ToString()));
                Assert.That(manifest.frozen.qualitySettings, Is.Not.Null.And.Not.Empty);
                Assert.That(manifest.frozen.renderSettings, Is.Not.Null.And.Not.Empty);
                Assert.That(manifest.frozen.crowdDrawn, Is.True);
                Assert.That(manifest.frozen.targetFormat, Is.EqualTo(RenderTextureFormat.ARGB32.ToString()));
                Assert.That(manifest.frozen.targetReadWrite, Is.EqualTo(RenderTextureReadWrite.sRGB.ToString()));
                Assert.That(manifest.frozen.antiAliasing, Is.EqualTo(1));
                Assert.That(manifest.frozen.poseInputs.windowSeconds, Is.EqualTo(PictureTestTool.WindowSeconds));
                Assert.That(manifest.frozen.poseInputs.lifeSeconds, Is.EqualTo(PictureTestTool.LifeSeconds));
                Assert.That(manifest.frozen.poseInputs.eyeNudge, Is.EqualTo(PictureTestTool.EyeNudge));
                string[] poses = { "left-cheat", "middle-depth", "right-late-settle" };
                CollectionAssert.AreEqual(poses, manifest.frozen.poseInputs.poseNames);
                string[] views = poses.SelectMany(p => new[] { p + "-easy-on", p + "-easy-off" }).ToArray();
                Assert.That(manifest.views.Length, Is.EqualTo(6));
                CollectionAssert.AreEquivalent(views, manifest.views.Select(v => v.id));
                int realGapGroups = 0;
                foreach (var view in manifest.views)
                {
                    Assert.That(view.camera.cameraPosition.Length, Is.EqualTo(3));
                    Assert.That(view.camera.cameraRotation.Length, Is.EqualTo(4));
                    Assert.That(view.camera.cameraProjection.Length, Is.EqualTo(16));
                    Assert.That(view.camera.cameraPixelWidth, Is.EqualTo(1280));
                    Assert.That(view.camera.cameraPixelHeight, Is.EqualTo(720));
                    foreach (float v in view.camera.cameraPosition.Concat(view.camera.cameraRotation).Concat(view.camera.cameraProjection))
                        Assert.That(float.IsNaN(v) || float.IsInfinity(v), Is.False);
                    Assert.That(view.camera.cameraFieldOfView, Is.GreaterThan(0).And.LessThan(180));
                    Assert.That(view.camera.cameraProjection[5] / view.camera.cameraProjection[0], Is.EqualTo(1280.0 / 720).Within(0.00001),
                        "projection must describe the assigned render target, not the Editor viewport");
                    Assert.That(2 * Math.Atan(1.0 / view.camera.cameraProjection[5]) * 180 / Math.PI,
                        Is.EqualTo(view.camera.cameraFieldOfView).Within(0.0001));
                    byte[] raw = Blob(output, view.image, 1280 * 720 * 4, false);
                    var pixels = new Color32[1280 * 720];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(raw[i * 4], raw[i * 4 + 1], raw[i * 4 + 2], raw[i * 4 + 3]);
                    var masks = new[] { view.masks.goalie, view.masks.glow, view.masks.gearSeen, view.masks.calm, view.masks.gear };
                    var bytes = masks.Select(m => Blob(output, m, pixels.Length, true)).ToArray();
                    bool[] calm = bytes[3].Select(b => b == 1).ToArray();
                    Assert.That(PictureMeasures.InnerPixels(calm, 1280, 720), Is.GreaterThan(0));
                    float[] luma = PictureMeasures.Luma(pixels);
                    Assert.That(view.calmContrast, Is.EqualTo(PictureMeasures.Contrast(luma, calm)).Within(0.000001));
                    Assert.That(view.calmEdges, Is.EqualTo(PictureMeasures.Edges(luma, calm, 1280, 720)).Within(0.000001));
                    var png = new Texture2D(2, 2);
                    try
                    {
                        Assert.That(png.LoadImage(File.ReadAllBytes(Path.Combine(output, view.id + ".png"))), Is.True);
                        Assert.That(png.width, Is.EqualTo(1280));
                        Assert.That(png.height, Is.EqualTo(720));
                        CollectionAssert.AreEqual(pixels, png.GetPixels32(), "raw RGBA and PNG must retain the same frame and row order");
                    }
                    finally { Object.DestroyImmediate(png); }
                    Assert.That(view.gaps, Is.Not.Null);
                    foreach (var gap in view.gaps)
                    {
                        realGapGroups++;
                        Assert.That(gap.cellIndices, Is.Not.Empty);
                        CollectionAssert.AreEqual(gap.cellIndices.Distinct().OrderBy(c => c), gap.cellIndices);
                        Assert.That(gap.cellIndices.All(c => c >= 0 && c < NetFaceGaps.Cells), Is.True);
                        Assert.That(gap.id, Is.EqualTo(PictureMeasures.GapName(gap.cellIndices.ToList(), NetFaceGaps.Columns)));
                        byte[] region = Blob(output, gap.projectedCellRegionMask, pixels.Length, true);
                        byte[] excluded = Blob(output, gap.gearExcludedMask, pixels.Length, true);
                        var expectedExcluded = new byte[pixels.Length];
                        for (int i = 0; i < pixels.Length; i++) expectedExcluded[i] = region[i] == 1 && bytes[2][i] == 0 ? (byte)1 : (byte)0;
                        CollectionAssert.AreEqual(expectedExcluded, excluded);
                    }
                }
                Assert.That(realGapGroups, Is.GreaterThan(0), "real reach Gaps must be captured, not six invented empty lists");
                foreach (string pose in poses)
                    CollectionAssert.AreEqual(manifest.views.Single(v => v.id == pose + "-easy-on").gaps.Select(g => string.Join(",", g.cellIndices)),
                        manifest.views.Single(v => v.id == pose + "-easy-off").gaps.Select(g => string.Join(",", g.cellIndices)));
                Assert.That(Snapshot(baseline).Count, Is.EqualTo(baselineCount));
                CollectionAssert.AreEquivalent(before, Snapshot(baseline));
                var savedSide = Snapshot(output);
                Assert.Throws<IOException>(() => PictureTestTool.CapturePairSide(output, context), "existing capture must not be replaced");
                CollectionAssert.AreEquivalent(savedSide, Snapshot(output));
                CollectionAssert.AreEquivalent(before, Snapshot(baseline));
            }
            finally
            {
                // This exact random destination was absent before the public call.
                if (Directory.Exists(output)) Directory.Delete(output, true);
                CollectionAssert.AreEquivalent(before, Snapshot(baseline), "saved baseline must remain unchanged even on failure");
            }
        }

        static Dictionary<string, string> Snapshot(string folder) => !Directory.Exists(folder)
            ? new Dictionary<string, string>()
            : Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(p => p.Substring(folder.Length), p => Hash(File.ReadAllBytes(p)));

        static byte[] Blob(string output, PictureSideBlob blob, int count, bool mask)
        {
            Assert.That(blob, Is.Not.Null);
            string path = Path.GetFullPath(Path.Combine(output, blob.path));
            Assert.That(path.StartsWith(Path.GetFullPath(output) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(blob.byteCount, Is.EqualTo(count));
            Assert.That(bytes.Length, Is.EqualTo(count));
            Assert.That(blob.sha256, Is.EqualTo(Hash(bytes)));
            if (mask)
            {
                Assert.That(bytes.All(b => b == 0 || b == 1), Is.True);
                Assert.That(blob.trueCount, Is.EqualTo(bytes.Count(b => b == 1)));
            }
            return bytes;
        }

        static string CaptureBundleHash(string project)
        {
            // Protocol v1: ordinal path order, lowercase file SHA256, two spaces, relative path, LF; final LF.
            string[] paths = {
                "Assets/Scripts/Game/Editor/PictureCompare.cs",
                "Assets/Scripts/Game/Editor/PictureMeasures.cs",
                "Assets/Scripts/Game/Editor/PicturePairCapture.cs",
                "Assets/Scripts/Game/Editor/PicturePairSideCapture.cs"
            };
            Array.Sort(paths, StringComparer.Ordinal);
            return Hash(Encoding.UTF8.GetBytes(string.Join("\n", paths.Select(path =>
                Hash(File.ReadAllBytes(Path.Combine(project, path))) + "  " + path)) + "\n"));
        }

        static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static byte[] Git(string project, string arguments)
        {
            using var process = new Process { StartInfo = new ProcessStartInfo("git", "-C \"" + project + "\" " + arguments) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            } };
            process.Start();
            using var output = new MemoryStream();
            var copy = process.StandardOutput.BaseStream.CopyToAsync(output);
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10000)) { process.Kill(); throw new TimeoutException("git source identity exceeded ten seconds"); }
            copy.GetAwaiter().GetResult();
            Assert.That(process.ExitCode, Is.EqualTo(0), error.GetAwaiter().GetResult());
            return output.ToArray();
        }
    }
}
