using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>Exercises the production ReplayCloth hook through a Round, without stepping cloth or drawing a camera.</summary>
    public class ReplayClothViewTests
    {
        [Test]
        public void A_goal_replays_the_bound_net_and_returns_the_live_net_before_the_next_shot()
        {
            var rink = new GameObject("Replay cloth integration rink");
            var driver = new GameObject("Replay cloth integration driver");
            var eye = new GameObject("Replay cloth integration eye", typeof(Camera));
            var net = new NetCloth(rink.transform);
            try
            {
                var boards = VideoBoards.Build(rink.transform);
                var flow = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches());
                var replay = driver.AddComponent<ReplayView>();
                replay.Init(flow.Round, boards, eye.GetComponent<Camera>());
                replay.ReplayCloth(net);
                var live = net.Cloth.GetComponent<SkinnedMeshRenderer>();
                GameObject played = net.Cloth.transform.Find("Replay Netting").gameObject;
                flow.Press();
                flow.Tick(3.7 + 0.5);
                flow.SubmitShot(50, 90, 100, false);
                Assert.AreEqual(RoundPhase.ResultHold, flow.Round.Phase);
                replay.Step(); // first record happens at release, before the result exists
                Assert.Greater(replay.RecordedMoments, 0);
                Assert.IsFalse(played.activeSelf);
                Assert.IsFalse(live.forceRenderingOff);

                const double frame = 1.0 / 60;
                int shown = 0;
                for (int f = 0; f < 360 && flow.Round.Phase == RoundPhase.ResultHold; f++)
                {
                    replay.RestoreLive();
                    Assert.IsFalse(live.forceRenderingOff, "live net restored before each frame");
                    Assert.IsFalse(played.activeSelf);
                    flow.Tick(frame);
                    if (flow.Round.AwaitingWhistle && flow.Round.PhaseSecondsElapsed >= 0.3 - 1e-9)
                        flow.Round.WhistleNow();
                    replay.Show(ArenaShow.From(flow));
                    replay.Step();
                    Assert.AreEqual(replay.Showing, played.activeSelf, "net stand-in follows the real replay lifecycle");
                    Assert.AreEqual(replay.Showing, live.forceRenderingOff);
                    if (replay.Showing)
                    {
                        shown++;
                        var recorded = played.GetComponent<MeshFilter>().sharedMesh;
                        Assert.AreEqual(live.sharedMesh.vertexCount, recorded.vertexCount);
                        Assert.IsNull(played.GetComponent<Cloth>(), "no second cloth solver");
                        Assert.IsNull(played.GetComponent<Collider>(), "no added puck collision");
                    }
                }
                Assert.AreEqual(120, shown, 1, "the net follows the full two-second replay");
                replay.RestoreLive();
                Assert.IsFalse(played.activeSelf);
                Assert.IsFalse(live.forceRenderingOff);
                flow.Tick(3.7 + 0.5 - frame);
                flow.SubmitShot(-20, 50, 100, false);
                replay.Step(); // clears the previous goal clip before recording the next shot
                Assert.AreEqual(1, replay.RecordedMoments, "the new shot starts a fresh clip");
                Assert.IsFalse(played.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(driver);
                net.Dispose();
                RinkTeardown.Destroy(rink);
                Object.DestroyImmediate(eye);
            }
        }
    }
}
