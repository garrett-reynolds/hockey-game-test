using System.Collections.Generic;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class RoundSoundCuesTests
    {
        private const double Frame = 0.05;
        private static readonly RoundSettings Defaults = new RoundSettings();

        private static Round StartedRound(int best = 0)
        {
            // Saved below half height, scored above it (the race itself: RoundReachTests).
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.Saving((x, y) => y < 50), best);
            round.StartRound();
            return round;
        }

        // Ticks the Round frame by frame for the given real seconds, collecting every cue.
        private static List<SoundCue> Run(Round round, RoundSoundCues cues, double seconds)
        {
            var heard = new List<SoundCue>();
            for (double t = 0; t < seconds - 1e-9; t += Frame)
            {
                round.Tick(Frame);
                heard.AddRange(cues.Update(round));
            }
            return heard;
        }

        [Test]
        public void Shooting_window_ticks_each_second_then_faster_in_the_last_second()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            var heard = Run(round, cues, 3.7 + 3.0 + 0.1); // skate-in, slow-down, the whole window (no Shot)
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            var ticks = heard.FindAll(c => c == SoundCue.Tick || c == SoundCue.FastTick);
            CollectionAssert.AreEqual(
                new[] { SoundCue.Tick, SoundCue.Tick, SoundCue.FastTick, SoundCue.FastTick, SoundCue.FastTick, SoundCue.FastTick },
                ticks);
        }

        [Test]
        public void Tick_setting_off_silences_the_ticks()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData { TickOn = false });
            var heard = Run(round, cues, 3.7 + 3.0);
            Assert.IsFalse(heard.Contains(SoundCue.Tick));
            Assert.IsFalse(heard.Contains(SoundCue.FastTick));
        }

        [Test]
        public void One_long_frame_plays_at_most_one_tick()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.7);
            round.Tick(2.5);                             // a hitch inside the window
            var heard = cues.Update(round);
            Assert.AreEqual(1, new List<SoundCue>(heard).FindAll(c => c == SoundCue.Tick || c == SoundCue.FastTick).Count);
        }

        [Test]
        public void Too_late_plays_buzzer_and_small_groan_at_once()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            var heard = Run(round, cues, 3.7 + 3.0 + Frame);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            CollectionAssert.IsSubsetOf(new[] { SoundCue.Buzzer, SoundCue.SmallGroan }, heard);
        }

        [Test]
        public void Goal_waits_for_the_Whistle_then_plays_net_hit_horn_and_cheer()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(50, 90, 100, false);        // high: a Goal
            var beforeWhistle = Run(round, cues, 0.2);
            Assert.IsFalse(beforeWhistle.Contains(SoundCue.GoalImpact));
            Assert.IsFalse(beforeWhistle.Contains(SoundCue.Cheer));

            round.Whistle(ShotResultKind.Goal);          // the line crossing
            var atWhistle = new List<SoundCue>(cues.Update(round));
            CollectionAssert.AreEquivalent(new[] { SoundCue.GoalImpact, SoundCue.Cheer }, atWhistle);
            Assert.IsEmpty(cues.Update(round)); // plays once
        }

        // The result is the Round's, at the Whistle (WhistleRule), not the race's at the Shot: the race saves this one, but the
        // physics puck went in (the goalie has no hit shapes yet, #111).
        [Test]
        public void The_result_sound_follows_the_Whistle_s_result_not_the_race()
        {
            var round = StandUpRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(20, 10, 100, false);        // the race: a pad save
            Assert.IsTrue(round.LastRace.Value.IsSave);
            Assert.IsEmpty(cues.Update(round));
            round.Whistle(ShotResultKind.Goal);
            CollectionAssert.AreEquivalent(new[] { SoundCue.GoalImpact, SoundCue.Cheer }, cues.Update(round));
        }

        [Test]
        public void Save_plays_pad_thud_and_groan_at_the_Whistle()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(20, 10, 100, false);        // low: a pad save
            cues.Update(round);
            round.Whistle(ShotResultKind.Save);
            CollectionAssert.AreEquivalent(new[] { SoundCue.SaveImpact, SoundCue.Groan }, cues.Update(round));
        }

        [Test]
        public void Miss_that_hit_nothing_plays_a_small_groan_at_the_Whistle()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(150, 90, 100, false);       // off the Net Face
            cues.Update(round);
            round.Whistle(ShotResultKind.Miss);
            CollectionAssert.AreEquivalent(new[] { SoundCue.SmallGroan }, cues.Update(round));
        }

        // Board and glass sounds come from the physics contacts (#110), when they happen; the Miss then gets only the small groan.
        [TestCase(SoundCue.MissImpact)]
        [TestCase(SoundCue.GlassImpact)]
        public void A_board_or_glass_hit_sounds_when_it_happens_and_the_Miss_gets_a_small_groan(SoundCue hit)
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(150, 90, 100, false);
            cues.Update(round);
            CollectionAssert.AreEquivalent(new[] { hit }, cues.Update(round, hit));
            round.Whistle(ShotResultKind.Miss);
            CollectionAssert.AreEquivalent(new[] { SoundCue.SmallGroan }, cues.Update(round));
        }

        // Post and crossbar sounds come from the physics contacts (PhysicsShotEffects.HitSound), when they happen.
        [Test]
        public void A_post_hit_pings_when_it_happens_and_the_Miss_then_gets_an_ooh()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(-2, 50, 100, false);        // on the left post
            cues.Update(round);
            CollectionAssert.AreEquivalent(new[] { SoundCue.PostPing }, cues.Update(round, SoundCue.PostPing));
            Assert.IsEmpty(cues.Update(round, null));
            round.Whistle(ShotResultKind.Miss);
            CollectionAssert.AreEquivalent(new[] { SoundCue.Ooh }, cues.Update(round));
        }

        // The netting's sound (#112) plays when the puck meets the netting; it is no frame hit, so a Miss after it is no "ooh".
        [Test]
        public void The_netting_sound_plays_when_it_happens_and_is_no_frame_hit()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(150, 90, 100, false);
            cues.Update(round);
            CollectionAssert.AreEquivalent(new[] { SoundCue.NetRustle }, cues.Update(round, SoundCue.NetRustle));
            round.Whistle(ShotResultKind.Miss);
            CollectionAssert.AreEquivalent(new[] { SoundCue.MissImpact, SoundCue.SmallGroan }, cues.Update(round));
        }

        [Test]
        public void A_post_hit_on_the_last_Shot_does_not_quiet_the_next_Miss()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(-2, 50, 100, false);
            cues.Update(round, SoundCue.PostPing);
            round.Whistle(ShotResultKind.Miss);
            Run(round, cues, round.ResultHoldLength + 0.1 + 3.8); // the hold (a Frame Hit: with its Replay), then the next Shot's skate-in, slow-down and window
            Assert.AreEqual(2, round.ShotNumber);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            round.SubmitShot(150, 90, 100, false);
            cues.Update(round);
            round.Whistle(ShotResultKind.Miss);
            CollectionAssert.AreEquivalent(new[] { SoundCue.SmallGroan }, cues.Update(round));
        }

        // Plays one Shot at (x, y) with the race's result at its Whistle (ScriptedShot) and returns the cues on the Whistle frame.
        private static List<SoundCue> ArrivalCues(Round round, double x, double y)
        {
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(x, y, 100, false);
            cues.Update(round);
            round.WhistleNow();
            return new List<SoundCue>(cues.Update(round));
        }

        // Gear already in the way everywhere: every Shot on the Net Face is saved, showing (and sounding) its point's Save Reaction.
        private static Round StandUpRound()
        {
            var round = new Round(ScriptedRandom.Picking(AttackSide.Middle), new RoundSettings(), FakeReach.InTheWay(), 0);
            round.StartRound();
            return round;
        }

        // The goalie catches with the left hand, which faces the shooter on the shooter's right.
        [Test]
        public void Save_high_on_the_right_is_a_glove_catch()
        {
            var round = StandUpRound();
            var heard = ArrivalCues(round, 85, 85);
            round.WhistleNow();
            Assert.AreEqual(ShotResultKind.Save, round.LastResult.Value.Kind);
            CollectionAssert.AreEquivalent(new[] { SoundCue.GloveCatch, SoundCue.Groan }, heard);
        }

        [Test]
        public void Save_high_on_the_left_is_a_blocker_hit()
        {
            var round = StandUpRound();
            var heard = ArrivalCues(round, 15, 85);
            round.WhistleNow();
            Assert.AreEqual(ShotResultKind.Save, round.LastResult.Value.Kind);
            CollectionAssert.AreEquivalent(new[] { SoundCue.BlockerHit, SoundCue.Groan }, heard);
        }

        // With no contact reported, a raced Save sounds like the gear that won the race, not the Shot point's gear.
        [Test]
        public void A_raced_Save_sounds_like_the_gear_that_won_the_race()
        {
            var round = new Round(ScriptedRandom.Picking(AttackSide.Middle), new RoundSettings(),
                new FakeReach(GoalieReachAnswer.Reaches(SaveReaction.Glove, 0)), 0);
            round.StartRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(15, 85, 45, false); // high on the left: a blocker by the Shot point; slow, so the glove gets there
            Assert.IsTrue(round.LastRace.Value.IsSave);
            Assert.AreEqual(SaveReaction.Glove, round.LastRace.Value.SaveReaction);
            cues.Update(round);
            round.Whistle(ShotResultKind.Save);
            CollectionAssert.AreEquivalent(new[] { SoundCue.GloveCatch, SoundCue.Groan }, cues.Update(round));
        }

        // The sound is the Save Reaction's gear (SaveReactions): pads low, the body in the middle, the stick along the ice
        // through the middle, else the blocker on the shooter's left and the glove on the right.
        [TestCase(55, 85, SoundCue.GloveCatch, TestName = "Top centre, right of middle: glove")]
        [TestCase(45, 85, SoundCue.BlockerHit, TestName = "Top centre, left of middle: blocker")]
        [TestCase(85, 60, SoundCue.GloveCatch, TestName = "Middle right, upper half: glove")]
        [TestCase(15, 60, SoundCue.BlockerHit, TestName = "Middle left, upper half: blocker")]
        [TestCase(40, 60, SoundCue.SaveImpact, TestName = "Centre, upper half, left: body")]
        [TestCase(85, 35, SoundCue.SaveImpact, TestName = "Middle right, low: pad")]
        [TestCase(15, 35, SoundCue.SaveImpact, TestName = "Middle left, low: pad")]
        [TestCase(50, 45, SoundCue.SaveImpact, TestName = "Centre, lower half: body")]
        public void Save_in_the_middle_band_sounds_the_save_reaction_s_gear(double x, double y, SoundCue expected)
        {
            var round = StandUpRound();
            var heard = ArrivalCues(round, x, y);
            round.WhistleNow();
            Assert.AreEqual(ShotResultKind.Save, round.LastResult.Value.Kind);
            CollectionAssert.AreEquivalent(new[] { expected, SoundCue.Groan }, heard);
        }

        // The post is 0.08 m of a 1.83 m wide Net Face (4.4 units); the crossbar 0.08 m of 1.22 m (6.6 units).
        [TestCase(-5, 50, TestName = "Just wide of the left post")]
        [TestCase(106, 50, TestName = "Just wide of the right post")]
        [TestCase(50, 108, TestName = "Just over the crossbar")]
        [TestCase(-2, -3, TestName = "Below the ice by the post")]
        public void Miss_wide_of_the_frame_gets_a_small_groan_and_no_clang_of_its_own(double x, double y)
        {
            var heard = ArrivalCues(StartedRound(), x, y);
            CollectionAssert.AreEquivalent(new[] { SoundCue.SmallGroan }, heard);
        }

        [Test]
        public void Slow_Shot_result_sound_waits_for_the_Whistle()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(20, 10, 10, false);         // very slow: the puck flies 2.9 s
            var inFlight = Run(round, cues, 2.8);
            Assert.IsFalse(inFlight.Contains(SoundCue.SaveImpact));
            Assert.IsFalse(inFlight.Contains(SoundCue.Groan));

            round.Whistle(ScriptedShot.Result(round).Kind); // the puck lands
            var landed = Run(round, cues, 0.1);
            CollectionAssert.IsSubsetOf(new[] { SoundCue.SaveImpact, SoundCue.Groan }, landed);
            Assert.AreEqual(1, landed.FindAll(c => c == SoundCue.SaveImpact).Count);
        }

        private static List<SoundCue> PlayRound(Round round, RoundSoundCues cues, int goals)
        {
            var heard = new List<SoundCue>();
            for (int i = 0; i < Round.ShotsPerRound; i++)
            {
                heard.AddRange(Run(round, cues, 3.8));
                round.SubmitShotAndWhistle(i < goals ? 50 : 150, 90, 100, false);
                heard.AddRange(Run(round, cues, round.ResultHoldLength + 0.1));
            }
            return heard;
        }

        [Test]
        public void End_screen_cheers_a_new_best_score()
        {
            var round = StartedRound(best: 1);
            var cues = new RoundSoundCues(new SaveData());
            var heard = PlayRound(round, cues, goals: 2);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(1, heard.FindAll(c => c == SoundCue.NewBestCheer).Count);
        }

        [Test]
        public void End_screen_is_quiet_without_a_new_best_score()
        {
            var round = StartedRound(best: 5);
            var cues = new RoundSoundCues(new SaveData());
            var heard = PlayRound(round, cues, goals: 2);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.IsFalse(heard.Contains(SoundCue.NewBestCheer));
        }

        [Test]
        public void The_hockey_stop_starts_with_the_skate_in_and_stops_on_the_frame_the_skate_in_ends()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            round.Tick(Frame);
            CollectionAssert.Contains(cues.Update(round), SoundCue.HockeyStop);

            var duringSkate = Run(round, cues, 2.4);     // still skating in at 2.45 s
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.IsFalse(duringSkate.Contains(SoundCue.HockeyStop));
            Assert.IsFalse(duringSkate.Contains(SoundCue.HockeyStopEnd));

            var heard = new List<SoundCue>();
            while (round.Phase == RoundPhase.SkateIn)
            {
                round.Tick(Frame);
                heard = new List<SoundCue>(cues.Update(round));
            }
            Assert.AreEqual(RoundPhase.SlowDown, round.Phase);
            CollectionAssert.Contains(heard, SoundCue.HockeyStopEnd);

            var after = Run(round, cues, 3.0);           // slow-down and shooting window: no hockey stop
            Assert.IsFalse(after.Contains(SoundCue.HockeyStop) || after.Contains(SoundCue.HockeyStopEnd));
        }

        [Test]
        public void The_hockey_stop_starts_and_stops_once_for_every_skate_in_from_every_Attack_Side()
        {
            var round = new Round(ScriptedRandom.Picking(AttackSide.Left, AttackSide.Middle, AttackSide.Right), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            var cues = new RoundSoundCues(new SaveData());
            var sides = new HashSet<AttackSide>();
            var order = new List<SoundCue>();

            void Watch(double seconds)
            {
                for (double t = 0; t < seconds - 1e-9; t += Frame)
                {
                    round.Tick(Frame);
                    foreach (SoundCue cue in cues.Update(round))
                    {
                        if (cue == SoundCue.HockeyStop) sides.Add(round.AttackSide);
                        if (cue == SoundCue.HockeyStop || cue == SoundCue.HockeyStopEnd) order.Add(cue);
                    }
                }
            }

            for (int i = 0; i < Round.ShotsPerRound; i++)
            {
                Watch(3.8);
                round.SubmitShotAndWhistle(150, 90, 100, false);
                Watch(2.6);
            }

            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            var expected = new List<SoundCue>();
            for (int i = 0; i < Round.ShotsPerRound; i++) { expected.Add(SoundCue.HockeyStop); expected.Add(SoundCue.HockeyStopEnd); }
            CollectionAssert.AreEqual(expected, order);
            CollectionAssert.AreEquivalent(new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right }, sides);
        }

        [Test]
        public void A_stride_plays_on_each_push_of_the_camera_stride_alternating_left_and_right_for_every_Attack_Side()
        {
            // The camera bob is sin(10 t): its low points (the push) fall at 0.471, 1.100, 1.728 and 2.356 s of the skate-in.
            // The last one is inside the hockey stop (the last 0.6 s of the 2.5 s skate-in), so it has no stride.
            // The roll sin(5 t) leans the camera left on the first push, then right, then left.
            const double StrideFrame = 0.01;
            var expectedTimes = new[] { 0.471, 1.100, 1.728 };
            var expectedSides = new[] { SoundCue.StrideLeft, SoundCue.StrideRight, SoundCue.StrideLeft };

            var round = new Round(ScriptedRandom.Picking(AttackSide.Left, AttackSide.Middle, AttackSide.Right), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            var cues = new RoundSoundCues(new SaveData());
            var sides = new HashSet<AttackSide>();
            int skateIns = 0;

            for (int shot = 0; shot < Round.ShotsPerRound; shot++)
            {
                Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
                sides.Add(round.AttackSide);
                skateIns++;
                var times = new List<double>();
                var heard = new List<SoundCue>();
                void Listen()
                {
                    foreach (SoundCue cue in cues.Update(round))
                        if (cue == SoundCue.StrideLeft || cue == SoundCue.StrideRight) { times.Add(round.PhaseSecondsElapsed); heard.Add(cue); }
                }
                Listen(); // the skate-in's first frame
                while (round.Phase == RoundPhase.SkateIn)
                {
                    round.Tick(StrideFrame);
                    if (round.Phase == RoundPhase.SkateIn) Listen();
                    else cues.Update(round);
                }

                CollectionAssert.AreEqual(expectedSides, heard, "Attack Side " + round.AttackSide);
                for (int i = 0; i < expectedTimes.Length; i++)
                {
                    Assert.GreaterOrEqual(times[i], expectedTimes[i] - 0.001, "stride " + i);
                    Assert.LessOrEqual(times[i], expectedTimes[i] + StrideFrame, "stride " + i); // on the first frame at or after the push
                }

                var rest = Run(round, cues, 3.0);         // slow-down and shooting window: no strides
                Assert.IsFalse(rest.Contains(SoundCue.StrideLeft) || rest.Contains(SoundCue.StrideRight));
                round.SubmitShotAndWhistle(150, 90, 100, false);
                while (round.Phase != RoundPhase.SkateIn && round.Phase != RoundPhase.RoundOver)
                {
                    round.Tick(Frame);
                    cues.Update(round);
                }
            }

            Assert.AreEqual(Round.ShotsPerRound, skateIns);
            CollectionAssert.AreEquivalent(new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right }, sides);
        }

        [TestCase(5.4, 2.5, 0.0, 2.9, TestName = "Short skate-in: start late in the clip so both end together")]
        [TestCase(5.4, 2.5, 0.05, 2.95, TestName = "First frame already 0.05 s into the skate-in")]
        [TestCase(2.0, 3.0, 0.0, -1.0, TestName = "Skate-in longer than the clip: wait 1 s, then play it all")]
        [TestCase(5.4, 5.4, 0.0, 0.0, TestName = "Same length: play from the start")]
        public void The_hockey_stop_clip_is_placed_so_its_end_lands_on_the_end_of_the_skate_in(
            double clipSeconds, double skateInSeconds, double skateSeconds, double expected)
        {
            Assert.AreEqual(expected, RoundSoundCues.HockeyStopClipTime(clipSeconds, skateInSeconds, skateSeconds), 1e-9);
        }

        [Test]
        public void Crowd_is_soft_on_the_start_screen_rises_in_the_skate_in_and_drops_in_the_slow_down()
        {
            double start = RoundSoundCues.CrowdLevel(RoundPhase.Idle, 0, Defaults);
            double skateBegin = RoundSoundCues.CrowdLevel(RoundPhase.SkateIn, 0, Defaults);
            double skateEnd = RoundSoundCues.CrowdLevel(RoundPhase.SkateIn, 2.5, Defaults);
            double slow = RoundSoundCues.CrowdLevel(RoundPhase.SlowDown, 0.5, Defaults);
            double window = RoundSoundCues.CrowdLevel(RoundPhase.ShootingWindow, 1.0, Defaults);

            Assert.Greater(start, 0.0);
            Assert.Greater(skateEnd, skateBegin);
            Assert.Greater(skateEnd, start);
            Assert.Less(slow, start);
            Assert.Less(window, start);
        }

        [Test]
        public void Crowd_rise_follows_the_skate_in_length_from_the_Round_settings()
        {
            var settings = new RoundSettings { SkateInSeconds = 5 };
            Assert.AreEqual(0.65, RoundSoundCues.CrowdLevel(RoundPhase.SkateIn, 2.5, settings), 1e-9); // halfway up 0.4..0.9
        }

        [Test]
        public void Slow_down_muffles_lowers_pitch_and_beats_the_heart()
        {
            var slow = RoundSoundCues.SlowMo(RoundPhase.ShootingWindow, 1.0);
            Assert.Less(slow.Pitch, 1.0);
            Assert.Less(slow.LowPassHz, 2000.0);
            Assert.IsTrue(slow.Heartbeat);
        }

        [Test]
        public void Slow_down_snaps_back_on_the_Shot()
        {
            var after = RoundSoundCues.SlowMo(RoundPhase.ResultHold, 0.0);
            Assert.AreEqual(1.0, after.Pitch, 1e-9);
            Assert.AreEqual(RoundSoundCues.OpenLowPassHz, after.LowPassHz, 1e-9);
            Assert.IsFalse(after.Heartbeat);
        }
    
        [TestCase(0.4, false, 0.4)]
        [TestCase(0.4, true, 0.0)]
        [TestCase(1.5, false, 1.0)]
        [TestCase(-0.2, false, 0.0)]
        public void Master_volume_and_mute_set_the_volume(double master, bool mute, double expected)
        {
            var save = new SaveData { MasterVolume = master, Mute = mute };
            Assert.AreEqual(expected, RoundSoundCues.ListenerVolume(save), 1e-9);
        }

        [Test]
        public void Tick_setting_changed_mid_window_applies_at_once()
        {
            var save = new SaveData();
            var round = StartedRound();
            var cues = new RoundSoundCues(save);
            Run(round, cues, 3.8);
            save.TickOn = false;
            var heard = Run(round, cues, 2.5);
            Assert.IsFalse(heard.Contains(SoundCue.Tick) || heard.Contains(SoundCue.FastTick));
        }
    }
}
