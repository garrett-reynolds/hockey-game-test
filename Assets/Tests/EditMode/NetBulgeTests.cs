using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class NetBulgeTests
    {
        const float Depth = 0.6f;     // the back of the net, metres behind the Net Face (independent of the code)
        const float Frame = 0.02f;

        Transform _rink;

        [SetUp]
        public void SetUp() => _rink = new GameObject("Test Rink").transform;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rink.gameObject);

        /// <summary>Where a goal at this Net Face point (0-100 across, 0-100 up) reaches the netting at the back of the net.</summary>
        static Vector3 BackOfNet(NetBulge bulge, double x, double y)
        {
            NetFaceMapping.ToMetres(x, y, out double right, out double up);
            return new Vector3((float)right, (float)up, bulge.BackOfNet((float)right, (float)up));
        }

        static void Run(NetBulge bulge, float seconds)
        {
            for (float t = 0f; t < seconds - 1e-6f; t += Frame) bulge.Step(Frame);
        }

        static Vector3[] WorldVertices(Transform netting)
        {
            Vector3[] v = netting.GetComponent<MeshFilter>().sharedMesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = netting.TransformPoint(v[i]);
            return v;
        }

        [Test]
        public void A_goal_bulges_the_netting_most_at_the_hit_point_and_it_settles_back_to_rest()
        {
            NetBuilder.Build(_rink);
            Transform netting = NetBuilder.FindNetting(_rink);
            var bulge = new NetBulge(netting);
            Vector3[] rest = WorldVertices(netting);
            Vector3 hit = BackOfNet(bulge, 30, 60);

            bulge.Hit(hit, 100f);
            Run(bulge, 0.1f);
            Vector3[] moved = WorldVertices(netting);

            int nearest = 0;
            for (int i = 1; i < rest.Length; i++)
                if ((rest[i] - hit).sqrMagnitude < (rest[nearest] - hit).sqrMagnitude) nearest = i;
            float atHit = moved[nearest].z - rest[nearest].z;
            Assert.Greater(atHit, 0.03f, "the netting at the hit point should be pushed back several centimetres");
            float farthest = 0f;
            for (int i = 0; i < rest.Length; i++)
            {
                float back = moved[i].z - rest[i].z;
                Assert.LessOrEqual(back, atHit + 1e-4f, "no vertex moves back more than the one at the hit point");
                if ((rest[i] - hit).magnitude > 0.6f) farthest = Mathf.Max(farthest, Mathf.Abs(back));
            }
            Assert.Less(farthest, atHit * 0.2f, "netting far from the hit point barely moves");

            Run(bulge, 2.4f);
            Vector3[] settled = WorldVertices(netting);
            for (int i = 0; i < rest.Length; i++)
                Assert.AreEqual(0f, (settled[i] - rest[i]).magnitude, 1e-5f, "every netting vertex is back at rest by the end of the result");
            Assert.IsFalse(bulge.Moving);
        }

        [Test]
        public void The_puck_rides_the_netting_at_a_low_hit_where_the_netting_is_held_by_the_base_frame()
        {
            NetBuilder.Build(_rink);
            Transform netting = NetBuilder.FindNetting(_rink);
            var bulge = new NetBulge(netting);
            Vector3[] rest = WorldVertices(netting);
            Vector3 hit = BackOfNet(bulge, 50, 4);

            bulge.Hit(hit, 110f);
            Run(bulge, 0.1f);
            Vector3[] moved = WorldVertices(netting);

            int nearest = 0;
            for (int i = 1; i < rest.Length; i++)
                if ((rest[i] - hit).sqrMagnitude < (rest[nearest] - hit).sqrMagnitude) nearest = i;
            Assert.AreEqual(moved[nearest].z - rest[nearest].z, bulge.BackAtHit, 0.003f);
        }

        [Test]
        public void A_goal_anywhere_on_the_Net_Face_stops_on_the_netting_not_past_it()
        {
            NetBuilder.Build(_rink);
            Transform netting = NetBuilder.FindNetting(_rink);
            var bulge = new NetBulge(netting);
            Vector3[] rest = WorldVertices(netting);

            for (int x = 5; x <= 95; x += 15)
                for (int y = 5; y <= 95; y += 15)
                {
                    Vector3 stop = BackOfNet(bulge, x, y);
                    string at = " at " + x + "," + y + " (stop z " + stop.z + ")";
                    Assert.LessOrEqual(stop.z, Depth + 0.003f, "inside the net depth" + at);
                    Assert.Greater(stop.z, 0.2f, "well behind the Net Face" + at);
                    bool touches = false;
                    for (int i = 0; i < rest.Length; i++)
                    {
                        Vector3 v = rest[i];
                        if (Mathf.Abs(v.x - stop.x) > 0.03f || Mathf.Abs(v.y - stop.y) > 0.03f) continue;
                        Assert.LessOrEqual(v.z, stop.z + 0.02f, "no netting behind the stop point in that column" + at);
                        if (Mathf.Abs(v.z - stop.z) < 0.04f) touches = true;
                    }
                    Assert.IsTrue(touches, "the stop point is on the netting" + at);
                }
            Assert.Less(BackOfNet(bulge, 50, 95).z, BackOfNet(bulge, 50, 5).z - 0.1f, "the back slopes: a high goal stops nearer the Net Face");
        }

        [Test]
        public void With_the_box_net_placeholder_a_goal_stops_at_the_net_depth()
        {
            NetBuilder.Build(_rink, name => null);
            var bulge = new NetBulge(NetBuilder.FindNetting(_rink));

            Assert.AreEqual(Depth, bulge.BackOfNet(0.3f, 0.9f), 1e-4f);
        }

        [Test]
        public void A_faster_Shot_makes_a_bigger_bulge()
        {
            NetBuilder.Build(_rink);
            Transform netting = NetBuilder.FindNetting(_rink);
            var bulge = new NetBulge(netting);
            Vector3[] rest = WorldVertices(netting);
            Vector3 hit = BackOfNet(bulge, 50, 50);

            float Peak(float speedKmh)
            {
                bulge.Reset();
                bulge.Hit(hit, speedKmh);
                Run(bulge, 0.1f);
                Vector3[] moved = WorldVertices(netting);
                float most = 0f;
                for (int i = 0; i < rest.Length; i++) most = Mathf.Max(most, moved[i].z - rest[i].z);
                return most;
            }

            float slow = Peak(50f), fast = Peak(110f);
            Assert.Greater(fast, slow * 1.5f);
        }

        [Test]
        public void With_the_box_net_placeholder_the_whole_netting_shakes_and_comes_back()
        {
            NetBuilder.Build(_rink, name => null);
            Transform netting = NetBuilder.FindNetting(_rink);
            Vector3 rest = netting.localPosition;
            var bulge = new NetBulge(netting);

            bulge.Hit(BackOfNet(bulge, 50, 50), 100f);
            Run(bulge, 0.1f);
            Assert.Greater((netting.localPosition - rest).magnitude, 0.001f, "the box netting shakes");

            Run(bulge, 2.4f);
            Assert.AreEqual(rest, netting.localPosition);
            Assert.IsFalse(bulge.Moving);
        }
    }
}
