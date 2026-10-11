using System;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The goalie plays the angle on the skate-in (#64): through the skate-in and slow-down his spot (SetUps.Approach) is on the
    /// line from the puck to the net centre, and from the shooting window on it is his Set-up spot (SetUps.Spot), the spot the
    /// goalie-reach port measures. Each test sets its own RoundSettings.
    /// </summary>
    public class GoalieAngleTests
    {
        const double Tol = 1e-9;
        static readonly AttackSide[] Sides = { AttackSide.Left, AttackSide.Middle, AttackSide.Right };

        static RoundSettings Settings() => new RoundSettings { NormalDepthMetres = 0.6, SkateInSeconds = 2.5, SlowDownSeconds = 1.0 };

        /// <summary>How far `spot` is off the line (on the ice) from the puck to the net centre (metres).</summary>
        static double OffTheLine(RinkPoint spot, RinkPoint puck) =>
            Math.Abs(puck.X * spot.Z - puck.Z * spot.X) / Math.Sqrt(puck.X * puck.X + puck.Z * puck.Z);

        static void AssertSame(RinkPoint expected, RinkPoint actual, string what)
        {
            Assert.AreEqual(expected.X, actual.X, Tol, what + ": x");
            Assert.AreEqual(expected.Y, actual.Y, Tol, what + ": y");
            Assert.AreEqual(expected.Z, actual.Z, Tol, what + ": z");
        }

        [Test]
        public void Through_the_skate_in_he_stays_on_the_line_from_the_puck_to_the_net_centre_at_his_normal_depth_for_every_Attack_Side()
        {
            RoundSettings settings = Settings();
            foreach (AttackSide side in Sides)
            foreach (double u in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
            {
                double seconds = u * settings.SkateInSeconds;
                RinkPoint puck = Breakaway.Puck(side, RoundPhase.SkateIn, seconds, settings);
                RinkPoint spot = SetUps.Approach(SetUp.Clean, side, RoundPhase.SkateIn, seconds, settings);
                string where = side + " at skate progress " + u;
                Assert.AreEqual(0.0, OffTheLine(spot, puck), 1e-9, where + ": on the angle line");
                Assert.AreEqual(-0.6, spot.Z, Tol, where + ": at his normal depth");
                Assert.AreEqual(0.0, spot.Y, Tol, where + ": on the ice");
                Assert.IsTrue(Math.Sign(spot.X) == Math.Sign(Math.Round(puck.X, 9)), where + ": on the puck's side of the net");
            }
        }

        [Test]
        public void On_a_side_attack_he_moves_across_toward_the_middle_as_the_shooter_curls_in()
        {
            RoundSettings settings = Settings();
            RinkPoint early = SetUps.Approach(SetUp.Clean, AttackSide.Left, RoundPhase.SkateIn, 0.25 * settings.SkateInSeconds, settings);
            RinkPoint late = SetUps.Approach(SetUp.Clean, AttackSide.Left, RoundPhase.SkateIn, settings.SkateInSeconds, settings);
            Assert.Less(early.X, late.X, "the angle comes in from the left as the shooter curls toward the net");
            Assert.Less(late.X, 0.0);
        }

        [Test]
        public void Through_the_slow_down_he_stays_on_the_angle_line_and_is_set_on_his_Set_up_spot_when_the_window_opens()
        {
            RoundSettings settings = Settings();
            foreach (AttackSide side in Sides)
            {
                foreach (double seconds in new[] { 0.0, 0.5, 1.0 })
                {
                    RinkPoint puck = Breakaway.Puck(side, RoundPhase.SlowDown, seconds, settings);
                    RinkPoint spot = SetUps.Approach(SetUp.Clean, side, RoundPhase.SlowDown, seconds, settings);
                    Assert.AreEqual(0.0, OffTheLine(spot, puck), 1e-9, side + " " + seconds + " s into the slow-down");
                }
                RinkPoint setUpSpot = SetUps.Spot(SetUp.Clean, side, 0, Breakaway.ShotStart(side, 0, settings), settings);
                AssertSame(setUpSpot, SetUps.Approach(SetUp.Clean, side, RoundPhase.ShootingWindow, 0, settings), side + " as the window opens");
                AssertSame(setUpSpot, SetUps.Approach(SetUp.Clean, side, RoundPhase.SlowDown, settings.SlowDownSeconds, settings),
                    side + " at the end of the slow-down: already set, so nothing jumps as the window opens");
            }
        }

        [Test]
        public void In_the_shooting_window_his_spot_is_the_Set_up_spot_the_reach_port_measures_for_a_Shot_then()
        {
            RoundSettings settings = Settings();
            foreach (AttackSide side in Sides)
            foreach (double seconds in new[] { 0.0, 1.0, 2.9 })
            {
                RinkPoint shotStart = Breakaway.ShotStart(side, seconds, settings);
                AssertSame(SetUps.Spot(SetUp.Clean, side, seconds, shotStart, settings),
                    SetUps.Approach(SetUp.Clean, side, RoundPhase.ShootingWindow, seconds, settings), side + " " + seconds + " s into the window");
            }
        }

        [Test]
        public void The_puck_on_the_approach_runs_on_from_the_skate_in_through_the_slow_down_to_where_the_Shot_starts()
        {
            RoundSettings settings = Settings();
            foreach (AttackSide side in Sides)
            {
                AssertSame(Breakaway.Puck(side, RoundPhase.SlowDown, 0, settings), Breakaway.Puck(side, RoundPhase.SkateIn, settings.SkateInSeconds, settings),
                    side + ": skate-in to slow-down");
                AssertSame(Breakaway.ShotStart(side, 0, settings), Breakaway.Puck(side, RoundPhase.SlowDown, settings.SlowDownSeconds, settings),
                    side + ": slow-down to window");
                AssertSame(Breakaway.ShotStart(side, 1.5, settings), Breakaway.Puck(side, RoundPhase.ShootingWindow, 1.5, settings), side + ": in the window");
            }
        }

        [Test]
        public void He_faces_the_puck_from_his_spot()
        {
            RoundSettings settings = Settings();
            RinkPoint puck = Breakaway.Puck(AttackSide.Right, RoundPhase.SkateIn, 0.5 * settings.SkateInSeconds, settings);
            RinkPoint spot = SetUps.Approach(SetUp.Clean, AttackSide.Right, RoundPhase.SkateIn, 0.5 * settings.SkateInSeconds, settings);
            double facing = SetUps.Facing(spot, puck);
            Assert.AreEqual(GoalieTurn.Facing(puck.X - spot.X, spot.Z - puck.Z), facing, Tol);
            Assert.Less(facing, 0.0, "a shooter on the right: turned toward the shooter's right");
        }

        // ---- The shuffle across the crease -------------------------------------------------------------------------------------

        [Test]
        public void The_shuffle_closes_on_a_new_spot_smoothly_never_faster_than_its_top_speed_and_gets_there()
        {
            var shuffle = new GoalieShuffle(new RinkPoint(0.3, 0, -0.6));
            var target = new RinkPoint(-0.2, 0, -0.6);
            const double dt = 1.0 / 60.0;
            RinkPoint was = shuffle.Shown;
            for (int frame = 0; frame < 120; frame++)
            {
                shuffle.Step(target, dt);
                double step = RinkPoint.Distance(was, shuffle.Shown);
                Assert.LessOrEqual(step, GoalieShuffle.TopSpeedMetres * dt + 1e-12, "frame " + frame);
                was = shuffle.Shown;
            }
            Assert.AreEqual(0.0, RinkPoint.Distance(shuffle.Shown, target), 1e-3, "on the new spot within 2 s");
        }

        [Test]
        public void The_shuffle_moves_with_a_moving_spot_without_falling_behind()
        {
            var shuffle = new GoalieShuffle(new RinkPoint(0, 0, -0.6));
            const double dt = 1.0 / 60.0;
            for (int frame = 0; frame < 120; frame++)
            {
                var target = new RinkPoint(0.3 * frame * dt, 0, -0.6); // the angle moving across at 0.3 m/s
                shuffle.Step(target, dt);
                Assert.AreEqual(0.0, RinkPoint.Distance(target, shuffle.Shown), 1e-12, "frame " + frame);
            }
        }

        [Test]
        public void The_shuffle_holds_still_with_no_time_and_a_reset_puts_him_on_the_spot_at_once()
        {
            var shuffle = new GoalieShuffle(new RinkPoint(0, 0, -0.6));
            shuffle.Step(new RinkPoint(0.5, 0, -0.6), 0);
            AssertSame(new RinkPoint(0, 0, -0.6), shuffle.Shown, "no time, no move");
            shuffle.Reset(new RinkPoint(0.1, 0, -0.7));
            AssertSame(new RinkPoint(0.1, 0, -0.7), shuffle.Shown, "reset");
        }
    }
}
