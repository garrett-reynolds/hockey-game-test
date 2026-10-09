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
            var round = new Round(new ScriptedRandom(ScriptedRandom.AlternatingButterflies()), new RoundSettings(), best);
            round.StartRound();
            return round;
        }

        // Ticks the Round frame by frame for the given real seconds, collecting every cue.
        private static List<SoundCue> Run(Round round, RoundSoundCues cues, double seconds, bool puckArrived = false)
        {
            var heard = new List<SoundCue>();
            for (double t = 0; t < seconds - 1e-9; t += Frame)
            {
                round.Tick(Frame);
                heard.AddRange(cues.Update(round, puckArrived));
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
            var heard = cues.Update(round, false);
            Assert.AreEqual(1, new List<SoundCue>(heard).FindAll(c => c == SoundCue.Tick || c == SoundCue.FastTick).Count);
        }

        [Test]
        public void Too_late_plays_buzzer_and_groan_at_once()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            var heard = Run(round, cues, 3.7 + 3.0 + Frame);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            CollectionAssert.IsSubsetOf(new[] { SoundCue.Buzzer, SoundCue.Groan }, heard);
        }

        [Test]
        public void Goal_waits_for_the_puck_then_plays_net_hit_horn_and_cheer()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(50, 90, 100, false);        // Open Zone for butterflies
            round.DelayResult(0.5);                      // the scene holds the result while the puck flies
            var beforeArrival = Run(round, cues, 0.2);
            Assert.IsFalse(beforeArrival.Contains(SoundCue.GoalImpact));
            Assert.IsFalse(beforeArrival.Contains(SoundCue.Cheer));

            var atArrival = new List<SoundCue>(cues.Update(round, true));
            CollectionAssert.AreEquivalent(new[] { SoundCue.GoalImpact, SoundCue.Cheer }, atArrival);
            Assert.IsEmpty(cues.Update(round, true)); // plays once
        }

        [Test]
        public void Save_plays_pad_thud_and_groan_on_arrival()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(50, 10, 100, false);        // Covered bottom row
            round.DelayResult(0.5);
            cues.Update(round, false);
            CollectionAssert.AreEquivalent(new[] { SoundCue.SaveImpact, SoundCue.Groan }, cues.Update(round, true));
        }

        [Test]
        public void Miss_plays_boards_clang_and_groan_on_arrival()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(150, 90, 100, false);       // off the Net Face
            round.DelayResult(0.5);
            cues.Update(round, false);
            CollectionAssert.AreEquivalent(new[] { SoundCue.MissImpact, SoundCue.Groan }, cues.Update(round, true));
        }

        [Test]
        public void Slow_Shot_result_sound_waits_for_the_landing_even_when_the_arrival_is_missed()
        {
            var round = StartedRound();
            var cues = new RoundSoundCues(new SaveData());
            Run(round, cues, 3.8);
            round.SubmitShot(50, 10, 10, false);         // very slow: the puck flies 2.9 s
            round.DelayResult(2.9);
            var inFlight = Run(round, cues, 2.8);
            Assert.IsFalse(inFlight.Contains(SoundCue.SaveImpact));
            Assert.IsFalse(inFlight.Contains(SoundCue.Groan));

            var landed = Run(round, cues, 0.3);          // no arrival frame seen: the result showing still plays it
            CollectionAssert.IsSubsetOf(new[] { SoundCue.SaveImpact, SoundCue.Groan }, landed);
            Assert.AreEqual(1, landed.FindAll(c => c == SoundCue.SaveImpact).Count);
        }

        private static List<SoundCue> PlayRound(Round round, RoundSoundCues cues, int goals)
        {
            var heard = new List<SoundCue>();
            for (int i = 0; i < Round.ShotsPerRound; i++)
            {
                heard.AddRange(Run(round, cues, 3.8));
                round.SubmitShot(i < goals ? 50 : 150, 90, 100, false);
                heard.AddRange(Run(round, cues, 2.6, puckArrived: true));
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
