using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame.Core.Tests
{
    public class RinkBoardsTests
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

        /// <summary>A stand-in model file: a 2 x 3 x 4 box (white boards) with a glass pane on top, off-centre in its file.</summary>
        static GameObject FakeBoardsFile()
        {
            var model = new GameObject("boards");
            var boards = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boards.name = "Boards";
            boards.transform.SetParent(model.transform);
            boards.transform.localPosition = new Vector3(5f, 1f, 2f);
            boards.transform.localScale = new Vector3(2f, 2f, 4f);
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "Glass";
            glass.transform.SetParent(model.transform);
            glass.transform.localPosition = new Vector3(5f, 2.5f, 2f);
            glass.transform.localScale = new Vector3(2f, 1f, 4f);
            return model;
        }

        [Test]
        public void With_no_boards_model_the_rink_keeps_todays_open_look()
        {
            GameObject boards = RinkBoards.Build(_rink.transform, name => null);
            Assert.AreEqual("Boards", boards.name);
            Assert.AreEqual(_rink.transform, boards.transform.parent);
            Assert.IsEmpty(boards.GetComponentsInChildren<Renderer>(), "nothing new to see");
        }

        [Test]
        public void The_boards_model_is_fitted_around_the_rink_from_behind_the_net_to_behind_the_skate_in()
        {
            string asked = null;
            GameObject boards = RinkBoards.Build(_rink.transform, name => { asked = name; return FakeBoardsFile(); });
            Assert.AreEqual(ModelFiles.Boards, asked);

            Bounds b = WorldBounds(boards);
            // NHL rink: 85 ft wide, end boards 11 ft behind the goal line (the Net Face plane), boards 0.2 m thick.
            Assert.AreEqual(-13.15f, b.min.x, 0.01f);
            Assert.AreEqual(13.15f, b.max.x, 0.01f);
            Assert.AreEqual(3.55f, b.max.z, 0.01f, "end boards 3.35 m behind the Net Face");
            Assert.AreEqual(0f, b.min.y, 0.01f, "standing on the ice");
            Assert.AreEqual(RinkBoards.GlassTop, b.max.y, 0.01f);
            Assert.That(b.min.z, Is.LessThan(-22.5f), "the rink runs on behind the furthest skate-in start");
            Assert.That(b.min.z, Is.GreaterThanOrEqualTo(-RinkIce.Size / 2f), "the rink stays on the ice sheet");
        }

        [Test]
        public void The_glass_is_see_through_and_casts_no_shadow_while_the_boards_stay_solid()
        {
            GameObject boards = RinkBoards.Build(_rink.transform, name => FakeBoardsFile());
            Renderer glass = null, solid = null;
            foreach (Renderer r in boards.GetComponentsInChildren<Renderer>())
                if (r.name == "Glass") glass = r; else solid = r;

            Material g = glass.sharedMaterial;
            Assert.AreEqual(1f, g.GetFloat("_Surface"), "transparent surface");
            Assert.That(g.renderQueue, Is.GreaterThanOrEqualTo((int)RenderQueue.Transparent));
            Assert.That(g.color.a, Is.LessThan(0.4f), "mostly clear");
            Assert.AreEqual(ShadowCastingMode.Off, glass.shadowCastingMode);

            Assert.That(solid.sharedMaterial.renderQueue, Is.LessThan((int)RenderQueue.Transparent));
            Assert.AreEqual(ShadowCastingMode.On, solid.shadowCastingMode, "the boards still shade the ice");
        }
    }
}
