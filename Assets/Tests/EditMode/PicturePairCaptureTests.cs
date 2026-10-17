using System.Collections.Generic;
using System.IO;
using HockeyGame.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class PicturePairCaptureTests
    {
        // One vertical slice: every original view remains measurable when its Gap list is empty.
        // NUnit's test directory contains the loaded test assembly and already exists.
        // Do not create output folders in the checkout.
        [Test]
        public void Fresh_pair_keeps_all_six_no_Gap_views_and_exports_same_frame_raw_masks()
        {
            string folder = TestContext.CurrentContext.TestDirectory;
            Assert.That(Directory.Exists(folder), Is.True, "the loaded test assembly must have an existing output folder");
            string[] views = {
                "left-cheat-easy-on", "left-cheat-easy-off",
                "middle-depth-easy-on", "middle-depth-easy-off",
                "right-late-settle-easy-on", "right-late-settle-easy-off"
            };
            var black = new Color32(0, 0, 0, 255);
            var white = new Color32(255, 255, 255, 255);
            // Rows run from bottom to top. Four white pixels out of nine give contrast
            // sqrt((4/9)*(5/9)) = sqrt(20)/9. The only interior pixel has Sobel magnitude 1.
            // A vertical flip changes the decoded picture. Flat black remains the zero case.
            Color32[] nonFlat = { white, white, white, black, white, black, black, black, black };
            Color32[] flat = { black, black, black, black, black, black, black, black, black };
            var masks = new Dictionary<string, bool[]> {
                { "goalie", new[] { true, true, false, false, true, false, false, false, true } },
                { "glow", new[] { false, true, false, false, false, false, false, false, false } },
                { "gearSeen", new[] { true, false, false, false, true, false, false, false, true } },
                { "calm", new[] { true, true, true, true, true, true, true, true, true } },
                { "gear", new[] { true, false, false, false, true, false, false, false, false } }
            };
            // Independent byte literals catch row flips, mask substitutions and 0/255 encoding.
            var expectedMasks = new Dictionary<string, byte[]> {
                { "goalie", new byte[] { 1, 1, 0, 0, 1, 0, 0, 0, 1 } },
                { "glow", new byte[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 } },
                { "gearSeen", new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 } },
                { "calm", new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 } },
                { "gear", new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 0 } }
            };
            var written = new List<string>();
            foreach (string view in views)
            {
                foreach (string name in masks.Keys)
                    written.Add(Path.Combine(folder, view + "." + name + ".bin"));
                written.Add(Path.Combine(folder, view + ".png"));
            }
            // Check before cleanup takes ownership of any path.
            foreach (string path in written) Assert.That(File.Exists(path), Is.False, path);
            try
            {
                foreach (string view in views)
                {
                    bool useNonFlat = view.EndsWith("-on", System.StringComparison.Ordinal);
                    Color32[] picture = useNonFlat ? nonFlat : flat;
                    ViewMeasures result = PicturePairCapture.ExportNoGapFrame(folder, view, 3, 3, picture, masks);
                    Assert.That(result.View, Is.EqualTo(view));
                    Assert.That(result.Contrast, Is.EqualTo(useNonFlat ? 0.4969039949999533 : 0).Within(0.000001));
                    Assert.That(result.Edges, Is.EqualTo(useNonFlat ? 1 : 0));
                    Assert.That(result.Gaps, Is.Empty);
                    foreach (string name in masks.Keys)
                        CollectionAssert.AreEqual(expectedMasks[name],
                            File.ReadAllBytes(Path.Combine(folder, view + "." + name + ".bin")),
                            view + ": " + name + " must use raw 0/1 bytes in bottom-left row order");
                    var decoded = new Texture2D(2, 2);
                    try
                    {
                        Assert.That(decoded.LoadImage(File.ReadAllBytes(Path.Combine(folder, view + ".png"))), Is.True);
                        Assert.That(decoded.width, Is.EqualTo(3));
                        Assert.That(decoded.height, Is.EqualTo(3));
                        CollectionAssert.AreEqual(picture, decoded.GetPixels32());
                    }
                    finally { Object.DestroyImmediate(decoded); }
                }
            }
            finally
            {
                foreach (string path in written) if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
