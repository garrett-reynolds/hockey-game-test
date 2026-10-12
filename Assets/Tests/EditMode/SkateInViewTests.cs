using System.Collections.Generic;
using System.Text;
using HockeyGame;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The shooter never sees the edge of the world: with the real ice, boards and arena models, every view ray of the
    /// skate-in camera (and the slow-down drift toward the net) lands on something solid, not on the empty backdrop or
    /// on the bare ice sheet outside the boards. The glass is left out, because the shooter sees through it.
    /// </summary>
    public class SkateInViewTests
    {
        GameObject _rink;
        Camera _camera;

        [SetUp]
        public void Build()
        {
            _rink = new GameObject("Rink");
            RinkIce.Build(_rink.transform);
            RinkBoards.Build(_rink.transform);
            ArenaStands.Build(_rink.transform);
            foreach (MeshFilter f in _rink.GetComponentsInChildren<MeshFilter>())
                if (f.name != RinkBoards.GlassPart) f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
            Physics.SyncTransforms();
            _camera = new GameObject("Test Camera").AddComponent<Camera>();
        }

        [TearDown]
        public void Remove()
        {
            Object.DestroyImmediate(_rink);
            Object.DestroyImmediate(_camera.gameObject);
        }

        /// <summary>Where the shooter's eye is: on the skate-in at progress u, moved toward the net by drift (metres).</summary>
        static Vector3 Eye(AttackSide side, double u, double drift, float bob)
        {
            Breakaway.SkatePoint(side, u, out double x, out double distance);
            var ground = new Vector3((float)x, 0f, (float)-distance);
            var toNet = new Vector3((float)-x, 0f, (float)distance);
            ground += toNet.normalized * (float)Breakaway.CappedDrift(drift, toNet.magnitude);
            return new Vector3(ground.x, (float)Breakaway.EyeHeight + bob, ground.z);
        }

        [Test]
        public void Every_view_ray_lands_on_the_rink_or_arena_during_the_skate_in_from_every_attack_side()
        {
            var failures = new List<string>();
            // 16:9 and an ultra-wide screen; a little extra field of view covers the stride bob and roll.
            foreach (float aspect in new[] { 16f / 9f, 21f / 9f })
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            foreach (double drift in new[] { 0.0, 100.0 }) // no drift, and drifted in as close as it goes
            foreach (float bob in new[] { -0.06f, 0.06f })
            for (int step = 0; step <= 10; step++)
            {
                double u = step / 10.0;
                if (drift > 0 && step < 10) continue; // the drift only happens at the end of the skate-in
                Vector3 eye = Eye(side, u, drift, bob);
                _camera.transform.position = eye;
                _camera.transform.LookAt(new Vector3(0f, (float)(NetFaceMapping.HeightMetres / 2.0), 0f));
                _camera.fieldOfView = 64f;
                _camera.aspect = aspect;

                for (int i = 0; i <= 24; i++)
                for (int j = 0; j <= 12; j++)
                {
                    Ray ray = _camera.ViewportPointToRay(new Vector3(i / 24f, j / 12f, 0f));
                    string where = $"{side} u={u:0.0} drift={drift} aspect={aspect:0.00} view=({i / 24f:0.00},{j / 12f:0.00})";
                    if (!Physics.Raycast(ray, out RaycastHit hit, 500f))
                        failures.Add(where + ": sees past the world");
                    else if (UnderIce(hit.collider.transform) && !InsideBoards(hit.point))
                        failures.Add(where + ": sees bare ice outside the boards at " + hit.point);
                }
            }
            if (failures.Count > 0)
            {
                var message = new StringBuilder(failures.Count + " view rays miss the rink and arena, for example:\n");
                for (int k = 0; k < Mathf.Min(failures.Count, 12); k++) message.AppendLine(failures[k]);
                Assert.Fail(message.ToString());
            }
        }

        static bool UnderIce(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == "Ice" && t.parent != null && t.parent.name == "Rink") return true;
            return false;
        }

        static bool InsideBoards(Vector3 p) =>
            Mathf.Abs(p.x) <= RinkBoards.HalfWidth + 0.01f && p.z <= RinkBoards.EndZ + 0.01f && p.z >= RinkBoards.FarZ - 0.01f;
    }
}
