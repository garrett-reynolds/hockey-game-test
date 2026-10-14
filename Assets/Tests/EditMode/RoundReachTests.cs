using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>Reach in time on the Round seam: ScriptedRandom and a fake goalie-reach port, each test with its own RoundSettings.</summary>
    public class RoundReachTests
    {
        const double Tol = 1e-9;

        /// <summary>Settings with a fixed Reaction Time (no wobble).</summary>
        static RoundSettings Fixed(double reactionSeconds = 0.12) =>
            new RoundSettings { ReactionSeconds = reactionSeconds, ReactionWobbleSeconds = 0 };

        /// <summary>A Round with a Clean Set-up on a middle attack (unless random says otherwise), in its shooting window, windowSeconds in.</summary>
        static Round InWindow(IGoalieReach reach, RoundSettings settings, double windowSeconds = 1.0, ScriptedRandom random = null)
        {
            var round = new Round(random ?? ScriptedRandom.Forcing(AttackSide.Middle), settings, reach, 0);
            round.StartRound();
            round.Tick(settings.SkateInSeconds + settings.SlowDownSeconds + windowSeconds);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            return round;
        }

        /// <summary>The Flight Time of a Shot at (x, y) and this speed, 1 s into the window (nothing reaches it).</summary>
        static double FlightOf(double x, double y, double kmh)
        {
            var round = InWindow(FakeReach.NeverReaches(), Fixed());
            round.SubmitShotAndWhistle(x, y, kmh, false);
            return round.LastRace.Value.FlightSeconds;
        }

        [Test]
        public void The_starting_numbers_are_in_RoundSettings()
        {
            var settings = new RoundSettings();
            Assert.AreEqual(0.12, settings.ReactionSeconds, Tol);
            Assert.AreEqual(0.04, settings.ReactionWobbleSeconds, Tol);
            Assert.AreEqual(8.0, settings.HandMetresPerSecond, Tol);
            Assert.AreEqual(6.0, settings.LegMetresPerSecond, Tol);
            Assert.AreEqual(0.3, settings.ShuffleMaxMetres, Tol);
            Assert.AreEqual(40, settings.PushAccelMetresPerSecondSquared, Tol);
            Assert.AreEqual(20, settings.GlideDecelMetresPerSecondSquared, Tol);
            Assert.AreEqual(0.6, settings.MaxPushMetres, Tol);
            Assert.AreEqual(0.2, settings.NormalDepthMetres, Tol);
        }

        [Test]
        public void A_Shot_is_saved_when_the_Reach_Time_equals_the_Flight_Time_and_scores_when_it_is_any_longer()
        {
            double flight = FlightOf(70, 60, 100);
            // Reaction Time 0, so the Reach Time is exactly the move time.
            var atTheEdge = InWindow(new FakeReach(GoalieReachAnswer.Reaches(SaveReaction.Glove, flight)), Fixed(0));
            atTheEdge.SubmitShotAndWhistle(70, 60, 100, false);
            Assert.AreEqual(atTheEdge.LastRace.Value.FlightSeconds, atTheEdge.LastRace.Value.ReachSeconds);
            Assert.AreEqual(ShotResultKind.Save, atTheEdge.LastResult.Value.Kind);

            var justLate = InWindow(new FakeReach(GoalieReachAnswer.Reaches(SaveReaction.Glove, flight + 1e-6)), Fixed(0));
            justLate.SubmitShotAndWhistle(70, 60, 100, false);
            Assert.AreEqual(ShotResultKind.Goal, justLate.LastResult.Value.Kind);
            Assert.AreEqual(1, justLate.Goals);
        }

        [Test]
        public void The_Reach_Time_is_the_Reaction_Time_plus_the_move_and_the_Late_Margin_is_Reach_minus_Flight()
        {
            var round = InWindow(new FakeReach(GoalieReachAnswer.Reaches(SaveReaction.Pads, 0.25)), Fixed(0.12));
            round.SubmitShotAndWhistle(20, 10, 90, false);
            ShotRace race = round.LastRace.Value;
            Assert.AreEqual(0.12, race.ReactionSeconds, Tol);
            Assert.AreEqual(0.25, race.MoveSeconds, Tol);
            Assert.AreEqual(0.37, race.ReachSeconds, Tol);
            Assert.AreEqual(race.ReachSeconds - race.FlightSeconds, race.LateMarginSeconds, Tol);
            Assert.AreEqual(race.LateMarginSeconds <= 0 ? ShotResultKind.Save : ShotResultKind.Goal, round.LastResult.Value.Kind);
        }

        [TestCase(120.0)]
        [TestCase(250.0)]
        public void A_Shot_into_gear_already_in_the_way_is_a_Save_at_top_speed(double kmh)
        {
            var round = InWindow(FakeReach.InTheWay(), Fixed(0.12), windowSeconds: 2.9); // drifted in close: the shortest flights
            round.SubmitShotAndWhistle(50, 50, kmh, false);
            ShotRace race = round.LastRace.Value;
            Assert.IsTrue(race.InTheWay);
            Assert.AreEqual(0, race.ReachSeconds, Tol);
            Assert.Less(race.FlightSeconds, 0.12, "the Reaction Time alone would lose this race");
            Assert.AreEqual(ShotResultKind.Save, round.LastResult.Value.Kind);
        }

        [Test]
        public void The_same_Net_Face_point_is_a_Save_on_a_slow_Shot_and_a_Goal_on_a_fast_one_when_the_margin_is_close()
        {
            // Reach Time 0.12 + 0.18 = 0.30 s.
            var reach = new FakeReach(GoalieReachAnswer.Reaches(SaveReaction.Glove, 0.18));
            var slow = InWindow(reach, Fixed(0.12));
            slow.SubmitShotAndWhistle(85, 80, 30, false);
            var fast = InWindow(reach, Fixed(0.12));
            fast.SubmitShotAndWhistle(85, 80, 110, false);

            Assert.Greater(slow.LastRace.Value.FlightSeconds, 0.30);
            Assert.Less(fast.LastRace.Value.FlightSeconds, 0.30);
            Assert.AreEqual(ShotResultKind.Save, slow.LastResult.Value.Kind);
            Assert.AreEqual(ShotResultKind.Goal, fast.LastResult.Value.Kind);
        }

        [TestCase(150.0, 50.0)]   // wide
        [TestCase(-1.0, 50.0)]    // left post
        [TestCase(101.0, 50.0)]   // right post
        [TestCase(50.0, 101.0)]   // crossbar
        public void Miss_and_Frame_Hit_ignore_the_goalie(double x, double y)
        {
            foreach (FakeReach reach in new[] { FakeReach.InTheWay(), FakeReach.NeverReaches() })
            {
                var round = InWindow(reach, Fixed());
                round.SubmitShotAndWhistle(x, y, 100, false);
                Assert.AreEqual(ShotResultKind.Miss, round.LastResult.Value.Kind);
                Assert.AreEqual(0, reach.Asks.Count, "the goalie-reach port is not asked");
                Assert.IsNull(round.LastRace);
            }
        }

        [Test]
        public void The_port_is_asked_about_the_Clean_Set_up_the_Attack_Side_the_window_time_the_shot_start_and_the_Net_Face_point()
        {
            var settings = Fixed();
            var reach = FakeReach.NeverReaches();
            var round = InWindow(reach, settings, windowSeconds: 1.25, random: ScriptedRandom.Forcing(AttackSide.Left));
            round.SubmitShotAndWhistle(30, 70, 100, false);

            Assert.AreEqual(1, reach.Asks.Count);
            FakeReach.Ask ask = reach.Asks[0];
            Assert.AreEqual(SetUp.Clean, ask.SetUp);
            Assert.AreEqual(SetUp.Clean, round.SetUp);
            Assert.AreEqual(AttackSide.Left, ask.Side);
            Assert.AreEqual(1.25, ask.SecondsSinceWindowOpened, 1e-6);
            RinkPoint start = Breakaway.ShotStart(AttackSide.Left, 1.25, settings);
            Assert.AreEqual(0, RinkPoint.Distance(start, ask.ShotStart), 1e-6);
            Assert.AreEqual(0, RinkPoint.Distance(RinkPoint.OnNetFace(30, 70), ask.NetPoint), 1e-9);
        }

        [Test]
        public void The_race_publishes_the_shot_start_and_the_Set_up_spot_the_port_measured_so_the_goalie_is_shown_there()
        {
            var settings = Fixed();
            var reach = FakeReach.NeverReaches();
            var round = InWindow(reach, settings, windowSeconds: 1.25, random: ScriptedRandom.Forcing(AttackSide.Right));
            round.SubmitShotAndWhistle(60, 40, 100, false);

            FakeReach.Ask ask = reach.Asks[0];
            ShotRace race = round.LastRace.Value;
            Assert.AreEqual(0, RinkPoint.Distance(ask.ShotStart, race.ShotStart), 1e-12);
            RinkPoint portSpot = SetUps.Spot(ask.SetUp, ask.Side, ask.SecondsSinceWindowOpened, ask.ShotStart, settings);
            Assert.AreEqual(0, RinkPoint.Distance(portSpot, race.Spot), 1e-12);
            Assert.AreEqual(-settings.NormalDepthMetres, race.Spot.Z, 1e-12);
        }

        [Test]
        public void The_Flight_Time_runs_from_the_shot_start_to_where_the_puck_passes_the_goalie_at_his_depth()
        {
            var settings = Fixed();
            var round = InWindow(FakeReach.NeverReaches(), settings, windowSeconds: 0.5);
            round.SubmitShotAndWhistle(80, 30, 90, false);

            RinkPoint start = Breakaway.ShotStart(AttackSide.Middle, 0.5, settings);
            RinkPoint passing = RinkPoint.AtDepth(start, RinkPoint.OnNetFace(80, 30), -settings.NormalDepthMetres);
            Assert.AreEqual(-0.2, passing.Z, 1e-9);
            Assert.AreEqual(ShotFlight.Seconds(RinkPoint.Distance(start, passing), 90), round.LastRace.Value.FlightSeconds, 1e-9);
            Assert.Less(round.LastRace.Value.FlightSeconds, ShotFlight.Seconds(RinkPoint.Distance(start, RinkPoint.OnNetFace(80, 30)), 90));
        }

        [Test]
        public void The_Save_Reaction_is_the_Shot_points_when_gear_is_already_in_the_way()
        {
            // The port says the pads would win a race, but the glove-side high point's own reaction shows.
            var round = InWindow(new FakeReach(GoalieReachAnswer.AlreadyInTheWay(SaveReaction.Pads)), Fixed());
            round.SubmitShotAndWhistle(90, 85, 100, false);
            Assert.AreEqual(SaveReactions.For(90, 85), round.LastRace.Value.SaveReaction);
            Assert.AreEqual(SaveReaction.Glove, round.LastRace.Value.SaveReaction);
        }

        [TestCase(0.01, ShotResultKind.Save)]
        [TestCase(5.0, ShotResultKind.Goal)]
        public void Otherwise_the_Save_Reaction_is_the_quickest_gear_in_the_race_on_a_Save_and_on_a_Goal(double move, ShotResultKind kind)
        {
            var round = InWindow(new FakeReach(GoalieReachAnswer.Reaches(SaveReaction.Stick, move)), Fixed());
            round.SubmitShotAndWhistle(90, 85, 60, false); // slow enough that a 0.01 s move beats the Flight Time
            Assert.AreEqual(kind, round.LastResult.Value.Kind);
            Assert.AreEqual(SaveReaction.Stick, round.LastRace.Value.SaveReaction);
        }

        [Test]
        public void When_no_gear_can_reach_it_is_a_Goal_showing_the_Shot_points_reaction()
        {
            var round = InWindow(FakeReach.NeverReaches(), Fixed());
            round.SubmitShotAndWhistle(10, 20, 60, false);
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind);
            Assert.AreEqual(SaveReactions.For(10, 20), round.LastRace.Value.SaveReaction);
            Assert.IsTrue(double.IsPositiveInfinity(round.LastRace.Value.ReachSeconds));
        }

        [TestCase(0.0, 0.08)]
        [TestCase(0.5, 0.12)]
        [TestCase(0.75, 0.14)]
        public void The_Reaction_Time_wobbles_by_up_to_0_04_s_from_one_roll_after_the_post_side(double roll, double expected)
        {
            var settings = new RoundSettings { ReactionSeconds = 0.12, ReactionWobbleSeconds = 0.04 };
            var random = ScriptedRandom.Forcing(AttackSide.Right, wobble: roll);
            var round = InWindow(new FakeReach(GoalieReachAnswer.Reaches(SaveReaction.Blocker, 0.1)), settings, random: random);
            Assert.AreEqual(AttackSide.Right, round.AttackSide);
            Assert.AreEqual(expected, round.ReactionSeconds, Tol);
            round.SubmitShotAndWhistle(20, 80, 100, false);
            Assert.AreEqual(expected, round.LastRace.Value.ReactionSeconds, Tol);
            Assert.AreEqual(expected + 0.1, round.LastRace.Value.ReachSeconds, Tol);
        }

        [TestCase(0.0, 0.08)]
        [TestCase(0.75, 0.14)]
        public void The_goalie_starts_his_move_after_the_Reaction_Time_the_Round_rolled(double roll, double rolled)
        {
            var settings = new RoundSettings { ReactionSeconds = 0.12, ReactionWobbleSeconds = 0.04 };
            var random = ScriptedRandom.Forcing(AttackSide.Right, wobble: roll);
            var round = InWindow(new FakeReach(GoalieReachAnswer.Reaches(SaveReaction.Blocker, 0.1)), settings, random: random);
            round.SubmitShotAndWhistle(20, 80, 100, false);
            ShotRace race = round.LastRace.Value;
            Assert.AreEqual(0.0, Breakaway.GoalieSet(RoundPhase.ResultHold, rolled - 0.005, true, race.FlightSeconds, race), Tol, "still before it");
            Assert.Greater(Breakaway.GoalieSet(RoundPhase.ResultHold, rolled + 0.005, true, race.FlightSeconds, race), 0.0, "moving after it");
            Assert.AreEqual(1.0, Breakaway.GoalieSet(RoundPhase.ResultHold, rolled + 0.1, true, race.FlightSeconds, race), Tol, "there at the Reach Time");
        }

        // Roll order: each Shot start takes the post side, then one wobble roll; the Shot itself rolls nothing, whatever its
        // result. The next Shot's values are queued right after, so any extra roll would shift the next post side and wobble.
        [TestCase(50.0, 50.0, true)]     // Save
        [TestCase(50.0, 50.0, false)]    // Goal
        [TestCase(150.0, 50.0, false)]   // Miss
        [TestCase(-1.0, 50.0, false)]    // Frame Hit
        public void Each_Shot_takes_a_post_side_and_a_wobble_roll_at_its_start_and_none_for_its_result(double x, double y, bool saved)
        {
            // The old deck (Late Settle on): ScriptedRandom.DeckValues scripts it, so the second Shot is a Cheat with a post side.
            var settings = new RoundSettings { ReactionSeconds = 0.12, ReactionWobbleSeconds = 0.04, LateSettles = true };
            var values = new System.Collections.Generic.List<double>(ScriptedRandom.DeckValues(SetUpKind.Clean, SetUpKind.Cheat));
            values.AddRange(ScriptedRandom.SideValues(AttackSide.Left, AttackSide.Middle));
            values.AddRange(new[] { ScriptedRandom.PostValue(Post.Left), 0.0, ScriptedRandom.PostValue(Post.Right), 0.75 });
            var random = new ScriptedRandom(new[] { 0.5 }, values.ToArray());
            var reach = saved ? FakeReach.InTheWay() : FakeReach.NeverReaches();
            var round = InWindow(reach, settings, random: random);
            Assert.AreEqual(0.08, round.ReactionSeconds, Tol);
            round.SubmitShotAndWhistle(x, y, 100, false);
            round.Tick(round.ResultHoldLength);
            Assert.AreEqual(2, round.ShotNumber);
            Assert.AreEqual(AttackSide.Middle, round.AttackSide);
            Assert.AreEqual(SetUp.Cheat(Post.Right), round.SetUp);
            Assert.AreEqual(0.14, round.ReactionSeconds, Tol);
        }

        [Test]
        public void TooLate_takes_no_roll_beyond_the_Shot_start()
        {
            var settings = new RoundSettings { ReactionSeconds = 0.12, ReactionWobbleSeconds = 0.04 };
            var round = InWindow(FakeReach.InTheWay(), settings, random: ScriptedRandom.Picking(AttackSide.Left, AttackSide.Middle));
            round.Tick(settings.ShootingWindowSeconds);
            Assert.AreEqual(ShotResultKind.TooLate, round.LastResult.Value.Kind);
            Assert.IsNull(round.LastRace);
            round.Tick(settings.ResultHoldSeconds);
            Assert.AreEqual(AttackSide.Middle, round.AttackSide);
        }

        [Test]
        public void The_race_is_cleared_when_the_next_Shot_starts()
        {
            var settings = Fixed();
            var round = InWindow(FakeReach.InTheWay(), settings);
            round.SubmitShotAndWhistle(50, 50, 100, false);
            Assert.IsNotNull(round.LastRace);
            round.Tick(round.ResultHoldLength);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.IsNull(round.LastRace);
        }

        // The Goal-rate tool previews races with ShotRace.At; it must be the very race Round decides by.
        [Test]
        public void The_race_at_a_point_is_the_race_Round_decides_by()
        {
            var settings = new RoundSettings(); // with the Reaction Time wobble
            var reach = new FakeReach(GoalieReachAnswer.Reaches(SaveReaction.Blocker, 0.07));
            var round = InWindow(reach, settings, 1.7, ScriptedRandom.Forcing(AttackSide.Left));
            double since = round.PhaseSecondsElapsed;
            ShotRace preview = ShotRace.At(round.SetUp, round.AttackSide, since, 22, 64, 120, round.ReactionSeconds, reach, settings);
            round.SubmitShotAndWhistle(22, 64, 120, false);
            ShotRace race = round.LastRace.Value;
            Assert.AreEqual(race.FlightSeconds, preview.FlightSeconds, Tol);
            Assert.AreEqual(race.ReachSeconds, preview.ReachSeconds, Tol);
            Assert.AreEqual(race.SaveReaction, preview.SaveReaction);
            Assert.AreEqual(race.IsSave, preview.IsSave);
            Assert.AreEqual(race.Spot.Z, preview.Spot.Z, Tol);
        }
    }
}
