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

        private static Round RoundInWindow(IRandomSource random)
        {
            var round = new Round(random, new RoundSettings(), FakeReach.NeverReaches(), 0);
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

        // ---- Through Round: the Goalie Pose now only follows the Attack Side (the 2-2-2 pick, see RoundDecksTests) until it goes ----

        [Test]
        public void Round_shows_the_first_pose_of_the_Shot_s_Attack_Side()
        {
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            {
                var round = RoundInWindow(ScriptedRandom.Forcing(side));
                Assert.AreEqual(side, round.AttackSide);
                Assert.AreEqual(GoaliePoses.PosesOf(side)[0], round.GoaliePose);
            }
        }
    }
}
