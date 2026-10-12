using System;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class ArenaShowTests
    {
        private static readonly RoundPhase[] Phases = (RoundPhase[])Enum.GetValues(typeof(RoundPhase));
        private static readonly ArenaResult[] Results = (ArenaResult[])Enum.GetValues(typeof(ArenaResult));
        private static readonly ArenaZone[] Zones = (ArenaZone[])Enum.GetValues(typeof(ArenaZone));

        private static ArenaState At(RoundPhase phase, double seconds = 0.5, ArenaResult result = ArenaResult.None,
                                     bool hidden = false, bool newBest = false, int goals = 0, int shot = 1) =>
            ArenaShow.At(new ArenaShowInput
            {
                Phase = phase,
                PhaseSeconds = seconds,
                Result = result,
                ResultHidden = hidden,
                IsNewBest = newBest,
                Goals = goals,
                ShotNumber = shot
            });

        private static ArenaState Showing(ArenaResult result) => At(RoundPhase.ResultHold, 1.0, result);

        // ---- the Shot's result, as the arena sees it ----

        [Test]
        public void A_Shot_result_reads_as_its_arena_result()
        {
            Assert.AreEqual(ArenaResult.None, ArenaShow.ResultOf(null));
            Assert.AreEqual(ArenaResult.Goal, ArenaShow.ResultOf(new ShotResult(ShotResultKind.Goal, true, 50, 50, 100, false)));
            Assert.AreEqual(ArenaResult.Save, ArenaShow.ResultOf(new ShotResult(ShotResultKind.Save, true, 50, 50, 100, false)));
            Assert.AreEqual(ArenaResult.TooLate, ArenaShow.ResultOf(new ShotResult(ShotResultKind.TooLate, false, 0, 0, 0, false)));
            Assert.AreEqual(ArenaResult.WideMiss, ArenaShow.ResultOf(new ShotResult(ShotResultKind.Miss, true, -20, 50, 100, false)));
        }

        [Test]
        public void A_Frame_Hit_reads_as_off_the_post_or_off_the_bar()
        {
            double justLeft = -NetFrame.PostWidth / 2, justAbove = 100 + NetFrame.CrossbarHeight / 2;
            Assert.AreEqual(ArenaResult.FrameHitPost, ArenaShow.ResultOf(new ShotResult(ShotResultKind.Miss, true, justLeft, 50, 100, false)));
            Assert.AreEqual(ArenaResult.FrameHitPost, ArenaShow.ResultOf(new ShotResult(ShotResultKind.Miss, true, 100 - justLeft, 50, 100, false)));
            Assert.AreEqual(ArenaResult.FrameHitBar, ArenaShow.ResultOf(new ShotResult(ShotResultKind.Miss, true, 50, justAbove, 100, false)));
        }

        // ---- Calm Area rules ----

        [Test]
        public void Calm_Area_rules_are_on_exactly_from_goalie_set_to_the_Shot()
        {
            foreach (RoundPhase phase in Phases)
            foreach (double t in new[] { 0.0, 0.01, 1.5, 2.99 })
                Assert.AreEqual(phase == RoundPhase.ShootingWindow, At(phase, t).CalmAreaRules, phase + " at " + t + " s");
        }

        [Test]
        public void Calm_Area_rules_end_at_the_Shot_even_while_its_result_is_hidden()
        {
            foreach (ArenaResult result in Results)
                Assert.IsFalse(At(RoundPhase.ResultHold, 0.0, result, hidden: true).CalmAreaRules, result.ToString());
        }

        [Test]
        public void In_the_shooting_window_the_Calm_Area_Ribbon_Boards_are_one_dim_still_colour()
        {
            foreach (double t in new[] { 0.0, 0.7, 1.5, 2.2, 2.99 })
            {
                RibbonBoard calm = At(RoundPhase.ShootingWindow, t).RibbonIn(ArenaZone.CalmArea);
                Assert.AreEqual(RibbonMessage.CalmStill, calm.Message);
                Assert.AreEqual("", calm.Text);
                Assert.AreEqual(ArenaShow.CalmRibbonColour, calm.Colour, "the same colour all window: still");
            }
            Assert.LessOrEqual(Math.Max(ArenaShow.CalmRibbonColour.R, Math.Max(ArenaShow.CalmRibbonColour.G, ArenaShow.CalmRibbonColour.B)), 64,
                               "dim");
        }

        [Test]
        public void Outside_the_shooting_window_the_Calm_Area_Ribbon_Boards_follow_the_rest()
        {
            foreach (RoundPhase phase in Phases)
            {
                if (phase == RoundPhase.ShootingWindow) continue;
                ArenaState state = At(phase);
                Assert.AreEqual(state.RibbonIn(ArenaZone.Near), state.RibbonIn(ArenaZone.CalmArea), phase.ToString());
            }
            ArenaState goal = Showing(ArenaResult.Goal);
            Assert.AreEqual(RibbonMessage.Goal, goal.RibbonIn(ArenaZone.CalmArea).Message);
        }

        [Test]
        public void The_Calm_Area_crowd_sits_still_from_goalie_set_to_the_Shot()
        {
            Assert.AreEqual(CrowdState.Still, At(RoundPhase.ShootingWindow, 0.0).CrowdIn(ArenaZone.CalmArea));
            Assert.AreEqual(CrowdState.Still, At(RoundPhase.ShootingWindow, 2.9).CrowdIn(ArenaZone.CalmArea));
            foreach (RoundPhase phase in Phases)
                if (phase != RoundPhase.ShootingWindow)
                    Assert.AreNotEqual(CrowdState.Still, At(phase).CrowdIn(ArenaZone.CalmArea), phase.ToString());
        }

        [Test]
        public void No_Goal_Show_light_in_the_shooting_window()
        {
            GoalShowLights lights = At(RoundPhase.ShootingWindow, 1.0).GoalShow;
            Assert.IsFalse(lights.GoalLight);
            Assert.IsFalse(lights.RoofPulse);
            Assert.IsFalse(lights.Lit);
        }

        // ---- Ribbon Board message per result ----

        [Test]
        public void Ribbon_Boards_show_GOAL_in_red_on_a_Goal()
        {
            RibbonBoard ribbon = Showing(ArenaResult.Goal).RibbonIn(ArenaZone.Far);
            Assert.AreEqual(RibbonMessage.Goal, ribbon.Message);
            Assert.AreEqual("GOAL!", ribbon.Text);
            Assert.AreEqual(ArenaShow.GoalRed, ribbon.Colour);
            Assert.IsFalse(ribbon.Small);
        }

        [Test]
        public void Ribbon_Boards_show_a_small_white_SAVE_on_a_Save()
        {
            RibbonBoard ribbon = Showing(ArenaResult.Save).RibbonIn(ArenaZone.Near);
            Assert.AreEqual(RibbonMessage.Save, ribbon.Message);
            Assert.AreEqual("SAVE", ribbon.Text);
            Assert.AreEqual(ArenaShow.SaveWhite, ribbon.Colour);
            Assert.IsTrue(ribbon.Small);
        }

        [Test]
        public void Ribbon_Boards_show_OFF_THE_POST_or_OFF_THE_BAR_on_a_Frame_Hit()
        {
            RibbonBoard post = Showing(ArenaResult.FrameHitPost).RibbonIn(ArenaZone.Near);
            Assert.AreEqual(RibbonMessage.OffThePost, post.Message);
            Assert.AreEqual("OFF THE POST!", post.Text);
            RibbonBoard bar = Showing(ArenaResult.FrameHitBar).RibbonIn(ArenaZone.Near);
            Assert.AreEqual(RibbonMessage.OffTheBar, bar.Message);
            Assert.AreEqual("OFF THE BAR!", bar.Text);
        }

        [Test]
        public void Ribbon_Boards_show_nothing_for_a_Wide_Miss_or_Too_late_and_keep_cycling_ads()
        {
            foreach (ArenaResult result in new[] { ArenaResult.WideMiss, ArenaResult.TooLate })
            foreach (ArenaZone zone in Zones)
                Assert.AreEqual(RibbonMessage.Ads, Showing(result).RibbonIn(zone).Message, result + " in " + zone);
        }

        [Test]
        public void The_result_message_waits_for_the_puck_to_land()
        {
            foreach (ArenaResult result in Results)
            {
                ArenaState inFlight = At(RoundPhase.ResultHold, 0.2, result, hidden: true);
                Assert.AreEqual(RibbonMessage.Ads, inFlight.RibbonIn(ArenaZone.Near).Message, result.ToString());
                Assert.IsFalse(inFlight.GoalShow.GoalLight, result.ToString());
            }
        }

        [Test]
        public void Ribbon_Boards_cycle_ads_in_every_other_phase()
        {
            foreach (RoundPhase phase in Phases)
            {
                if (phase == RoundPhase.ResultHold) continue;
                Assert.AreEqual(RibbonMessage.Ads, At(phase).RibbonIn(ArenaZone.Near).Message, phase.ToString());
                Assert.AreEqual(RibbonMessage.Ads, At(phase).RibbonIn(ArenaZone.Far).Message, phase.ToString());
            }
        }

        // ---- the rest of the arena, phase by phase ----

        [Test]
        public void Video_Board_content_follows_the_phase()
        {
            Assert.AreEqual(VideoBoardContent.StartCard, At(RoundPhase.Idle).VideoBoard);
            Assert.AreEqual(VideoBoardContent.Score, At(RoundPhase.SkateIn).VideoBoard);
            Assert.AreEqual(VideoBoardContent.Score, At(RoundPhase.SlowDown).VideoBoard);
            Assert.AreEqual(VideoBoardContent.CalmStill, At(RoundPhase.ShootingWindow).VideoBoard);
            Assert.AreEqual(VideoBoardContent.Score, At(RoundPhase.ResultHold, 0.1, ArenaResult.Goal, hidden: true).VideoBoard);
            Assert.AreEqual(VideoBoardContent.Goal, Showing(ArenaResult.Goal).VideoBoard);
            Assert.AreEqual(VideoBoardContent.Replay, Showing(ArenaResult.Save).VideoBoard);
            Assert.AreEqual(VideoBoardContent.Replay, Showing(ArenaResult.FrameHitPost).VideoBoard);
            Assert.AreEqual(VideoBoardContent.Replay, Showing(ArenaResult.FrameHitBar).VideoBoard);
            Assert.AreEqual(VideoBoardContent.AdSlide, Showing(ArenaResult.WideMiss).VideoBoard);
            Assert.AreEqual(VideoBoardContent.AdSlide, Showing(ArenaResult.TooLate).VideoBoard);
            Assert.AreEqual(VideoBoardContent.Final, At(RoundPhase.RoundOver).VideoBoard);
            Assert.AreEqual(VideoBoardContent.NewBest, At(RoundPhase.RoundOver, newBest: true).VideoBoard);
        }

        [Test]
        public void The_Video_Board_is_one_dim_still_colour_with_no_words_exactly_from_goalie_set_to_the_Shot()
        {
            foreach (RoundPhase phase in System.Enum.GetValues(typeof(RoundPhase)))
            foreach (double t in new[] { 0.0, 1.0, 2.99 })
            {
                ArenaState s = At(phase, t);
                Assert.AreEqual(phase == RoundPhase.ShootingWindow, s.VideoBoard == VideoBoardContent.CalmStill, phase + " at " + t + " s");
                Assert.AreEqual(s.CalmAreaRules, s.VideoBoard == VideoBoardContent.CalmStill, "the same window as the Calm Area rules, " + phase);
                if (s.VideoBoard == VideoBoardContent.CalmStill) Assert.AreEqual(new VideoBoardWords("", ""), s.VideoBoardWords, "no words");
            }
        }

        [Test]
        public void Video_Board_ads_never_show_in_the_shooting_window()
        {
            foreach (double t in new[] { 0.0, 1.0, 2.0, 2.99 })
                Assert.AreNotEqual(VideoBoardContent.AdSlide, At(RoundPhase.ShootingWindow, t).VideoBoard);
        }

        [Test]
        public void Video_Board_ad_slides_never_show_from_goalie_set_to_the_Shot_whatever_came_before()
        {
            foreach (ArenaResult result in Results)
            foreach (bool hidden in new[] { false, true })
            foreach (bool newBest in new[] { false, true })
            foreach (double t in new[] { 0.0, 0.01, 1.5, 2.99, 3.0 })
            {
                ArenaState s = At(RoundPhase.ShootingWindow, t, result, hidden, newBest, goals: 2, shot: 4);
                Assert.AreEqual(VideoBoardContent.CalmStill, s.VideoBoard, "one dim still colour, no ad slide, at " + t + " s after " + result);
            }
        }

        [Test]
        public void Video_Board_ad_slides_show_only_outside_the_shooting_window()
        {
            foreach (RoundPhase phase in Phases)
            foreach (ArenaResult result in Results)
            foreach (bool hidden in new[] { false, true })
                if (At(phase, 1.0, result, hidden).VideoBoard == VideoBoardContent.AdSlide)
                    Assert.AreNotEqual(RoundPhase.ShootingWindow, phase, result.ToString());
            Assert.AreEqual(VideoBoardContent.AdSlide, Showing(ArenaResult.WideMiss).VideoBoard, "a slide fills a quiet result");
        }

        // ---- the Video Board's words (the Centre Board shows the same) ----

        private static VideoBoardWords Words(RoundPhase phase, ArenaResult result = ArenaResult.None, bool newBest = false,
                                             int goals = 2, int shot = 4, int best = 3, double? speed = 92.4) =>
            ArenaShow.At(new ArenaShowInput
            {
                Phase = phase,
                PhaseSeconds = 1.0,
                Result = result,
                IsNewBest = newBest,
                Goals = goals,
                ShotNumber = shot,
                BestScore = best,
                LastShotSpeedKmh = speed
            }).VideoBoardWords;

        [Test]
        public void The_start_screen_Video_Board_shows_the_game_name_the_best_score_and_SHOOT_TO_PLAY()
        {
            VideoBoardWords w = Words(RoundPhase.Idle, best: 3);
            Assert.AreEqual(ArenaShow.GameName, w.Headline);
            Assert.AreEqual("BEST SCORE 3 OF 6\nSHOOT TO PLAY", w.Details);
        }

        [Test]
        public void The_skate_in_Video_Board_shows_the_score_and_the_last_Shot_speed()
        {
            VideoBoardWords w = Words(RoundPhase.SkateIn, goals: 2, shot: 4, speed: 92.4);
            Assert.AreEqual("GOALS 2 · SHOT 4 OF 6", w.Headline);
            Assert.AreEqual("LAST SHOT 92 km/h", w.Details);
            Assert.AreEqual("", Words(RoundPhase.SkateIn, shot: 1, speed: null).Details, "no Shot with a puck yet");
            Assert.AreEqual(new VideoBoardWords("", ""), Words(RoundPhase.ShootingWindow, goals: 2, shot: 4, speed: 92.4), "no words from goalie set to the Shot: the Calm Area's dim still colour");
        }

        [Test]
        public void The_Round_over_Video_Board_shows_the_final_score_or_NEW_BEST()
        {
            VideoBoardWords final = Words(RoundPhase.RoundOver, goals: 4, shot: 6, best: 5);
            Assert.AreEqual("FINAL SCORE", final.Headline);
            Assert.AreEqual("4 OF 6 GOALS\nBEST 5 OF 6", final.Details);
            VideoBoardWords best = Words(RoundPhase.RoundOver, newBest: true, goals: 5, shot: 6, best: 5);
            Assert.AreEqual("NEW BEST!", best.Headline);
            Assert.AreEqual("5 OF 6 GOALS", best.Details);
        }

        [Test]
        public void A_result_on_the_Video_Board_shows_its_words_and_an_ad_slide_brings_its_own()
        {
            Assert.AreEqual(new VideoBoardWords("GOAL!", "GOALS 2 · SHOT 4 OF 6"), Words(RoundPhase.ResultHold, ArenaResult.Goal));
            Assert.AreEqual("REPLAY", Words(RoundPhase.ResultHold, ArenaResult.Save).Headline);
            Assert.AreEqual(new VideoBoardWords("", ""), Words(RoundPhase.ResultHold, ArenaResult.WideMiss));
        }

        [Test]
        public void Crowd_zones_follow_the_phase_and_result()
        {
            foreach (ArenaZone zone in Zones)
            {
                Assert.AreEqual(CrowdState.Clap, At(RoundPhase.Idle).CrowdIn(zone), "start screen, " + zone);
                Assert.AreEqual(CrowdState.Wave, At(RoundPhase.SkateIn).CrowdIn(zone), "skate-in, " + zone);
                Assert.AreEqual(CrowdState.Sit, At(RoundPhase.SlowDown).CrowdIn(zone), "slow-down, " + zone);
                Assert.AreEqual(CrowdState.Cheer, Showing(ArenaResult.Goal).CrowdIn(zone), "Goal, " + zone);
                Assert.AreEqual(CrowdState.Sit, Showing(ArenaResult.WideMiss).CrowdIn(zone), "Wide Miss, " + zone);
                Assert.AreEqual(CrowdState.Sit, Showing(ArenaResult.TooLate).CrowdIn(zone), "Too late, " + zone);
                Assert.AreEqual(CrowdState.Clap, At(RoundPhase.RoundOver).CrowdIn(zone), "Round over, " + zone);
            }
            Assert.AreEqual(CrowdState.Sit, At(RoundPhase.ShootingWindow).CrowdIn(ArenaZone.Near));
            Assert.AreEqual(CrowdState.Sit, At(RoundPhase.ShootingWindow).CrowdIn(ArenaZone.Far));
            foreach (ArenaResult closeCall in new[] { ArenaResult.Save, ArenaResult.FrameHitPost, ArenaResult.FrameHitBar })
            {
                Assert.AreEqual(CrowdState.Oh, Showing(closeCall).CrowdIn(ArenaZone.Near), closeCall.ToString());
                Assert.AreEqual(CrowdState.Oh, Showing(closeCall).CrowdIn(ArenaZone.CalmArea), closeCall.ToString());
                Assert.AreEqual(CrowdState.Sit, Showing(closeCall).CrowdIn(ArenaZone.Far), closeCall.ToString());
            }
        }

        [Test]
        public void The_Goal_Show_lights_go_on_a_Goal_and_never_flash_faster_than_3_a_second()
        {
            GoalShowLights goal = Showing(ArenaResult.Goal).GoalShow;
            Assert.IsTrue(goal.GoalLight);
            Assert.IsTrue(goal.RoofPulse);
            Assert.Greater(goal.FlashesPerSecond, 0);
            Assert.LessOrEqual(ArenaShow.MaxFlashesPerSecond, 3.0);

            foreach (RoundPhase phase in Phases)
            foreach (ArenaResult result in Results)
            foreach (bool newBest in new[] { false, true })
            {
                // The light at every millisecond of 6 s in this phase.
                var lit = new bool[6001];
                for (int ms = 0; ms < lit.Length; ms++)
                {
                    GoalShowLights lights = At(phase, ms / 1000.0, result, newBest: newBest).GoalShow;
                    Assert.LessOrEqual(lights.FlashesPerSecond, ArenaShow.MaxFlashesPerSecond);
                    lit[ms] = lights.Lit;
                }
                AssertAtMost3FlashesInAnySecond(lit, phase + ", " + result + ", new best " + newBest);
            }
        }

        [Test]
        public void A_last_Goal_into_the_new_best_show_never_flashes_faster_than_3_a_second()
        {
            // One timeline: the puck flies (hidden), the Goal Show for the whole result hold, then Round over with a new best.
            foreach (double hold in new[] { 2.5, 2.3, 4.5 })
            foreach (double delay in new[] { 0.0, 0.137, 0.5 })
            {
                int holdMs = (int)Math.Round((delay + hold) * 1000);
                var lit = new bool[holdMs + 3000];
                for (int ms = 0; ms < lit.Length; ms++)
                    lit[ms] = ms < holdMs
                        ? GoalHold(ms / 1000.0, delay).GoalShow.Lit
                        : At(RoundPhase.RoundOver, (ms - holdMs) / 1000.0, ArenaResult.Goal, newBest: true).GoalShow.Lit;
                AssertAtMost3FlashesInAnySecond(lit, "hold " + hold + " s, puck flight " + delay + " s");
            }
        }

        [Test]
        public void The_Goal_Show_is_the_same_on_every_Goal()
        {
            // However long the puck flew, whatever the score: the same lights at the same time after the puck lands.
            var cases = new (double delay, int goals, int shot, bool newBest)[] { (0.137, 3, 4, true), (0.5, 6, 6, false), (0.31, 1, 2, false) };
            for (int ms = 0; ms <= 2500; ms++)
            {
                GoalShowLights first = GoalHold(ms / 1000.0, 0.0, goals: 1, shot: 1).GoalShow;
                if (ms == 0) Assert.IsTrue(first.Lit, "the roof lights start with the horn");
                foreach (var c in cases)
                {
                    GoalShowLights other = GoalHold(c.delay + ms / 1000.0, c.delay, c.goals, c.shot, c.newBest).GoalShow;
                    Assert.AreEqual(first, other, ms + " ms after the puck lands, " + c);
                }
            }
        }

        [Test]
        public void No_Goal_Show_light_before_the_Shot_or_without_a_Goal_or_new_best()
        {
            foreach (RoundPhase phase in Phases)
            foreach (ArenaResult result in Results)
            foreach (bool newBest in new[] { false, true })
            for (int ms = 0; ms <= 5000; ms += 7)
            {
                bool show = phase == RoundPhase.ResultHold && result == ArenaResult.Goal || phase == RoundPhase.RoundOver && newBest;
                if (show) continue;
                GoalShowLights lights = At(phase, ms / 1000.0, result, newBest: newBest).GoalShow;
                string label = phase + ", " + result + ", new best " + newBest + ", " + ms + " ms";
                Assert.IsFalse(lights.GoalLight, label);
                Assert.IsFalse(lights.RoofPulse, label);
                Assert.IsFalse(lights.Lit, label);
            }
            // While the Goal's puck is still flying, nothing lights yet.
            Assert.AreEqual(new GoalShowLights(), GoalHold(0.2, 0.3).GoalShow);
        }

        [Test]
        public void The_roof_lights_pulse_Goal_red_on_a_Goal_and_new_best_gold_at_a_new_best()
        {
            Assert.AreEqual(ArenaShow.GoalRed, Showing(ArenaResult.Goal).GoalShow.PulseColour);
            Assert.AreEqual(ArenaShow.NewBestGold, At(RoundPhase.RoundOver, newBest: true).GoalShow.PulseColour);
        }

        /// <summary>A Goal's result hold, the puck flying (result hidden) for the first delay seconds.</summary>
        private static ArenaState GoalHold(double seconds, double delay, int goals = 1, int shot = 1, bool newBest = false) =>
            ArenaShow.At(new ArenaShowInput
            {
                Phase = RoundPhase.ResultHold,
                PhaseSeconds = seconds,
                Result = ArenaResult.Goal,
                ResultHidden = seconds < delay,
                ResultDelaySeconds = delay,
                IsNewBest = newBest,
                Goals = goals,
                ShotNumber = shot
            });

        /// <summary>Counts the light's off-to-on changes (one sample a millisecond) in every 1 s window: never over 3.</summary>
        private static void AssertAtMost3FlashesInAnySecond(bool[] litPerMs, string label)
        {
            var starts = new System.Collections.Generic.List<int>();
            for (int ms = 1; ms < litPerMs.Length; ms++)
                if (litPerMs[ms] && !litPerMs[ms - 1]) starts.Add(ms);
            if (litPerMs.Length > 0 && litPerMs[0]) starts.Insert(0, 0);
            for (int i = 0; i + 3 < starts.Count; i++)
                Assert.GreaterOrEqual(starts[i + 3] - starts[i], 1000, label + ": 4 flashes in under 1 s from " + starts[i] + " ms");
        }

        [Test]
        public void No_Goal_Show_light_without_a_Goal()
        {
            foreach (ArenaResult result in Results)
                if (result != ArenaResult.Goal)
                    Assert.IsFalse(Showing(result).GoalShow.GoalLight, result.ToString());
        }

        [Test]
        public void A_new_best_gets_a_small_light_show_at_Round_over()
        {
            Assert.IsTrue(At(RoundPhase.RoundOver, newBest: true).GoalShow.RoofPulse);
            Assert.IsFalse(At(RoundPhase.RoundOver, newBest: true).GoalShow.GoalLight);
            Assert.IsFalse(At(RoundPhase.RoundOver).GoalShow.RoofPulse);
        }

        [Test]
        public void The_chant_is_on_only_on_the_start_screen_and_at_Round_over()
        {
            foreach (RoundPhase phase in Phases)
                Assert.AreEqual(phase == RoundPhase.Idle || phase == RoundPhase.RoundOver, At(phase).Chant, phase.ToString());
        }

        [Test]
        public void The_camera_is_on_the_net_outside_the_result_hold()
        {
            foreach (RoundPhase phase in Phases)
            foreach (ArenaResult result in Results)
                if (phase != RoundPhase.ResultHold)
                    Assert.AreEqual(CameraMode.OnNet, At(phase, 3.0, result).Camera, phase + ", " + result);
        }

        // ---- Score Box ----

        [Test]
        public void The_Score_Box_shows_the_score_all_Round()
        {
            Assert.IsFalse(At(RoundPhase.Idle).ScoreBox.Shown);
            foreach (RoundPhase phase in Phases)
                if (phase != RoundPhase.Idle)
                    Assert.IsTrue(At(phase).ScoreBox.Shown, phase.ToString());
            Assert.AreEqual("GOALS 2 · SHOT 4 OF 6", At(RoundPhase.SkateIn, goals: 2, shot: 4).ScoreBox.Text);
        }

        [Test]
        public void The_Score_Box_counts_a_Goal_only_once_its_puck_lands()
        {
            Assert.AreEqual("GOALS 2 · SHOT 3 OF 6", At(RoundPhase.ResultHold, 0.1, ArenaResult.Goal, hidden: true, goals: 3, shot: 3).ScoreBox.Text);
            Assert.AreEqual("GOALS 3 · SHOT 3 OF 6", At(RoundPhase.ResultHold, 0.5, ArenaResult.Goal, goals: 3, shot: 3).ScoreBox.Text);
        }

        // ---- from the game's own screen flow ----

        [Test]
        public void From_the_screen_flow_a_Round_drives_the_show_end_to_end()
        {
            var flow = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches());
            Assert.AreEqual(VideoBoardContent.StartCard, ArenaShow.From(flow).VideoBoard);

            flow.Press();
            Assert.AreEqual(CrowdState.Wave, ArenaShow.From(flow).CrowdIn(ArenaZone.Near));
            flow.Tick(3.7 + 0.5); // skate-in, slow-down, half a second into the window
            Assert.IsTrue(ArenaShow.From(flow).CalmAreaRules);

            flow.SubmitShot(50, 90, 100, false); // nothing reaches: a Goal, at its Whistle
            ArenaState inFlight = ArenaShow.From(flow);
            Assert.IsFalse(inFlight.CalmAreaRules);
            Assert.AreEqual(RibbonMessage.Ads, inFlight.RibbonIn(ArenaZone.CalmArea).Message);
            Assert.AreEqual("GOALS 0 · SHOT 1 OF 6", inFlight.ScoreBox.Text);

            flow.Tick(0.3);                      // the puck's flight
            flow.Round.WhistleNow();             // the line crossing
            flow.Tick(0.1);
            ArenaState landed = ArenaShow.From(flow);
            Assert.AreEqual(RibbonMessage.Goal, landed.RibbonIn(ArenaZone.CalmArea).Message);
            Assert.AreEqual("GOALS 1 · SHOT 1 OF 6", landed.ScoreBox.Text);
        }

        [Test]
        public void From_the_screen_flow_the_start_screen_after_a_Round_is_the_start_screen()
        {
            var save = new SaveData();
            var flow = new ScreenFlow(ScriptedRandom.Halves(), save, FakeReach.NeverReaches());
            flow.Press();
            for (int shot = 0; shot < Round.ShotsPerRound; shot++)
                flow.Tick(3.7 + 3.0 + 2.5 + 0.01); // no Shot in time, then the hold
            Assert.AreEqual(FlowScreen.End, flow.Screen);
            Assert.AreEqual(VideoBoardContent.Final, ArenaShow.From(flow).VideoBoard);
            Assert.IsTrue(ArenaShow.From(flow).Chant);

            flow.Tick(save.AutoReturnSeconds);
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
            ArenaState start = ArenaShow.From(flow);
            Assert.AreEqual(VideoBoardContent.StartCard, start.VideoBoard);
            Assert.IsFalse(start.ScoreBox.Shown);
        }

        [Test]
        public void From_the_screen_flow_the_skate_in_shows_the_last_Shots_speed_and_the_start_card_the_saved_best()
        {
            var save = new SaveData { BestScore = 3 };
            var flow = new ScreenFlow(ScriptedRandom.Halves(), save, FakeReach.NeverReaches());
            Assert.AreEqual("BEST SCORE 3 OF 6\nSHOOT TO PLAY", ArenaShow.From(flow).VideoBoardWords.Details);

            flow.Press();
            Assert.AreEqual("", ArenaShow.From(flow).VideoBoardWords.Details, "shot 1: no last Shot yet");
            flow.Tick(3.7 + 0.5);
            flow.SubmitShot(50, 90, 87.6, false);
            flow.Round.WhistleNow();
            flow.Tick(ReplayTiming.HoldSeconds + 0.01); // a Goal's result hold (with its Replay), into shot 2's skate-in
            Assert.AreEqual(RoundPhase.SkateIn, flow.Round.Phase);
            Assert.AreEqual("LAST SHOT 88 km/h", ArenaShow.From(flow).VideoBoardWords.Details);

            flow.Tick(3.7 + 3.0 + 2.5 + 0.01); // shot 2: no Shot in time (no puck, no speed)
            Assert.AreEqual(RoundPhase.SkateIn, flow.Round.Phase);
            Assert.AreEqual(3, flow.Round.ShotNumber);
            Assert.AreEqual("", ArenaShow.From(flow).VideoBoardWords.Details, "Too late has no speed");

            for (int shot = 3; shot <= Round.ShotsPerRound; shot++)
                flow.Tick(3.7 + 3.0 + 2.5 + 0.01); // the rest: no Shot in time
            flow.Tick(save.AutoReturnSeconds);
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
            save.BestScore = 0; // a reset on the settings screen shows on the start card at once
            Assert.AreEqual("BEST SCORE 0 OF 6\nSHOOT TO PLAY", ArenaShow.From(flow).VideoBoardWords.Details);
        }
    }
}
