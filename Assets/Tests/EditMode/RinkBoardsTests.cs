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
            // NHL rink: 200 x 85 ft, end boards 11 ft behind each goal line (the near one is the Net Face plane), boards 0.2 m thick.
            Assert.AreEqual(-13.155f, b.min.x, 0.01f);
            Assert.AreEqual(13.155f, b.max.x, 0.01f);
            Assert.AreEqual(3.55f, b.max.z, 0.01f, "end boards 3.35 m behind the Net Face");
            Assert.AreEqual(-57.81f, b.min.z, 0.01f, "far end boards 3.35 m behind the far goal line");
            Assert.AreEqual(0f, b.min.y, 0.01f, "standing on the ice");
            Assert.AreEqual(RinkBoards.GlassTop, b.max.y, 0.01f);
            Assert.AreEqual(3.51f, RinkBoards.GlassTop, 0.01f, "8 ft of glass on 42 in boards at the ends");
            Assert.That(b.min.z, Is.GreaterThanOrEqualTo(RinkIce.CentreZ - RinkIce.Size / 2f), "the rink stays on the ice sheet");
            Assert.That(b.max.z, Is.LessThanOrEqualTo(RinkIce.CentreZ + RinkIce.Size / 2f), "the rink stays on the ice sheet");
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

        [Test]
        public void A_missed_puck_that_reaches_the_end_boards_bounces_back_off_them_slower()
        {
            var position = new Vector3(1f, 1.5f, RinkBoards.EndZ + 0.2f);
            var velocity = new Vector3(0.5f, 1f, 20f);
            RinkBoards.BounceOffEndBoards(ref position, ref velocity, 0.04f);
            Assert.AreEqual(RinkBoards.EndZ - 0.04f, position.z, 0.001f, "back on the rink side of the boards");
            Assert.That(velocity.z, Is.LessThan(0f).And.GreaterThan(-10f), "heading back toward the shooter, slower");
            Assert.AreEqual(0.5f, velocity.x, 1e-5f);
            Assert.AreEqual(1f, position.x, 1e-5f);

            var inside = new Vector3(0f, 0.5f, 2f);
            var going = new Vector3(0f, 0f, 20f);
            RinkBoards.BounceOffEndBoards(ref inside, ref going, 0.04f);
            Assert.AreEqual(new Vector3(0f, 0.5f, 2f), inside, "a puck short of the boards flies on");
            Assert.AreEqual(new Vector3(0f, 0f, 20f), going);
        }

        [Test]
        public void The_rink_has_boards_only_when_the_boards_model_was_found()
        {
            RinkBoards.Build(_rink.transform, name => null);
            Assert.IsFalse(RinkBoards.Present(_rink.transform));
            Object.DestroyImmediate(_rink);
            _rink = new GameObject("Rink");
            RinkBoards.Build(_rink.transform, name => FakeBoardsFile());
            Assert.IsTrue(RinkBoards.Present(_rink.transform));
        }
    }
}
