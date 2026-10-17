using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>The fresh pair's export boundary. Inputs must come from one fixed captured frame.
    /// This first slice handles a view with no Gaps; normal picture runs do not call it yet.</summary>
    public static class PicturePairCapture
    {
        public static ViewMeasures ExportNoGapFrame(string folder, string view, int width, int height,
            Color32[] picture, IDictionary<string, bool[]> masks)
        {
            if (!Directory.Exists(folder)) throw new ArgumentException("An existing output folder is required.", nameof(folder));
            string[] views = {
                "left-cheat-easy-on", "left-cheat-easy-off",
                "middle-depth-easy-on", "middle-depth-easy-off",
                "right-late-settle-easy-on", "right-late-settle-easy-off"
            };
            if (Array.IndexOf(views, view) < 0) throw new ArgumentException("Unknown picture view.", nameof(view));
            if (width < 3 || height < 3) throw new ArgumentOutOfRangeException(nameof(width), "A picture must be at least 3 by 3.");
            int count = checked(width * height);
            if (picture == null || picture.Length != count) throw new ArgumentException("Pixel count differs from picture size.", nameof(picture));
            string[] names = { "goalie", "glow", "gearSeen", "calm", "gear" };
            if (masks == null || masks.Count != names.Length) throw new ArgumentException("Exactly five masks are required.", nameof(masks));
            foreach (string name in names)
                if (!masks.TryGetValue(name, out bool[] mask) || mask == null || mask.Length != count)
                    throw new ArgumentException("Missing mask or wrong mask size: " + name, nameof(masks));

            bool[] calm = masks["calm"];
            bool hasInterior = false;
            for (int y = 1; y < height - 1 && !hasInterior; y++)
            for (int x = 1; x < width - 1 && !hasInterior; x++)
            {
                bool inner = true;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++) inner &= calm[(y + dy) * width + x + dx];
                hasInterior = inner;
            }
            if (!hasInterior) throw new ArgumentException("Calm mask has no complete interior sample.", nameof(masks));

            string pngPath = Path.Combine(folder, view + ".png");
            var outputs = new List<KeyValuePair<string, byte[]>>();
            foreach (string name in names)
            {
                var bytes = new byte[count];
                for (int i = 0; i < count; i++) bytes[i] = masks[name][i] ? (byte)1 : (byte)0;
                outputs.Add(new KeyValuePair<string, byte[]>(Path.Combine(folder, view + "." + name + ".bin"), bytes));
            }
            foreach (string path in OutputPaths())
                if (File.Exists(path) || Directory.Exists(path)) throw new IOException("Output already exists: " + path);

            float[] luma = PictureMeasures.Luma(picture);
            var result = new ViewMeasures {
                View = view,
                Contrast = PictureMeasures.Contrast(luma, calm),
                Edges = PictureMeasures.Edges(luma, calm, width, height)
            };
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels32(picture);
                texture.Apply();
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length == 0) throw new IOException("PNG encoding produced no data.");
                outputs.Insert(0, new KeyValuePair<string, byte[]>(pngPath, png));
            }
            finally { if (texture != null) UnityEngine.Object.DestroyImmediate(texture); }

            var created = new List<string>();
            try
            {
                foreach (var output in outputs)
                {
                    using (var stream = new FileStream(output.Key, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        created.Add(output.Key);
                        stream.Write(output.Value, 0, output.Value.Length);
                    }
                }
            }
            catch (Exception error)
            {
                var failures = new List<Exception> { error };
                foreach (string path in created)
                    try { File.Delete(path); } catch (Exception cleanupError) { failures.Add(cleanupError); }
                if (failures.Count > 1) throw new AggregateException("Export failed and cleanup was incomplete.", failures);
                throw;
            }
            return result;

            IEnumerable<string> OutputPaths()
            {
                yield return pngPath;
                foreach (var output in outputs) yield return output.Key;
            }
        }
    }
}
