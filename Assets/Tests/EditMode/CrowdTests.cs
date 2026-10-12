using System;
using System.Linq;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class CrowdTests
    {
        private static readonly CrowdState[] States = (CrowdState[])Enum.GetValues(typeof(CrowdState));

        // ---- the seats: the lower stand behind the near net ----

        [Test]
        public void Seats_fill_every_lower_row_behind_the_near_net()
        {
            FanSeat[] seats = CrowdSeats.BehindNearNet();

            Assert.That(seats.Select(s => s.Row).Distinct().Count(), Is.EqualTo(RinkLayout.LowerRows));
            Assert.That(seats.Length, Is.EqualTo(RinkLayout.LowerRows * CrowdSeats.SeatsPerRowBehindNearNet));
            Assert.That(CrowdSeats.SeatsPerRowBehindNearNet, Is.EqualTo((int)(2 * RinkLayout.CornerX / CrowdSeats.SeatWidth)));
        }

        [Test]
        public void Seats_are_on_the_end_straight_behind_the_near_end_boards()
        {
            foreach (FanSeat seat in CrowdSeats.BehindNearNet())
            {
                Assert.That(Math.Abs(seat.X), Is.LessThanOrEqualTo(RinkLayout.CornerX), seat.ToString());
                Assert.That(seat.Z, Is.GreaterThan(RinkLayout.NearEndBoardsZ + RinkLayout.StandsFront), seat.ToString());
                Assert.That(seat.Z, Is.LessThan(RinkLayout.NearEndBoardsZ + RinkLayout.RibbonOffset), seat.ToString());
            }
        }

        [Test]
        public void Each_fan_sits_on_its_rows_tread_near_the_back_facing_the_rink()
        {
            foreach (FanSeat seat in CrowdSeats.BehindNearNet())
            {
                double front = RinkLayout.NearEndBoardsZ + RinkLayout.StandsFront + seat.Row * RinkLayout.RowDepth;
                Assert.That(seat.Y, Is.EqualTo(RinkLayout.StandsFrontHeight + seat.Row * RinkLayout.LowerRise).Within(1e-9));
                Assert.That(seat.Z, Is.GreaterThan(front + RinkLayout.RowDepth / 2).And.LessThan(front + RinkLayout.RowDepth));
                Assert.That(Math.Sin(seat.Yaw), Is.EqualTo(0).Within(1e-9)); // faces -z, toward the rink and the player
                Assert.That(Math.Cos(seat.Yaw), Is.EqualTo(-1).Within(1e-9));
            }
        }

        [Test]
        public void Seats_in_a_row_are_a_seat_width_apart_and_centred()
        {
            double[] xs = CrowdSeats.BehindNearNet().Where(s => s.Row == 3).Select(s => s.X).OrderBy(x => x).ToArray();
            for (int i = 1; i < xs.Length; i++) Assert.That(xs[i] - xs[i - 1], Is.EqualTo(CrowdSeats.SeatWidth).Within(1e-9));
            Assert.That(xs.First() + xs.Last(), Is.EqualTo(0).Within(1e-9));
        }

        [Test]
        public void Fans_behind_the_near_end_boards_are_in_the_Calm_Area_zone_like_the_Ribbon_Boards()
        {
            Assert.That(CrowdSeats.BehindNearNet().All(s => s.Zone == ArenaZone.CalmArea));
            Assert.That(CrowdSeats.ZoneAt(RinkLayout.NearEndBoardsZ + 1), Is.EqualTo(ArenaZone.CalmArea));
            Assert.That(CrowdSeats.ZoneAt(RinkLayout.NearEndBoardsZ - 1), Is.EqualTo(ArenaZone.Near));
            Assert.That(CrowdSeats.ZoneAt(RinkLayout.CentreLineZ - 1), Is.EqualTo(ArenaZone.Far));
        }

        // ---- what the fans in a zone do ----

        [Test]
        public void Still_fans_stop_moving_and_are_dimmer_than_any_other_state()
        {
            Assert.That(CrowdLooks.Motion(CrowdState.Still), Is.EqualTo(0));
            foreach (CrowdState state in States.Where(s => s != CrowdState.Still))
            {
                Assert.That(CrowdLooks.Motion(state), Is.GreaterThan(0), state.ToString());
                Assert.That(CrowdLooks.Light(CrowdState.Still), Is.LessThan(CrowdLooks.Light(state)), state.ToString());
            }
        }

        [Test]
        public void The_Calm_Area_crowd_is_still_from_goalie_set_to_the_Shot_and_moves_otherwise()
        {
            ArenaState window = ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.ShootingWindow, ShotNumber = 1 });
            ArenaState skateIn = ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.SkateIn, ShotNumber = 1 });

            Assert.That(CrowdLooks.Motion(window.CrowdIn(ArenaZone.CalmArea)), Is.EqualTo(0));
            Assert.That(CrowdLooks.Motion(skateIn.CrowdIn(ArenaZone.CalmArea)), Is.GreaterThan(0));
        }

        // ---- each fan's own look ----

        [Test]
        public void Fans_get_a_random_phase_and_about_four_in_ten_wear_the_home_jersey()
        {
            FanLook[] looks = CrowdLooks.Make(2000, new Random(7));

            Assert.That(looks.All(l => l.Phase >= 0 && l.Phase < 1));
            Assert.That(looks.Select(l => Math.Round(l.Phase, 2)).Distinct().Count(), Is.GreaterThan(90));
            double home = looks.Count(l => l.Shirt.Equals(CrowdLooks.HomeJersey)) / (double)looks.Length;
            Assert.That(home, Is.EqualTo(CrowdLooks.HomeShare).Within(0.05));
            Assert.That(CrowdLooks.HomeShare, Is.EqualTo(0.4));
        }

        [Test]
        public void The_rest_wear_dark_plain_clothes_darker_than_the_home_jersey()
        {
            foreach (FanLook look in CrowdLooks.Make(500, new Random(3)).Where(l => !l.Shirt.Equals(CrowdLooks.HomeJersey)))
            {
                Assert.That(CrowdLooks.DarkPlain, Does.Contain(look.Shirt));
                ArenaColour home = CrowdLooks.HomeJersey;
                Assert.That(look.Shirt.R + look.Shirt.G + look.Shirt.B, Is.LessThan(home.R + home.G + home.B));
            }
        }

        [Test]
        public void The_same_seed_gives_the_same_crowd()
        {
            FanLook[] a = CrowdLooks.Make(50, new Random(11)), b = CrowdLooks.Make(50, new Random(11));
            Assert.That(a.Select(l => l.Phase), Is.EqualTo(b.Select(l => l.Phase)));
            Assert.That(a.Select(l => l.Shirt), Is.EqualTo(b.Select(l => l.Shirt)));
        }
    }
}
