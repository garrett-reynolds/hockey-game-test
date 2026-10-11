using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class GoalPuckTests
    {
        const float Frame = 1f / 60f;
        const float HalfWidth = 0.915f;  // the Net Face, metres (independent of the code)
        const float Depth = 0.6f;        // the back of the net at the ice

        GameObject _rink, _camera;

        [SetUp]
        public void SetUp()
        {
            _rink = new GameObject("Rink (test)");
            _camera = new GameObject("Camera (test)", typeof(Camera));
            _camera.transform.position = new Vector3(0f, 1.5f, -9f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rink);
            Object.DestroyImmediate(_camera);
        }

        [TestCase(50, 90)]
        [TestCase(60, 80)]
        public void A_goal_puck_stops_on_the_netting_never_past_it_and_drops_to_the_ice_inside_the_net(double x, double y)
        {
            NetBuilder.Build(_rink.transform);
            var effects = new ShotEffects(_rink.transform, _camera.GetComponent<Camera>());
            Transform netting = NetBuilder.FindNetting(_rink.transform);
            Transform puck = _rink.transform.Find("Puck");
            var round = new Round(new ScriptedRandom(ScriptedRandom.AlternatingButterflies()), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            round.Tick(3.7);
            round.SubmitShot(x, y, 100, false);
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind);

            float deepest = float.NegativeInfinity;
            for (float t = 0f; t < 2f; t += Frame)
            {
                effects.Update(round, Frame);
                round.Tick(Frame);
                Vector3 p = puck.position;
                deepest = Mathf.Max(deepest, p.z);
                float back = NettingBackAround(netting, p);
                if (!float.IsNegativeInfinity(back))
                    Assert.LessOrEqual(p.z, back + 0.01f, "the puck is never behind the netting in its column, at t = " + t);
            }

            Vector3 rest = puck.position;
            Assert.Greater(deepest, 0.25f, "the puck went well into the net");
            Assert.Less(rest.y, 0.1f, "the puck ends on the ice");
            Assert.Greater(rest.z, 0.05f, "inside the net");
            Assert.Less(rest.z, Depth, "inside the net");
            Assert.Less(Mathf.Abs(rest.x), HalfWidth, "inside the net");
        }

        /// <summary>The deepest netting (as it is now, bulge and all) within 3 cm across and up of p; -infinity if none.</summary>
        static float NettingBackAround(Transform netting, Vector3 p)
        {
            Vector3[] v = netting.GetComponent<MeshFilter>().sharedMesh.vertices;
            float back = float.NegativeInfinity;
            foreach (Vector3 local in v)
            {
                Vector3 w = netting.TransformPoint(local);
                if (Mathf.Abs(w.x - p.x) <= 0.03f && Mathf.Abs(w.y - p.y) <= 0.03f) back = Mathf.Max(back, w.z);
            }
            return back;
        }
    }
}
