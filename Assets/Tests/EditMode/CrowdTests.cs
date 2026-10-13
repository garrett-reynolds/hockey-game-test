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
        /// <summary>The League Skin's home jersey.</summary>
        private static readonly ArenaColour HomeJersey = TeamSkin.League.HomeJerseyColour;
        private static readonly FanLook[] Looks = CrowdLooks.Make(Seats, Neighbours, new Random(95), HomeJersey);

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
            FanSeat[] everyone = CrowdSeats.AllIncludingHidden(); // the Video Board hides the fans straight behind it, in the rows level with it
            foreach (IGrouping<int, FanSeat> row in Seats.GroupBy(s => s.Row))
            {
                Assert.That(everyone.Any(s => s.Row == row.Key && s.Z > RinkLayout.NearEndBoardsZ && Math.Abs(s.X) < 1), "behind the near net, row " + row.Key);
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
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void No_two_neighbouring_fans_wear_the_same_outfit()
        {
            for (int a = 0; a < Seats.Length; a++)
                foreach (int b in Neighbours[a])
                    Assert.That(CrowdLooks.BodyTypes[Looks[a].Body].Base, Is.Not.EqualTo(CrowdLooks.BodyTypes[Looks[b].Body].Base), Seats[a] + " / " + Seats[b]);
        }

        [Test]
        public void Every_outfit_is_worn_all_round_the_bowl_and_teens_are_shorter()
        {
            Assert.That(CrowdLooks.BodyTypes.Select(t => t.Base).Distinct().Count(), Is.EqualTo(CrowdLooks.Outfits));
            for (int o = 0; o < CrowdLooks.Outfits; o++)
            {
                double share = Looks.Count(l => CrowdLooks.BodyTypes[l.Body].Base == o) / (double)Looks.Length;
                Assert.That(share, Is.GreaterThan(0.04), "outfit " + o);
                foreach (bool nearHalf in new[] { true, false })
                    Assert.That(Looks.Where((l, i) => (Seats[i].Z < RinkLayout.CentreLineZ) == nearHalf).Any(l => CrowdLooks.BodyTypes[l.Body].Base == o), "outfit " + o + (nearHalf ? " near half" : " far half"));
            }
            BodyType[] types = CrowdLooks.BodyTypes;
            for (int b = 0; b < types.Length; b++)
                if (types[b].Name.StartsWith("teen"))
                    Assert.That(types[b].MaxHeight, Is.LessThan(types.First(t => t.Base == types[b].Base && !t.Name.StartsWith("teen")).MinHeight), types[b].Name);
        }

        [Test]
        public void Fans_get_a_random_phase_and_about_a_third_wear_the_home_jersey()
        {
            Assert.That(Looks.All(l => l.Phase >= 0 && l.Phase < 1));
            Assert.That(Looks.Select(l => Math.Round(l.Phase, 2)).Distinct().Count(), Is.GreaterThan(90));
            double home = Looks.Count(l => l.TeamTop) / (double)Looks.Length;
            Assert.That(home, Is.EqualTo(CrowdLooks.HomeShare).Within(0.03));
            Assert.That(CrowdLooks.HomeShare, Is.InRange(0.3, 0.35));
        }

        [Test]
        public void About_a_third_wear_a_team_top_in_the_home_jersey_and_the_rest_their_outfits_own_colours()
        {
            double team = Looks.Count(l => l.TeamTop) / (double)Looks.Length;
            Assert.That(team, Is.EqualTo(CrowdLooks.HomeShare).Within(0.03));
            Assert.That(Looks.Where(l => l.TeamTop).All(l => l.Shirt.Equals(HomeJersey)));
        }

        [Test]
        public void An_outfit_colour_near_a_reserved_colour_is_nudged_to_a_safe_one_and_safe_ones_are_kept()
        {
            foreach (ArenaColour r in ReservedColours.All)
            {
                ArenaColour c = CrowdLooks.SafeOutfitColour(r);
                Assert.That(ReservedColours.IsSafe(c), r + " -> " + c);
            }
            var safe = new ArenaColour(40, 98, 62);
            Assert.That(CrowdLooks.SafeOutfitColour(safe), Is.EqualTo(safe));
            Assert.That(CrowdLooks.SafeOutfitColour(new ArenaColour(20, 20, 22)), Is.Not.EqualTo(new ArenaColour(20, 20, 22)), "puck-ish black moves");
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
            var colours = new List<ArenaColour>();
            // Every Team Skin's home jersey: the team-top fans wear it in the Calm Area. (The outfits' own colours go
            // through CrowdLooks.SafeOutfitColour; CrowdViewTests checks every baked palette.)
            TeamSkin[] skins = UnityEngine.Resources.LoadAll<TeamSkin>(TeamSkin.Folder);
            Assert.That(skins, Is.Not.Empty);
            foreach (TeamSkin skin in skins) colours.Add(skin.HomeJerseyColour);
            colours.AddRange(CrowdLooks.Skins);
            colours.AddRange(CrowdLooks.Hairs);
            colours.AddRange(Looks.Select(l => l.Hair).Distinct());
            colours.Add(CrowdLooks.FlatTone);
            foreach (ArenaColour c in colours)
            {
                Assert.That(ReservedColours.IsSafe(c), c + " is not safe");
                foreach (ArenaColour r in ReservedColours.All)
                    Assert.That(ReservedColours.Distance(c, r), Is.GreaterThanOrEqualTo(ReservedColours.MinDistance), c + " near " + r);
            }
        }

        [Test]
        public void The_same_seed_gives_the_same_crowd()
        {
            FanLook[] a = CrowdLooks.Make(Seats, Neighbours, new Random(11), HomeJersey), b = CrowdLooks.Make(Seats, Neighbours, new Random(11), HomeJersey);
            Assert.That(a.Select(l => l.Phase), Is.EqualTo(b.Select(l => l.Phase)));
            Assert.That(a.Select(l => l.Shirt), Is.EqualTo(b.Select(l => l.Shirt)));
            Assert.That(a.Select(l => l.Body), Is.EqualTo(b.Select(l => l.Body)));
            Assert.That(a.Select(l => l.GoalAct), Is.EqualTo(b.Select(l => l.GoalAct)));
            Assert.That(a.Select(l => l.OhBack), Is.EqualTo(b.Select(l => l.OhBack)));
        }

        [Test]
        public void On_a_Goal_about_one_fan_in_five_stays_seated_half_clapping_half_still_spread_round_the_bowl()
        {
            double Share(Func<FanLook, bool> p, IEnumerable<FanLook> ls) => ls.Count(p) / (double)ls.Count();
            bool Seated(FanLook l) => l.GoalAct == CrowdLooks.GoalAct.SeatedClap || l.GoalAct == CrowdLooks.GoalAct.SeatedStill;
            Assert.That(Share(Seated, Looks), Is.EqualTo(CrowdLooks.SeatedOnGoalShare).Within(0.02));
            Assert.That(Share(l => l.GoalAct == CrowdLooks.GoalAct.SeatedClap, Looks), Is.EqualTo(CrowdLooks.SeatedOnGoalShare / 2).Within(0.015));
            Assert.That(CrowdLooks.SeatedOnGoalShare, Is.InRange(0.15, 0.25));
            for (int sector = 0; sector < 8; sector++)
            {
                var part = Looks.Where((l, i) => (int)((Math.Atan2(Seats[i].Z - RinkLayout.CentreLineZ, Seats[i].X) + Math.PI) / (2 * Math.PI) * 8) % 8 == sector).ToList();
                Assert.That(Share(Seated, part), Is.InRange(0.1, 0.3), "part " + sector + " of the bowl");
            }
        }

        static CrowdClip InClapState(FanLook l) => CrowdMotion.FanClip(CrowdMotion.ClipFor(CrowdState.Clap), l.GoalAct, l.OhBack, l.Claps);

        [Test]
        public void In_the_Clap_state_the_fans_give_clappers_and_still_fans_near_the_clap_share_spread_round_the_bowl()
        {
            Assert.That(CrowdLooks.ClapShare, Is.InRange(0.5, 0.7));
            double Share(IEnumerable<FanLook> ls) => ls.Count(l => InClapState(l) == CrowdClip.Clap) / (double)ls.Count();
            Assert.That(Looks.All(l => InClapState(l) == CrowdClip.Clap || InClapState(l) == CrowdClip.SitIdle), "a fan claps or sits still");
            Assert.That(Share(Looks), Is.EqualTo(CrowdLooks.ClapShare).Within(0.02));
            for (int sector = 0; sector < 8; sector++)
            {
                var part = Looks.Where((l, i) => (int)((Math.Atan2(Seats[i].Z - RinkLayout.CentreLineZ, Seats[i].X) + Math.PI) / (2 * Math.PI) * 8) % 8 == sector).ToList();
                Assert.That(Share(part), Is.InRange(0.45, 0.75), "part " + sector + " of the bowl");
            }
        }

        [Test]
        public void A_fans_clap_pick_is_the_same_every_frame_and_with_the_same_seed()
        {
            FanLook[] again = CrowdLooks.Make(Seats, Neighbours, new Random(95), HomeJersey);
            Assert.That(again.Select(l => l.Claps), Is.EqualTo(Looks.Select(l => l.Claps)));
            var motion = new CrowdMotion(new double[] { 2, 2, 2, 2, 1, 1, 2, 1 });
            motion.Snap(CrowdState.Clap);
            var first = Looks.Select(InClapState).ToArray();
            Assert.That(first.Distinct().Count(), Is.EqualTo(2), "some fans clap and some sit still, so a flicker would show");
            for (int frame = 0; frame < 120; frame++)
            {
                motion.Step(CrowdState.Clap, 1 / 60.0);
                for (int i = 0; i < Looks.Length; i += 7)
                    Assert.That(CrowdMotion.FanClip(motion.Clip, Looks[i].GoalAct, Looks[i].OhBack, Looks[i].Claps), Is.EqualTo(first[i]), "fan " + i + " at frame " + frame);
            }
        }

        [Test]
        public void The_fans_who_clap_do_not_all_clap_in_step()
        {
            // A regression pin: passes on the old code too (every fan already plays its clip at its own Phase); it guards the clappers' offsets.
            double[] phases = Looks.Where(l => InClapState(l) == CrowdClip.Clap).Select(l => l.Phase).ToArray();
            Assert.That(phases.Select(p => Math.Round(p, 2)).Distinct().Count(), Is.GreaterThan(80), "clappers' own offsets in the clip");
            for (int tenth = 0; tenth < 10; tenth++)
                Assert.That(phases.Count(p => (int)(p * 10) == tenth), Is.GreaterThan(phases.Length / 20), "clappers in tenth " + tenth + " of the loop");
        }

        [Test]
        public void Not_clapping_changes_only_the_Clap_clip()
        {
            Assert.That(CrowdMotion.FanClip(CrowdClip.Clap, CrowdLooks.GoalAct.FistPumpLeft, false, false), Is.EqualTo(CrowdClip.SitIdle));
            Assert.That(CrowdMotion.FanClip(CrowdClip.Clap, CrowdLooks.GoalAct.FistPumpLeft, false, true), Is.EqualTo(CrowdClip.Clap));
            Assert.That(CrowdMotion.FanClip(CrowdClip.StandCheer, CrowdLooks.GoalAct.SeatedClap, false, false), Is.EqualTo(CrowdClip.Clap), "a Goal's seated clap stays");
            Assert.That(CrowdMotion.FanClip(CrowdClip.StandCheer, CrowdLooks.GoalAct.FistPumpLeft, false, false), Is.EqualTo(CrowdClip.StandCheer));
            Assert.That(CrowdMotion.FanClip(CrowdClip.Oh, CrowdLooks.GoalAct.SeatedClap, true, false), Is.EqualTo(CrowdClip.OhBack));
            foreach (CrowdClip c in new[] { CrowdClip.SitIdle, CrowdClip.Wave, CrowdClip.Miss })
                Assert.That(CrowdMotion.FanClip(c, CrowdLooks.GoalAct.SeatedClap, false, false), Is.EqualTo(c), c.ToString());
        }

        [Test]
        public void Standing_fans_pump_left_and_right_fists_about_equally()
        {
            int left = Looks.Count(l => l.GoalAct == CrowdLooks.GoalAct.FistPumpLeft);
            int right = Looks.Count(l => l.GoalAct == CrowdLooks.GoalAct.FistPumpRight);
            Assert.That(left / (double)(left + right), Is.InRange(0.45, 0.55));
        }

        [Test]
        public void Each_fan_plays_its_own_goal_act_and_oh_and_the_zone_clip_otherwise()
        {
            Assert.That(CrowdMotion.FanClip(CrowdClip.StandCheer, CrowdLooks.GoalAct.FistPumpLeft, false, true), Is.EqualTo(CrowdClip.StandCheer));
            Assert.That(CrowdMotion.FanClip(CrowdClip.StandCheer, CrowdLooks.GoalAct.FistPumpRight, false, true), Is.EqualTo(CrowdClip.StandCheerRight));
            Assert.That(CrowdMotion.FanClip(CrowdClip.StandCheer, CrowdLooks.GoalAct.SeatedClap, true, true), Is.EqualTo(CrowdClip.Clap));
            Assert.That(CrowdMotion.FanClip(CrowdClip.StandCheer, CrowdLooks.GoalAct.SeatedStill, true, true), Is.EqualTo(CrowdClip.SitIdle));
            Assert.That(CrowdMotion.FanClip(CrowdClip.Oh, CrowdLooks.GoalAct.SeatedClap, true, true), Is.EqualTo(CrowdClip.OhBack));
            Assert.That(CrowdMotion.FanClip(CrowdClip.Oh, CrowdLooks.GoalAct.FistPumpLeft, false, true), Is.EqualTo(CrowdClip.Oh));
            foreach (CrowdClip c in new[] { CrowdClip.SitIdle, CrowdClip.Wave, CrowdClip.Clap, CrowdClip.Miss })
                for (int act = 0; act < 4; act++)
                    Assert.That(CrowdMotion.FanClip(c, act, act % 2 == 0, true), Is.EqualTo(c), c + " act " + act);
        }

        [Test]
        public void A_fans_own_clip_fits_its_zones_loop_clock()
        {
            // A seated fan plays clap or sit-idle on the cheer's clock: both must loop a whole number of times in it.
            var m = new CrowdMotion(ClipSeconds);
            foreach (CrowdClip c in new[] { CrowdClip.StandCheerRight, CrowdClip.Clap, CrowdClip.SitIdle })
            {
                double fits = m.LoopPeriod / ClipSeconds[(int)c];
                Assert.That(fits, Is.EqualTo(Math.Round(fits)).Within(1e-6), c.ToString());
            }
            Assert.That(ClipSeconds[(int)CrowdClip.OhBack], Is.EqualTo(ClipSeconds[(int)CrowdClip.Oh]));
        }

        [Test]
        public void About_three_in_ten_fans_oh_with_the_head_back()
        {
            Assert.That(Looks.Count(l => l.OhBack) / (double)Looks.Length, Is.EqualTo(CrowdLooks.OhBackShare).Within(0.03));
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

        private static readonly double[] ClipSeconds = { 2, 2, 1, 1.5, 1, 20 / 12.0, 1, 1.5 };

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
                var miss = new CrowdMotion(ClipSeconds);
                miss.Step(In(RoundPhase.ResultHold, ArenaResult.WideMiss).CrowdIn(z), 0.1);
                Assert.That(miss.Clip, Is.EqualTo(CrowdClip.Miss), "Miss: a frustrated swipe, " + z);
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
        public void On_a_Miss_fans_swipe_once_then_sit()
        {
            var m = new CrowdMotion(ClipSeconds);
            m.Step(CrowdState.Miss, 0.0);
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.Miss));
            double t = 0;
            while (m.Clip == CrowdClip.Miss && t < 10) { m.Step(CrowdState.Miss, 0.05); t += 0.05; }
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.SitIdle));
            Assert.That(t, Is.EqualTo(ClipSeconds[(int)CrowdClip.Miss] + CrowdMotion.OhSpreadSeconds).Within(0.06));
            for (int i = 0; i < 100; i++) m.Step(CrowdState.Miss, 0.05);
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.SitIdle), "no second swipe in the same result hold");
        }

        [Test]
        public void A_Miss_after_an_Oh_still_swipes()
        {
            var m = new CrowdMotion(ClipSeconds);
            for (int i = 0; i < 100; i++) m.Step(CrowdState.Oh, 0.05);
            m.Step(CrowdState.Sit, 0.05);
            m.Step(CrowdState.Miss, 0.05);
            Assert.That(m.Clip, Is.EqualTo(CrowdClip.Miss));
        }

        [Test]
        public void Once_clips_do_not_loop_and_the_rest_do()
        {
            Assert.That(Enum.GetValues(typeof(CrowdClip)).Cast<CrowdClip>().Where(c => !CrowdMotion.Loops(c)),
                Is.EquivalentTo(new[] { CrowdClip.Oh, CrowdClip.Miss, CrowdClip.OhBack }));
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
        public void Fans_who_do_not_wave_sit_on_smoothly_through_the_skate_in()
        {
            // The zone's one clock drives the wave and, for the fans who do not wave, sit-idle: it must never jump in
            // either clip's loop.
            var m = new CrowdMotion(ClipSeconds);
            m.Snap(CrowdState.Wave);
            double sitIdle = ClipSeconds[(int)CrowdClip.SitIdle], wave = ClipSeconds[(int)CrowdClip.Wave];
            double lastSit = 0, lastWave = 0;
            for (int i = 0; i < 400; i++)
            {
                m.Step(CrowdState.Wave, 0.05);
                double sit = m.Clock / sitIdle % 1, w = m.Clock / wave % 1;
                double ds = (sit - lastSit + 1) % 1, dw = (w - lastWave + 1) % 1;
                Assert.That(ds, Is.EqualTo(0.05 / sitIdle).Within(1e-6), "step " + i);
                Assert.That(dw, Is.EqualTo(0.05 / wave).Within(1e-6), "step " + i);
                lastSit = sit; lastWave = w;
            }
        }

        [Test]
        public void Every_loop_fits_a_whole_number_of_times_in_the_clock_period()
        {
            Assert.That(() => new CrowdMotion(new double[] { 2, 3, 1, 1.5, 1, 20 / 12.0, 1, 1.5 }), Throws.ArgumentException);
            Assert.That(new CrowdMotion(ClipSeconds).LoopPeriod, Is.EqualTo(2));
        }

        [Test]
        public void Loop_clocks_stay_within_the_loop_period()
        {
            var m = new CrowdMotion(ClipSeconds);
            for (int i = 0; i < 2000; i++) m.Step(CrowdState.Clap, 0.05);
            Assert.That(m.Clock, Is.InRange(0, m.LoopPeriod));
        }
    }
}
