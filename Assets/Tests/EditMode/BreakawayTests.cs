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
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.Idle, 0, false, 0.5), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.SkateIn, 2.0, false, 0.5), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.SlowDown, 0.6, false, 0.5), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.SlowDown, 5.0, false, 0.5), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ShootingWindow, 0, false, 0.5), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ShootingWindow, 3.0, false, 0.5), Tol);
        }

        [Test]
        public void On_the_Shot_the_goalie_reacts_into_the_pose_over_0_2_s_when_the_puck_takes_longer()
        {
            const double Flight = 0.5; // 6 m at about 43 km/h
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0, true, Flight), Tol, "still ready at the Shot");
            double midway = Breakaway.GoalieSet(RoundPhase.ResultHold, 0.1, true, Flight);
            Assert.AreEqual(0.75, midway, Tol, "eased out, three quarters there half way through 0.2 s");
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0.2, true, Flight), Tol, "fully posed after 0.2 s");
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 2.0, true, Flight), Tol);
        }

        [TestCase(200.0)]
        [TestCase(1000.0)]
        public void A_puck_faster_than_the_reaction_gets_a_quicker_reaction_fully_posed_when_the_puck_lands(double kmh)
        {
            // The fastest puck: from the closest the shooter gets to the net (the puck starts about 1 m in front of the eye).
            double flight = ShotFlight.Seconds(Breakaway.ClosestDrift - 1.0, kmh);
            Assert.Less(flight, Breakaway.GoalieReactSeconds, "the puck lands before a 0.2 s reaction would end");
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0, true, flight), Tol, "still ready at the Shot");
            Assert.AreEqual(0.75, Breakaway.GoalieSet(RoundPhase.ResultHold, flight / 2, true, flight), Tol, "the same ease, quicker");
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, flight, true, flight), Tol, "fully posed when the puck lands");
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 2.0, true, flight), Tol);
        }

        /// <summary>A race the goalie's gear moved in (not in the way): Reach Time = Reaction Time + the move.</summary>
        static ShotRace Raced(double reaction, double move, double flight) =>
            new ShotRace(SetUp.Clean, AttackSide.Middle, reaction, move, reaction + move, flight, false, SaveReaction.Glove, default, default);

        [Test]
        public void On_a_raced_Shot_the_goalie_holds_the_ready_stance_until_the_Reaction_Time()
        {
            ShotRace race = Raced(0.15, 0.1, 0.3);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0, true, 0.3, race), Tol, "still ready at the Shot");
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0.1, true, 0.3, race), Tol, "still ready before his Reaction Time");
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0.15, true, 0.3, race), Tol, "starts at his Reaction Time");
            Assert.Greater(Breakaway.GoalieSet(RoundPhase.ResultHold, 0.16, true, 0.3, race), 0.0, "moving just after it");
        }

        [Test]
        public void A_raced_Save_has_the_gear_on_the_line_at_or_before_the_Flight_Time()
        {
            ShotRace race = Raced(0.12, 0.08, 0.25);
            Assert.IsTrue(race.IsSave);
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, race.ReachSeconds, true, 0.25, race), Tol, "there at the Reach Time");
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, race.FlightSeconds, true, 0.25, race), Tol, "there when the puck passes");
            Assert.Less(Breakaway.GoalieSet(RoundPhase.ResultHold, race.ReachSeconds - 0.01, true, 0.25, race), 1.0, "not before the Reach Time");
        }

        [Test]
        public void A_Save_won_at_the_edge_arrives_exactly_as_the_puck_passes()
        {
            ShotRace race = Raced(0.12, 0.08, 0.2);
            Assert.IsTrue(race.IsSave);
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, race.FlightSeconds, true, 0.2, race), Tol);
        }

        [Test]
        public void On_a_raced_Goal_the_gear_arrives_after_the_puck_passes()
        {
            ShotRace race = Raced(0.12, 0.2, 0.25);
            Assert.IsFalse(race.IsSave);
            Assert.Less(Breakaway.GoalieSet(RoundPhase.ResultHold, race.FlightSeconds, true, 0.4, race), 1.0, "still on the way when the puck passes");
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, race.ReachSeconds, true, 0.4, race), Tol, "there at the Reach Time, too late");
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 2.0, true, 0.4, race), Tol);
        }

        [TestCase(0.05)]
        [TestCase(0.3)]
        public void The_gear_moves_at_the_race_speed_from_the_Reaction_Time_to_the_Reach_Time(double move)
        {
            ShotRace race = Raced(0.12, move, 0.25);
            Assert.AreEqual(0.5, Breakaway.GoalieSet(RoundPhase.ResultHold, 0.12 + move / 2, true, 0.25, race), Tol, "half way in half the move time");
            Assert.AreEqual(0.25, Breakaway.GoalieSet(RoundPhase.ResultHold, 0.12 + move / 4, true, 0.25, race), Tol);
        }

        [Test]
        public void When_no_gear_can_reach_he_still_waits_his_Reaction_Time_then_reaches_over_0_2_s()
        {
            ShotRace race = Raced(0.12, double.PositiveInfinity, 0.25);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0.12, true, 0.4, race), Tol);
            Assert.Greater(Breakaway.GoalieSet(RoundPhase.ResultHold, 0.13, true, 0.4, race), 0.0);
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0.12 + Breakaway.GoalieReactSeconds, true, 0.4, race), Tol);
        }

        [Test]
        public void With_gear_already_in_the_way_he_is_fully_in_the_Save_Reaction_when_the_puck_gets_there()
        {
            // No move in the race (move time 0): his gear is on the line already, so he needs no Reaction Time to get there.
            var race = new ShotRace(SetUp.Clean, AttackSide.Middle, 0.12, 0, 0, 0.1, true, SaveReaction.Glove, default, default);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0, true, 0.1, race), Tol);
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0.1, true, 0.1, race), Tol, "fully posed when the puck lands");
        }

        [Test]
        public void Before_the_Shot_a_race_does_not_move_him()
        {
            ShotRace race = Raced(0.12, 0.1, 0.3);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ShootingWindow, 1.0, false, 0.3, race), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 1.0, false, 0.3, race), Tol, "no Shot");
        }

        [Test]
        public void With_no_Shot_too_late_the_goalie_stays_in_the_ready_stance()
        {
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 0, false, 0.5), Tol);
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, 2.0, false, 0.5), Tol);
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
            var round = new Round(new ScriptedRandom(ScriptedRandom.AlternatingButterflies()), new RoundSettings(), FakeReach.NeverReaches(), 0);
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
            var round = new Round(new ScriptedRandom(ScriptedRandom.AlternatingButterflies()), settings, FakeReach.NeverReaches(), 0);
            Assert.AreSame(settings, round.Settings);
        }

        [Test]
        public void Game_seconds_of_the_slow_down_ease_from_real_time_into_the_slow_time_scale()
        {
            Assert.AreEqual(0.0, Breakaway.GameSeconds(RoundPhase.SlowDown, 0), Tol);
            // The ramp: 0.35 s eased from 1x to 0.25x is 0.35 - 0.75 * 0.35 * 2/3 = 0.175 game seconds; then a quarter of real time.
            Assert.AreEqual(0.175, Breakaway.GameSeconds(RoundPhase.SlowDown, Breakaway.SlowRampSeconds), Tol);
            Assert.AreEqual(0.175 + 0.85 * 0.25, Breakaway.GameSeconds(RoundPhase.SlowDown, 1.2), Tol);
            Assert.AreEqual(0.5, Breakaway.GameSeconds(RoundPhase.ShootingWindow, 2.0), Tol);
            Assert.AreEqual(1.5, Breakaway.GameSeconds(RoundPhase.SkateIn, 1.5), Tol);

            // The same as summing TimeScale frame by frame.
            double summed = 0, dt = 1e-5;
            for (double t = 0; t < 0.2; t += dt) summed += Breakaway.TimeScale(RoundPhase.SlowDown, t + dt / 2) * dt;
            Assert.AreEqual(summed, Breakaway.GameSeconds(RoundPhase.SlowDown, 0.2), 1e-4);
        }

        [Test]
        public void The_drift_is_the_skate_speed_over_the_game_seconds_of_the_slow_down_and_the_window()
        {
            double speed = Breakaway.SkateSpeed(AttackSide.Right, Defaults);
            Assert.AreEqual(speed * 0.175, Breakaway.Drift(AttackSide.Right, 0.35, 0, Defaults), Tol);
            Assert.AreEqual(speed * (0.3875 + 0.25), Breakaway.Drift(AttackSide.Right, 1.2, 1.0, Defaults), Tol);
            Assert.AreEqual(speed * 0.3875, Breakaway.Drift(AttackSide.Right, 5.0, 0, Defaults), Tol, "the slow-down ends at its length");
        }

        [Test]
        public void The_shot_start_drifts_in_through_the_window_and_stops_3_m_from_the_net()
        {
            RinkPoint opening = Breakaway.ShotStart(AttackSide.Middle, 0, Defaults);
            RinkPoint later = Breakaway.ShotStart(AttackSide.Middle, 1.0, Defaults);
            RinkPoint last = Breakaway.ShotStart(AttackSide.Middle, 3.0, Defaults);
            double eyeOut = Breakaway.StopDistance - Breakaway.Drift(AttackSide.Middle, 1.2, 0, Defaults);
            Assert.AreEqual(-eyeOut + 0.9, opening.Z, 0.1, "about a metre ahead of the eye");
            Assert.Greater(later.Z, opening.Z);
            Assert.AreEqual(-Breakaway.ClosestDrift + 0.9, last.Z, 0.1);
            Assert.AreEqual(0, opening.X, Tol);
            Assert.Less(opening.Y, Breakaway.EyeHeight - 0.3);
        }
    }
}
