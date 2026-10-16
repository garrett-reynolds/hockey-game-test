using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// #212 part 2: with the live Save Pads code pose under it (the real GoalieView and BreakawayView, a physics Shot, the Round's
    /// Whistle), a Save Pads clip must not pop the goalie's hips faster than the play-check limit (6 m/s). The constant-pose
    /// GoalieClipJumpTests could not reproduce the jump.
    /// </summary>
    public class GoalieSaveClipJumpTests
    {
        const float Frame = 1f / 60f;
        const float LimitMetresPerSecond = 6f;

        [Test, Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Save_Pads_clip_played_over_the_live_Pads_pose_does_not_pop_the_hips()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/AIClips")
                || UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GoalieClipReactions.ControllerPath) == null)
                Assert.Ignore("Assets/AIClips is missing (the AI clips are never committed): nothing to measure.");

            double[,] spots = { { 50, 10 }, { 20, 10 }, { 80, 10 }, { 35, 20 }, { 65, 20 }, { 10, 15 }, { 90, 15 }, { 50, 25 } };
            var worst = new Dictionary<string, float>();
            var worstAt = new Dictionary<string, string>();
            int padsRuns = 0;
            for (int seed = 1; seed <= 24; seed++)
            {
                int spot = seed % spots.GetLength(0);
                padsRuns += RunOnce(seed, spots[spot, 0], spots[spot, 1], worst, worstAt);
            }

            var problems = new List<string>();
            var report = new System.Text.StringBuilder();
            foreach (var pair in worst)
            {
                report.AppendLine($"{pair.Key}: fastest hips {pair.Value:F2} m/s ({worstAt[pair.Key]})");
                if (pair.Value > LimitMetresPerSecond) problems.Add($"{pair.Key}: hips {pair.Value:F2} m/s ({worstAt[pair.Key]}), limit {LimitMetresPerSecond}");
            }
            Debug.Log($"[SaveClipJump] {padsRuns} runs played a Save Pads clip\n" + report);
            Assert.Greater(padsRuns, 0, "no Shot played a Save Pads clip (else this test proves nothing)\n" + report);
            Assert.IsEmpty(problems, string.Join("\n", problems) + "\n" + report);
        }

        /// <summary>One Shot; returns 1 when a Save Pads clip played.</summary>
        static int RunOnce(int seed, double x, double y, Dictionary<string, float> worst, Dictionary<string, string> worstAt)
        {
            var oldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            var parent = new GameObject("Rink (test)");
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            PhysicsShotEffects effects = null;
            GoalieView view = null;
            int played = 0;
            try
            {
                NetBuilder.Build(parent.transform);
                var character = new GoalieCharacter(parent.transform);
                Assume.That(character.Present, "needs the goalie model");
                view = new GoalieView(parent.transform, character);
                Assume.That(view.Clips != null, "needs the clip player");
                view.Clips.UseRandom(new SeededRandom(seed));
                effects = new PhysicsShotEffects(parent.transform, cameraObject.GetComponent<Camera>(), new SeededRandom(7), view, name => null);
                Physics.SyncTransforms();
                var round = new Round(ScriptedRandom.Forcing(AttackSide.Middle), new RoundSettings(), FakeReach.Saving((sx, sy) => true), 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, cameraObject.GetComponent<Camera>(), view, () => effects.FlightSeconds);
                Transform hips = character.Animator.GetBoneTransform(HumanBodyBones.Hips);
                round.StartRound();
                for (int i = 0; i < 400 && round.Phase != RoundPhase.ShootingWindow; i++)
                {
                    round.Tick(Frame);
                    view.Show(true);
                    effects.Update(round, Frame);
                    view.Clips.Pre();
                    breakaway.Step(Frame, Frame);
                    view.Clips.Post(round, true, Frame);
                }
                Assume.That(round.Phase == RoundPhase.ShootingWindow, "the Round reached the shooting window");
                round.SubmitShot(x, y, 100, false);
                effects.Update(round, 0f);
                bool have = false;
                Vector3 last = Vector3.zero;
                string clipName = null;
                for (int frame = 0; frame < 900; frame++)
                {
                    for (int step = 0; step < 2; step++) { Physics.Simulate(PhysicsShot.StepSeconds); effects.AfterPhysicsStep(PhysicsShot.StepSeconds); }
                    round.Tick(Frame);
                    view.Show(true);
                    effects.Update(round, Frame);
                    view.Clips.Pre();
                    breakaway.Step(Frame, Frame);
                    view.Clips.Post(round, true, Frame);
                    if (!view.Clips.Active || view.Clips.LastKind != "Save Pads") { have = false; continue; }
                    played = 1;
                    clipName = view.Clips.LastClipName;
                    Vector3 p = hips.position;
                    if (have)
                    {
                        float speed = Vector3.Distance(p, last) / Frame;
                        if (!worst.TryGetValue(clipName, out float old) || speed > old)
                        {
                            worst[clipName] = speed;
                            worstAt[clipName] = $"seed {seed} frame {frame}, hips y {last.y:F3} -> {p.y:F3}";
                        }
                    }
                    else if (!worst.ContainsKey(clipName)) worst[clipName] = 0f;
                    last = p;
                    have = true;
                }
            }
            finally
            {
                effects?.Dispose();
                view?.Dispose();
                Object.DestroyImmediate(driver);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(parent);
                Physics.simulationMode = oldMode;
            }
            return played;
        }
    }
}
