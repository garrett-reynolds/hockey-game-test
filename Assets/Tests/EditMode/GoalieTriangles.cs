using System.Collections.Generic;
using HockeyGame;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The goalie character's real triangles as it stands now (gear and the skinned body baked), and the tests' own
    /// segment-through-triangle check: independent of GoalieShape, so it can judge what the game measured.
    /// </summary>
    internal static class GoalieTriangles
    {
        internal readonly struct Triangle
        {
            public readonly Vector3 A, B, C;
            public Triangle(Vector3 a, Vector3 b, Vector3 c) { A = a; B = b; C = c; }
        }

        /// <summary>The posed gear and body (as the goalie stands now) in world space.</summary>
        internal static List<Triangle> Of(GoalieCharacter character)
        {
            var list = new List<Triangle>();
            foreach (MeshFilter f in character.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                Vector3[] v = f.sharedMesh.vertices;
                int[] t = f.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                    list.Add(new Triangle(f.transform.TransformPoint(v[t[i]]), f.transform.TransformPoint(v[t[i + 1]]), f.transform.TransformPoint(v[t[i + 2]])));
            }
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer body in character.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                body.BakeMesh(mesh, true);
                Matrix4x4 m = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                    list.Add(new Triangle(m.MultiplyPoint3x4(v[t[i]]), m.MultiplyPoint3x4(v[t[i + 1]]), m.MultiplyPoint3x4(v[t[i + 2]])));
            }
            Object.DestroyImmediate(mesh);
            return list;
        }

        /// <summary>The triangles whose bounds overlap a box (padded): every triangle a move inside the box can cross.</summary>
        internal static List<Triangle> Near(List<Triangle> triangles, Bounds box, float pad)
        {
            box.Expand(2f * pad);
            var near = new List<Triangle>();
            foreach (Triangle t in triangles)
            {
                Vector3 min = Vector3.Min(t.A, Vector3.Min(t.B, t.C)), max = Vector3.Max(t.A, Vector3.Max(t.B, t.C));
                if (min.x <= box.max.x && max.x >= box.min.x && min.y <= box.max.y && max.y >= box.min.y && min.z <= box.max.z && max.z >= box.min.z)
                    near.Add(t);
            }
            return near;
        }

        /// <summary>True when the straight move from a to b passes through a triangle (Moller-Trumbore).</summary>
        internal static bool Crosses(List<Triangle> triangles, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-12f) return false;
            foreach (Triangle tri in triangles)
            {
                Vector3 e1 = tri.B - tri.A, e2 = tri.C - tri.A, p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-12f) continue;
                float inv = 1f / det;
                Vector3 s = a - tri.A;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(dir, q) * inv;
                if (v < 0f || u + v > 1f) continue;
                float t = Vector3.Dot(e2, q) * inv;
                if (t >= 0f && t <= 1f) return true;
            }
            return false;
        }

        /// <summary>
        /// True when a face-on puck (GoalieShape.LinePuckDiameter across) flying from `from` to `to` would touch a triangle: its
        /// middle or any of four edge points, each a straight line. Only the stretch from 2.5 m out to the Net Face is checked
        /// (the goalie is nowhere else).
        /// </summary>
        internal static bool PuckTouches(List<Triangle> triangles, Vector3 from, Vector3 to)
        {
            float t = Mathf.Clamp01((-2.5f - from.z) / (to.z - from.z));
            Vector3 start = Vector3.Lerp(from, to, t);
            float r = GoalieShape.LinePuckDiameter / 2f;
            var box = new Bounds(start, Vector3.zero);
            box.Encapsulate(to);
            List<Triangle> near = Near(triangles, box, r);
            foreach (Vector3 edge in new[] { Vector3.zero, Vector3.left * r, Vector3.right * r, Vector3.up * r, Vector3.down * r })
                if (Crosses(near, start + edge, to + edge)) return true;
            return false;
        }
    }
}
