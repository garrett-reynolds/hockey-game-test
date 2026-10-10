using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The goalie squares up to the shooter: which way it faces, and how it turns there.</summary>
    public class GoalieTurnTests
    {
        const double Frame = 1.0 / 60.0;

        [TestCase(-2.7, 8.6, 17.4299)]   // the Left Attack Side stop, seen from the goalie 0.4 m out: turn to the goalie's right (+)
        [TestCase(2.7, 8.6, -17.4299)]
        [TestCase(0.0, 8.6, 0.0)]
        [TestCase(-1.0, 1.0, 30.0)]      // wider than the cap
        [TestCase(1.0, 1.0, -30.0)]
        public void The_goalie_faces_the_shooter_up_to_a_30_degree_cap(double right, double ahead, double expected)
        {
            Assert.AreEqual(30.0, GoalieTurn.MaxDegrees, 1e-9);
            Assert.AreEqual(expected, GoalieTurn.Facing(right, ahead), 1e-3);
        }

        [TestCase(AttackSide.Left)]
        [TestCase(AttackSide.Middle)]
        [TestCase(AttackSide.Right)]
        public void The_cap_never_stops_the_goalie_following_the_skate_in(AttackSide side)
        {
            for (double u = 0; u <= 1.0; u += 0.01)
            {
                Breakaway.SkatePoint(side, u, out double x, out double distance);
                Assert.Less(System.Math.Abs(GoalieTurn.Facing(x, distance - 0.4)), GoalieTurn.MaxDegrees, "pinned at the cap at u " + u);
            }
        }

        [Test]
        public void A_turn_is_smooth_with_no_frame_faster_than_the_top_turn_speed_and_it_settles_on_the_shooter()
        {
            var turn = new GoalieTurn(0.0);
            double last = 0.0, fastest = 0.0;
            for (int i = 0; i < 120; i++)
            {
                turn.Step(20.0, Frame, true);
                fastest = System.Math.Max(fastest, System.Math.Abs(turn.Shown - last) / Frame);
                last = turn.Shown;
            }
            Assert.LessOrEqual(fastest, GoalieTurn.TopSpeedDegrees + 1e-6, "no snap");
            Assert.Greater(fastest, 20.0, "it does turn");
            Assert.AreEqual(20.0, turn.Shown, 0.05, "settled within 2 s");
        }

        [Test]
        public void While_skating_in_the_goalie_shuffles_only_once_the_shooter_is_a_couple_of_degrees_off()
        {
            var turn = new GoalieTurn(10.0);
            for (int i = 0; i < 30; i++) turn.Step(11.0, Frame, false);
            Assert.AreEqual(10.0, turn.Shown, 1e-9, "1 degree off: stays put");

            for (int i = 0; i < 60; i++) turn.Step(13.0, Frame, false);
            Assert.AreEqual(13.0, turn.Shown, 0.25, "3 degrees off: shuffles round to face the shooter");

            for (int i = 0; i < 60; i++) turn.Step(14.0, Frame, true);
            Assert.AreEqual(14.0, turn.Shown, 0.05, "settling (slow-down and window): follows even a small change");
        }

        [Test]
        public void Reset_faces_the_shooter_at_once()
        {
            var turn = new GoalieTurn(0.0);
            turn.Reset(-24.0);
            Assert.AreEqual(-24.0, turn.Shown, 1e-9);
        }
    }
}
