using System;
using System.Collections.Generic;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class RoundZoneTests
    {
        // Expected tables, written exactly as the spec shows them: TOP ROW FIRST, O/C/X.
        private static readonly Dictionary<GoaliePose, string[]> Expected = new Dictionary<GoaliePose, string[]>
        {
            { GoaliePose.LeftPostHug,     new[] { "XCO", "XXC", "XXX" } },
            { GoaliePose.LeftButterfly,   new[] { "XOO", "XCC", "XXX" } },
            { GoaliePose.MiddleButterfly, new[] { "OXO", "CXC", "XXX" } },
            { GoaliePose.MiddleStandUp,   new[] { "XXX", "XXX", "COC" } },
            // RIGHT 1 and RIGHT 2: left and right columns swapped, written out by hand.
            { GoaliePose.RightPostHug,    new[] { "OCX", "CXX", "XXX" } },
            { GoaliePose.RightButterfly,  new[] { "OOX", "CCX", "XXX" } },
        };

        // Test-only conversion: grid[0] is the TOP row, so row r (0 = bottom) is grid[2 - r].
        private static ZoneCover ExpectedCover(string[] grid, int column, int row)
        {
            char c = grid[2 - row][column];
            return c == 'O' ? ZoneCover.Open : c == 'C' ? ZoneCover.Contested : ZoneCover.Covered;
        }

        private static readonly GoaliePose[] AllPoses =
        {
            GoaliePose.LeftPostHug, GoaliePose.LeftButterfly, GoaliePose.MiddleButterfly,
            GoaliePose.MiddleStandUp, GoaliePose.RightPostHug, GoaliePose.RightButterfly
        };

        // A point in the middle of a Zone.
        private static double Centre(int index) => 100.0 / 6 + index * (100.0 / 3);

        private static Round RoundInWindow(IRandomSource random)
        {
            var round = new Round(random, new RoundSettings(), 0);
            round.StartRound();
            round.Tick(3.7);
            return round;
        }

        // ---- Zone edges ----

        [TestCase(0.0, 0)]
        [TestCase(33.33, 0)]
        [TestCase(100.0 / 3, 1)]
        [TestCase(50.0, 1)]
        [TestCase(66.66, 1)]
        [TestCase(200.0 / 3, 2)]
        [TestCase(100.0, 2)]
        public void Zone_column_and_row_follow_the_third_lines(double value, int expected)
        {
            Assert.AreEqual(expected, Zones.Column(value));
            Assert.AreEqual(expected, Zones.Row(value));
        }

        // ---- Pose table ----

        [Test]
        public void Every_pose_has_the_exact_table_for_all_9_Zones()
        {
            foreach (var pose in AllPoses)
                for (int column = 0; column < 3; column++)
                    for (int row = 0; row < 3; row++)
                        Assert.AreEqual(ExpectedCover(Expected[pose], column, row),
                            GoaliePoses.CoverAt(pose, column, row),
                            pose + " column " + column + " row " + row);
        }

        [Test]
        public void Right_poses_are_the_mirror_of_the_Left_poses()
        {
            for (int p = 0; p < 2; p++)
            {
                var left = (GoaliePose)p;
                var right = (GoaliePose)(4 + p);
                for (int column = 0; column < 3; column++)
                    for (int row = 0; row < 3; row++)
                        Assert.AreEqual(GoaliePoses.CoverAt(left, column, row),
                            GoaliePoses.CoverAt(right, 2 - column, row),
                            left + " column " + column + " row " + row);
            }
        }

        [Test]
        public void Attack_Sides_own_two_poses_each_pose_1_first()
        {
            CollectionAssert.AreEqual(new[] { GoaliePose.LeftPostHug, GoaliePose.LeftButterfly }, GoaliePoses.PosesOf(AttackSide.Left));
            CollectionAssert.AreEqual(new[] { GoaliePose.MiddleButterfly, GoaliePose.MiddleStandUp }, GoaliePoses.PosesOf(AttackSide.Middle));
            CollectionAssert.AreEqual(new[] { GoaliePose.RightPostHug, GoaliePose.RightButterfly }, GoaliePoses.PosesOf(AttackSide.Right));
            Assert.AreEqual(AttackSide.Left, GoaliePoses.SideOf(GoaliePose.LeftButterfly));
            Assert.AreEqual(AttackSide.Middle, GoaliePoses.SideOf(GoaliePose.MiddleStandUp));
            Assert.AreEqual(AttackSide.Right, GoaliePoses.SideOf(GoaliePose.RightPostHug));
        }

        // ---- Contested chance ----

        [TestCase(0.0, 0.50)]
        [TestCase(60.0, 0.675)]
        [TestCase(120.0, 0.85)]
        [TestCase(200.0, 0.85)]
        [TestCase(-30.0, 0.50)]
        public void Contested_chance_runs_linearly_from_low_to_top_speed(double speed, double expected)
        {
            Assert.AreEqual(expected, GoaliePoses.ContestedChance(speed, new RoundSettings()), 1e-9);
        }

        // ---- Through Round ----

        [Test]
        public void A_Shot_in_every_Open_Zone_scores_and_in_every_Covered_Zone_is_saved_for_every_pose()
        {
            foreach (var pose in AllPoses)
                for (int column = 0; column < 3; column++)
                    for (int row = 0; row < 3; row++)
                    {
                        var cover = ExpectedCover(Expected[pose], column, row);
                        if (cover == ZoneCover.Contested)
                            continue;
                        foreach (double speed in new[] { 0.0, 60.0, 120.0 })
                        {
                            var round = RoundInWindow(ScriptedRandom.Forcing(pose));
                            Assert.AreEqual(pose, round.GoaliePose);
                            round.SubmitShot(Centre(column), Centre(row), speed, false);
                            var expected = cover == ZoneCover.Open ? ShotResultKind.Goal : ShotResultKind.Save;
                            Assert.AreEqual(expected, round.LastResult.Value.Kind,
                                pose + " column " + column + " row " + row + " speed " + speed);
                        }
                    }
        }

        [Test]
        public void Round_reports_the_forced_pose_and_its_Attack_Side()
        {
            foreach (var pose in AllPoses)
            {
                var round = RoundInWindow(ScriptedRandom.Forcing(pose));
                Assert.AreEqual(pose, round.GoaliePose);
                Assert.AreEqual(GoaliePoses.SideOf(pose), round.AttackSide);
            }
        }

        // LeftButterfly column 1 row 1 is Contested; at 60 km/h the chance is 0.675.
        [TestCase(0.674, ShotResultKind.Goal)]
        [TestCase(0.676, ShotResultKind.Save)]
        public void A_Contested_Shot_scores_when_the_roll_is_below_the_chance(double roll, ShotResultKind expected)
        {
            var round = RoundInWindow(ScriptedRandom.Forcing(GoaliePose.LeftButterfly, roll));
            Assert.AreEqual(GoaliePose.LeftButterfly, round.GoaliePose);
            round.SubmitShot(50, 50, 60, false);
            Assert.AreEqual(expected, round.LastResult.Value.Kind);
        }

        // Random call order: one roll for a Contested Shot, none for Open, Covered or Miss. The next
        // Shot's pick values are queued right after, so a wrong roll count would shift the next pose.
        [TestCase(50.0, 50.0, ShotResultKind.Save, 1)]   // Contested, roll 0.9 -> Save
        [TestCase(50.0, 90.0, ShotResultKind.Goal, 0)]   // Open
        [TestCase(10.0, 10.0, ShotResultKind.Save, 0)]   // Covered
        [TestCase(150.0, 50.0, ShotResultKind.Miss, 0)]  // off the Net Face
        public void Rolls_are_used_only_by_Contested_Shots(double x, double y, ShotResultKind expected, int rolls)
        {
            var queued = new double[rolls];
            for (int i = 0; i < rolls; i++)
                queued[i] = 0.9;
            var round = RoundInWindow(ScriptedRandom.ForcingThen(GoaliePose.LeftButterfly, queued, GoaliePose.MiddleStandUp));
            Assert.AreEqual(GoaliePose.LeftButterfly, round.GoaliePose);
            round.SubmitShot(x, y, 100, false);
            Assert.AreEqual(expected, round.LastResult.Value.Kind);
            round.Tick(2.5);
            Assert.AreEqual(GoaliePose.MiddleStandUp, round.GoaliePose);
        }

        [Test]
        public void TooLate_uses_no_roll()
        {
            var round = RoundInWindow(ScriptedRandom.ForcingThen(GoaliePose.LeftButterfly, new double[0], GoaliePose.MiddleStandUp));
            round.Tick(3.0);
            Assert.AreEqual(ShotResultKind.TooLate, round.LastResult.Value.Kind);
            round.Tick(2.5);
            Assert.AreEqual(GoaliePose.MiddleStandUp, round.GoaliePose);
        }

        [Test]
        public void The_pose_is_picked_per_Shot_and_stays_the_same_through_the_Shot()
        {
            var round = new Round(ScriptedRandom.Picking(GoaliePose.RightPostHug, GoaliePose.MiddleButterfly), new RoundSettings(), 0);
            round.StartRound();
            Assert.AreEqual(GoaliePose.RightPostHug, round.GoaliePose);
            round.Tick(9.0);
            Assert.AreEqual(GoaliePose.RightPostHug, round.GoaliePose);
            round.Tick(0.3);
            Assert.AreEqual(2, round.ShotNumber);
            Assert.AreEqual(GoaliePose.MiddleButterfly, round.GoaliePose);
        }

        [Test]
        public void A_repeat_of_the_previous_pose_is_picked_again()
        {
            var round = new Round(
                ScriptedRandom.Picking(GoaliePose.LeftPostHug, GoaliePose.LeftPostHug, GoaliePose.RightButterfly),
                new RoundSettings(), 0);
            round.StartRound();
            Assert.AreEqual(GoaliePose.LeftPostHug, round.GoaliePose);
            round.Tick(9.3);
            Assert.AreEqual(GoaliePose.RightButterfly, round.GoaliePose);
        }

        [TestCase(GoaliePose.MiddleStandUp, GoaliePose.RightPostHug)]
        [TestCase(GoaliePose.RightButterfly, GoaliePose.LeftPostHug)] // wraps
        public void A_source_that_only_repeats_the_previous_pose_moves_to_the_next_pose_in_order(GoaliePose stuck, GoaliePose next)
        {
            var round = new Round(new ScriptedRandom(ScriptedRandom.PickValues(stuck)), new RoundSettings(), 0);
            round.StartRound();
            Assert.AreEqual(stuck, round.GoaliePose); // first Shot: any pose
            round.Tick(9.3);
            Assert.AreEqual(next, round.GoaliePose);
        }

        [Test]
        public void The_first_Shot_of_a_new_Round_has_no_previous_pose()
        {
            var pose = GoaliePose.LeftPostHug;
            var round = new Round(new ScriptedRandom(ScriptedRandom.PickValues(pose)), new RoundSettings(), 0);
            round.StartRound();
            Assert.AreEqual(pose, round.GoaliePose);
            round.Tick(1000);
            round.StartRound();
            Assert.AreEqual(pose, round.GoaliePose);
        }

        private sealed class SeededRandom : IRandomSource
        {
            private readonly Random _random;
            public SeededRandom(int seed) { _random = new Random(seed); }
            public double Next() => _random.NextDouble();
        }

        [Test]
        public void With_a_seeded_source_the_same_pose_never_comes_twice_in_a_row_and_every_pose_appears()
        {
            var seen = new HashSet<GoaliePose>();
            int shots = 0;
            for (int seed = 1; seed <= 120; seed++)
            {
                var round = new Round(new SeededRandom(seed), new RoundSettings(), 0);
                round.StartRound();
                GoaliePose? previous = null;
                for (int shot = 1; shot <= Round.ShotsPerRound; shot++)
                {
                    if (previous != null)
                        Assert.AreNotEqual(previous.Value, round.GoaliePose, "seed " + seed + " shot " + shot);
                    seen.Add(round.GoaliePose);
                    previous = round.GoaliePose;
                    shots++;
                    round.Tick(9.3);
                }
            }
            Assert.GreaterOrEqual(shots, 600);
            Assert.AreEqual(6, seen.Count);
        }
    }
}
