using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The goalie's jersey, pants and socks are matte cloth (#170): no smoothness, no specular highlight, so the arena lights
    /// leave no bright shine. The hard gear (pads, mask, blocker, glove, stick, skates) keeps its look.
    /// The jersey (#220) is its own model, Resources/Models/goalie-jersey.fbx (Tools/make_goalie_jersey.py), swapped onto the
    /// imported Jersey renderer by bone name; its stripes, crest and number are painted by GoalieClothSurface.
    /// </summary>
    public class GoalieClothTests
    {
        const float ClothSmoothnessLimit = 0.1f; // the model imported at 0.4, which shines like plastic
        const float GearSmoothness = 0.4f;       // what the hard gear imports at, and keeps
        const int JerseyTriangles = 5376;       // the build in goalie-jersey.fbx (17 torso rings, 11 sleeve rings, 7 mm shell); update with make_goalie_jersey.py
        const int ImportedJerseyTriangles = 6972; // the jersey of goalie-dressing.fbx

        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_parent); }

        static bool IsCloth(string rendererName) => rendererName == "Jersey" || rendererName == "Pants" || rendererName == "Socks";

        static float Smoothness(Material m) => m.GetFloat("_Smoothness");

        static SkinnedMeshRenderer JerseyOf(GoalieCharacter character)
        {
            foreach (var r in character.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name == "Jersey") return r;
            return null;
        }

        static SkinnedMeshRenderer JerseyOf(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name == "Jersey") return r;
            return null;
        }

        [Test]
        public void Jersey_pants_and_socks_are_matte()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            int seen = 0;
            foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsCloth(r.name)) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    seen++;
                    Assert.LessOrEqual(Smoothness(m), ClothSmoothnessLimit, r.name + " / " + m.name + " smoothness");
                }
            }
            Assert.AreEqual(4, seen, "jersey (1), pants (1) and socks (2) materials");
        }

        [Test]
        public void Cloth_has_a_mipmapped_surface_and_goalies_share_it()
        {
            var first = new GoalieCharacter(_parent.transform);
            var second = new GoalieCharacter(_parent.transform);
            Assume.That(first.Present && second.Present, "the goalie model loads");
            var other = new Dictionary<string, Renderer>();
            foreach (Renderer r in second.Root.GetComponentsInChildren<Renderer>(true))
                if (IsCloth(r.name)) other.Add(r.name, r);
            foreach (Renderer r in first.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != "Jersey") continue;
                for (int i = 0; i < r.sharedMaterials.Length; i++)
                {
                    var texture = r.sharedMaterials[i].mainTexture as Texture2D;
                    Assert.IsNotNull(texture, r.name + " cloth surface");
                    Assert.Greater(texture.mipmapCount, 1, "cloth must filter at shot distance");
                    Assert.AreSame(r.sharedMaterials[i], other[r.name].sharedMaterials[i], "reuse the cloth material");
                }
            }
        }

        [Test]
        public void Cloth_print_does_not_cross_the_texture_wrap_seam()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            foreach (var renderer in character.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "Jersey") continue;
                Mesh mesh = renderer.sharedMesh;
                Vector2[] uv = mesh.uv;
                Assert.AreEqual(mesh.vertexCount, mesh.boneWeights.Length, "every seam corner keeps its skin weights");
                int[] face = mesh.triangles;
                for (int i = 0; i < face.Length; i += 3)
                {
                    float a = uv[face[i]].x, b = uv[face[i + 1]].x, c = uv[face[i + 2]].x;
                    // Repeated-tile addition can round a half-tile span by one float step.
                    // Keep a one-millionth-tile allowance; a real wrap still fails.
                    Assert.LessOrEqual(Mathf.Max(a, Mathf.Max(b, c)) - Mathf.Min(a, Mathf.Min(b, c)), 0.5f + 0.000001f,
                        renderer.name + " triangle " + i / 3 + " must not stretch the print across a side seam");
                }
            }
        }

        [Test]
        public void Pants_and_socks_keep_the_imported_uvs_colour_and_texture()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            var model = Resources.Load<GameObject>("Models/goalie-dressing");
            var originals = new Dictionary<string, SkinnedMeshRenderer>();
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name == "Pants" || renderer.name == "Socks") originals.Add(renderer.name, renderer);
            Assert.AreEqual(2, originals.Count, "pants and socks source meshes");
            foreach (var renderer in character.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!originals.TryGetValue(renderer.name, out var original)) continue;
                CollectionAssert.AreEqual(original.sharedMesh.uv, renderer.sharedMesh.uv, renderer.name + " original UVs");
                for (int i = 0; i < original.sharedMaterials.Length; i++)
                {
                    Material before = original.sharedMaterials[i], after = renderer.sharedMaterials[i];
                    Assert.AreEqual(before.color, after.color, renderer.name + " original colour");
                    Assert.AreSame(before.mainTexture, after.mainTexture, renderer.name + " original texture");
                    Assert.AreEqual(before.mainTextureScale, after.mainTextureScale);
                    Assert.AreEqual(before.mainTextureOffset, after.mainTextureOffset);
                }
            }
        }

        [Test]
        public void The_imported_model_materials_are_left_as_imported()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            var model = Resources.Load<GameObject>("Models/goalie-dressing");
            int seen = 0;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsCloth(r.name)) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    seen++;
                    Assert.AreEqual(GearSmoothness, Smoothness(m), 0.001f, r.name + " / " + m.name + " (the imported asset)");
                }
            }
            Assert.AreEqual(5, seen, "imported cloth materials");
        }

        [Test]
        public void The_hard_gear_and_skin_keep_their_look_and_share_no_material_with_the_cloth()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            var cloth = new HashSet<Material>();
            foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
                if (IsCloth(r.name)) cloth.UnionWith(r.sharedMaterials);
            int seen = 0;
            foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (IsCloth(r.name)) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    seen++;
                    Assert.IsFalse(cloth.Contains(m), r.name + " / " + m.name + " is shared with the cloth");
                    if (m.name == "Visor") continue; // glossy on purpose
                    Assert.AreEqual(GearSmoothness, Smoothness(m), 0.001f, r.name + " / " + m.name + " smoothness");
                }
            }
            Assert.Greater(seen, 10, "gear materials seen");
        }

        // ---- the goalie-cut jersey model (#220) -------------------------------------------------------------

        [Test]
        public void The_jersey_is_the_separate_goalie_jersey_model_on_the_same_rig()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            var jersey = JerseyOf(character);
            Assert.IsNotNull(jersey, "the Jersey renderer");
            var asset = Resources.Load<GameObject>("Models/goalie-jersey");
            Assert.IsNotNull(asset, "Models/goalie-jersey");
            var source = JerseyOf(asset) ?? asset.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(source, "the jersey model has a skinned mesh");
            Assert.AreSame(source.sharedMesh, jersey.sharedMesh, "the Jersey renderer uses the goalie-jersey mesh");
            Assert.AreEqual(source.bones.Length, jersey.bones.Length, "one bone per bone of the jersey model");
            for (int i = 0; i < jersey.bones.Length; i++)
            {
                Assert.IsNotNull(jersey.bones[i], "bone " + source.bones[i].name + " is found on the goalie");
                Assert.AreEqual(source.bones[i].name, jersey.bones[i].name, "bone " + i + " by name");
                Assert.IsTrue(jersey.bones[i].IsChildOf(character.Root.transform), jersey.bones[i].name + " belongs to this goalie");
            }
        }

        [Test]
        public void The_jersey_bindposes_match_the_imported_goalie_rig()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            var jersey = JerseyOf(character);
            var old = JerseyOf(Resources.Load<GameObject>("Models/goalie-dressing"));
            Assert.IsNotNull(old, "the imported Jersey renderer");
            var oldPose = new Dictionary<string, Matrix4x4>();
            Matrix4x4[] oldPoses = old.sharedMesh.bindposes;
            for (int i = 0; i < old.bones.Length; i++) oldPose[old.bones[i].name] = oldPoses[i];
            Matrix4x4[] poses = jersey.sharedMesh.bindposes;
            Assert.AreEqual(jersey.bones.Length, poses.Length, "a bindpose per bone");
            for (int i = 0; i < jersey.bones.Length; i++)
            {
                Assert.IsTrue(oldPose.TryGetValue(jersey.bones[i].name, out Matrix4x4 expected), jersey.bones[i].name + " exists in the imported rig");
                for (int k = 0; k < 16; k++)
                    Assert.AreEqual(expected[k], poses[i][k], 1e-4f, jersey.bones[i].name + " bindpose element " + k + " (the skin must bind to the same rig)");
            }
        }

        [Test]
        public void The_jersey_has_fewer_triangles_than_the_imported_one_and_adds_no_renderer()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            var jersey = JerseyOf(character);
            int triangles = jersey.sharedMesh.triangles.Length / 3;
            Assert.AreEqual(JerseyTriangles, triangles, "jersey triangles");
            Assert.Less(triangles, ImportedJerseyTriangles, "no more triangles than the jersey it replaces");
            Assert.AreEqual(1, jersey.sharedMaterials.Length, "one material, one draw");
            int jerseys = 0;
            foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Jersey")) jerseys++;
            Assert.AreEqual(1, jerseys, "one jersey renderer (no extra pass)");
        }

        [Test]
        public void The_print_edges_lie_on_mesh_edge_loops_so_they_draw_straight()
        {
            // A print edge across a quad draws a saw-tooth where the rings change shape fast (the yoke at the shoulder, #220).
            // Every horizontal edge of the print is a ring of the mesh: a ring has a whole loop of vertices at the same V.
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            Vector2[] uv = JerseyOf(character).sharedMesh.uv;
            foreach (float z in new[] { 0.940f, 0.975f, 1.005f, 1.040f, 1.578f })   // torso: V = (z - 0.90) / 0.70 * 0.4375
                Assert.GreaterOrEqual(CountAtV(uv, (z - 0.90f) / 0.70f * 0.4375f), 48, "torso ring at z " + z + " (hem stripes, yoke)");
            foreach (float d in new[] { 0.235f, 0.270f, 0.296f, 0.331f })    // sleeves: V = 0.5 + d / 0.535 * 0.34
                Assert.GreaterOrEqual(CountAtV(uv, 0.5f + d / 0.535f * 0.34f), 48, "sleeve rings at d " + d + " (elbow stripes)");
        }

        static int CountAtV(Vector2[] uv, float v)
        {
            int n = 0;
            foreach (Vector2 p in uv) if (Mathf.Abs(p.y - v) < 1e-4f) n++;
            return n;
        }

        // ---- Play mode keeps statics but destroys runtime objects (#220: the magenta jersey) -----------------

        [Test]
        public void A_cloth_material_that_Unity_destroyed_is_rebuilt_not_reused()
        {
            // Enter Play Mode Options without a domain reload: the static cloth cache outlives Play, but Unity destroys the
            // runtime-made materials. A dead material draws magenta. The next goalie must get a live one.
            var first = new GoalieCharacter(_parent.transform);
            Assume.That(first.Present, "the goalie model loads");
            var dead = new List<Material>();
            foreach (Renderer r in first.Root.GetComponentsInChildren<Renderer>(true))
                if (IsCloth(r.name)) foreach (Material m in r.sharedMaterials) if (!dead.Contains(m)) dead.Add(m);
            Assert.Greater(dead.Count, 0, "cloth materials made");
            foreach (Material m in dead) Object.DestroyImmediate(m);

            var second = new GoalieCharacter(_parent.transform);
            int seen = 0;
            foreach (Renderer r in second.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsCloth(r.name)) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    seen++;
                    Assert.IsTrue(m != null, r.name + " got a destroyed material from the cache");
                    Assert.IsNotNull(m.shader, r.name + " material shader");
                }
            }
            Assert.AreEqual(4, seen, "cloth materials on the second goalie");
            Assert.IsTrue(JerseyOf(second).sharedMaterials[0].mainTexture != null, "the rebuilt jersey material has its print");
        }
    }
}
