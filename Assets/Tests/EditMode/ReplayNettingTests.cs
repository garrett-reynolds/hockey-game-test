using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class ReplayNettingTests
    {
        [Test]
        public void The_welded_cloth_positions_match_the_render_mesh_in_its_local_space()
        {
            var root = new GameObject("Offset net replay test");
            root.transform.SetPositionAndRotation(new Vector3(4f, 0f, 7f), Quaternion.Euler(0f, 25f, 0f));
            var net = new NetCloth(root.transform);
            try
            {
                Vector3[] drawn = net.Cloth.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices;
                Vector3[] simulated = net.Cloth.vertices;
                Assert.AreEqual(drawn.Length, simulated.Length);
                for (int i = 0; i < drawn.Length; i++)
                    Assert.Less(Vector3.Distance(drawn[i], simulated[i]), 0.001f, "cloth vertex " + i);
            }
            finally
            {
                net.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Recorded_net_shape_plays_between_samples_and_restores_the_live_renderer()
        {
            var root = new GameObject("Net replay test");
            var source = root.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            source.sharedMesh = mesh;
            var replay = new ReplayNetting(source, () => mesh.vertices);
            try
            {
                replay.Record(0);
                mesh.vertices = new[] { Vector3.forward * 0.08f, Vector3.right, Vector3.up };
                replay.Record(1);
                replay.Apply(0.5);
                Assert.That(replay.Look.GetComponent<MeshFilter>().sharedMesh.vertices[0].z, Is.EqualTo(0.04f).Within(0.001f));
                Assert.IsTrue(source.forceRenderingOff);
                Assert.IsTrue(replay.Look.activeSelf);
                replay.Restore();
                Assert.IsFalse(source.forceRenderingOff);
                Assert.IsFalse(replay.Look.activeSelf);
                Assert.That(mesh.vertices[0].z, Is.EqualTo(0.08f), "live net stays at its current shape");
                replay.Begin();
                Assert.AreEqual(0, replay.SampleCount);
            }
            finally
            {
                replay.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
