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

        static RoundSettings Settings() => new RoundSettings { NormalDepthMetres = 0.2, SkateInSeconds = 2.5, SlowDownSeconds = 1.0 };

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
                Assert.AreEqual(-0.2, spot.Z, Tol, where + ": at his normal depth");
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
        public void He_reaches_the_spot_and_stops_on_it()
        {
            RoundSettings settings = new RoundSettings();
            var shuffle = new GoalieShuffle(new RinkPoint(0.3, 0, -0.2), settings);
            var target = new RinkPoint(-0.2, 0, -0.2);
            const double dt = 1.0 / 60.0;
            double peak = new SkatePush(-0.5, settings).PeakSpeed;
            RinkPoint was = shuffle.Shown;
            int frames = 0;
            while (frames < 180)
            {
                shuffle.Step(target, dt);
                frames++;
                if (frames == 1)
                {
                    Assert.AreEqual(PushKind.TPush, shuffle.Kind, "a 0.5 m move is a T-push");
                    Assert.AreEqual(Skate.Right, shuffle.PushingSkate, "a move left pushes from the right skate");
                }
                Assert.LessOrEqual(RinkPoint.Distance(was, shuffle.Shown), peak * dt + 1e-9, "frame " + frames);
                was = shuffle.Shown;
                if (shuffle.Phase == PushPhase.Done) break;
            }
            Assert.AreEqual(PushPhase.Done, shuffle.Phase, "done within 3 s");
            AssertSame(target, shuffle.Shown, "on the spot");
            Assert.AreEqual(SkatePush.SecondsFor(0.5, settings), frames * dt, 2 * dt, "the time the push model gives");

            var small = new GoalieShuffle(new RinkPoint(0.2, 0, -0.2), settings);
            small.Step(new RinkPoint(0, 0, -0.2), dt);
            Assert.AreEqual(PushKind.Shuffle, small.Kind, "a 0.2 m move is a shuffle");
        }

        [Test]
        public void His_speed_rises_then_falls_with_no_steady_stretch()
        {
            RoundSettings settings = new RoundSettings();
            var shuffle = new GoalieShuffle(new RinkPoint(0.3, 0, -0.2), settings);
            var target = new RinkPoint(-0.2, 0, -0.2);
            const double dt = 1.0 / 240.0; // fine frames: at 60 Hz the move is only 17 frames and the one peak frame is already 6 percent
            var speeds = new System.Collections.Generic.List<double>();
            var phases = new System.Collections.Generic.List<PushPhase>();
            for (int frame = 0; frame < 720; frame++)
            {
                shuffle.Step(target, dt);
                speeds.Add(shuffle.Speed);
                phases.Add(shuffle.Phase);
                if (shuffle.Phase == PushPhase.Done) break;
            }
            double top = 0; int topAt = 0;
            for (int i = 0; i < speeds.Count; i++) if (speeds[i] > top) { top = speeds[i]; topAt = i; }
            for (int i = 1; i <= topAt; i++) Assert.GreaterOrEqual(speeds[i], speeds[i - 1] - 1e-12, "rising at frame " + i);
            for (int i = topAt + 1; i < speeds.Count; i++) Assert.LessOrEqual(speeds[i], speeds[i - 1] + 1e-12, "falling at frame " + i);
            int nearTop = 0;
            foreach (double s in speeds) if (s >= 0.99 * top) nearTop++;
            Assert.Less(nearTop, 0.05 * speeds.Count, "no steady stretch");
            int stage = 0; // Push 0, Glide 1, Done 2: never back
            foreach (PushPhase p in phases)
            {
                int now = p == PushPhase.Push ? 0 : p == PushPhase.Glide ? 1 : 2;
                Assert.GreaterOrEqual(now, stage, "phase never goes back");
                stage = now;
            }
            Assert.AreEqual(2, stage, "ends Done");
        }

        [TestCase(-0.6)]
        [TestCase(-0.2)]
        public void A_moving_spot_chains_pushes_and_he_catches_it(double depth)
        {
            RoundSettings settings = new RoundSettings();
            var shuffle = new GoalieShuffle(new RinkPoint(0, 0, depth), settings);
            const double dt = 1.0 / 60.0;
            double was = 0, maxSpeed = 0;
            RinkPoint target = new RinkPoint(0, 0, depth);
            for (int frame = 0; frame < 60; frame++)
            {
                target = new RinkPoint(0.3 * (frame + 1) * dt, 0, depth); // the angle moving across at 0.3 m/s for 1 s
                shuffle.Step(target, dt);
                maxSpeed = Math.Max(maxSpeed, shuffle.Speed);
                Assert.LessOrEqual(Math.Abs(shuffle.Speed - was), settings.PushAccelMetresPerSecondSquared * dt * 1.01, "speed jump at frame " + frame);
                was = shuffle.Speed;
            }
            Assert.Less(maxSpeed, 5.0, "never faster than 5 m/s");
            for (int frame = 0; frame < 60; frame++)
            {
                shuffle.Step(target, dt);
                Assert.LessOrEqual(Math.Abs(shuffle.Speed - was), settings.PushAccelMetresPerSecondSquared * dt * 1.01, "speed jump after the spot stopped, frame " + frame);
                was = shuffle.Speed;
            }
            AssertSame(target, shuffle.Shown, "on the spot within 1 s of it stopping");
        }

        [Test]
        public void A_new_target_while_gliding_turns_him_round_smoothly()
        {
            RoundSettings settings = new RoundSettings();
            var shuffle = new GoalieShuffle(new RinkPoint(0, 0, -0.2), settings);
            var right = new RinkPoint(1.0, 0, -0.2);
            var left = new RinkPoint(-0.5, 0, -0.2);
            const double dt = 1.0 / 60.0;
            int guard = 0;
            while (shuffle.Phase != PushPhase.Glide && guard++ < 180) shuffle.Step(right, dt);
            Assert.AreEqual(PushPhase.Glide, shuffle.Phase, "gliding toward the right spot");
            Assert.Greater(shuffle.Speed, 1.0, "mid-glide");
            double was = shuffle.Speed;
            bool stopped = false;
            for (int frame = 0; frame < 300; frame++)
            {
                shuffle.Step(left, dt);
                if (!stopped) Assert.LessOrEqual(Math.Abs(shuffle.Speed - was), settings.GlideDecelMetresPerSecondSquared * dt * 1.01, "speed change at frame " + frame);
                if (shuffle.Speed <= 0) stopped = true;
                was = shuffle.Speed;
                if (stopped && shuffle.Phase == PushPhase.Done) break;
            }
            Assert.IsTrue(stopped, "he came to rest");
            AssertSame(left, shuffle.Shown, "ends on the new target");
        }

        [Test]
        public void No_time_no_move()
        {
            var shuffle = new GoalieShuffle(new RinkPoint(0, 0, -0.6));
            shuffle.Step(new RinkPoint(0.5, 0, -0.6), 0);
            AssertSame(new RinkPoint(0, 0, -0.6), shuffle.Shown, "no time, no move");
            Assert.AreEqual(PushPhase.Done, shuffle.Phase);
        }

        [Test]
        public void Reset_puts_him_on_the_spot_at_once_and_at_rest()
        {
            var shuffle = new GoalieShuffle(new RinkPoint(0, 0, -0.6));
            for (int i = 0; i < 8; i++) shuffle.Step(new RinkPoint(0.5, 0, -0.6), 1.0 / 60.0);
            Assert.Greater(shuffle.Speed, 0.0, "half way through the move");
            shuffle.Reset(new RinkPoint(0.1, 0, -0.7));
            AssertSame(new RinkPoint(0.1, 0, -0.7), shuffle.Shown, "reset");
            Assert.AreEqual(0.0, shuffle.Speed);
            Assert.AreEqual(PushPhase.Done, shuffle.Phase);
        }

        [Test]
        public void He_moves_on_scaled_game_time_the_same_in_big_and_small_steps()
        {
            var big = new GoalieShuffle(new RinkPoint(0, 0, -0.2));
            var small = new GoalieShuffle(new RinkPoint(0, 0, -0.2));
            var target = new RinkPoint(10.0, 0, -0.2); // far enough that the second is mid-move
            for (int i = 0; i < 10; i++) big.Step(target, 0.1);
            for (int i = 0; i < 100; i++) small.Step(target, 0.01);
            Assert.Greater(big.Shown.X, 0.5, "partway along the move");
            Assert.Less(big.Shown.X, 10.0, "partway along the move");
            Assert.AreEqual(small.Shown.X, big.Shown.X, 0.02);
        }
    }
}
