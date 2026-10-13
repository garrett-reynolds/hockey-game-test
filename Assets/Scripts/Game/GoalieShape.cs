using System.Collections.Generic;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The goalie's gear and body triangles, placed for a reaction; GoalieCharacter checks the Shot's line against them
    /// (Reach in time). Never moves the puck.
    /// </summary>
    public sealed class GoalieShape
    {
        /// <summary>The face-on puck the reach checks use (the old scripted puck's size). Not the physics puck (PhysicsPuck).</summary>
        public const float LinePuckDiameter = 0.15f, LinePuckThickness = 0.05f;

        const int ChunkTriangles = 64; // triangles under one bounding box, so a cast only tests the parts it passes near

        sealed class Chunk
        {
            public Bounds Box;
            public int First, Count; // triangles, into _corners (three corners each)
            public string Piece;
        }

        readonly List<Vector3> _corners = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>(); // one per triangle (zero for a degenerate one), worked out once
        Bounds _box;                                     // around every chunk (meshes only), when _meshes
        bool _meshes;
        readonly List<string> _pieces = new List<string>(); // the pieces its meshes are named
        readonly List<Chunk> _chunks = new List<Chunk>();
        readonly List<(GoalieShape shape, Matrix4x4 toWorld, Matrix4x4 toLocal)> _placed = new List<(GoalieShape, Matrix4x4, Matrix4x4)>();

        /// <summary>Takes everything out, to be filled again (so one can be reused without a new one each Shot frame).</summary>
        public void Clear()
        {
            _corners.Clear();
            _normals.Clear();
            _box = default;
            _meshes = false;
            _pieces.Clear();
            _chunks.Clear();
            _placed.Clear();
        }

        /// <summary>
        /// Adds another set of meshes, built in its own space, placed by toWorld: a rigid move (turn and shift, no
        /// scale), so distances are the same in both spaces. Queries move into its space instead of every triangle moving out,
        /// so a shape measured once (GoalieCharacter.AddReaction) costs nothing to place again.
        /// </summary>
        public void AddPlaced(GoalieShape shape, Matrix4x4 toWorld)
        {
            _placed.Add((shape, toWorld, toWorld.inverse));
        }

        /// <summary>Adds a triangle mesh in world space. piece names it for Cast's pieces (for example "Left Pad").</summary>
        public void AddMesh(Vector3[] vertices, int[] triangles, string piece = null)
        {
            int count = triangles.Length / 3;
            for (int t = 0; t < count; t += ChunkTriangles)
            {
                int end = Mathf.Min(t + ChunkTriangles, count);
                var chunk = new Chunk { First = _corners.Count / 3, Count = end - t, Piece = piece };
                chunk.Box = new Bounds(vertices[triangles[t * 3]], Vector3.zero);
                for (int i = t * 3; i < end * 3; i++)
                {
                    _corners.Add(vertices[triangles[i]]);
                    chunk.Box.Encapsulate(vertices[triangles[i]]);
                }
                for (int i = t * 3; i < end * 3; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 n = Vector3.Cross(vertices[triangles[i + 1]] - a, vertices[triangles[i + 2]] - a);
                    _normals.Add(n.sqrMagnitude < 1e-14f ? Vector3.zero : n.normalized);
                }
                _chunks.Add(chunk);
                if (_meshes) _box.Encapsulate(chunk.Box);
                else { _box = chunk.Box; _meshes = true; }
            }
            if (!_pieces.Contains(piece)) _pieces.Add(piece);
        }

        /// <summary>
        /// True when the puck's centre, moving from `from` to `to`, comes within `radius` of the bounding box of any mesh chunk:
        /// a quick, cautious "may touch". False means it surely meets no mesh.
        /// </summary>
        public bool NearAnyMesh(Vector3 from, Vector3 to, float radius)
        {
            Vector3 move = to - from;
            float length = move.magnitude;
            if (length < 1e-7f) return false;
            if (ChunksNear(from, move / length, length, radius)) return true;
            foreach (var (shape, _, toLocal) in _placed)
                if (shape._meshes && shape.ChunksNear(toLocal.MultiplyPoint3x4(from), toLocal.MultiplyVector(move / length), length, radius)) return true;
            return false;
        }

        bool ChunksNear(Vector3 from, Vector3 dir, float length, float radius)
        {
            if (!_meshes) return false;
            var ray = new Ray(from, dir);
            Bounds all = _box;
            all.Expand(2f * radius);
            if (!all.IntersectRay(ray, out float enter) || enter > length) return false;
            foreach (Chunk chunk in _chunks)
            {
                Bounds reach = chunk.Box;
                reach.Expand(2f * radius);
                if (reach.IntersectRay(ray, out enter) && enter <= length) return true;
            }
            return false;
        }

        /// <summary>
        /// The first surface the line puck's centre, moving from `from` to `to`, comes within `radius` of (only surfaces it is
        /// moving toward). at: where the centre is then; normal: the surface's, facing the puck. pieces: only these (null: all).
        /// </summary>
        public bool Cast(Vector3 from, Vector3 to, float radius, out Vector3 at, out Vector3 normal, string[] pieces = null)
        {
            at = to;
            normal = default;
            Vector3 move = to - from;
            float length = move.magnitude;
            if (length < 1e-7f) return false;
            Vector3 dir = move / length;
            float best = float.PositiveInfinity;
            CastChunks(from, to, dir, length, radius, pieces, ref best, ref normal);
            foreach (var (shape, toWorld, toLocal) in _placed)
            {
                if (!shape._meshes || (pieces != null && !shape.HasAny(pieces))) continue;
                // Skip a placed set the line passes nowhere near (or only beyond the nearest hit so far).
                Vector3 localFrom = toLocal.MultiplyPoint3x4(from), localDir = toLocal.MultiplyVector(dir);
                Bounds reach = shape._box;
                reach.Expand(2f * radius);
                if ((!reach.IntersectRay(new Ray(localFrom, localDir), out float enter) || enter > length || enter >= best)) continue;
                float before = best;
                Vector3 n = default;
                shape.CastChunks(localFrom, toLocal.MultiplyPoint3x4(to), localDir, length, radius, pieces, ref best, ref n);
                if (best < before) normal = toWorld.MultiplyVector(n);
            }
            if (float.IsPositiveInfinity(best)) return false;
            at = from + dir * best;
            return true;
        }

        bool HasAny(string[] pieces)
        {
            foreach (string piece in pieces)
                if (_pieces.Contains(piece)) return true;
            return false;
        }

        static readonly List<(float enter, int chunk)> NearChunks = new List<(float, int)>(); // CastChunks' scratch list

        /// <summary>Cast's test against this set's own triangles: improves best (distance along dir) and its normal.</summary>
        void CastChunks(Vector3 from, Vector3 to, Vector3 dir, float length, float radius, string[] pieces, ref float best, ref Vector3 normal)
        {
            Vector3 move = to - from;
            var sweep = new Bounds((from + to) / 2f, new Vector3(Mathf.Abs(move.x), Mathf.Abs(move.y), Mathf.Abs(move.z)) + Vector3.one * (2f * radius));
            // Chunks the move's line passes within the radius of (a long move's sweep box holds most of the goalie), nearest
            // first: where the line enters a chunk's box grown by the puck's size comes before any hit in it, so once a hit is
            // nearer than the next chunk's entry, the rest cannot be nearer.
            List<(float enter, int chunk)> near = NearChunks;
            near.Clear();
            for (int i = 0; i < _chunks.Count; i++)
            {
                Chunk chunk = _chunks[i];
                if (!chunk.Box.Intersects(sweep) || (pieces != null && System.Array.IndexOf(pieces, chunk.Piece) < 0)) continue;
                Bounds reach = chunk.Box;
                reach.Expand(2f * radius);
                if (!reach.IntersectRay(new Ray(from, dir), out float enter) || enter > length) continue;
                near.Add((Mathf.Max(0f, enter), i));
            }
            near.Sort((x, y) => x.enter.CompareTo(y.enter));
            foreach (var (enter, index) in near)
            {
                if (enter >= best) break;
                Chunk chunk = _chunks[index];
                for (int t = chunk.First; t < chunk.First + chunk.Count; t++)
                {
                    Vector3 n = _normals[t];
                    if (n.x == 0f && n.y == 0f && n.z == 0f) continue;
                    Vector3 a = _corners[t * 3];
                    float height = Vector3.Dot(from - a, n);
                    if (height < 0f) { n = -n; height = -height; }
                    float closing = -Vector3.Dot(dir, n);
                    if (closing <= 1e-6f) continue; // moving away from (or along) it
                    float d = Mathf.Max(0f, (height - radius) / closing);
                    if (d > length || d >= best) continue;
                    Vector3 onPlane = from + dir * d - n * Mathf.Min(height, radius);
                    Vector3 b = _corners[t * 3 + 1], c = _corners[t * 3 + 2];
                    if (!Inside(onPlane, a, b, c, n))
                    {
                        // Not met face-on within the radius; the centre itself may still cross the triangle (it starts close
                        // beside it): stop at the crossing, kept back by the radius as far as the move allows.
                        float through = height / closing;
                        if (through > length || !Inside(from + dir * through, a, b, c, n)) continue;
                        d = Mathf.Max(0f, through - radius);
                        if (d >= best) continue;
                    }
                    best = d;
                    normal = n;
                }
            }
        }

        static bool Inside(Vector3 p, Vector3 a, Vector3 b, Vector3 c, Vector3 n)
        {
            float u = Vector3.Dot(Vector3.Cross(b - a, p - a), n);
            float v = Vector3.Dot(Vector3.Cross(c - b, p - b), n);
            float w = Vector3.Dot(Vector3.Cross(a - c, p - c), n);
            return (u >= 0f && v >= 0f && w >= 0f) || (u <= 0f && v <= 0f && w <= 0f);
        }
    }
}
