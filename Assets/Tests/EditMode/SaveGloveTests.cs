using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class SaveGloveTests
    {
        GameObject _rink, _camera;

        [SetUp]
        public void SetUp()
        {
            _rink = new GameObject("Rink (test)");
            _camera = new GameObject("Camera (test)", typeof(Camera));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rink);
            Object.DestroyImmediate(_camera);
        }

        [Test]
        public void The_save_block_is_the_save_glove_model_at_the_save_block_size()
        {
            new ShotEffects(_rink.transform, _camera.GetComponent<Camera>());
            Transform block = _rink.transform.Find("Save Block");
            Assert.IsNotNull(block);
            Transform glove = block.Find(ModelFiles.SaveGlove);
            Assert.IsNotNull(glove, "the save block should show the save-glove model");
            Bounds size = WorldBounds(block.gameObject);
            Assert.AreEqual(0.3f, size.size.x, 0.01f);
            Assert.AreEqual(0.25f, size.size.y, 0.01f);
            Assert.AreEqual(0.1f, size.size.z, 0.01f);
            Assert.AreEqual(0, block.GetComponentsInChildren<Collider>(true).Length);
        }

        [Test]
        public void With_no_save_glove_model_the_save_block_is_the_old_light_blue_block()
        {
            new ShotEffects(_rink.transform, _camera.GetComponent<Camera>(), name => null);
            Transform block = _rink.transform.Find("Save Block");
            Assert.IsNull(block.Find(ModelFiles.SaveGlove));
            var renderer = block.GetComponentInChildren<Renderer>(true);
            Assert.IsNotNull(renderer);
            Assert.AreEqual(new Color(0.45f, 0.6f, 1f), renderer.sharedMaterial.color);
            Bounds size = WorldBounds(block.gameObject);
            Assert.AreEqual(0.3f, size.size.x, 0.01f);
            Assert.AreEqual(0.25f, size.size.y, 0.01f);
        }

        /// <summary>World bounds of all meshes under root (works while the block is hidden, unlike Renderer.bounds).</summary>
        static Bounds WorldBounds(GameObject root)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Bounds m = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = f.transform.TransformPoint(new Vector3(
                        (i & 1) == 0 ? m.min.x : m.max.x, (i & 2) == 0 ? m.min.y : m.max.y, (i & 4) == 0 ? m.min.z : m.max.z));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
    }
}
