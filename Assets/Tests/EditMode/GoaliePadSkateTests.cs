using System.Collections.Generic;
using System.Text;
using HockeyGame;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The goalie's skates stay inside or behind his leg pads, seen from the shooter (#131). A skate point pokes through when it
    /// is in front of the pad: a line from it away from the shooter meets the pad (the pad is behind the point, so the point is
    /// within the pad's outline) while a line toward the shooter does not (nothing of the pad is in front of it). Measured on
    /// the real gear meshes in every pose the goalie shows legs in.
    /// </summary>
    public class GoaliePadSkateTests
    {
        /// <summary>How far in front of a pad a skate point may show (metres): mesh rounding only.</summary>
        const float Tolerance = 0.003f;

        static readonly Vector3 TowardShooter = Vector3.back;

        GameObject _rink;
        GoalieCharacter _goalie;

        [SetUp]
        public void SetUp()
        {
            _rink = new GameObject("Rink (test)");
            _goalie = new GoalieCharacter(_rink.transform);
            Assert.IsTrue(_goalie.Present, "Assets/Resources/Models/goalie-dressing.fbx is missing");
            _goalie.Show(true);
            _goalie.ShowReaction(0f);
        }

        [TearDown]
        public void TearDown() { RinkTeardown.Destroy(_rink); }

        static Vector3 NetPoint(double x, double y)
        {
            NetFaceMapping.ToMetres(x, y, out double right, out double up);
            return new Vector3((float)right, (float)up, 0f);
        }

        sealed class Shell
        {
            public Vector3[] Vertices;
            public int[] Triangles;
            /// <summary>Per triangle: min x, max x, min y, max y (a ray along Z only meets triangles whose box holds its x and y).</summary>
            public float[] Box;
        }

        Shell WorldMesh(string piece)
        {
            string name = GearPiece.ObjectName(piece);
            foreach (MeshRenderer r in _goalie.Root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name != name) continue;
                Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] local = mesh.vertices;
                var world = new Vector3[local.Length];
                for (int i = 0; i < local.Length; i++) world[i] = r.transform.TransformPoint(local[i]);
                int[] tris = mesh.triangles;
                var box = new float[tris.Length / 3 * 4];
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = world[tris[i]], b = world[tris[i + 1]], c = world[tris[i + 2]];
                    int k = i / 3 * 4;
                    box[k] = Mathf.Min(a.x, Mathf.Min(b.x, c.x)); box[k + 1] = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                    box[k + 2] = Mathf.Min(a.y, Mathf.Min(b.y, c.y)); box[k + 3] = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                }
                return new Shell { Vertices = world, Triangles = tris, Box = box };
            }
            Assert.Fail("no " + name);
            return null;
        }

        /// <summary>The nearest distance along a ray to the shell (Moller-Trumbore, both sides), or -1 for none.</summary>
        static float Hit(Shell shell, Vector3 origin, Vector3 dir)
        {
            float best = -1f;
            Vector3[] v = shell.Vertices;
            int[] t = shell.Triangles;
            float[] box = shell.Box;
            for (int i = 0; i < t.Length; i += 3)
            {
                int k = i / 3 * 4;
                if (origin.x < box[k] || origin.x > box[k + 1] || origin.y < box[k + 2] || origin.y > box[k + 3]) continue;
                Vector3 a = v[t[i]], e1 = v[t[i + 1]] - a, e2 = v[t[i + 2]] - a;
                Vector3 p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float inv = 1f / det;
                Vector3 s = origin - a;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float w = Vector3.Dot(dir, q) * inv;
                if (w < 0f || u + w > 1f) continue;
                float d = Vector3.Dot(e2, q) * inv;
                if (d > 1e-5f && (best < 0f || d < best)) best = d;
            }
            return best;
        }

        /// <summary>How far in front of the pad (toward the shooter) the skate's most forward point inside the pad's outline shows; 0 when none.</summary>
        float WorstPoke(string skate, string pad, out int points)
        {
            Shell s = WorldMesh(skate), p = WorldMesh(pad);
            float worst = 0f;
            points = 0;
            foreach (Vector3 v in s.Vertices)
            {
                if (Hit(p, v, TowardShooter) >= 0f) continue;     // the pad is in front of it: hidden (behind or inside)
                float inFront = Hit(p, v, -TowardShooter);        // the pad is behind it: the point shows in front of the pad
                if (inFront < 0f) continue;                       // outside the pad's outline: not through the pad
                if (inFront > Tolerance) points++;
                worst = Mathf.Max(worst, inFront);
            }
            return worst;
        }

        /// <summary>Every pose a goalie shows legs in: name and the way to show it.</summary>
        static IEnumerable<string> Poses()
        {
            yield return "Ready";
            foreach (PushKind kind in new[] { PushKind.Shuffle, PushKind.TPush })
                foreach (Skate skate in new[] { Skate.Left, Skate.Right })
                    yield return kind + " " + skate;
            yield return "Pads reaction low";
            yield return "Pads reaction standing";
            yield return "Glove reaction";
            yield return "Blocker reaction";
        }

        void Show(string pose)
        {
            _goalie.ShowReaction(0f);
            var eye = new Vector3(0f, 1.5f, -9f);
            switch (pose)
            {
                case "Ready": return;
                case "Pads reaction low": _goalie.React(SaveReaction.Pads, eye, NetPoint(15, 8), 0f, true); _goalie.ShowReaction(1f); return;
                case "Pads reaction standing": _goalie.React(SaveReaction.Pads, eye, NetPoint(15, 35), 0f, true); _goalie.ShowReaction(1f); return;
                case "Glove reaction": _goalie.React(SaveReaction.Glove, eye, NetPoint(95, 95), 0f, true); _goalie.ShowReaction(1f); return;
                case "Blocker reaction": _goalie.React(SaveReaction.Blocker, eye, NetPoint(5, 95), 0f, true); _goalie.ShowReaction(1f); return;
            }
            string[] parts = pose.Split(' ');
            PushKind kind = (PushKind)System.Enum.Parse(typeof(PushKind), parts[0]);
            Skate skate = (Skate)System.Enum.Parse(typeof(Skate), parts[1]);
            _goalie.ShowPush(new PushShown(kind, skate, PushPhase.Push, 1), new PushPoseMoment(1, kind == PushKind.TPush ? 1 : 0));
        }

        [Test, Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void No_skate_shows_through_a_leg_pad_in_any_pose()
        {
            var report = new StringBuilder();
            int bad = 0;
            foreach (string pose in Poses())
            {
                Show(pose);
                foreach (var (skate, pad) in new[] { (GearPiece.LeftSkate, GearPiece.LeftPad), (GearPiece.RightSkate, GearPiece.RightPad) })
                {
                    float worst = WorstPoke(skate, pad, out int points);
                    report.AppendLine(pose + ", " + skate + ": " + points + " points through, worst " + (worst * 100f).ToString("F1") + " cm");
                    if (worst > Tolerance) bad++;
                }
            }
            TestContext.Out.WriteLine(report.ToString());
            Assert.AreEqual(0, bad, report.ToString());
        }

        [Test]
        public void The_Ready_Stance_skates_do_not_show_through_the_pads()
        {
            foreach (var (skate, pad) in new[] { (GearPiece.LeftSkate, GearPiece.LeftPad), (GearPiece.RightSkate, GearPiece.RightPad) })
            {
                float worst = WorstPoke(skate, pad, out int points);
                Assert.LessOrEqual(worst, Tolerance, skate + ": " + points + " points through the " + pad + ", worst " + (worst * 100f).ToString("F1") + " cm");
            }
        }
    }
}
