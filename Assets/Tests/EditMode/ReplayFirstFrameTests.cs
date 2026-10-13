using System.IO;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The Replay's first frame (#132): in the built rink, a slow Shot's Replay opens on the release (the puck where the Shot
    /// was shot), not on the last stretch of its flight. Also draws that frame on the Video Board for the look check.
    /// </summary>
    public class ReplayFirstFrameTests
    {
        const float Step = PhysicsShot.StepSeconds;

        static string Notes => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_notes"));

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_slow_Shot_s_Replay_opens_on_the_release_with_the_puck_where_it_was_shot()
        {
            SimulationMode oldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            Transform arena = RinkBuilder.Build(out Camera camera);
            GameObject rink = GameObject.Find("Rink");
            GoalieView goalie = null;
            PhysicsShotEffects effects = null;
            RenderTexture target = null;
            try
            {
                goalie = new GoalieView(rink.transform, new GoalieCharacter(rink.transform));
                goalie.Show(true);
                effects = new PhysicsShotEffects(rink.transform, camera, new SeededRandom(7), goalie);
                var flow = new ScreenFlow(ScriptedRandom.Forcing(AttackSide.Middle), new SaveData(), FakeReach.NeverReaches());
                Round round = flow.Round;
                var boards = rink.GetComponentInChildren<VideoBoards>();
                var host = new GameObject("Replay (test)");
                host.transform.SetParent(rink.transform, false);
                var replay = host.AddComponent<ReplayView>();
                replay.Init(round, boards, camera, goalie.Body);
                replay.ReplayPhysicsPuck(() => effects.Shot?.Puck);

                flow.Press();
                flow.Tick(3.7 + 1.0);
                Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
                Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, 0f, 0f);
                Physics.SyncTransforms();

                round.SubmitShot(50, 50, 30, false); // a slow Shot: 30 km/h
                effects.Update(round, 0f);
                Vector3 release = effects.Shot.Puck.position;

                bool first = false;
                for (int f = 0; f < 12.0 / Step && round.Phase == RoundPhase.ResultHold && !first; f++)
                {
                    replay.RestoreLive();
                    Physics.Simulate(Step);
                    effects.AfterPhysicsStep(Step);
                    round.Tick(Step);
                    effects.Update(round, Step);
                    replay.Show(ArenaShow.From(flow));
                    replay.Step();
                    first = replay.Showing;
                }
                Assert.IsTrue(first, "the Replay played");
                Assert.Greater(round.ResultDelaySeconds, 1.0, "a slow Shot flies longer than a second");
                Assert.LessOrEqual(round.ResultDelaySeconds, ReplayTiming.ShowSeconds - ReplayTiming.TailSeconds,
                    "this Shot's flight must stay within the release-start limit (ShowSeconds - TailSeconds); a longer one starts its Replay after the release (the rate cap), so tune the Shot's speed here");
                Vector3 shown = replay.ReplayPuck.transform.position;

                // The Replay picture on the Video Board, seen from 12 m in front of the first screen.
                replay.ReplayCamera.Render();
                Vector3 spot = boards.CentreOf(0) + boards.FacingOf(0) * 12f;
                camera.transform.position = spot;
                camera.transform.LookAt(boards.CentreOf(0));
                camera.fieldOfView = 60f;
                camera.aspect = 1920f / 1080f;
                Save(replay.Picture, "t132-first-frame-picture.png");
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                camera.targetTexture = target;
                camera.Render();
                Save(target, "t132-first-frame-board.png");
                // The first recorded moment is the Shot's first frame, one physics step after the launch: the puck has flown 30 km/h x Step (0.07 m).
                float oneStep = 30f / 3.6f * Step;
                Assert.Less(Vector3.Distance(shown, release), oneStep * 1.5f, "the Replay's first frame has the puck at the release " + release + ", not " + shown);
            }
            finally
            {
                camera.targetTexture = null;
                if (target != null) Object.DestroyImmediate(target);
                effects?.Dispose();
                goalie?.Dispose();
                Object.DestroyImmediate(rink);
                Object.DestroyImmediate(camera.gameObject);
                Physics.simulationMode = oldMode;
            }
        }

        static void Save(RenderTexture source, string name)
        {
            if (!Directory.Exists(Notes)) return; // only the dev checkout has a _notes folder
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(Notes, name), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
