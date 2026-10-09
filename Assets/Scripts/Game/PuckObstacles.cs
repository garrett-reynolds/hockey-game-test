using System.Collections.Generic;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// What a puck can hit after its first contact, in world space and pure code: triangle meshes (the posed goalie gear
    /// and body) and round tubes (the posts and crossbar). Cast finds the first surface the puck's centre would come within
    /// a radius of along a move; Nearest gives the real surface's outward direction at a contact point.
    /// </summary>
    public sealed class PuckObstacles
    {
        const int ChunkTriangles = 64; // triangles under one bounding box, so a cast only tests the parts it passes near

        sealed class Chunk
        {
            public Bounds Box;
            public int First, Count; // triangles, into _corners (three corners each)
            public PuckSurface Surface;
            public string Piece;
        }

        readonly struct Tube
        {
            public readonly Vector3 A, B;
            public readonly float Radius;
            public readonly PuckSurface Surface;
            public readonly string Piece;
            public Tube(Vector3 a, Vector3 b, float radius, PuckSurface surface, string piece) { A = a; B = b; Radius = radius; Surface = surface; Piece = piece; }
        }

        readonly List<Vector3> _corners = new List<Vector3>();
        readonly List<Chunk> _chunks = new List<Chunk>();
        readonly List<Tube> _tubes = new List<Tube>();

        /// <summary>Adds a triangle mesh in world space. piece names it for Nearest (for example "Left Pad").</summary>
        public void AddMesh(Vector3[] vertices, int[] triangles, PuckSurface surface, string piece = null)
        {
            int count = triangles.Length / 3;
            for (int t = 0; t < count; t += ChunkTriangles)
            {
                int end = Mathf.Min(t + ChunkTriangles, count);
                var chunk = new Chunk { First = _corners.Count / 3, Count = end - t, Surface = surface, Piece = piece };
                chunk.Box = new Bounds(vertices[triangles[t * 3]], Vector3.zero);
                for (int i = t * 3; i < end * 3; i++)
                {
                    _corners.Add(vertices[triangles[i]]);
                    chunk.Box.Encapsulate(vertices[triangles[i]]);
                }
                _chunks.Add(chunk);
            }
        }

        /// <summary>Adds a round tube (a post or the crossbar) from a to b.</summary>
        public void AddTube(Vector3 a, Vector3 b, float radius, PuckSurface surface, string piece = null)
        {
            _tubes.Add(new Tube(a, b, radius, surface, piece));
        }

        /// <summary>
        /// The first surface the puck's centre, moving from `from` to `to`, comes within `radius` of (only surfaces it is
        /// moving toward). at: where the centre is then; normal: the surface's, facing the puck.
        /// </summary>
        public bool Cast(Vector3 from, Vector3 to, float radius, out Vector3 at, out Vector3 normal, out PuckSurface surface)
        {
            at = to;
            normal = default;
            surface = default;
            Vector3 move = to - from;
            float length = move.magnitude;
            if (length < 1e-7f) return false;
            Vector3 dir = move / length;
            float best = float.PositiveInfinity;

            var sweep = new Bounds((from + to) / 2f, new Vector3(Mathf.Abs(move.x), Mathf.Abs(move.y), Mathf.Abs(move.z)) + Vector3.one * (2f * radius));
            foreach (Chunk chunk in _chunks)
            {
                if (!chunk.Box.Intersects(sweep)) continue;
                for (int t = chunk.First; t < chunk.First + chunk.Count; t++)
                {
                    Vector3 a = _corners[t * 3], b = _corners[t * 3 + 1], c = _corners[t * 3 + 2];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-14f) continue;
                    n.Normalize();
                    float height = Vector3.Dot(from - a, n);
                    if (height < 0f) { n = -n; height = -height; }
                    float closing = -Vector3.Dot(dir, n);
                    if (closing <= 1e-6f) continue; // moving away from (or along) it
                    float d = Mathf.Max(0f, (height - radius) / closing);
                    if (d > length || d >= best) continue;
                    Vector3 onPlane = from + dir * d - n * Mathf.Min(height, radius);
                    if (!Inside(onPlane, a, b, c, n)) continue;
                    best = d;
                    normal = n;
                    surface = chunk.Surface;
                }
            }
            foreach (Tube tube in _tubes)
            {
                if (!CastTube(tube, from, dir, length, radius, out float d, out Vector3 n) || d >= best) continue;
                best = d;
                normal = n;
                surface = tube.Surface;
            }
            if (float.IsPositiveInfinity(best)) return false;
            at = from + dir * best;
            return true;
        }

        static bool Inside(Vector3 p, Vector3 a, Vector3 b, Vector3 c, Vector3 n)
        {
            float u = Vector3.Dot(Vector3.Cross(b - a, p - a), n);
            float v = Vector3.Dot(Vector3.Cross(c - b, p - b), n);
            float w = Vector3.Dot(Vector3.Cross(a - c, p - c), n);
            return (u >= 0f && v >= 0f && w >= 0f) || (u <= 0f && v <= 0f && w <= 0f);
        }

        /// <summary>The centre's ray against the tube grown by the puck radius (the side wall only: no end caps).</summary>
        static bool CastTube(Tube tube, Vector3 from, Vector3 dir, float length, float radius, out float distance, out Vector3 normal)
        {
            distance = 0f;
            normal = default;
            Vector3 axis = (tube.B - tube.A).normalized;
            float span = Vector3.Distance(tube.A, tube.B);
            Vector3 o = Vector3.ProjectOnPlane(from - tube.A, axis);
            Vector3 d = Vector3.ProjectOnPlane(dir, axis);
            float r = tube.Radius + radius;
            float a = Vector3.Dot(d, d), b = 2f * Vector3.Dot(o, d), c = Vector3.Dot(o, o) - r * r;
            if (b >= 0f || a < 1e-10f) return false; // moving away from the axis, or along it
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;
            float t = Mathf.Max(0f, (-b - Mathf.Sqrt(disc)) / (2f * a));
            if (t > length) return false;
            Vector3 p = from + dir * t;
            float along = Vector3.Dot(p - tube.A, axis);
            if (along < 0f || along > span) return false;
            normal = Vector3.ProjectOnPlane(p - tube.A, axis).normalized;
            distance = t;
            return true;
        }

        /// <summary>
        /// The outward direction of the surface nearest the point (from the nearest surface point to it), among the meshes
        /// and tubes named piece (null: all). False when there is nothing by that name.
        /// </summary>
        public bool Nearest(Vector3 point, string piece, out Vector3 normal, out PuckSurface surface)
        {
            return Nearest(point, piece, out normal, out surface, out _);
        }

        /// <param name="distance">From the point to the nearest surface, metres.</param>
        public bool Nearest(Vector3 point, string piece, out Vector3 normal, out PuckSurface surface, out float distance)
        {
            normal = default;
            surface = default;
            float best = float.PositiveInfinity;
            foreach (Chunk chunk in _chunks)
            {
                if (piece != null && chunk.Piece != piece) continue;
                if (chunk.Box.SqrDistance(point) >= best) continue;
                for (int t = chunk.First; t < chunk.First + chunk.Count; t++)
                {
                    Vector3 a = _corners[t * 3], b = _corners[t * 3 + 1], c = _corners[t * 3 + 2];
                    Vector3 q = ClosestOnTriangle(point, a, b, c);
                    float d = (point - q).sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    surface = chunk.Surface;
                    normal = d > 1e-10f ? (point - q).normalized : FaceToward(a, b, c, point);
                }
            }
            foreach (Tube tube in _tubes)
            {
                if (piece != null && tube.Piece != piece) continue;
                Vector3 axis = tube.B - tube.A;
                float s = Mathf.Clamp01(Vector3.Dot(point - tube.A, axis) / axis.sqrMagnitude);
                Vector3 outward = point - (tube.A + axis * s);
                float d = Mathf.Max(0f, outward.magnitude - tube.Radius);
                if (d * d >= best || outward.sqrMagnitude < 1e-12f) continue;
                best = d * d;
                surface = tube.Surface;
                normal = outward.normalized;
            }
            distance = Mathf.Sqrt(best);
            return !float.IsPositiveInfinity(best);
        }

        static Vector3 FaceToward(Vector3 a, Vector3 b, Vector3 c, Vector3 point)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            return Vector3.Dot(point - a, n) >= 0f ? n : -n;
        }

        /// <summary>Closest point on triangle abc to p (Ericson, Real-Time Collision Detection 5.1.5).</summary>
        static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) return b + (c - b) * ((d4 - d3) / (d4 - d3 + d5 - d6));
            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }
    }
}
