using System;
using System.Collections.Generic;
using HockeyGame;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The goalie character plays the angle on the skate-in (#64), played through a whole Round with BreakawayView, the goalie
    /// view and the Shot effects at 60 fps: on the line from the puck to the net centre through the skate-in, set on his
    /// Set-up spot when the window opens, squared up to the puck, and reacting from the spot the Round's race measured.
    /// </summary>
    public class GoalieAngleViewTests
    {
        const float Frame = 1f / 60f;
        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
            Time.timeScale = 1f;
        }

        static double OffTheLine(Vector3 spot, RinkPoint puck) =>
            Math.Abs(puck.X * spot.z - puck.Z * spot.x) / Math.Sqrt(puck.X * puck.X + puck.Z * puck.Z);

        static float Distance(Vector3 a, RinkPoint b) => Vector3.Distance(a, new Vector3((float)b.X, (float)b.Y, (float)b.Z));

        [Test]
        public void Through_a_Round_he_plays_the_angle_on_the_skate_in_is_set_on_his_Set_up_spot_at_the_window_and_reacts_from_the_raced_spot()
        {
            var settings = new RoundSettings { NormalDepthMetres = 0.6, ReactionWobbleSeconds = 0 };
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                var character = new GoalieCharacter(_parent.transform);
                Assume.That(character.Present, "needs the goalie model");
                var view = new GoalieView(_parent.transform, character);
                var effects = new ShotEffects(_parent.transform, camera, null, view);
                // Shots 1-3 (left, middle, right) are Clean, Depth (centred on a middle attack too) and Clean; 4-6 are dealt mistakes.
                var round = new Round(ScriptedRandom.Dealing(new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right,
                    AttackSide.Left, AttackSide.Middle, AttackSide.Right }, new[] { HockeyGame.Core.SetUp.Clean, HockeyGame.Core.SetUp.Depth, HockeyGame.Core.SetUp.Clean }),
                    settings, FakeReach.NeverReaches(), 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, camera, view);

                var failures = new List<string>();
                var hipsAtWindow = new Dictionary<AttackSide, float>();
                double worstOffLine = 0, worstFacing = 0, worstMistakeFacing = 0;
                string offWhere = "", facingWhere = "";
                int shots = 0, windows = 0, skateChecks = 0;
                RoundPhase last = RoundPhase.Idle;
                round.StartRound();
                for (int frame = 0; frame < 60 * 80 && round.Phase != RoundPhase.RoundOver; frame++)
                {
                    round.Tick(Frame);
                    view.Show(true, round.GoaliePose, false);
                    bool shoot = round.Phase == RoundPhase.ShootingWindow && round.PhaseSecondsElapsed > 0.5;
                    if (shoot) round.SubmitShot(70, 60, 100, false);
                    effects.Update(round, Frame);
                    if (shoot)
                    {
                        // The Shot frame: he reacts (React, before this frame's BreakawayView step) from the spot the race measured.
                        shots++;
                        ShotRace race = round.LastRace.Value;
                        if (Distance(view.ShownSpot, race.Spot) > 1e-5f) failures.Add("Shot " + shots + ": reacts from " + view.ShownSpot + ", race " + race.Spot);
                        double facing = SetUps.Facing(race.Spot, race.ShotStart);
                        if (Math.Abs(view.PosedFacing() - facing) > 1e-3) failures.Add("Shot " + shots + ": reaction built at " + view.PosedFacing() + " deg, race " + facing);
                    }
                    breakaway.Step(Frame * Time.timeScale, Frame);

                    RoundPhase phase = round.Phase;
                    AttackSide side = round.AttackSide;
                    double seconds = round.PhaseSecondsElapsed;
                    // On the angle through the second half of the skate-in; through the slow-down too in a Clean Set-up (a mistake
                    // settles off it then, onto the Set-up spot checked as the window opens). After a Depth he is still easing the last
                    // millimetre back out early in the second half of the skate-in, so the depth holds to 1 mm.
                    if (phase == RoundPhase.SkateIn && seconds >= 0.5 * settings.SkateInSeconds || phase == RoundPhase.SlowDown && round.SetUp == HockeyGame.Core.SetUp.Clean)
                    {
                        RinkPoint puck = Breakaway.Puck(side, phase, seconds, settings);
                        double off = OffTheLine(view.ShownSpot, puck);
                        if (off > worstOffLine) { worstOffLine = off; offWhere = side + " " + phase + " " + seconds.ToString("F2") + " s: spot " + view.ShownSpot.ToString("F3") + ", puck " + puck; }
                        if (Math.Abs(view.ShownSpot.z + 0.6f) > 1e-3f) failures.Add(phase + " " + seconds.ToString("F2") + ": depth " + view.ShownSpot.z);
                        skateChecks++;
                    }
                    if (phase == RoundPhase.ShootingWindow && last != RoundPhase.ShootingWindow)
                    {
                        windows++;
                        RinkPoint spot = SetUps.Spot(round.SetUp, side, seconds, Breakaway.ShotStart(side, seconds, settings), settings);
                        if (Distance(view.ShownSpot, spot) > 1e-5f) failures.Add(side + ": at the window " + view.ShownSpot + ", Set-up spot " + spot);
                        if (!hipsAtWindow.ContainsKey(side)) hipsAtWindow[side] = character.Bone(HumanBodyBones.Hips).position.x; // Shots 1-3
                    }
                    if (phase == RoundPhase.ShootingWindow)
                    {
                        RinkPoint puck = Breakaway.ShotStart(side, seconds, settings);
                        RinkPoint spot = new RinkPoint(view.ShownSpot.x, 0, view.ShownSpot.z);
                        double facingOff = Math.Abs(view.ShownFacing - SetUps.Facing(spot, puck));
                        if (round.SetUp.IsMistake) // he moved across into it (in slow motion) or still slides: his turn trails a little
                            worstMistakeFacing = Math.Max(worstMistakeFacing, facingOff);
                        else if (facingOff > worstFacing) { worstFacing = facingOff; facingWhere = side + " " + round.SetUp + " " + seconds.ToString("F2") + " s"; }
                    }
                    last = phase;
                }
                TestContext.Out.WriteLine("worst off the angle line " + worstOffLine.ToString("F4") + " m over " + skateChecks + " frames; worst facing " + worstFacing.ToString("F2") + " deg (" + worstMistakeFacing.ToString("F2") + " deg in a mistake)");
                Assert.AreEqual(6, shots, "six Shots");
                Assert.AreEqual(6, windows, "six windows");
                Assert.IsEmpty(failures, string.Join("\n", failures));
                Assert.Less(worstOffLine, 0.01, "on the angle line through the second half of the skate-in and the slow-down: " + offWhere);
                Assert.Less(worstFacing, 0.5, "squared up to the puck in the window: " + facingWhere);
                Assert.Less(worstMistakeFacing, 2.0, "nearly squared up in a Set-up Mistake");
                Assert.Greater(hipsAtWindow[AttackSide.Right] - hipsAtWindow[AttackSide.Left], 0.25f, "his body is across to the left on a left attack, to the right on a right one");
                Assert.Less(Math.Abs(hipsAtWindow[AttackSide.Middle] - (hipsAtWindow[AttackSide.Left] + hipsAtWindow[AttackSide.Right]) / 2f), 0.05f, "centred on a middle attack");
            }
            finally
            {
                Object.DestroyImmediate(driver);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
