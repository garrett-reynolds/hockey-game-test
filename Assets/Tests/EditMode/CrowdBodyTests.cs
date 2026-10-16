using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class CrowdBodyTests
    {
        /// <summary>A one-triangle v3 body: 3 vertices, palette (skin, team top), one 2-frame looping clip, no cards.</summary>
        private static byte[] TinyBody(uint version = 3)
        {
            var s = new MemoryStream();
            var w = new BinaryWriter(s);
            w.Write(Encoding.ASCII.GetBytes("CRWD")); w.Write(version); w.Write(3u); w.Write(1u); w.Write(1u); w.Write(1u);
            w.Write(2u); w.Write(new byte[] { 0, 200, 150, 120 }); w.Write(new byte[] { 1, 10, 20, 30 });
            for (int v = 0; v < 3; v++)
            {
                w.Write((float)v); w.Write(0f); w.Write(0f); w.Write(0f); w.Write(0f); w.Write(1f);
                w.Write((byte)(v == 2 ? 1 : 0)); w.Write(new byte[3]);
            }
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)2);
            w.Write((byte)8); w.Write(Encoding.ASCII.GetBytes("sit-idle")); w.Write(2u); w.Write(12f); w.Write((byte)1);
            w.Write(new byte[3 * 2 * 8]);   // 1 shape x 2 frames x 3 vertices x RGBAHalf
            w.Write(0u);                    // no cards
            return s.ToArray();
        }

        [Test]
        public void A_version_3_body_has_its_palette_and_each_vertex_its_paint()
        {
            CrowdBody body = CrowdBody.Read(TinyBody(), "tiny");
            try
            {
                Assert.That(body.Palette.Length, Is.EqualTo(2));
                Assert.That(body.Palette[0].Kind, Is.EqualTo(CrowdBody.PaintKind.Skin));
                Assert.That(body.Palette[1].Kind, Is.EqualTo(CrowdBody.PaintKind.TeamTop));
                Assert.That(body.Palette[1].Colour, Is.EqualTo(new Color32(10, 20, 30, 255)));
                var uv = new System.Collections.Generic.List<Vector2>();
                body.Mesh.GetUVs(0, uv);
                Assert.That(uv[2], Is.EqualTo(new Vector2(2, 1)), "uv0 = (vertex number, paint)");
                Assert.That(body.Clips[0].Name, Is.EqualTo("sit-idle"));
            }
            finally { body.Destroy(); }
        }

        [Test]
        public void A_body_keeps_its_mesh_and_VAT_picture_after_Unity_unloads_unused_assets_so_the_crowd_never_draws_nothing()
        {
            CrowdBody body = CrowdBody.Read(TinyBody(), "tiny"); // held only by managed code, like the crowd's (#203)
            try
            {
                UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
                Assert.IsTrue(body.Mesh != null, "Unity unloaded the body mesh as unused (#203)");
                Assert.IsTrue(body.Vat != null, "Unity unloaded the VAT picture as unused (#203)");
            }
            finally { body.Destroy(); }
        }

        [Test]
        public void A_version_2_body_is_refused()
        {
            Assert.Throws<InvalidDataException>(() => CrowdBody.Read(TinyBody(2), "old"));
        }
    }
}
