using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// #206: the Idle clip (an AI clip the goalie plays in the Idle phase, one of two picked at random) and its blend out when the Round
    /// starts must keep the skates (their meshes, as the play-check measures them) within 1 cm of the ice, not only the foot bones:
    /// the clip's ankles dipped the skates 4 cm.
    /// </summary>
    public class GoalieIdleClipGroundTests
    {
        const float Frame = 1f / 60f, SinkLimit = -0.01f;

        [Test, Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void The_Idle_clips_and_their_blend_out_into_the_skate_in_keep_the_skates_on_the_ice()
        {
            float worst = 0f;
            string at = "";
            for (int seed = 1; seed <= 4; seed++)
            {
                RunOnce(seed, out float low, out string where, out bool clipRan);
                Assert.IsTrue(clipRan, $"seed {seed}: the Idle clip played (else this test proves nothing)");
                if (low < worst) { worst = low; at = $"seed {seed} {where}"; }
            }
            Assert.That(worst, Is.GreaterThanOrEqualTo(SinkLimit), $"lowest skate {worst:F4} m under the ice at {at} (limit {SinkLimit})");
        }

        static void RunOnce(int seed, out float worst, out string at, out bool clipRan)
        {
            var oldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            var parent = new GameObject("Rink (test)");
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            PhysicsShotEffects effects = null;
            GoalieView view = null;
            worst = 0f;
            at = "";
            clipRan = false;
            try
            {
                var character = new GoalieCharacter(parent.transform);
                Assume.That(character.Present, "needs the goalie model");
                view = new GoalieView(parent.transform, character);
                Assume.That(view.Clips != null, "needs the clip player");
                view.Clips.UseRandom(new SeededRandom(seed));
                effects = new PhysicsShotEffects(parent.transform, cameraObject.GetComponent<Camera>(), new SeededRandom(7), view, name => null);
                Physics.SyncTransforms();
                var round = new Round(ScriptedRandom.Forcing(AttackSide.Middle), new RoundSettings(), FakeReach.InTheWay(), 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, cameraObject.GetComponent<Camera>(), view, () => effects.FlightSeconds);
                for (int frame = 0; frame < 360; frame++)
                {
                    if (frame == 300) round.StartRound(); // the Idle clip (starts 1 s into Idle) is still playing: it blends out into the skate-in
                    for (int step = 0; step < 2; step++) { Physics.Simulate(PhysicsShot.StepSeconds); effects.AfterPhysicsStep(PhysicsShot.StepSeconds); }
                    round.Tick(Frame);
                    view.Show(true);
                    effects.Update(round, Frame);
                    view.Clips.Pre();
                    breakaway.Step(Frame, Frame);
                    view.Clips.Post(round, true, Frame);
                    clipRan |= view.Clips.Active;
                    float low = float.MaxValue;
                    foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
                        if (r.enabled && r.name.ToLower().Contains("skate")) low = Mathf.Min(low, r.bounds.min.y);
                    float rel = low - character.Root.transform.position.y;
                    if (rel < worst) { worst = rel; at = $"frame {frame} {round.Phase}"; }
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
        }
    }
}
