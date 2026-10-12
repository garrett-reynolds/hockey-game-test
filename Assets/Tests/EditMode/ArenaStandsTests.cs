using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame.Core.Tests
{
    public class ArenaStandsTests
    {
        GameObject _rink;

        [SetUp]
        public void MakeRink() => _rink = new GameObject("Rink");

        [TearDown]
        public void RemoveRink() => Object.DestroyImmediate(_rink);

        static Bounds WorldBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        /// <summary>A stand-in arena file: a 10 m stands block, a roof slab above it and an LED ribbon board.</summary>
        static GameObject FakeArenaFile()
        {
            var model = new GameObject("arena");
            void Part(string name, Vector3 at, Vector3 size)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = name;
                part.transform.SetParent(model.transform);
                part.transform.localPosition = at;
                part.transform.localScale = size;
            }
            Part("Stands", new Vector3(0f, 5f, 0f), new Vector3(10f, 10f, 10f));
            Part("Ceiling", new Vector3(0f, 11f, 0f), new Vector3(10f, 2f, 10f));
            Part("Ribbon", new Vector3(0f, 6f, -4f), new Vector3(10f, 1f, 1f));
            return model;
        }

        [Test]
        public void With_no_arena_model_the_dark_backdrop_stays_as_it_was()
        {
            GameObject arena = ArenaStands.Build(_rink.transform, name => null);
            Assert.AreEqual("Arena", arena.name);
            Assert.AreEqual(_rink.transform, arena.transform.parent);
            Assert.IsEmpty(arena.GetComponentsInChildren<Renderer>());
        }

        [Test]
        public void The_arena_model_is_fitted_around_the_boards_from_the_ice_up_to_the_roof()
        {
            string asked = null;
            GameObject arena = ArenaStands.Build(_rink.transform, name => { asked = name; return FakeArenaFile(); });
            Assert.AreEqual(ModelFiles.Arena, asked);

            Bounds b = WorldBounds(arena);
            Bounds boards = RinkBoards.Box;
            Assert.AreEqual(0f, b.min.y, 0.01f, "on the ice");
            Assert.AreEqual(ArenaStands.RoofHeight, b.max.y, 0.01f);
            Assert.That(b.min.x, Is.LessThan(boards.min.x - 20f), "stands rise beside the rink");
            Assert.That(b.max.x, Is.GreaterThan(boards.max.x + 20f));
            Assert.That(b.max.z, Is.GreaterThan(boards.max.z + 20f), "stands rise behind the net");
            Assert.AreEqual(b.max.x - boards.max.x, b.max.z - boards.max.z, 0.01f, "stands reach as far behind the net as beside the rink");
            Assert.AreEqual(-b.min.x, b.max.x, 0.01f, "centred across the rink");
        }

        [Test]
        public void The_arena_casts_no_shadows_on_the_ice_and_its_ribbon_board_glows()
        {
            GameObject arena = ArenaStands.Build(_rink.transform, name => FakeArenaFile());
            foreach (Renderer r in arena.GetComponentsInChildren<Renderer>())
            {
                Assert.AreEqual(ShadowCastingMode.Off, r.shadowCastingMode, r.name + " would shade the ice under the roof");
                bool glows = r.sharedMaterial.IsKeywordEnabled("_EMISSION");
                Assert.AreEqual(r.name == ArenaStands.GlowPart, glows, r.name);
            }
        }

        [Test]
        public void The_boards_and_arena_models_are_URP_Lit()
        {
            GameObject boards = RinkBoards.Build(_rink.transform);
            GameObject arena = ArenaStands.Build(_rink.transform);
            Assert.IsNotEmpty(boards.GetComponentsInChildren<Renderer>(), "boards.obj is in Assets/Resources/Models");
            Assert.IsNotEmpty(arena.GetComponentsInChildren<Renderer>(), "arena.obj is in Assets/Resources/Models");
            foreach (Renderer r in _rink.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.sharedMaterials)
                    Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name, r.name + " / " + m.name);
        }
        [Test]
        public void The_stands_are_dimmer_than_the_white_ice_so_the_lit_rink_pops()
        {
            GameObject arena = ArenaStands.Build(_rink.transform);
            foreach (Renderer r in arena.GetComponentsInChildren<Renderer>())
                if (r.name != ArenaStands.GlowPart)
                    Assert.That(r.sharedMaterial.color.maxColorComponent, Is.LessThanOrEqualTo(0.5f), r.name);
        }
    }
}
