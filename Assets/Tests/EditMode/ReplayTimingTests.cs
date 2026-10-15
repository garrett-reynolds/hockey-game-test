using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Replay's timing (#91): which results get one, how long the result holds, and the camera's part in it.</summary>
    public class ReplayTimingTests
    {
        const double Tol = 1e-9;
        const double Flight = 0.3; // Shot to Whistle: the physics puck's flight to the line or its Miss (Round.Whistle)

        static readonly double PostX = -NetFrame.PostWidth / 2, BarY = 100 + NetFrame.CrossbarHeight / 2;

        static Round InWindow(IGoalieReach reach, bool replayOn = true)
        {
            var settings = new RoundSettings { ReplayOn = replayOn };
            var round = new Round(ScriptedRandom.Forcing(AttackSide.Middle), settings, reach, 0);
            round.StartRound();
            round.Tick(settings.SkateInSeconds + settings.SlowDownSeconds + 1.0);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            return round;
        }

        /// <summary>A Round at this Shot's Whistle ("late": no Shot in time), Flight after the Shot.</summary>
        static Round After(string shot, bool replayOn = true)
        {
            Round round = InWindow(shot == "save" ? FakeReach.InTheWay() : FakeReach.NeverReaches(), replayOn);
            switch (shot)
            {
                case "goal": round.SubmitShot(50, 50, 100, false); break;
                case "save": round.SubmitShot(50, 50, 100, false); break;
                case "post": round.SubmitShot(PostX, 50, 100, false); break;
                case "bar": round.SubmitShot(50, BarY, 100, false); break;
                case "wide": round.SubmitShot(-20, 50, 100, false); break;
                case "late": round.Tick(round.Settings.ShootingWindowSeconds - 1.0); break; // to the end of the window
            }
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            if (shot != "late") { round.Tick(Flight); round.WhistleNow(); } // the puck gets there: its Whistle
            return round;
        }

        static readonly string[] WithReplay = { "goal", "save", "post", "bar" };
        static readonly string[] WithoutReplay = { "wide", "late" };

        // ---- which results get a Replay ----

        [Test]
        public void A_Replay_follows_a_Goal_a_Save_and_a_Frame_Hit_but_not_a_Wide_Miss_or_Too_late()
        {
            Assert.IsTrue(ReplayTiming.Follows(ArenaResult.Goal));
            Assert.IsTrue(ReplayTiming.Follows(ArenaResult.Save));
            Assert.IsTrue(ReplayTiming.Follows(ArenaResult.FrameHitPost));
            Assert.IsTrue(ReplayTiming.Follows(ArenaResult.FrameHitBar));
            Assert.IsFalse(ReplayTiming.Follows(ArenaResult.WideMiss));
            Assert.IsFalse(ReplayTiming.Follows(ArenaResult.TooLate));
            Assert.IsFalse(ReplayTiming.Follows(ArenaResult.None));
            foreach (string shot in WithReplay)
                Assert.IsTrue(ReplayTiming.Follows(After(shot).LastResult), shot);
            foreach (string shot in WithoutReplay)
                Assert.IsFalse(ReplayTiming.Follows(After(shot).LastResult), shot);
        }

        // ---- the result time ----

        [Test]
        public void The_Replay_hold_is_1_5_s_on_the_net_0_5_s_up_2_s_of_Replay_and_0_5_s_back()
        {
            Assert.AreEqual(1.5, ReplayTiming.OnNetSeconds);
            Assert.AreEqual(0.5, ReplayTiming.LookUpSeconds);
            Assert.AreEqual(2.0, ReplayTiming.ShowSeconds);
            Assert.AreEqual(0.5, ReplayTiming.LookBackSeconds);
            Assert.AreEqual(4.5, ReplayTiming.HoldSeconds, Tol);
        }

        [Test]
        public void The_result_holds_about_4_5_s_with_a_Replay_from_when_the_puck_lands()
        {
            foreach (string shot in WithReplay)
            {
                Round round = After(shot);
                // The Replay is 4.5 s; before Shots 2 to 6 the hold runs the fade's lead (0.35 s) longer, so the fade starts after the look-back (#129).
                double hold = ReplayTiming.HoldSeconds + ShotFade.LeadSeconds;
                Assert.AreEqual(4.85, round.ResultHoldLength, Tol, shot);
                round.Tick(hold - 0.01); // After has ticked the flight: the hold runs from the Whistle
                Assert.AreEqual(RoundPhase.ResultHold, round.Phase, shot + ": still holding just before the end");
                round.Tick(0.02);
                Assert.AreEqual(RoundPhase.SkateIn, round.Phase, shot + ": the next Shot after the hold");
                Assert.AreEqual(2, round.ShotNumber, shot);
            }
        }

        // ---- the clip hold (PROTOTYPE): ClipHoldSeconds more after a Goal or a Save with no Replay ----
        // The clip starts 0.3 s after the Whistle (not after the Replay). With a Replay the hold (4.5 s, 4.85 s before
        // Shots 2 to 6) already fits a 2.9 s clip and a 1.2 s blend out; the 2.5 s hold with no Replay does not, so only it grows.

        [Test]
        public void ClipHoldSeconds_adds_to_the_hold_of_a_Goal_and_a_Save_with_no_Replay_only_and_0_keeps_today_s_timing()
        {
            foreach (bool replayOn in new[] { true, false })
            {
                foreach (string shot in new[] { "goal", "save", "post", "bar", "wide", "late" })
                {
                    Round round = After(shot, replayOn);
                    double today = round.ResultHoldLength;
                    round.Settings.ClipHoldSeconds = 1.5;
                    double extra = !replayOn && (shot == "goal" || shot == "save") ? 1.5 : 0;
                    Assert.AreEqual(today + extra, round.ResultHoldLength, Tol, shot + " replay " + replayOn);
                    round.Settings.ClipHoldSeconds = 0;
                    Assert.AreEqual(today, round.ResultHoldLength, Tol, shot + " 0 = today");
                }
            }
        }

        [Test]
        public void With_ClipHoldSeconds_a_Goal_with_the_Replay_keeps_the_Replay_s_own_hold()
        {
            Round round = After("goal");
            round.Settings.ClipHoldSeconds = BreakawayView.ClipHoldSecondsOn;
            double hold = ReplayTiming.HoldSeconds + ShotFade.LeadSeconds;
            Assert.AreEqual(hold, round.ResultHoldLength, Tol);
            round.Tick(hold - 0.01);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(0.02);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
        }

        [Test]
        public void With_ClipHoldSeconds_a_Goal_with_no_Replay_holds_long_enough_for_a_2_9_s_clip_and_the_default_blend()
        {
            Round round = After("goal", false);
            round.Settings.ClipHoldSeconds = BreakawayView.ClipHoldSecondsOn;
            Assert.AreEqual(1.5, BreakawayView.ClipHoldSecondsOn, Tol);
            double window = round.ResultHoldLength - GoalieClipReactions.AfterWhistleSeconds;
            Assert.GreaterOrEqual(window - GoalieClipReactions.DefaultBlendSeconds, 2.9 - Tol, "the clip plays at its own speed");
            round.Tick(round.ResultHoldLength - 0.01);
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(0.02);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
        }

        [Test]
        public void The_result_holds_2_5_s_with_no_Replay()
        {
            foreach (string shot in WithoutReplay)
            {
                Round round = After(shot);
                Assert.AreEqual(2.5, round.ResultHoldLength, Tol, shot);
                round.Tick(2.5 - 0.01); // After has ticked the flight: the hold runs from the Whistle (Too late: from the window's end)
                Assert.AreEqual(RoundPhase.ResultHold, round.Phase, shot);
                round.Tick(0.02);
                Assert.AreEqual(RoundPhase.SkateIn, round.Phase, shot + ": the next Shot after 2.5 s");
            }
        }

        [Test]
        public void The_shooting_window_and_the_normal_hold_do_not_change()
        {
            var settings = new RoundSettings();
            Assert.AreEqual(2.5, settings.ResultHoldSeconds);
            Assert.AreEqual(3.0, settings.ShootingWindowSeconds);
        }

        // ---- the camera, from the Arena Show ----

        static ArenaState Show(ArenaResult result, double landedSeconds, bool hidden = false) =>
            ArenaShow.At(new ArenaShowInput
            {
                Phase = RoundPhase.ResultHold,
                PhaseSeconds = Flight + landedSeconds,
                Result = result,
                ResultHidden = hidden,
                ResultDelaySeconds = Flight,
                ShotNumber = 1
            });

        static readonly ArenaResult[] ReplayResults = { ArenaResult.Goal, ArenaResult.Save, ArenaResult.FrameHitPost, ArenaResult.FrameHitBar };

        [Test]
        public void After_a_Goal_Save_or_Frame_Hit_the_camera_stays_on_the_net_looks_up_shows_the_Replay_and_looks_back()
        {
            foreach (ArenaResult result in ReplayResults)
            {
                Assert.AreEqual(CameraMode.OnNet, Show(result, -0.2, hidden: true).Camera, result + ": puck in flight");
                Assert.AreEqual(CameraMode.OnNet, Show(result, 0.0).Camera, result.ToString());
                Assert.AreEqual(CameraMode.OnNet, Show(result, 1.49).Camera, result.ToString());
                Assert.AreEqual(CameraMode.LookUp, Show(result, 1.51).Camera, result.ToString());
                Assert.AreEqual(CameraMode.LookUp, Show(result, 1.99).Camera, result.ToString());
                Assert.AreEqual(CameraMode.Replay, Show(result, 2.01).Camera, result.ToString());
                Assert.AreEqual(CameraMode.Replay, Show(result, 3.99).Camera, result.ToString());
                Assert.AreEqual(CameraMode.LookBack, Show(result, 4.01).Camera, result.ToString());
                Assert.AreEqual(CameraMode.LookBack, Show(result, 4.49).Camera, result.ToString());
            }
        }

        [Test]
        public void After_a_Wide_Miss_or_Too_late_the_camera_stays_on_the_net()
        {
            foreach (ArenaResult result in new[] { ArenaResult.WideMiss, ArenaResult.TooLate })
                for (double t = 0; t <= 2.5; t += 0.05)
                {
                    ArenaState state = Show(result, t);
                    Assert.AreEqual(CameraMode.OnNet, state.Camera, result + " at " + t);
                    Assert.AreEqual(0, state.CameraLookUp, result + " at " + t);
                    Assert.AreNotEqual(VideoBoardContent.Replay, state.VideoBoard, result + " at " + t);
                }
        }

        [Test]
        public void The_camera_eases_up_to_the_Video_Board_and_back_down()
        {
            Assert.AreEqual(0, Show(ArenaResult.Save, 1.0).CameraLookUp, Tol);
            Assert.AreEqual(0.5, Show(ArenaResult.Save, 1.75).CameraLookUp, Tol);
            Assert.AreEqual(1, Show(ArenaResult.Save, 2.0).CameraLookUp, Tol);
            Assert.AreEqual(1, Show(ArenaResult.Save, 3.0).CameraLookUp, Tol);
            Assert.AreEqual(0.5, Show(ArenaResult.Save, 4.25).CameraLookUp, Tol);
            Assert.AreEqual(0, Show(ArenaResult.Save, 4.5).CameraLookUp, Tol);
            double last = 0;
            for (double t = 1.5; t <= 2.0; t += 0.01)
            {
                double up = Show(ArenaResult.Goal, t).CameraLookUp;
                Assert.GreaterOrEqual(up, last - Tol, "never turns back down on the way up, at " + t);
                last = up;
            }
        }

        [Test]
        public void Both_boards_play_the_Replay_only_while_the_Replay_shows()
        {
            Assert.AreEqual(VideoBoardContent.Goal, Show(ArenaResult.Goal, 1.0).VideoBoard, "the Goal first");
            Assert.AreEqual(VideoBoardContent.Goal, Show(ArenaResult.Goal, 1.8).VideoBoard);
            Assert.AreEqual(VideoBoardContent.Replay, Show(ArenaResult.Goal, 2.5).VideoBoard, "then the Replay");
            Assert.AreEqual(VideoBoardContent.Replay, Show(ArenaResult.Save, 2.5).VideoBoard);
            Assert.AreEqual(VideoBoardContent.Replay, Show(ArenaResult.FrameHitPost, 2.5).VideoBoard);
            Assert.AreEqual(0.5, Show(ArenaResult.Save, 2.5).ReplaySeconds, Tol);
            Assert.AreEqual(0, Show(ArenaResult.Save, 1.0).ReplaySeconds, Tol);
        }

        [Test]
        public void From_the_screen_flow_a_Goal_runs_the_whole_Replay_and_then_the_next_Shot()
        {
            var flow = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches());
            flow.Press();
            flow.Tick(3.7 + 0.5);
            flow.SubmitShot(50, 90, 100, false); // nothing reaches: a Goal
            flow.Tick(Flight);
            flow.Round.WhistleNow();
            Assert.AreEqual(CameraMode.OnNet, ArenaShow.From(flow).Camera);
            flow.Tick(1.75);
            Assert.AreEqual(CameraMode.LookUp, ArenaShow.From(flow).Camera);
            flow.Tick(1.0);
            Assert.AreEqual(CameraMode.Replay, ArenaShow.From(flow).Camera);
            Assert.AreEqual(VideoBoardContent.Replay, ArenaShow.From(flow).VideoBoard);
            flow.Tick(1.5);
            Assert.AreEqual(CameraMode.LookBack, ArenaShow.From(flow).Camera);
            flow.Tick(0.26);
            Assert.AreEqual(RoundPhase.ResultHold, flow.Round.Phase, "the Replay is over; the fade to black runs before Shot 2 (#129)");
            flow.Tick(ShotFade.LeadSeconds);
            Assert.AreEqual(RoundPhase.SkateIn, flow.Round.Phase);
            Assert.AreEqual(CameraMode.OnNet, ArenaShow.From(flow).Camera);
        }

        // ---- Replay off (#145) ----

        static ArenaState ShowNoReplay(ArenaResult result, double landedSeconds, bool hidden = false) =>
            ArenaShow.At(new ArenaShowInput
            {
                Phase = RoundPhase.ResultHold,
                PhaseSeconds = Flight + landedSeconds,
                Result = result,
                ResultHidden = hidden,
                ResultDelaySeconds = Flight,
                ShotNumber = 1,
                NoReplay = true
            });

        [Test]
        public void With_the_Replay_off_no_Replay_follows_any_result_and_with_it_on_the_Round_is_as_before()
        {
            foreach (string shot in WithReplay)
            {
                Assert.IsFalse(After(shot, replayOn: false).ReplayFollows, shot + ": Replay off");
                Assert.IsTrue(After(shot).ReplayFollows, shot + ": Replay on");
            }
            foreach (string shot in WithoutReplay)
            {
                Assert.IsFalse(After(shot, replayOn: false).ReplayFollows, shot);
                Assert.IsFalse(After(shot).ReplayFollows, shot);
            }
        }

        [Test]
        public void With_the_Replay_off_a_Goal_Save_or_Frame_Hit_still_holds_4_5_s_with_the_fade_lead_and_a_Wide_Miss_or_Too_late_2_5_s()
        {
            foreach (string shot in WithReplay)
            {
                Round round = After(shot, replayOn: false);
                Assert.AreEqual(4.85, round.ResultHoldLength, Tol, shot);
                round.Tick(ReplayTiming.HoldSeconds + ShotFade.LeadSeconds - 0.01);
                Assert.AreEqual(RoundPhase.ResultHold, round.Phase, shot + ": still holding just before the end");
                round.Tick(0.02);
                Assert.AreEqual(RoundPhase.SkateIn, round.Phase, shot + ": the next Shot after the hold");
            }
            foreach (string shot in WithoutReplay)
            {
                Round round = After(shot, replayOn: false);
                Assert.AreEqual(2.5, round.ResultHoldLength, Tol, shot);
                round.Tick(2.5 - 0.01);
                Assert.AreEqual(RoundPhase.ResultHold, round.Phase, shot);
                round.Tick(0.02);
                Assert.AreEqual(RoundPhase.SkateIn, round.Phase, shot);
            }
        }

        [Test]
        public void With_the_Replay_off_the_camera_stays_on_the_net_and_the_board_shows_the_result_word_for_the_whole_hold()
        {
            var words = new System.Collections.Generic.Dictionary<ArenaResult, string>
            {
                { ArenaResult.Goal, "GOAL!" }, { ArenaResult.Save, "SAVE" },
                { ArenaResult.FrameHitPost, "OFF THE POST!" }, { ArenaResult.FrameHitBar, "OFF THE BAR!" }
            };
            foreach (ArenaResult result in ReplayResults)
                for (double t = 0; t <= 4.5; t += 0.05)
                {
                    ArenaState state = ShowNoReplay(result, t);
                    string at = result + " at " + t;
                    Assert.AreEqual(CameraMode.OnNet, state.Camera, at);
                    Assert.AreEqual(0, state.CameraLookUp, at);
                    Assert.AreEqual(0, state.ReplaySeconds, at);
                    Assert.AreNotEqual(VideoBoardContent.Replay, state.VideoBoard, at);
                    Assert.AreEqual(words[result], state.VideoBoardWords.Headline, at);
                }
            Assert.AreEqual(VideoBoardContent.Score, ShowNoReplay(ArenaResult.Save, -0.2, hidden: true).VideoBoard, "the puck is still in flight: no result word yet");
        }

        [Test]
        public void With_the_Replay_off_a_Wide_Miss_and_Too_late_look_as_they_do_with_it_on()
        {
            foreach (ArenaResult result in new[] { ArenaResult.WideMiss, ArenaResult.TooLate })
                for (double t = 0; t <= 2.5; t += 0.05)
                {
                    ArenaState on = Show(result, t), off = ShowNoReplay(result, t);
                    Assert.AreEqual(on.Camera, off.Camera, result + " at " + t);
                    Assert.AreEqual(on.VideoBoard, off.VideoBoard, result + " at " + t);
                    Assert.AreEqual(on.VideoBoardWords, off.VideoBoardWords, result + " at " + t);
                }
        }

        [Test]
        public void From_the_screen_flow_with_the_Replay_off_a_Goal_holds_on_the_net_and_then_the_next_Shot()
        {
            var settings = new RoundSettings { ReplayOn = false };
            var flow = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches(), settings);
            flow.Press();
            flow.Tick(3.7 + 0.5);
            flow.SubmitShot(50, 90, 100, false); // nothing reaches: a Goal
            flow.Tick(Flight);
            flow.Round.WhistleNow();
            foreach (double step in new[] { 0.0, 1.75, 1.0, 1.5 }) // 0, 1.75, 2.75, 4.25 s after the Whistle
            {
                flow.Tick(step);
                ArenaState state = ArenaShow.From(flow);
                Assert.AreEqual(CameraMode.OnNet, state.Camera, "at +" + step);
                Assert.AreEqual(VideoBoardContent.Goal, state.VideoBoard, "at +" + step);
            }
            flow.Tick(0.26);
            Assert.AreEqual(RoundPhase.ResultHold, flow.Round.Phase, "4.51 s: the 4.5 s hold, then the fade's lead");
            flow.Tick(ShotFade.LeadSeconds);
            Assert.AreEqual(RoundPhase.SkateIn, flow.Round.Phase);
        }

        [Test]
        public void With_the_Replay_off_the_lower_third_says_the_same_through_the_hold_as_with_it_on()
        {
            Round on = After("goal"), off = After("goal", replayOn: false);
            for (double t = 0; t <= 4.4; t += 0.05)
            {
                Assert.AreEqual(LowerThird.Of(on).Stat, LowerThird.Of(off).Stat, "at " + t);
                Assert.AreEqual(LowerThird.Of(on).Tally, LowerThird.Of(off).Tally, "at " + t);
                on.Tick(0.05);
                off.Tick(0.05);
            }
        }

        // ---- what the Replay shows ----

        [Test]
        public void The_Replay_plays_the_Shot_from_its_release_to_just_after_the_puck_lands()
        {
            foreach (double flight in new[] { 0.036, 0.3, 0.6, 1.0, 1.7 }) // the clip (flight + tail) fits ShowSeconds: from the release
            {
                Assert.AreEqual(0.0, ReplayTiming.ClipStart(flight), Tol, "starts at the release, flight " + flight);
                Assert.AreEqual(0.0, ReplayTiming.ShotSecondsAt(0, flight), Tol, "the first frame is the release, flight " + flight);
            }
            foreach (double flight in new[] { 0.036, 0.3, 0.6, 1.0, 1.7, 2.5, 7.0 })
            {
                Assert.AreEqual(flight + ReplayTiming.TailSeconds, ReplayTiming.ClipEnd(flight), Tol, "ends TailSeconds after the puck lands, flight " + flight);
                Assert.AreEqual(flight + ReplayTiming.TailSeconds, ReplayTiming.ShotSecondsAt(ReplayTiming.ShowSeconds, flight), Tol, "fills ShowSeconds, flight " + flight);
                Assert.AreEqual(flight + ReplayTiming.TailSeconds, ReplayTiming.ShotSecondsAt(ReplayTiming.ShowSeconds * 3, flight), Tol, "holds the last frame after ShowSeconds, flight " + flight);
            }
        }

        [Test]
        public void A_knocked_bottle_makes_the_clip_run_on_until_it_lands_and_no_knock_keeps_the_old_end()
        {
            foreach (double flight in new[] { 0.3, 0.6, 1.0, 2.5 })
            {
                Assert.AreEqual(flight + ReplayTiming.TailSeconds, ReplayTiming.ClipEnd(flight, false), Tol, "no knock: the old end, flight " + flight);
                Assert.GreaterOrEqual(ReplayTiming.ClipEnd(flight, true), flight + ReplayTiming.BottleTailSeconds, "knock: at least the bottle tail after the Whistle, flight " + flight);
                Assert.AreEqual(ReplayTiming.ClipEnd(flight, true), ReplayTiming.ShotSecondsAt(ReplayTiming.ShowSeconds, flight, true), Tol, "the clip's last frame, flight " + flight);
                Assert.LessOrEqual(ReplayTiming.ClipEnd(flight, true) - ReplayTiming.ClipStart(flight, true), ReplayTiming.ShowSeconds + Tol, "never faster than real time, flight " + flight);
            }
            Assert.AreEqual(1.2, ReplayTiming.BottleTailSeconds, Tol);
        }

        // Garrett, 2026-10-13: "I miss the shot in every replay." A knocked bottle (a hard Goal) stretched the clip by the bottle's 0.9 s,
        // and the clip is slowed to fill ShowSeconds as one: a 0.3 s flight got 0.43 s of the Replay and flashed by. Now the Shot keeps
        // its own part of the Replay and the bottle's fall plays after it at real time.
        [Test]
        public void With_a_knocked_bottle_the_Shot_still_plays_slowly_from_the_release_and_the_bottle_runs_on_at_real_time()
        {
            double bottle = ReplayTiming.BottleTailSeconds - ReplayTiming.TailSeconds;
            double shotShow = ReplayTiming.ShowSeconds - bottle; // the Shot's part of the Replay
            foreach (double flight in new[] { 0.036, 0.1, 0.3, 0.6 })
            {
                Assert.AreEqual(0.0, ReplayTiming.ClipStart(flight, true), Tol, "starts at the release, flight " + flight);
                Assert.AreEqual(0.0, ReplayTiming.ShotSecondsAt(0, flight, true), Tol, "the first frame is the release, flight " + flight);
                Assert.AreEqual(flight + ReplayTiming.TailSeconds, ReplayTiming.ShotSecondsAt(shotShow, flight, true), Tol, "the puck has landed when the Shot's part ends, flight " + flight);
                Assert.AreEqual(ReplayTiming.ShotSecondsAt(shotShow, flight, true) + 0.5, ReplayTiming.ShotSecondsAt(shotShow + 0.5, flight, true), Tol, "then real time, flight " + flight);
                Assert.AreEqual(ReplayTiming.ClipEnd(flight, true), ReplayTiming.ShotSecondsAt(ReplayTiming.ShowSeconds, flight, true), Tol, "ends when the bottle has landed, flight " + flight);
                double clipSecondsPerReplaySecond = (flight + ReplayTiming.TailSeconds) / shotShow;
                Assert.Less(clipSecondsPerReplaySecond, 1.0, "the Shot is in slow motion (under real time), flight " + flight);
            }
            // A quick hard Goal's flight (0.3 s) shows for about a third longer than the one-piece stretch gave it (0.4 s then, 0.55 s now).
            double onePieceSeconds = 0.3 / ReplayTiming.ClipEnd(0.3, true) * ReplayTiming.ShowSeconds;
            Assert.Greater(shotShow * 0.3 / (0.3 + ReplayTiming.TailSeconds), onePieceSeconds * 1.3, "more of the Replay shows the puck flying");
            double previous = -1;
            for (double s = 0; s <= ReplayTiming.ShowSeconds; s += 0.01)
            {
                double now = ReplayTiming.ShotSecondsAt(s, 0.3, true);
                Assert.GreaterOrEqual(now, previous - Tol, "the clip never runs backwards, at " + s);
                previous = now;
            }
        }

        [Test]
        public void The_Replay_plays_slow_motion_for_a_quick_Shot_and_at_a_readable_rate_for_the_slowest()
        {
            double Rate(double flight) => (ReplayTiming.ClipEnd(flight) - ReplayTiming.ClipStart(flight)) / ReplayTiming.ShowSeconds;
            Assert.Less(Rate(0.1), 0.25, "slow motion for a quick Shot");
            Assert.Less(Rate(1.0), 1.0, "a 1 s flight still plays slower than real time");
            // The slowest game Shot: 10 km/h from the far start (Breakaway.StartDistance), about 7.9 s of flight.
            double slowest = ShotFlight.Seconds(Breakaway.StartDistance, ShotFlight.MinSpeedKmh);
            Assert.LessOrEqual(Rate(slowest), 1.0 + Tol, "the slowest Shot never plays faster than real time");
            Assert.AreEqual(slowest + ReplayTiming.TailSeconds, ReplayTiming.ClipEnd(slowest), Tol, "and still ends where it did");
            Assert.AreEqual(ReplayTiming.ClipEnd(slowest) - ReplayTiming.ShowSeconds, ReplayTiming.ClipStart(slowest), Tol, "starting ShowSeconds before the end");
            Assert.Greater(ReplayTiming.ClipStart(slowest), 0.0, "after the release");
            foreach (double flight in new[] { 0.036, 0.5, 1.7, 2.0, 3.0, 7.9 })
                Assert.LessOrEqual(Rate(flight), 1.0 + Tol, "never faster than real time, flight " + flight);
        }
    }
}
