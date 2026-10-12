using System;
using System.Collections.Generic;
using System.Linq;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class CrowdTests
    {
        private static readonly CrowdState[] States = (CrowdState[])Enum.GetValues(typeof(CrowdState));
        private static readonly FanSeat[] Seats = CrowdSeats.All();
        private static readonly int[][] Neighbours = CrowdSeats.Neighbours(Seats);
        private static readonly FanLook[] Looks = CrowdLooks.Make(Seats, Neighbours, new Random(95));

        private const double ZNear = RinkLayout.NearCornerZ, ZFar = RinkLayout.FarCornerZ;

        /// <summary>How far a point is out from the inside face of the boards (the rounded rectangle).</summary>
        private static double OutFromBoards(double x, double z)
        {
            double dx = Math.Max(Math.Abs(x) - RinkLayout.CornerX, 0), dz = Math.Max(Math.Max(z - ZNear, ZFar - z), 0);
            return Math.Sqrt(dx * dx + dz * dz) - RinkLayout.CornerRadius;
        }

        private static bool BesideAnOpening(FanSeat s) =>
            RinkLayout.Benches.Concat(RinkLayout.PenaltyBoxes).Any(o =>
                Math.Sign(s.X) == o.Side && Math.Abs(s.X) > RinkLayout.HalfWidth && s.Z >= o.FromZ && s.Z <= o.ToZ);

        // ---- the seats: every seat of both tiers, all round the bowl ----

        [Test]
        public void Every_row_of_both_tiers_has_fans_all_round_the_rink()
        {
            Assert.That(Seats.Select(s => s.Row).Distinct().Count(), Is.EqualTo(RinkLayout.LowerRows + RinkLayout.UpperRows));
            foreach (IGrouping<int, FanSeat> row in Seats.GroupBy(s => s.Row))
            {
                Assert.That(row.Any(s => s.Z > RinkLayout.NearEndBoardsZ && Math.Abs(s.X) < 1), "behind the near net, row " + row.Key);
                Assert.That(row.Any(s => s.Z < RinkLayout.FarEndBoardsZ && Math.Abs(s.X) < 1), "behind the far net, row " + row.Key);
                Assert.That(row.Any(s => s.X > RinkLayout.HalfWidth), "the +x side, row " + row.Key);
                Assert.That(row.Any(s => s.X < -RinkLayout.HalfWidth), "the -x side, row " + row.Key);
            }
            Assert.That(Seats.Length, Is.GreaterThan(13000), "a sold-out bowl");
        }

        [Test]
        public void Each_fan_sits_near_the_back_of_its_rows_tread_facing_the_rink()
        {
            foreach (FanSeat s in Seats)
            {
                bool upper = s.Row >= RinkLayout.LowerRows;
                int r = upper ? s.Row - RinkLayout.LowerRows : s.Row;
                double front = (upper ? RinkLayout.RibbonOffset : RinkLayout.StandsFront) + r * RinkLayout.RowDepth
                               + (BesideAnOpening(s) ? RinkLayout.BenchDepth : 0);
                double tread = upper ? RinkLayout.RibbonBottom + RinkLayout.RibbonHeight + r * RinkLayout.UpperRise
                                     : RinkLayout.StandsFrontHeight + r * RinkLayout.LowerRise;
                double o = OutFromBoards(s.X, s.Z);
                Assert.That(s.Y, Is.EqualTo(tread).Within(1e-9), s.ToString());
                Assert.That(o, Is.GreaterThan(front + RinkLayout.RowDepth / 2).And.LessThan(front + RinkLayout.RowDepth), s.ToString());

                // Faces straight in: toward the nearest point of the line between the corner centres.
                double cx = Math.Max(-RinkLayout.CornerX, Math.Min(RinkLayout.CornerX, s.X));
                double cz = Math.Max(ZFar, Math.Min(ZNear, s.Z));
                double ix = cx - s.X, iz = cz - s.Z, len = Math.Sqrt(ix * ix + iz * iz);
                Assert.That((Math.Sin(s.Yaw) * ix + Math.Cos(s.Yaw) * iz) / len, Is.GreaterThan(0.999), s.ToString());
            }
        }

        [Test]
        public void No_fan_sits_in_front_of_a_bench_or_penalty_box_or_on_the_step_beside_it()
        {
            foreach (FanSeat s in Seats.Where(s => s.Row == 0 && Math.Abs(s.X) > RinkLayout.HalfWidth))
                foreach (RinkLayout.Opening o in RinkLayout.Benches.Concat(RinkLayout.PenaltyBoxes))
                {
                    if (Math.Sign(s.X) != o.Side || s.Z < o.FromZ - CrowdSeats.OpeningStep || s.Z > o.ToZ + CrowdSeats.OpeningStep) continue;
                    Assert.That(s.Z, Is.InRange(o.FromZ, o.ToZ), "not on the step: " + s);
                    Assert.That(Math.Abs(s.X), Is.GreaterThan(RinkLayout.HalfWidth + RinkLayout.StandsFront + RinkLayout.BenchDepth), s.ToString());
                }
        }

        [Test]
        public void Fans_in_a_row_are_at_least_a_seat_width_apart()
        {
            foreach (IGrouping<int, FanSeat> row in Seats.GroupBy(s => s.Row))
            {
                FanSeat[] r = row.ToArray();
                for (int i = 0; i < r.Length; i++)
                {
                    FanSeat a = r[i], b = r[(i + 1) % r.Length];
                    double d = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
                    Assert.That(d, Is.GreaterThanOrEqualTo(CrowdSeats.SeatWidth - 1e-6), a + " / " + b);
                }
            }
        }

        [Test]
        public void Fans_behind_the_near_end_boards_are_in_the_Calm_Area_zone_like_the_Ribbon_Boards()
        {
            foreach (FanSeat s in Seats) Assert.That(s.Zone, Is.EqualTo(CrowdSeats.ZoneAt(s.Z)));
            Assert.That(CrowdSeats.ZoneAt(RinkLayout.NearEndBoardsZ + 1), Is.EqualTo(ArenaZone.CalmArea));
            Assert.That(CrowdSeats.ZoneAt(RinkLayout.NearEndBoardsZ - 1), Is.EqualTo(ArenaZone.Near));
            Assert.That(CrowdSeats.ZoneAt(RinkLayout.CentreLineZ - 1), Is.EqualTo(ArenaZone.Far));
            Assert.That(Seats.Count(s => s.Zone == ArenaZone.CalmArea), Is.GreaterThan(RinkLayout.LowerRows * 17));
        }

        [Test]
        public void Lower_tier_fans_in_the_near_half_are_near_fans_and_the_upper_tier_and_far_end_are_far_fans()
        {
            foreach (FanSeat s in Seats)
            {
                bool near = s.Row < RinkLayout.LowerRows && s.Z > RinkLayout.CentreLineZ;
                Assert.That(CrowdSeats.IsNearFan(s), Is.EqualTo(near), s.ToString());
            }
        }

        [Test]
        public void A_fans_neighbours_are_the_fans_beside_it_and_in_the_rows_in_front_and_behind()
        {
            int i = Array.FindIndex(Seats, s => s.Row == 5 && s.Z > RinkLayout.NearEndBoardsZ && Math.Abs(s.X) < 0.3);
            FanSeat me = Seats[i];
            int[] mine = Neighbours[i];
            Assert.That(mine.Count(j => Seats[j].Row == 5), Is.EqualTo(2), "left and right");
            Assert.That(mine.Any(j => Seats[j].Row == 4) && mine.Any(j => Seats[j].Row == 6), "in front and behind");
            Assert.That(mine.All(j => Math.Abs(Seats[j].Row - me.Row) <= 1));
            for (int a = 0; a < Neighbours.Length; a += 97)
                foreach (int b in Neighbours[a]) Assert.That(Neighbours[b], Does.Contain(a));
        }

        // ---- each fan's own look ----

        [Test]
        public void No_two_neighbouring_fans_look_alike()
        {
            for (int a = 0; a < Seats.Length; a++)
                foreach (int b in Neighbours[a])
                    Assert.That(Looks[a].Body, Is.Not.EqualTo(Looks[b].Body), Seats[a] + " / " + Seats[b]);
        }

        [Test]
        public void There_are_six_body_types_each_well_used_and_teens_are_shorter()
        {
            Assert.That(CrowdLooks.BodyTypes.Length, Is.EqualTo(6));
            for (int b = 0; b < 6; b++)
                Assert.That(Looks.Count(l => l.Body == b) / (double)Looks.Length, Is.GreaterThan(0.1), "body type " + b);
            double Height(int b) => Looks.Where(l => l.Body == b).Average(l => l.Height);
            BodyType[] types = CrowdLooks.BodyTypes;
            for (int b = 0; b < 6; b++)
                if (types[b].Name.StartsWith("teen"))
                    Assert.That(Height(b), Is.LessThan(Height(types.ToList().FindIndex(t => t.Name == (types[b].Base == 0 ? "man" : "woman")))));
            Assert.That(types.Select(t => (t.Base, t.Shape)).Distinct().Count(), Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void Fans_have_many_hair_styles()
        {
            int styles = Looks.Select(l => (CrowdLooks.BodyTypes[l.Body].Base, l.HairStyle)).Distinct().Count();
            Assert.That(styles, Is.GreaterThanOrEqualTo(10));
            Assert.That(Looks.Select(l => l.Hair).Distinct().Count(), Is.GreaterThanOrEqualTo(8), "hair and cap colours");
        }

        [Test]
        public void Fans_get_a_random_phase_and_about_four_in_ten_wear_the_home_jersey()
        {
            Assert.That(Looks.All(l => l.Phase >= 0 && l.Phase < 1));
            Assert.That(Looks.Select(l => Math.Round(l.Phase, 2)).Distinct().Count(), Is.GreaterThan(90));
            double home = Looks.Count(l => l.Shirt.Equals(CrowdLooks.HomeJersey)) / (double)Looks.Length;
            Assert.That(home, Is.EqualTo(CrowdLooks.HomeShare).Within(0.03));
            Assert.That(CrowdLooks.HomeShare, Is.EqualTo(0.4));
        }

        [Test]
        public void The_rest_wear_dark_plain_clothes_darker_than_the_home_jersey()
        {
            foreach (FanLook look in Looks.Where(l => !l.Shirt.Equals(CrowdLooks.HomeJersey)))
            {
                Assert.That(CrowdLooks.DarkPlain, Does.Contain(look.Shirt));
                ArenaColour home = CrowdLooks.HomeJersey;
                Assert.That(look.Shirt.R + look.Shirt.G + look.Shirt.B, Is.LessThan(home.R + home.G + home.B));
            }
        }

        [Test]
        public void Some_fans_wave_in_the_skate_in()
        {
            double share = Looks.Count(l => l.Waves) / (double)Looks.Length;
            Assert.That(share, Is.EqualTo(CrowdLooks.WaveShare).Within(0.03));
            Assert.That(CrowdLooks.WaveShare, Is.InRange(0.15, 0.5));
        }

        [Test]
        public void No_crowd_colour_is_a_reserved_colour()
        {
            var colours = new List<ArenaColour> { CrowdLooks.HomeJersey, CrowdLooks.Pants, CrowdLooks.Shoes };
            colours.AddRange(CrowdLooks.DarkPlain);
            colours.AddRange(CrowdLooks.Skins);
            colours.AddRange(CrowdLooks.Hairs);
            colours.AddRange(CrowdLooks.Caps);
            colours.AddRange(Looks.Select(l => l.Hair).Distinct());
            colours.Add(CrowdLooks.FlatTone);
            foreach (ArenaColour c in colours)
                foreach (ArenaColour r in ReservedColours.All)
                    Assert.That(ReservedColours.Distance(c, r), Is.GreaterThanOrEqualTo(ReservedColours.MinDistance), c + " near " + r);
        }

        [Test]
        public void The_same_seed_gives_the_same_crowd()
        {
            FanLook[] a = CrowdLooks.Make(Seats, Neighbours, new Random(11)), b = CrowdLooks.Make(Seats, Neighbours, new Random(11));
            Assert.That(a.Select(l => l.Phase), Is.EqualTo(b.Select(l => l.Phase)));
            Assert.That(a.Select(l => l.Shirt), Is.EqualTo(b.Select(l => l.Shirt)));
            Assert.That(a.Select(l => l.Body), Is.EqualTo(b.Select(l => l.Body)));
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
                Assert.That(CrowdLooks.Contrast(CrowdState.Still), Is.LessThan(CrowdLooks.Contrast(state)), state.ToString());
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

        private static readonly double[] ClipSeconds = { 4, 2, 2, 1.5, 1 };

        private static CrowdClip Settled(CrowdState state, double seconds = 2)
        {
            var m = new CrowdMotion(ClipSeconds);
            for (double t = 0; t < seconds; t += 0.05) m.Step(state, 0.05);
            return m.Clip;
        }

        private static ArenaState In(RoundPhase phase, ArenaResult result = ArenaResult.None) =>
            ArenaShow.At(new ArenaShowInput { Phase = phase, Result = result, ShotNumber = 1 });

        [Test]
        public void Each_Arena_Show_moment_plays_its_clip()
        {
            foreach (ArenaZone z in new[] { ArenaZone.CalmArea, ArenaZone.Near, ArenaZone.Far })
            {
                Assert.That(Settled(In(RoundPhase.SkateIn).CrowdIn(z)), Is.EqualTo(CrowdClip.Wave), "skate-in: sit with some hand waves");
                Assert.That(Settled(In(RoundPhase.ResultHold, ArenaResult.Goal).CrowdIn(z)), Is.EqualTo(CrowdClip.StandCheer), "Goal: all stand and cheer");
                Assert.That(Settled(In(RoundPhase.Idle).CrowdIn(z)), Is.EqualTo(CrowdClip.Clap), "start screen: clap with the chant");
                Assert.That(Settled(In(RoundPhase.RoundOver).CrowdIn(z)), Is.EqualTo(CrowdClip.Clap), "Round over: clap with the chant");
                Assert.That(Settled(In(RoundPhase.SlowDown).CrowdIn(z)), Is.EqualTo(CrowdClip.SitIdle));
            }
            var save = new CrowdMotion(ClipSeconds);
            save.Step(In(RoundPhase.ResultHold, ArenaResult.Save).CrowdIn(ArenaZone.Near), 0.1);
            Assert.That(save.Clip, Is.EqualTo(CrowdClip.Oh), "Save: near fans 'oh!'");
            Assert.That(Settled(In(RoundPhase.ResultHold, ArenaResult.FrameHitPost).CrowdIn(ArenaZone.Far), 0.1), Is.EqualTo(CrowdClip.SitIdle));
        }

        [Test]
        public void Near_fans_oh_once_then_sit()
        {
            var m = new CrowdMotion(ClipSeconds);
            m.Step(CrowdState.Oh, 0.0);
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.Oh));
            double t = 0;
            while (m.Clip == CrowdClip.Oh && t < 10) { m.Step(CrowdState.Oh, 0.05); t += 0.05; }
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.SitIdle));
            Assert.That(t, Is.EqualTo(ClipSeconds[(int)CrowdClip.Oh] + CrowdMotion.OhSpreadSeconds).Within(0.06));
            for (int i = 0; i < 100; i++) m.Step(CrowdState.Oh, 0.05);
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.SitIdle), "no second oh in the same result hold");
        }

        [Test]
        public void A_new_clip_fades_in_from_the_last()
        {
            var m = new CrowdMotion(ClipSeconds);
            m.Step(CrowdState.Sit, 0.0);
            for (int i = 0; i < 20; i++) m.Step(CrowdState.Sit, 0.05);
            m.Step(CrowdState.Cheer, 0.0);
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.StandCheer));
            Assert.That(m.FromClip, Is.EqualTo(CrowdClip.SitIdle));
            Assert.That(m.Blend, Is.EqualTo(0));
            m.Step(CrowdState.Cheer, CrowdMotion.FadeSeconds / 2);
            Assert.That(m.Blend, Is.EqualTo(0.5).Within(1e-9));
            m.Step(CrowdState.Cheer, CrowdMotion.FadeSeconds);
            Assert.That(m.Blend, Is.EqualTo(1));
        }

        [Test]
        public void Still_fans_hold_their_seated_pose_with_no_snap_at_goalie_set()
        {
            var m = new CrowdMotion(ClipSeconds);
            for (int i = 0; i < 30; i++) m.Step(CrowdState.Sit, 0.05);  // the slow-down
            double clock = m.Clock;
            m.Step(CrowdState.Still, 0.05);
            m.Step(CrowdState.Still, 0.2);
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.SitIdle));
            Assert.That(m.Clock, Is.EqualTo(clock), "the pose holds where it was");
            Assert.That(m.Blend, Is.EqualTo(1));
        }

        [Test]
        public void Still_from_any_other_clip_is_the_seated_pose()
        {
            var m = new CrowdMotion(ClipSeconds);
            m.Step(CrowdState.Clap, 0.0);
            m.Step(CrowdState.Clap, 0.3);
            m.Step(CrowdState.Still, 0.0);
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.SitIdle));
            Assert.That(m.Blend, Is.EqualTo(1), "no clap left in the pose");
            Assert.That(m.Clock, Is.EqualTo(0));
        }

        [Test]
        public void The_first_moment_starts_straight_in_its_clip()
        {
            var m = new CrowdMotion(ClipSeconds);
            m.Snap(CrowdState.Cheer);
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.StandCheer));
            Assert.That(m.Blend, Is.EqualTo(1));
        }

        [Test]
        public void Loop_clocks_stay_within_the_clip()
        {
            var m = new CrowdMotion(ClipSeconds);
            for (int i = 0; i < 2000; i++) m.Step(CrowdState.Clap, 0.05);
            Assert.That(m.Clock, Is.InRange(0, ClipSeconds[(int)CrowdClip.Clap]));
        }
    }
}
