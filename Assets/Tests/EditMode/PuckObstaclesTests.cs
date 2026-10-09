using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>What the puck can hit after a contact (PuckObstacles), and PuckMotion moving among them.</summary>
    public class PuckObstaclesTests
    {
        /// <summary>A square of side 2 m centred on centre, facing `facing`.</summary>
        static void AddSquare(PuckObstacles obstacles, Vector3 centre, Vector3 facing, PuckSurface surface, string piece = null)
        {
            Quaternion q = Quaternion.LookRotation(-facing);
            Vector3 a = centre + q * new Vector3(-1f, -1f, 0f), b = centre + q * new Vector3(1f, -1f, 0f);
            Vector3 c = centre + q * new Vector3(1f, 1f, 0f), d = centre + q * new Vector3(-1f, 1f, 0f);
            obstacles.AddMesh(new[] { a, b, c, d }, new[] { 0, 1, 2, 0, 2, 3 }, surface, piece);
        }

        [Test]
        public void A_puck_flying_into_a_wall_bounces_off_it_and_never_goes_through()
        {
            var obstacles = new PuckObstacles();
            AddSquare(obstacles, new Vector3(0f, 1f, 0f), Vector3.back, PuckSurface.Pad);
            var motion = new PuckMotion(new Vector3(0.2f, 1.2f, -1f), Quaternion.identity, new Vector3(1f, 1f, 9f), 0f, obstacles);
            float deepest = float.NegativeInfinity;
            for (int i = 0; i < 60; i++)
            {
                motion.Advance(1f / 60f);
                deepest = Mathf.Max(deepest, motion.Position.z);
            }
            Assert.LessOrEqual(deepest, -0.02f, "the centre stays in front of the wall");
            Assert.Less(motion.Velocity.z, 0f, "came back off it");
        }

        [Test]
        public void The_surface_normal_near_a_point_follows_the_real_surface()
        {
            var obstacles = new PuckObstacles();
            Vector3 tilted = new Vector3(0.5f, 0f, -1f).normalized;
            AddSquare(obstacles, Vector3.zero, tilted, PuckSurface.Blocker, "Blocker");

            Assert.IsTrue(obstacles.Nearest(tilted * 0.1f, "Blocker", out Vector3 n, out PuckSurface s));
            Assert.AreEqual(0f, Vector3.Angle(tilted, n), 0.5f, "in front of the face: the face's normal");
            Assert.AreEqual(PuckSurface.Blocker, s);

            Vector3 offSide = Quaternion.LookRotation(-tilted) * new Vector3(1.3f, 0f, 0f) + tilted * 0.05f;
            Assert.IsTrue(obstacles.Nearest(offSide, "Blocker", out Vector3 side, out _));
            Assert.Greater(Vector3.Dot(side, Quaternion.LookRotation(-tilted) * Vector3.right), 0.9f, "off the edge: out from the edge");

            Assert.IsFalse(obstacles.Nearest(Vector3.zero, "Catcher", out _, out _), "only the named piece");
        }

        [Test]
        public void A_round_post_faces_out_from_its_axis_where_the_puck_meets_it()
        {
            var obstacles = new PuckObstacles();
            obstacles.AddTube(new Vector3(1f, 0f, 0f), new Vector3(1f, 1.2f, 0f), 0.04f, PuckSurface.Frame);

            Assert.IsTrue(obstacles.Nearest(new Vector3(1.03f, 0.5f, -0.06f), null, out Vector3 outer, out PuckSurface s));
            Assert.AreEqual(PuckSurface.Frame, s);
            Assert.IsTrue(obstacles.Nearest(new Vector3(0.97f, 0.5f, -0.06f), null, out Vector3 inner, out _));
            Assert.IsTrue(obstacles.Nearest(new Vector3(1f, 0.5f, -0.06f), null, out Vector3 front, out _));

            Assert.AreEqual(0f, Vector3.Angle(new Vector3(0.03f, 0f, -0.06f), outer), 0.5f);
            Assert.AreEqual(0f, Vector3.Angle(new Vector3(-0.03f, 0f, -0.06f), inner), 0.5f);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.back, front), 0.5f);
        }

        [Test]
        public void A_puck_dropping_onto_gear_lying_on_the_ice_bounces_off_it_and_never_goes_through()
        {
            var obstacles = new PuckObstacles();
            AddSquare(obstacles, new Vector3(0f, 0.1f, -1f), Vector3.up, PuckSurface.Stick); // a flat blade 10 cm up
            var motion = new PuckMotion(new Vector3(0f, 0.8f, -1f), Quaternion.identity, new Vector3(0f, 0f, -0.5f), 0f, obstacles);
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < 30; i++)
            {
                motion.Advance(1f / 60f);
                if (Mathf.Abs(motion.Position.z + 1f) < 0.95f) lowest = Mathf.Min(lowest, motion.Position.y);
            }
            Assert.GreaterOrEqual(lowest, 0.1f + 0.02f, "rests on top of the blade, not in it");
        }
    }
}
