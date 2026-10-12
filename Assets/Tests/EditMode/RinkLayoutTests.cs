using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class RinkLayoutTests
    {
        const double Cm = 0.01;
        const double Ft = 0.3048;

        [Test]
        public void The_rink_has_NHL_sizes()
        {
            Assert.AreEqual(60.96, RinkLayout.Length, Cm);
            Assert.AreEqual(25.91, RinkLayout.Width, Cm);
            Assert.AreEqual(25.91 / 2, RinkLayout.HalfWidth, Cm);
            Assert.AreEqual(8.53, RinkLayout.CornerRadius, Cm);
            Assert.AreEqual(3.35, RinkLayout.GoalLineFromEnd, Cm);
        }

        [Test]
        public void The_near_goal_line_stays_at_z_0_and_the_rink_grows_toward_the_player()
        {
            Assert.AreEqual(0.0, RinkLayout.NearGoalLineZ);
            Assert.AreEqual(3.35, RinkLayout.NearEndBoardsZ, Cm, "end boards behind the near net");
            Assert.AreEqual(-54.26, RinkLayout.FarGoalLineZ, Cm, "about -54.3 m");
            Assert.AreEqual(-57.61, RinkLayout.FarEndBoardsZ, Cm, "about -57.6 m");
            Assert.AreEqual(RinkLayout.Length, RinkLayout.NearEndBoardsZ - RinkLayout.FarEndBoardsZ, Cm);
        }

        [Test]
        public void The_lines_sit_at_NHL_places_and_the_far_end_mirrors_the_near_end()
        {
            Assert.AreEqual(-27.13, RinkLayout.CentreLineZ, Cm, "89 ft from each goal line");
            Assert.AreEqual(-19.51 - 0.15, RinkLayout.NearBlueLineZ, Cm, "near edge 64 ft out, 12 in wide");
            Assert.AreEqual(RinkLayout.Mirror(RinkLayout.NearBlueLineZ), RinkLayout.FarBlueLineZ, 1e-9);
            Assert.AreEqual(RinkLayout.FarGoalLineZ, RinkLayout.Mirror(RinkLayout.NearGoalLineZ), 1e-9);
            Assert.AreEqual(RinkLayout.FarEndBoardsZ, RinkLayout.Mirror(RinkLayout.NearEndBoardsZ), 1e-9);
            Assert.AreEqual(RinkLayout.CentreLineZ, RinkLayout.Mirror(RinkLayout.CentreLineZ), 1e-9);

            Assert.AreEqual(4.57, RinkLayout.CircleRadius, Cm, "15 ft faceoff and centre circles");
            Assert.AreEqual(6.71, RinkLayout.EndZoneDotX, Cm, "22 ft either side of the middle");
            Assert.AreEqual(-6.10, RinkLayout.NearEndZoneDotZ, Cm, "20 ft out from the goal line");
            Assert.AreEqual(RinkLayout.NearBlueLineZ - RinkLayout.WideLine / 2 - 1.52, RinkLayout.NearNeutralDotZ, Cm,
                "5 ft from the blue line, in the neutral zone");
            Assert.AreEqual(1.83, RinkLayout.CreaseRadius, Cm);
            Assert.AreEqual(1.22, RinkLayout.CreaseHalfWidth, Cm, "8 ft wide");
        }

        [Test]
        public void The_trapezoid_runs_from_6_71_m_at_the_goal_line_to_8_53_m_at_the_end_boards()
        {
            Assert.AreEqual(6.71, RinkLayout.TrapezoidGoalLineWidth, Cm);
            Assert.AreEqual(8.53, RinkLayout.TrapezoidEndBoardsWidth, Cm);
        }

        [Test]
        public void Boards_ads_and_glass_have_NHL_heights()
        {
            Assert.AreEqual(1.07, RinkLayout.BoardHeight, Cm);
            Assert.AreEqual(0.24, RinkLayout.AdBandBottom, Cm);
            Assert.AreEqual(0.97, RinkLayout.AdBandTop, Cm);
            Assert.AreEqual(1.07 + 2.44, RinkLayout.EndGlassTop, Cm);
            Assert.That(RinkLayout.SideGlassTop - RinkLayout.BoardHeight, Is.GreaterThanOrEqualTo(1.83 - 1e-9));
        }

        [Test]
        public void The_Calm_Area_is_as_wide_as_the_back_of_the_trapezoid_from_the_crease_front_to_the_end_boards()
        {
            Assert.AreEqual(4.27, RinkLayout.CalmAreaHalfWidth, Cm);
            Assert.AreEqual(RinkLayout.TrapezoidEndBoardsWidth / 2, RinkLayout.CalmAreaHalfWidth, Cm);
            Assert.AreEqual(-1.9, RinkLayout.CalmAreaFrontZ, Cm);
            Assert.AreEqual(RinkLayout.NearEndBoardsZ, RinkLayout.CalmAreaBackZ);
        }

        [Test]
        public void The_far_net_stands_on_the_far_goal_line_facing_the_near_net()
        {
            Assert.AreEqual(0.0, RinkLayout.FarNet.X);
            Assert.AreEqual(0.0, RinkLayout.FarNet.Y);
            Assert.AreEqual(RinkLayout.FarGoalLineZ, RinkLayout.FarNet.Z);
        }

        [Test]
        public void Benches_and_penalty_boxes_face_each_other_across_centre_ice()
        {
            Assert.AreEqual(2, RinkLayout.Benches.Length);
            Assert.AreEqual(2, RinkLayout.PenaltyBoxes.Length);
            int benchSide = RinkLayout.Benches[0].Side;
            foreach (RinkLayout.Opening o in RinkLayout.Benches) Assert.AreEqual(benchSide, o.Side, "benches on one side");
            foreach (RinkLayout.Opening o in RinkLayout.PenaltyBoxes) Assert.AreEqual(-benchSide, o.Side, "boxes on the other");
            foreach (RinkLayout.Opening o in RinkLayout.Benches.Concat(RinkLayout.PenaltyBoxes))
            {
                Assert.That(o.ToZ, Is.GreaterThan(o.FromZ));
                Assert.That(o.ToZ, Is.LessThan(RinkLayout.NearBlueLineZ), "near centre ice: in the neutral zone");
                Assert.That(o.FromZ, Is.GreaterThan(RinkLayout.FarBlueLineZ));
                Assert.That(o.Depth, Is.GreaterThan(1.0), "room to sit");
            }
            foreach (RinkLayout.Opening[] pair in new[] { RinkLayout.Benches, RinkLayout.PenaltyBoxes })
            {
                Assert.AreEqual(pair[0].FromZ, RinkLayout.Mirror(pair[1].ToZ), 1e-9, "one each side of the centre line");
                Assert.That(pair[0].ToZ, Is.LessThanOrEqualTo(pair[1].FromZ), "apart");
            }
        }

        [Test]
        public void The_board_outline_is_a_closed_rounded_rectangle()
        {
            double p = RinkLayout.BoardsPerimeter;
            Assert.AreEqual(2 * (RinkLayout.Length + RinkLayout.Width) - (8 - 2 * System.Math.PI) * RinkLayout.CornerRadius, p, 1e-6);
            for (int i = 0; i < 200; i++)
            {
                RinkLayout.BoardPoint b = RinkLayout.OnBoards(p * i / 200);
                // Every point is on the inside face: inside the box, and on a side or a corner arc.
                Assert.That(System.Math.Abs(b.X), Is.LessThanOrEqualTo(RinkLayout.HalfWidth + 1e-6));
                Assert.That(b.Z, Is.InRange(RinkLayout.FarEndBoardsZ - 1e-6, RinkLayout.NearEndBoardsZ + 1e-6));
                Assert.AreEqual(1.0, b.InX * b.InX + b.InZ * b.InZ, 1e-6, "unit normal into the rink");
                double sx = b.X + b.InX * 0.5, sz = b.Z + b.InZ * 0.5; // half a metre in, still on the ice
                Assert.That(System.Math.Abs(sx), Is.LessThan(RinkLayout.HalfWidth));
                Assert.That(sz, Is.InRange(RinkLayout.FarEndBoardsZ, RinkLayout.NearEndBoardsZ));
            }
            RinkLayout.BoardPoint a = RinkLayout.OnBoards(0), z = RinkLayout.OnBoards(p);
            Assert.AreEqual(a.X, z.X, 1e-6, "closed");
            Assert.AreEqual(a.Z, z.Z, 1e-6, "closed");
        }

        [Test]
        public void Ad_panels_run_all_round_the_boards_corners_included_with_soft_panels_only_behind_the_near_net()
        {
            RinkLayout.AdPanelSlot[] slots = RinkLayout.AdPanelSlots;
            Assert.AreEqual(RinkLayout.BoardsPerimeter, slots.Sum(s => s.Width), 0.01, "no gap round the boards");
            Assert.That(slots.Count(s => s.Corner), Is.GreaterThanOrEqualTo(4 * 4), "corners included");
            Assert.That(slots.Count(s => s.Z < RinkLayout.FarGoalLineZ), Is.GreaterThan(0), "far end too");
            foreach (RinkLayout.AdPanelSlot s in slots)
            {
                bool behindNearNet = s.Z > RinkLayout.NearGoalLineZ && System.Math.Abs(s.X) <= RinkLayout.CalmAreaHalfWidth;
                Assert.AreEqual(behindNearNet, s.CalmArea, s.ToString());
                Assert.That(s.Width, Is.InRange(1.0, 3.5));
            }
            Assert.That(slots.Count(s => s.CalmArea), Is.GreaterThan(0));
        }

        [Test]
        public void No_ice_logo_sits_in_an_end_zone_or_behind_a_goal_line()
        {
            Assert.That(RinkLayout.IceLogoSpots.Length, Is.GreaterThanOrEqualTo(1 + 2 + 4));
            foreach (RinkLayout.IceLogoSpot s in RinkLayout.IceLogoSpots)
            {
                Assert.That(s.Z + s.Height / 2, Is.LessThan(RinkLayout.NearBlueLineZ - RinkLayout.WideLine / 2), s.ToString());
                Assert.That(s.Z - s.Height / 2, Is.GreaterThan(RinkLayout.FarBlueLineZ + RinkLayout.WideLine / 2), s.ToString());
                Assert.That(System.Math.Abs(s.X) + s.Width / 2, Is.LessThan(RinkLayout.HalfWidth), s.ToString());
            }
            RinkLayout.IceLogoSpot centre = RinkLayout.IceLogoSpots[0];
            Assert.AreEqual(0.0, centre.X);
            Assert.AreEqual(RinkLayout.CentreLineZ, centre.Z);
            Assert.That(centre.Width / 2, Is.LessThan(RinkLayout.CircleRadius), "inside the centre circle");
        }

        [Test]
        public void The_Video_Board_hangs_behind_the_near_net_and_the_Centre_Board_over_centre_ice()
        {
            Assert.AreEqual(22.0, RinkLayout.VideoBoard.Z, 0.5);
            Assert.AreEqual(14.5, RinkLayout.VideoBoard.Y, 0.5);
            Assert.AreEqual(11.0, RinkLayout.VideoBoardWidth, 0.5);
            Assert.AreEqual(6.0, RinkLayout.VideoBoardHeight, 0.5);
            Assert.That(RinkLayout.VideoBoardTiltDegrees, Is.InRange(5.0, 25.0), "tilted toward the player");
            Assert.AreEqual(RinkLayout.CentreLineZ, RinkLayout.CentreBoard.Z);
            Assert.AreEqual(20.0, RinkLayout.CentreBoardBottom, 0.5);
        }

        /// <summary>Tools/rink_layout.py copies the numbers the Python art tools build to; they must match.</summary>
        [Test]
        public void The_Python_art_tools_use_the_same_numbers()
        {
            string text = File.ReadAllText("Tools/rink_layout.py");
            double Py(string name)
            {
                Match m = Regex.Match(text, "^" + name + @"\s*=\s*(-?[0-9.]+)", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, name + " missing from Tools/rink_layout.py");
                return double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            Assert.AreEqual(RinkLayout.HalfWidth, Py("HALF_WIDTH"), 1e-4);
            Assert.AreEqual(RinkLayout.CornerRadius, Py("CORNER_RADIUS"), 1e-4);
            Assert.AreEqual(RinkLayout.NearEndBoardsZ, Py("NEAR_END_BOARDS_Z"), 1e-4);
            Assert.AreEqual(RinkLayout.FarEndBoardsZ, Py("FAR_END_BOARDS_Z"), 1e-4);
            Assert.AreEqual(RinkLayout.CentreLineZ, Py("CENTRE_LINE_Z"), 1e-4);
            Assert.AreEqual(RinkLayout.NearBlueLineZ, Py("NEAR_BLUE_LINE_Z"), 1e-4);
            Assert.AreEqual(RinkLayout.TrapezoidGoalLineWidth, Py("TRAPEZOID_GOAL_LINE_WIDTH"), 1e-4);
            Assert.AreEqual(RinkLayout.TrapezoidEndBoardsWidth, Py("TRAPEZOID_END_BOARDS_WIDTH"), 1e-4);
            Assert.AreEqual(RinkLayout.BoardHeight, Py("BOARD_HEIGHT"), 1e-4);
            Assert.AreEqual(RinkLayout.EndGlassTop, Py("END_GLASS_TOP"), 1e-4);
            Assert.AreEqual(RinkLayout.SideGlassTop, Py("SIDE_GLASS_TOP"), 1e-4);
            Assert.AreEqual(RinkLayout.IceSize, Py("ICE_SIZE"), 1e-4);
            Assert.AreEqual(RinkLayout.IceCentreZ, Py("ICE_CENTRE_Z"), 1e-4);
            Assert.AreEqual(RinkLayout.Benches[0].Side, Py("BENCH_SIDE"), 1e-9);
            Assert.AreEqual(RinkLayout.BenchDepth, Py("BENCH_DEPTH"), 1e-4);
            Assert.AreEqual(RinkLayout.BenchLength, Py("BENCH_LENGTH"), 1e-4);
            Assert.AreEqual(RinkLayout.BenchGap, Py("BENCH_GAP"), 1e-4);
            Assert.AreEqual(RinkLayout.PenaltyBoxLength, Py("PENALTY_BOX_LENGTH"), 1e-4);
            Assert.AreEqual(RinkLayout.TimekeeperLength, Py("TIMEKEEPER_LENGTH"), 1e-4);
            Assert.AreEqual(RinkLayout.Benches[0].Side, RinkLayout.BenchSide);
        }
    }
}
