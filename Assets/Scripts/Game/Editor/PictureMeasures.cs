using System;
using System.Collections.Generic;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>
    /// The picture test's measures (#85), pure math over a picture's pixels and a mask (both row by row from the bottom, as
    /// Unity reads a picture back: index = y * width + x). Brightness is luma from the picture's own (sRGB) values, 0 to 1.
    /// <list type="bullet">
    /// <item>Contrast: the spread (standard deviation) of brightness in the mask. Higher is busier, so worse.</item>
    /// <item>Edges (busy-ness): the share of mask pixels on an edge. Higher is worse.</item>
    /// <item>Gap stand-out: the brightness difference between Gap pixels and gear pixels. Lower is worse.</item>
    /// </list>
    /// </summary>
    public static class PictureMeasures
    {
        /// <summary>A pixel is on an edge when its brightness gradient (Sobel, scaled so a black-to-white step is 1) is above this.</summary>
        public const float EdgeThreshold = 0.1f;

        /// <summary>Rec. 709 luma of each pixel, 0 (black) to 1 (white).</summary>
        public static float[] Luma(Color32[] pixels)
        {
            var luma = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                luma[i] = (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;
            }
            return luma;
        }

        /// <summary>The standard deviation of brightness over the mask's pixels; 0 for an empty mask.</summary>
        public static double Contrast(float[] luma, bool[] mask)
        {
            double sum = 0, sumSquares = 0;
            int count = 0;
            for (int i = 0; i < luma.Length; i++)
            {
                if (!mask[i]) continue;
                sum += luma[i];
                sumSquares += (double)luma[i] * luma[i];
                count++;
            }
            if (count == 0) return 0;
            double mean = sum / count;
            return Math.Sqrt(Math.Max(0, sumSquares / count - mean * mean));
        }

        /// <summary>
        /// The share of the mask's inner pixels (all 8 neighbours in the mask too) whose brightness gradient is above
        /// EdgeThreshold. The mask's own border is left out, so a shape cut out of the mask (the goalie) adds no edges.
        /// </summary>
        public static double Edges(float[] luma, bool[] mask, int width, int height)
        {
            int inner = 0, edges = 0;
            for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
            {
                if (!InnerPixel(mask, width, x, y)) continue;
                inner++;
                float a = luma[(y - 1) * width + x - 1], b = luma[(y - 1) * width + x], c = luma[(y - 1) * width + x + 1];
                float d = luma[y * width + x - 1], f = luma[y * width + x + 1];
                float g = luma[(y + 1) * width + x - 1], h = luma[(y + 1) * width + x], k = luma[(y + 1) * width + x + 1];
                float gx = (c + 2f * f + k) - (a + 2f * d + g);
                float gy = (g + 2f * h + k) - (a + 2f * b + c);
                if (Math.Sqrt(gx * gx + gy * gy) / 4.0 > EdgeThreshold) edges++;
            }
            return inner == 0 ? 0 : (double)edges / inner;
        }

        /// <summary>How many mask pixels have all 8 neighbours in the mask too: the pixels Edges measures (0: nothing to measure).</summary>
        public static int InnerPixels(bool[] mask, int width, int height)
        {
            int inner = 0;
            for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
                if (InnerPixel(mask, width, x, y)) inner++;
            return inner;
        }

        /// <summary>
        /// Each Gap: a group of Gap cells that touch side by side (not only at a corner), as cell indexes (row * columns +
        /// column, row 0 at the bottom) in order. The groups come in the order of their first cell.
        /// </summary>
        public static List<List<int>> Groups(bool[] isGap, int columns, int rows)
        {
            var groups = new List<List<int>>();
            var seen = new bool[isGap.Length];
            for (int start = 0; start < isGap.Length; start++)
            {
                if (!isGap[start] || seen[start]) continue;
                var group = new List<int>();
                var open = new Stack<int>();
                open.Push(start);
                seen[start] = true;
                while (open.Count > 0)
                {
                    int cell = open.Pop();
                    group.Add(cell);
                    int column = cell % columns, row = cell / columns;
                    if (column > 0) Visit(cell - 1);
                    if (column < columns - 1) Visit(cell + 1);
                    if (row > 0) Visit(cell - columns);
                    if (row < rows - 1) Visit(cell + columns);
                }
                group.Sort();
                groups.Add(group);

                void Visit(int next)
                {
                    if (!isGap[next] || seen[next]) return;
                    seen[next] = true;
                    open.Push(next);
                }
            }
            return groups;
        }

        /// <summary>A Gap's name from its first cell, for example "Gap c3r2" (column 3, row 2 from the bottom).</summary>
        public static string GapName(List<int> gap, int columns) => "Gap c" + gap[0] % columns + "r" + gap[0] / columns;

        static bool InnerPixel(bool[] mask, int width, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (!mask[(y + dy) * width + x + dx]) return false;
            return true;
        }

        /// <summary>How far the Gap pixels' mean brightness is from the gear pixels' (0 to 1); NaN when either has no pixels.</summary>
        public static double GapStandOut(float[] luma, bool[] gaps, bool[] gear)
        {
            double gapMean = Mean(luma, gaps), gearMean = Mean(luma, gear);
            return Math.Abs(gapMean - gearMean); // NaN stays NaN
        }

        static double Mean(float[] luma, bool[] mask)
        {
            double sum = 0;
            int count = 0;
            for (int i = 0; i < luma.Length; i++)
            {
                if (!mask[i]) continue;
                sum += luma[i];
                count++;
            }
            return count == 0 ? double.NaN : sum / count;
        }

        /// <summary>
        /// The pixels whose centres (x + 0.5, y + 0.5) lie inside the convex hull of these points (pixel units, from the
        /// bottom left), cut at the picture's edge. The points can come in any order; points inside the hull do not matter.
        /// </summary>
        public static bool[] Polygon(IReadOnlyList<Vector2> points, int width, int height)
        {
            List<Vector2> hull = ConvexHull(points);
            var mask = new bool[width * height];
            if (hull.Count < 3) return mask;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                mask[y * width + x] = Inside(hull, new Vector2(x + 0.5f, y + 0.5f));
            return mask;
        }

        /// <summary>The convex hull, counter-clockwise (Andrew's monotone chain).</summary>
        static List<Vector2> ConvexHull(IReadOnlyList<Vector2> points)
        {
            var sorted = new List<Vector2>(points);
            sorted.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            var hull = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                foreach (Vector2 p in sorted)
                {
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0)
                        hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1); // the last point starts the other chain
                sorted.Reverse();
            }
            return hull;
        }

        static double Cross(Vector2 o, Vector2 a, Vector2 b) => (double)(a.x - o.x) * (b.y - o.y) - (double)(a.y - o.y) * (b.x - o.x);

        static bool Inside(List<Vector2> hull, Vector2 p)
        {
            for (int i = 0; i < hull.Count; i++)
                if (Cross(hull[i], hull[(i + 1) % hull.Count], p) < 0) return false;
            return true;
        }

        /// <summary>Each pixel that is not exactly the clear colour: what a render of only some objects drew.</summary>
        public static bool[] Differs(Color32[] pixels, Color32 clear)
        {
            var mask = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                mask[i] = p.r != clear.r || p.g != clear.g || p.b != clear.b;
            }
            return mask;
        }

        /// <summary>The share of pixels (0 to 1) that differ between two pictures of the same size.</summary>
        public static double ChangedShare(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) throw new ArgumentException("The pictures differ in size.");
            if (a.Length == 0) return 0;
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a) changed++;
            return (double)changed / a.Length;
        }

        /// <summary>a and not b.</summary>
        public static bool[] Without(bool[] a, bool[] b)
        {
            var mask = new bool[a.Length];
            for (int i = 0; i < a.Length; i++) mask[i] = a[i] && !b[i];
            return mask;
        }

        /// <summary>a and b.</summary>
        public static bool[] And(bool[] a, bool[] b)
        {
            var mask = new bool[a.Length];
            for (int i = 0; i < a.Length; i++) mask[i] = a[i] && b[i];
            return mask;
        }

        /// <summary>a or b, into a.</summary>
        public static void AddTo(bool[] a, bool[] b)
        {
            for (int i = 0; i < a.Length; i++) a[i] |= b[i];
        }
    }
}
