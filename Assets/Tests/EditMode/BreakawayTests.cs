using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class BreakawayTests
    {
        private const double Tol = 1e-6;
        private static readonly RoundSettings Defaults = new RoundSettings();

        [TestCase(AttackSide.Left, -10.0)]
        [TestCase(AttackSide.Middle, 6.0)]
        [TestCase(AttackSide.Right, 10.0)]
        public void Skate_in_starts_22_m_out_at_the_side_start_x(AttackSide side, double expectedX)
        {
            Breakaway.SkatePoint(side, 0, out double x, out double distance);
            Assert.AreEqual(expectedX, x, Tol);
            Assert.AreEqual(22.0, distance, Tol);
        }

        [TestCase(AttackSide.Left, -2.7)]
        [TestCase(AttackSide.Middle, 0.0)]
        [TestCase(AttackSide.Right, 2.7)]
        public void Skate_in_stops_9_m_out_at_side_times_0_3_times_9(AttackSide side, double expectedX)
        {
            Breakaway.SkatePoint(side, 1, out double x, out double distance);
            Assert.AreEqual(expectedX, x, Tol);
            Assert.AreEqual(9.0, distance, Tol);
        }

        [Test]
        public void Skate_in_curls_on_a_quadratic_with_the_control_point_straight_ahead_halfway_in()
        {
            // Left: start (-10, 22), control (-10, 15.5), stop (-2.7, 9). At u = 0.5 the quadratic gives
            // 0.25 * start + 0.5 * control + 0.25 * stop = (-8.175, 15.5).
            Breakaway.SkatePoint(AttackSide.Left, 0.5, out double x, out double distance);
            Assert.AreEqual(-8.175, x, Tol);
            Assert.AreEqual(15.5, distance, Tol);
        }

        [Test]
        public void Skate_progress_is_linear_in_real_time_over_2_5_s()
        {
            Assert.AreEqual(0.0, Breakaway.SkateProgress(0, Defaults), Tol);
            Assert.AreEqual(0.4, Breakaway.SkateProgress(1.0, Defaults), Tol);
            Assert.AreEqual(1.0, Breakaway.SkateProgress(2.5, Defaults), Tol);
            Assert.AreEqual(1.0, Breakaway.SkateProgress(4.0, Defaults), Tol);
        }

        [Test]
        public void Skate_speed_is_start_to_stop_distance_over_the_skate_in_time()
        {
            // Middle: (6, 22) to (0, 9): sqrt(36 + 169) = 14.3178 m over 2.5 s.
            Assert.AreEqual(5.727128, Breakaway.SkateSpeed(AttackSide.Middle, Defaults), 1e-5);
        }

        [Test]
        public void Time_scale_eases_from_1_to_quarter_over_the_first_0_35_s_of_the_slow_down()
        {
            Assert.AreEqual(1.0, Breakaway.TimeScale(RoundPhase.SlowDown, 0), Tol);
            // ease(0.5) = 1 - 0.25 = 0.75; 1 + (0.25 - 1) * 0.75 = 0.4375
            Assert.AreEqual(0.4375, Breakaway.TimeScale(RoundPhase.SlowDown, 0.175), Tol);
            Assert.AreEqual(0.25, Breakaway.TimeScale(RoundPhase.SlowDown, 0.35), Tol);
            Assert.AreEqual(0.25, Breakaway.TimeScale(RoundPhase.SlowDown, 1.0), Tol);
        }

        [TestCase(RoundPhase.Idle, 1.0)]
        [TestCase(RoundPhase.SkateIn, 1.0)]
        [TestCase(RoundPhase.ShootingWindow, 0.25)]
        [TestCase(RoundPhase.ResultHold, 1.0)]
        [TestCase(RoundPhase.RoundOver, 1.0)]
        public void Time_scale_stays_slow_in_the_window_and_is_normal_otherwise(RoundPhase phase, double expected)
        {
            Assert.AreEqual(expected, Breakaway.TimeScale(phase, 0.5), Tol);
        }

        [TestCase(1.0, 0.0)]
        [TestCase(0.625, 0.5)]
        [TestCase(0.25, 1.0)]
        public void Slow_mo_look_follows_the_time_scale(double timeScale, double expected)
        {
            Assert.AreEqual(expected, Breakaway.LookAmount(timeScale), Tol);
        }

        [Test]
        public void Goalie_holds_the_ready_stance_until_the_Shot()
        {
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.Idle, 0, false), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.SkateIn, 2.0, false), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.SlowDown, 0.6, false), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.SlowDown, 5.0, false), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ShootingWindow, 0, false), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ShootingWindow, 3.0, false), Tol);
        }

        [Test]
        public void On_the_Shot_the_goalie_reacts_into_the_pose_before_the_fastest_puck_reaches_the_Net_Face()
        {
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0, true), Tol, "still ready at the Shot");
            double midway = Breakaway.GoalieSet(RoundPhase.ResultHold, 0.1, true);
            Assert.Greater(midway, 0.0, "moving");
            Assert.Less(midway, 1.0, "not there yet");
            // The fastest puck: a huge speed from the closest the shooter gets to the net.
            double fastestFlight = ShotFlight.Seconds(Breakaway.ClosestDrift, 1000);
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, fastestFlight, true), Tol, "fully posed when the puck lands");
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 2.0, true), Tol);
        }

        [Test]
        public void With_no_Shot_too_late_the_goalie_stays_in_the_ready_stance()
        {
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0, false), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 2.0, false), Tol);
        }

        [Test]
        public void Ring_fraction_drains_over_the_3_s_window()
        {
            Assert.AreEqual(1.0, Breakaway.RingFraction(3.0, Defaults), Tol);
            Assert.AreEqual(0.5, Breakaway.RingFraction(1.5, Defaults), Tol);
            Assert.AreEqual(0.0, Breakaway.RingFraction(0, Defaults), Tol);
        }

        [Test]
        public void Drift_stops_3_m_before_the_net()
        {
            Assert.AreEqual(2.0, Breakaway.CappedDrift(2.0, 9.0), Tol);
            Assert.AreEqual(6.0, Breakaway.CappedDrift(20.0, 9.0), Tol);
        }

        [Test]
        public void Round_reports_real_seconds_spent_in_the_current_phase()
        {
            var round = new Round(new ScriptedRandom(ScriptedRandom.AlternatingButterflies()), new RoundSettings(), 0);
            round.StartRound();
            round.Tick(1.0);
            Assert.AreEqual(1.0, round.PhaseSecondsElapsed, Tol);
            round.Tick(2.0); // 0.5 s into the slow-down
            Assert.AreEqual(RoundPhase.SlowDown, round.Phase);
            Assert.AreEqual(0.5, round.PhaseSecondsElapsed, Tol);
        }

        [Test]
        public void Skate_in_and_window_follow_the_Round_settings()
        {
            var slow = new RoundSettings { SkateInSeconds = 5, SlowDownSeconds = 2.4, ShootingWindowSeconds = 6 };
            Assert.AreEqual(0.5, Breakaway.SkateProgress(2.5, slow), Tol);
            Assert.AreEqual(14.317821 / 5, Breakaway.SkateSpeed(AttackSide.Middle, slow), 1e-5);
            Assert.AreEqual(0.5, Breakaway.RingFraction(3.0, slow), Tol);
        }

        [Test]
        public void Round_reports_the_settings_it_runs_on()
        {
            var settings = new RoundSettings { SkateInSeconds = 4 };
            var round = new Round(new ScriptedRandom(ScriptedRandom.AlternatingButterflies()), settings, 0);
            Assert.AreSame(settings, round.Settings);
        }
    }
}
