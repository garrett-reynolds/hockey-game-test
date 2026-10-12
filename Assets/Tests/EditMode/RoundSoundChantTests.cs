using System;
using System.Collections.Generic;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Clap Chant and the crowd's loudness, through the sound cue seam (RoundSoundCues driven by the screen flow).</summary>
    public class RoundSoundChantTests
    {
        private const double Frame = 0.05;
        private const double ChantSeconds = 3.0;
        private const double CheerSeconds = 6.0;
        private const double HornSeconds = 1.6;
        private const double NewBestCheerSeconds = 4.0;

        /// <summary>One cue, when it was heard (real seconds since the cues began) and on which screen.</summary>
        private readonly struct Heard
        {
            public readonly double Time;
            public readonly SoundCue Cue;
            public readonly FlowScreen Screen;
            public readonly RoundPhase Phase;

            public Heard(double time, SoundCue cue, FlowScreen screen, RoundPhase phase)
            {
                Time = time;
                Cue = cue;
                Screen = screen;
                Phase = phase;
            }
        }

        /// <summary>A screen flow and its sound cues, run frame by frame like RoundDriver does.</summary>
        private sealed class Rig
        {
            public readonly ScreenFlow Flow;
            public readonly RoundSoundCues Cues;
            public readonly List<Heard> Heard = new List<Heard>();
            public double Clock;

            public Rig(IRandomSource chantRandom, int best = 0)
            {
                var save = new SaveData { BestScore = best };
                Flow = new ScreenFlow(ScriptedRandom.Halves(), save, FakeReach.NeverReaches());
                Cues = new RoundSoundCues(save, chantRandom, Lengths);
            }

            public void Frame()
            {
                Flow.Tick(RoundSoundChantTests.Frame);
                Clock += RoundSoundChantTests.Frame;
                foreach (SoundCue cue in Cues.Update(Flow, RoundSoundChantTests.Frame))
                    Heard.Add(new Heard(Clock, cue, Flow.Screen, Flow.Round.Phase));
            }

            public void Run(double seconds)
            {
                for (double t = 0; t < seconds - 1e-9; t += RoundSoundChantTests.Frame) Frame();
            }

            /// <summary>Starts a Round and plays its 6 Shots (the first `goals` score, the rest miss wide) until the end screen.</summary>
            public void PlayRound(int goals)
            {
                Flow.Press();
                int shot = 0;
                while (Flow.Screen == FlowScreen.Playing)
                {
                    if (Flow.Round.Phase == RoundPhase.ShootingWindow)
                    {
                        Flow.SubmitShot(shot < goals ? 50 : -10, shot < goals ? 90 : 50, 100, false);
                        Flow.Round.WhistleNow(); // the puck there at once
                        shot++;
                    }
                    Frame();
                }
            }

            public List<double> TimesOf(SoundCue cue) => Heard.FindAll(h => h.Cue == cue).ConvertAll(h => h.Time);
        }

        private static double Lengths(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.Chant: return ChantSeconds;
                case SoundCue.Cheer: return CheerSeconds;
                case SoundCue.GoalImpact: return HornSeconds;
                case SoundCue.NewBestCheer: return NewBestCheerSeconds;
                default: return 0.5;
            }
        }

        [Test]
        public void Start_screen_chant_waits_the_rolled_gap_then_repeats_after_a_quiet_gap()
        {
            // Rolls 0 and 0.75: a 4 s gap first, then 7 s of quiet after the first chant ends.
            var rig = new Rig(new ScriptedRandom(new[] { 0.5 }, 0.0, 0.75));
            rig.Run(4.0 + ChantSeconds + 7.0 + 1.0);
            var chants = rig.TimesOf(SoundCue.Chant);
            Assert.AreEqual(2, chants.Count);
            Assert.AreEqual(4.0, chants[0], Frame + 1e-6);
            Assert.AreEqual(7.0, chants[1] - (chants[0] + ChantSeconds), Frame + 1e-6);
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        public void Start_screen_chant_gaps_are_4_to_8_seconds(int seed)
        {
            var rig = new Rig(new SeededRandom(seed));
            rig.Run(150);
            var chants = rig.TimesOf(SoundCue.Chant);
            Assert.GreaterOrEqual(chants.Count, 10);
            Assert.AreEqual(6.0, chants[0], 2.0 + Frame + 1e-6); // the first gap: 4 to 8 s
            double shortest = double.MaxValue, longest = 0;
            for (int i = 1; i < chants.Count; i++)
            {
                double gap = chants[i] - (chants[i - 1] + ChantSeconds);
                Assert.GreaterOrEqual(gap, 4.0 - 1e-6, "gap " + i);
                Assert.LessOrEqual(gap, 8.0 + Frame + 1e-6, "gap " + i);
                shortest = Math.Min(shortest, gap);
                longest = Math.Max(longest, gap);
            }
            Assert.Greater(longest - shortest, 1.0, "the gaps vary");
            Assert.IsFalse(rig.Heard.Exists(h => h.Cue == SoundCue.ChantEnd), "nothing stops the chant on the start screen");
        }

        [Test]
        public void The_chant_follows_the_Arena_Show_it_is_handed_for_the_frame()
        {
            // RoundDriver works out the show once a frame and hands it on: the cues follow that show, not their own.
            var rig = new Rig(new ScriptedRandom(new[] { 0.5 }, 0.0, 0.75));
            var heard = new List<SoundCue>();
            for (double t = 0; t < 20.0; t += Frame)
            {
                rig.Flow.Tick(Frame);
                heard.AddRange(rig.Cues.Update(rig.Flow, false, Frame)); // a show with no chant
            }
            Assert.AreEqual(FlowScreen.Start, rig.Flow.Screen);
            CollectionAssert.DoesNotContain(heard, SoundCue.Chant, "no chant when the show says none, even on the start screen");

            for (double t = 0; t < 20.0; t += Frame)
            {
                rig.Flow.Tick(Frame);
                heard.AddRange(rig.Cues.Update(rig.Flow, ArenaShow.From(rig.Flow).Chant, Frame));
            }
            CollectionAssert.Contains(heard, SoundCue.Chant, "the chant comes when the show says so");
        }

        [Test]
        public void No_chant_in_the_skate_in_or_anywhere_in_the_Round()
        {
            var rig = new Rig(new SeededRandom(3));
            rig.Run(20);
            rig.PlayRound(goals: 3);
            Assert.IsTrue(rig.Heard.Exists(h => h.Phase == RoundPhase.SkateIn), "the skate-in was heard");
            Assert.IsFalse(rig.Heard.Exists(h => h.Cue == SoundCue.Chant && h.Screen == FlowScreen.Playing));
        }

        [Test]
        public void A_chant_still_playing_when_the_Round_starts_stops_with_the_skate_in()
        {
            var rig = new Rig(new ScriptedRandom(new[] { 0.5 }, 0.0)); // first chant at 4 s
            rig.Run(4.0 + 1.0);                                       // 1 s into it
            Assert.AreEqual(1, rig.TimesOf(SoundCue.Chant).Count);

            rig.Flow.Press();
            rig.Frame();
            Assert.AreEqual(RoundPhase.SkateIn, rig.Flow.Round.Phase);
            Assert.IsTrue(rig.Heard.Exists(h => h.Cue == SoundCue.ChantEnd && h.Phase == RoundPhase.SkateIn));
            Assert.AreEqual(rig.Clock, rig.TimesOf(SoundCue.ChantEnd)[0], 1e-9, "on the skate-in's first frame");
        }

        [Test]
        public void No_chant_stop_when_the_Round_starts_in_a_quiet_gap()
        {
            var rig = new Rig(new ScriptedRandom(new[] { 0.5 }, 0.0, 0.0)); // chants at 4 s and 11 s
            rig.Run(9.0);
            rig.Flow.Press();
            rig.Frame();
            Assert.IsFalse(rig.Heard.Exists(h => h.Cue == SoundCue.ChantEnd));
        }

        [Test]
        public void Round_over_plays_the_chant_once_after_the_last_cheer_and_horn_end()
        {
            var rig = new Rig(new SeededRandom(5), best: 99);
            rig.PlayRound(goals: 6);                          // the last Shot's cheer runs on into the end screen
            Assert.AreEqual(FlowScreen.End, rig.Flow.Screen);
            double roundOver = rig.Clock;
            List<double> cheers = rig.TimesOf(SoundCue.Cheer), horns = rig.TimesOf(SoundCue.GoalImpact);
            double cheerEnds = Math.Max(cheers[cheers.Count - 1] + CheerSeconds, horns[horns.Count - 1] + HornSeconds);
            Assert.Greater(cheerEnds, roundOver, "the cheer is still going at Round over");

            rig.Run(12);                                      // under the 15 s auto return
            Assert.AreEqual(FlowScreen.End, rig.Flow.Screen);
            var chants = rig.Heard.FindAll(h => h.Cue == SoundCue.Chant && h.Screen == FlowScreen.End);
            Assert.AreEqual(1, chants.Count);
            Assert.GreaterOrEqual(chants[0].Time, cheerEnds - 1e-6);
            Assert.Less(chants[0].Time, cheerEnds + Frame + 1e-6);
        }

        [Test]
        public void Round_over_chant_waits_for_the_new_best_cheer()
        {
            var rig = new Rig(new SeededRandom(5), best: 0);
            rig.PlayRound(goals: 1);                          // one Goal, then misses: only the new-best cheer is left
            List<double> newBest = rig.TimesOf(SoundCue.NewBestCheer);
            Assert.AreEqual(1, newBest.Count);

            rig.Run(10);
            var chants = rig.Heard.FindAll(h => h.Cue == SoundCue.Chant && h.Screen == FlowScreen.End);
            Assert.AreEqual(1, chants.Count);
            Assert.AreEqual(newBest[0] + NewBestCheerSeconds, chants[0].Time, Frame + 1e-6);
        }

        [Test]
        public void Round_over_after_misses_chants_at_once()
        {
            var rig = new Rig(new SeededRandom(5), best: 99);
            rig.PlayRound(goals: 0);
            double roundOver = rig.Clock;
            rig.Run(5);
            var chants = rig.Heard.FindAll(h => h.Cue == SoundCue.Chant && h.Screen == FlowScreen.End);
            Assert.AreEqual(1, chants.Count);
            Assert.AreEqual(roundOver, chants[0].Time, Frame + 1e-6);
        }

        [Test]
        public void Back_on_the_start_screen_the_chant_repeats_again()
        {
            var rig = new Rig(new SeededRandom(9), best: 99);
            rig.PlayRound(goals: 0);
            rig.Run(16);                                      // past the 15 s auto return
            Assert.AreEqual(FlowScreen.Start, rig.Flow.Screen);
            rig.Run(30);
            var onStart = rig.Heard.FindAll(h => h.Cue == SoundCue.Chant && h.Screen == FlowScreen.Start);
            Assert.GreaterOrEqual(onStart.Count, 2);
        }

        [Test]
        public void Loudness_order_is_horn_then_cheer_then_chant_then_crowd_loop()
        {
            var settings = new RoundSettings();
            double horn = RoundSoundCues.Volume(SoundCue.GoalImpact);
            double cheer = RoundSoundCues.Volume(SoundCue.Cheer);
            double chant = RoundSoundCues.Volume(SoundCue.Chant);
            Assert.Greater(horn, cheer);
            Assert.Greater(cheer, chant);
            Assert.Greater(chant, RoundSoundCues.CrowdLevel(RoundPhase.Idle, 0, settings));
            Assert.Greater(chant, RoundSoundCues.CrowdLevel(RoundPhase.RoundOver, 0, settings));
            Assert.AreEqual(RoundSoundCues.Volume(SoundCue.Cheer), RoundSoundCues.Volume(SoundCue.NewBestCheer));
        }

        [Test]
        public void Chant_is_about_6_dB_under_the_cheer()
        {
            double db = 20 * Math.Log10(RoundSoundCues.Volume(SoundCue.Chant) / RoundSoundCues.Volume(SoundCue.Cheer));
            Assert.AreEqual(-6.0, db, 0.5);
        }

        [Test]
        public void A_small_groan_is_softer_than_the_Save_groan()
        {
            Assert.Less(RoundSoundCues.Volume(SoundCue.SmallGroan), RoundSoundCues.Volume(SoundCue.Groan));
        }

        // Every sound, the chant included, plays through the AudioListener, whose volume is ListenerVolume (RoundSound).
        [TestCase(false, 1.0, 1.0)]
        [TestCase(false, 0.5, 0.5)]
        [TestCase(true, 1.0, 0.0)]
        [TestCase(true, 0.5, 0.0)]
        public void Mute_and_master_volume_apply_to_every_sound(bool mute, double master, double scale)
        {
            var save = new SaveData { Mute = mute, MasterVolume = master };
            Assert.AreEqual(scale, RoundSoundCues.ListenerVolume(save), 1e-9);
        }
    }
}
