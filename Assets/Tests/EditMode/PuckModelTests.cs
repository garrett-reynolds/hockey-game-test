using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class PuckModelTests
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
        public void The_supplied_puck_has_a_face_and_a_rim_material_both_near_black_URP_Lit()
        {
            new ShotEffects(_rink.transform, _camera.GetComponent<Camera>());
            Transform model = _rink.transform.Find("Puck").Find(ModelFiles.Puck);
            Assert.IsNotNull(model, "the puck should show the puck model");

            var names = new System.Collections.Generic.List<string>();
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                {
                    Assert.IsNotNull(m);
                    names.Add(m.name);
                    Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name, m.name);
                    Color c = m.color;
                    Assert.Less(Mathf.Max(c.r, c.g, c.b), 0.2f, m.name + " is not near-black: " + c);
                    Assert.IsNull(m.mainTexture, m.name + " should have no texture");
                }
            CollectionAssert.AreEquivalent(new[] { "Puck Face", "Puck Rim" }, names);
        }

        [Test]
        public void The_supplied_puck_fills_the_puck_size_with_its_flat_face_on_the_pivots_up_axis()
        {
            new ShotEffects(_rink.transform, _camera.GetComponent<Camera>());
            Transform pivot = _rink.transform.Find("Puck");
            Transform model = pivot.Find(ModelFiles.Puck);
            Assert.IsNotNull(model);

            Bounds size = WorldBounds(pivot.gameObject);
            Assert.AreEqual(0.15f, size.size.x, 0.005f);
            Assert.AreEqual(0.05f, size.size.y, 0.005f);
            Assert.AreEqual(0.15f, size.size.z, 0.005f);
            Assert.AreEqual(0, pivot.GetComponentsInChildren<Collider>(true).Length);

            // The model's own thin axis is its Y: fitting scales it nearly evenly. A disc lying on its edge would need
            // y squeezed to about a third of x and z stretched (about 0.075, 0.025, 0.29 for this model).
            Vector3 s = model.localScale;
            Assert.AreEqual(s.x, s.z, s.x * 0.05f, "x and z scale differ: " + s);
            float ratio = s.y / s.x;
            Assert.That(ratio, Is.InRange(0.5f, 2f), "uneven scale, the model is not flat on Y: " + s);
        }

        [Test]
        public void With_no_puck_model_the_puck_is_the_black_cylinder()
        {
            new ShotEffects(_rink.transform, _camera.GetComponent<Camera>(), name => null);
            Transform pivot = _rink.transform.Find("Puck");
            Assert.IsNull(pivot.Find(ModelFiles.Puck));
            Assert.IsNotNull(pivot.Find("Puck Placeholder"));
            var renderer = pivot.GetComponentInChildren<Renderer>(true);
            Color c = renderer.sharedMaterial.color;
            Assert.AreEqual(0.05f, c.r, 0.001f);
            Assert.AreEqual(0.05f, c.g, 0.001f);
            Assert.AreEqual(0.05f, c.b, 0.001f);
            Bounds size = WorldBounds(pivot.gameObject);
            Assert.AreEqual(0.15f, size.size.x, 0.005f);
            Assert.AreEqual(0.05f, size.size.y, 0.005f);
            Assert.AreEqual(0, pivot.GetComponentsInChildren<Collider>(true).Length);
        }

        /// <summary>World bounds of all meshes under root (works while the puck is hidden, unlike Renderer.bounds).</summary>
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
