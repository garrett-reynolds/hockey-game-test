using System;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>Test Mode (#156): endless full-speed Shots from the shooter's spot, no slow-down, no slow-motion look, no Replay, nothing saved.</summary>
    public class TestModeTests
    {
        private static ScreenFlow NewFlow(SaveData save = null, RoundSettings settings = null) =>
            new ScreenFlow(ScriptedRandom.Halves(), save ?? new SaveData(), FakeReach.NeverReaches(), settings);

        private static ScreenFlow NewTestModeFlow(SaveData save = null, RoundSettings settings = null)
        {
            ScreenFlow flow = NewFlow(save, settings);
            flow.OpenSettings();
            flow.StartTestMode();
            return flow;
        }

        // One Shot: into the net (a Goal here), the Whistle, then the whole hold.
        private static void ShootAndWait(ScreenFlow flow, double x = 50, double y = 90)
        {
            flow.SubmitShot(x, y, 100, false);
            flow.Round.WhistleNow();
            flow.Tick(flow.Round.ResultHoldLength);
        }

        private static int[] BestsOf(SaveData save)
        {
            var bests = new int[11];
            for (int n = 3; n <= 10; n++) bests[n] = save.BestFor(n);
            bests[0] = save.BestScore;
            return bests;
        }

        // ---- opening it ----

        [Test]
        public void Opening_Test_Mode_puts_the_first_Shot_in_the_shooting_window_at_once()
        {
            ScreenFlow flow = NewTestModeFlow();
            Assert.IsTrue(flow.TestMode);
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            Assert.IsFalse(flow.SettingsOpen);
            Assert.AreEqual(RoundPhase.ShootingWindow, flow.Round.Phase);
            Assert.AreEqual(1, flow.Round.ShotNumber);
        }

        [Test]
        public void Test_Mode_opens_from_the_start_screen_only()
        {
            ScreenFlow flow = NewFlow();
            flow.Press(); // a normal Round
            flow.StartTestMode();
            Assert.IsFalse(flow.TestMode);
            Assert.AreEqual(RoundPhase.SkateIn, flow.Round.Phase);
        }

        [Test]
        public void A_Shot_is_decided_as_in_a_Round()
        {
            ScreenFlow flow = NewTestModeFlow();
            flow.SubmitShot(50, 90, 100, false); // gear never reaches
            flow.Round.WhistleNow();
            Assert.AreEqual(ShotResultKind.Goal, flow.Round.LastResult.Value.Kind);
            Assert.AreEqual(1, flow.Round.Goals);
            flow.Tick(flow.Round.ResultHoldLength);
            flow.SubmitShot(-10, 50, 100, true); // a Test Shot works too, and a wide one is a Miss
            flow.Round.WhistleNow();
            Assert.AreEqual(ShotResultKind.Miss, flow.Round.LastResult.Value.Kind);
        }

        // ---- endless ----

        [Test]
        public void Twenty_five_Shots_in_a_row_never_end_the_Round()
        {
            ScreenFlow flow = NewTestModeFlow();
            for (int shot = 1; shot <= 25; shot++)
            {
                Assert.AreEqual(RoundPhase.ShootingWindow, flow.Round.Phase, "Shot " + shot + " is in the window at once");
                Assert.AreEqual(shot, flow.Round.ShotNumber);
                ShootAndWait(flow, shot % 2 == 0 ? 50 : -10);
                Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            }
            Assert.AreEqual(RoundPhase.ShootingWindow, flow.Round.Phase);
            Assert.AreEqual(25, flow.Round.Results.Count);
        }

        [Test]
        public void Every_Shot_gets_a_Set_up_and_an_Attack_Side_from_a_deck_that_is_dealt_again_when_it_runs_out()
        {
            ScreenFlow flow = NewTestModeFlow();
            var sides = new System.Collections.Generic.HashSet<AttackSide>();
            var kinds = new System.Collections.Generic.HashSet<SetUpKind>();
            for (int shot = 1; shot <= 14; shot++)
            {
                sides.Add(flow.Round.AttackSide);
                kinds.Add(flow.Round.SetUp.Kind);
                ShootAndWait(flow);
            }
            Assert.AreEqual(3, sides.Count, "left, middle and right all come up");
            Assert.GreaterOrEqual(kinds.Count, 3);
        }

        [Test]
        public void There_is_no_Too_late_after_a_long_wait()
        {
            ScreenFlow flow = NewTestModeFlow();
            flow.Tick(60 * 60); // an hour
            Assert.AreEqual(RoundPhase.ShootingWindow, flow.Round.Phase);
            Assert.IsNull(flow.Round.LastResult);
            Assert.AreEqual(0, flow.Round.Results.Count);
            ShootAndWait(flow);
            Assert.AreEqual(ShotResultKind.Goal, flow.Round.Results[0], "a Shot after the long wait still counts");
        }

        [Test]
        public void After_the_Whistle_the_hold_is_the_one_named_value_and_the_next_Shot_starts_in_the_window()
        {
            var settings = new RoundSettings();
            ScreenFlow flow = NewTestModeFlow(null, settings);
            flow.SubmitShot(50, 90, 100, false);
            flow.Round.WhistleNow();
            Assert.AreEqual(RoundPhase.ResultHold, flow.Round.Phase);
            Assert.AreEqual(settings.TestModeHoldSeconds, flow.Round.ResultHoldLength, 1e-9);
            Assert.Less(settings.TestModeHoldSeconds, settings.ResultHoldSeconds, "short: about 1 s");
            flow.Tick(settings.TestModeHoldSeconds - 0.01);
            Assert.AreEqual(RoundPhase.ResultHold, flow.Round.Phase);
            Assert.IsTrue(flow.Round.ResultShowing);
            flow.Tick(0.02);
            Assert.AreEqual(RoundPhase.ShootingWindow, flow.Round.Phase);
            Assert.AreEqual(2, flow.Round.ShotNumber);
        }

        [Test]
        public void The_hold_is_short_for_a_Save_and_a_Frame_Hit_too_and_has_no_Replay_lead()
        {
            var settings = new RoundSettings();
            ScreenFlow flow = NewTestModeFlow(null, settings);
            foreach (double x in new[] { 50.0, -10.0, -NetFrame.PostWidth / 2 })
            {
                flow.SubmitShot(x, 50, 100, false);
                flow.Round.WhistleNow();
                Assert.AreEqual(settings.TestModeHoldSeconds, flow.Round.ResultHoldLength, 1e-9);
                flow.Tick(flow.Round.ResultHoldLength);
            }
        }

        [Test]
        public void A_Shot_during_the_hold_is_ignored()
        {
            ScreenFlow flow = NewTestModeFlow();
            flow.SubmitShot(50, 90, 100, false);
            flow.Round.WhistleNow();
            int submitted = flow.Round.ShotsSubmitted;
            flow.Tick(0.3);
            flow.SubmitShot(50, 90, 100, false);
            flow.SubmitShot(50, 90, 100, true);
            Assert.AreEqual(submitted, flow.Round.ShotsSubmitted);
            Assert.AreEqual(1, flow.Round.Results.Count);
            Assert.AreEqual(RoundPhase.ResultHold, flow.Round.Phase);
        }

        [Test]
        public void A_Shot_while_the_puck_is_still_in_flight_is_ignored_too()
        {
            ScreenFlow flow = NewTestModeFlow();
            flow.SubmitShot(50, 90, 100, false); // no Whistle yet
            flow.Tick(5);
            Assert.AreEqual(RoundPhase.ResultHold, flow.Round.Phase, "the hold waits for the Whistle");
            flow.SubmitShot(50, 90, 100, false);
            Assert.AreEqual(1, flow.Round.ShotsSubmitted);
        }

        // ---- nothing is saved ----

        [Test]
        public void The_best_score_and_save_data_never_change()
        {
            var save = new SaveData { BestScore = 2 };
            int[] before = BestsOf(save);
            ScreenFlow flow = NewTestModeFlow(save);
            int reports = 0;
            flow.BestScoreChanged += () => reports++;
            for (int shot = 0; shot < 25; shot++) ShootAndWait(flow); // 25 Goals, far over the best of 2
            Assert.AreEqual(25, flow.Round.Goals);
            Assert.IsFalse(flow.Round.IsNewBest);
            Assert.AreEqual(2, flow.Round.BestScore);
            Assert.AreEqual(before, BestsOf(save));
            Assert.AreEqual(0, reports);
            flow.LeaveTestMode();
            Assert.AreEqual(before, BestsOf(save));
            Assert.AreEqual(0, reports);
        }

        [Test]
        public void A_new_ScreenFlow_starts_at_Start_not_in_Test_Mode()
        {
            ScreenFlow flow = NewTestModeFlow();
            Assert.IsTrue(flow.TestMode);
            ScreenFlow next = NewFlow(new SaveData());
            Assert.AreEqual(FlowScreen.Start, next.Screen);
            Assert.IsFalse(next.TestMode);
            Assert.AreEqual(RoundPhase.Idle, next.Round.Phase);
            Assert.IsFalse(new RoundSettings().TestMode);
        }

        // ---- leaving ----

        [Test]
        public void Esc_goes_to_the_start_screen_and_the_next_Press_starts_a_normal_Round()
        {
            ScreenFlow flow = NewTestModeFlow();
            ShootAndWait(flow);
            flow.LeaveTestMode();
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
            Assert.IsFalse(flow.TestMode);
            Assert.AreEqual(RoundPhase.Idle, flow.Round.Phase);
            flow.Press();
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            Assert.AreEqual(RoundPhase.SkateIn, flow.Round.Phase, "a normal Round skates in");
            Assert.AreEqual(1, flow.Round.ShotNumber);
            Assert.AreEqual(0, flow.Round.Goals);
            flow.Tick(60);
            Assert.AreEqual(ShotResultKind.TooLate, flow.Round.Results[0], "a normal Round has Too late again");
        }

        [Test]
        public void Leaving_Test_Mode_outside_it_does_nothing()
        {
            ScreenFlow flow = NewFlow();
            flow.Press();
            flow.LeaveTestMode();
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            Assert.AreEqual(RoundPhase.SkateIn, flow.Round.Phase);
        }

        // ---- no Replay ----

        [Test]
        public void The_saved_Replay_setting_cannot_turn_the_Replay_on_in_Test_Mode()
        {
            var settings = new RoundSettings();
            ScreenFlow flow = NewTestModeFlow(new SaveData { ReplayOn = true }, settings);
            for (int shot = 0; shot < 4; shot++)
            {
                settings.ApplySavedReplay(true); // what RoundDriver does every frame
                flow.SubmitShot(50, 90, 100, false);
                flow.Round.WhistleNow();
                settings.ApplySavedReplay(true);
                Assert.IsFalse(settings.ReplayOn);
                Assert.IsFalse(flow.Round.ReplayFollows, "a Goal, and still no Replay");
                flow.Tick(flow.Round.ResultHoldLength);
            }
            flow.LeaveTestMode();
            settings.ApplySavedReplay(true);
            Assert.IsTrue(settings.ReplayOn, "outside Test Mode the saved setting applies again");
            settings.ApplySavedReplay(false);
            Assert.IsFalse(settings.ReplayOn);
        }

        [Test]
        public void The_Arena_Show_in_Test_Mode_has_no_Replay_camera_or_board_and_shows_the_result_word_in_the_hold()
        {
            var settings = new RoundSettings();
            ScreenFlow flow = NewTestModeFlow(null, settings);
            foreach (double x in new[] { 50.0, -10.0, 50.0 })
            {
                flow.Round.Settings.ApplySavedReplay(true);
                flow.SubmitShot(x, x > 0 ? 90 : 50, 100, false);
                flow.Round.WhistleNow();
                for (double t = 0; t <= settings.TestModeHoldSeconds; t += 0.05)
                {
                    flow.Round.Settings.ApplySavedReplay(true);
                    ArenaState state = ArenaShow.From(flow);
                    Assert.AreEqual(CameraMode.OnNet, state.Camera, "t " + t);
                    Assert.AreEqual(0, state.CameraLookUp, "t " + t);
                    Assert.AreNotEqual(VideoBoardContent.Replay, state.VideoBoard, "t " + t);
                    Assert.AreEqual(0, state.ReplaySeconds, "t " + t);
                    if (t == 0 && x > 0) Assert.AreEqual(VideoBoardContent.Goal, state.VideoBoard);
                    flow.Tick(0.05);
                    if (flow.Round.Phase != RoundPhase.ResultHold) break;
                }
                flow.Tick(1);
            }
            // The pure show too, whatever NoReplay says: the Test Mode flag alone keeps the Replay out.
            foreach (ArenaResult result in new[] { ArenaResult.Goal, ArenaResult.Save, ArenaResult.FrameHitPost, ArenaResult.FrameHitBar })
                for (double t = 0; t <= 4.5; t += 0.1)
                {
                    ArenaState state = ArenaShow.At(new ArenaShowInput
                    {
                        Phase = RoundPhase.ResultHold, PhaseSeconds = t, Result = result, ResultDelaySeconds = 0.4, TestMode = true, ShotNumber = 3
                    });
                    Assert.AreEqual(CameraMode.OnNet, state.Camera);
                    Assert.AreNotEqual(VideoBoardContent.Replay, state.VideoBoard);
                }
            Assert.AreEqual(VideoBoardContent.ResultWord, ArenaShow.At(new ArenaShowInput
            {
                Phase = RoundPhase.ResultHold, PhaseSeconds = 0.5, Result = ArenaResult.Save, TestMode = true
            }).VideoBoard, "a Save shows its result word");
        }

        // ---- no slow motion ----

        [Test]
        public void Time_scale_is_1_and_the_look_is_0_at_every_phase_in_Test_Mode()
        {
            var settings = new RoundSettings { TestMode = true };
            foreach (RoundPhase phase in Enum.GetValues(typeof(RoundPhase)))
                for (double t = 0; t <= 5; t += 0.1)
                {
                    Assert.AreEqual(1.0, Breakaway.TimeScale(phase, t, settings), 1e-12, phase + " " + t);
                    Assert.AreEqual(0.0, Breakaway.Look(phase, t, settings), 1e-12, phase + " " + t);
                    SlowMoSound sound = RoundSoundCues.SlowMo(phase, t, settings);
                    Assert.IsFalse(sound.Heartbeat, "no heartbeat " + phase);
                    Assert.AreEqual(1.0, sound.Pitch, 1e-12);
                }
        }

        [Test]
        public void Outside_Test_Mode_the_slow_down_is_unchanged()
        {
            var settings = new RoundSettings();
            Assert.Less(Breakaway.TimeScale(RoundPhase.ShootingWindow, 1, settings), 1.0);
            Assert.AreEqual(Breakaway.TimeScale(RoundPhase.ShootingWindow, 1), Breakaway.TimeScale(RoundPhase.ShootingWindow, 1, settings));
            Assert.Greater(Breakaway.Look(RoundPhase.ShootingWindow, 1, settings), 0.99);
            Assert.IsTrue(RoundSoundCues.SlowMo(RoundPhase.ShootingWindow, 1, settings).Heartbeat);
        }

        [Test]
        public void The_camera_and_puck_start_stand_still_through_a_long_window()
        {
            var settings = new RoundSettings { TestMode = true };
            foreach (AttackSide side in Enum.GetValues(typeof(AttackSide)))
            {
                double drift0 = Breakaway.Drift(side, settings.SlowDownSeconds, 0, settings);
                RinkPoint start0 = Breakaway.ShotStart(side, 0, settings);
                foreach (double t in new[] { 0.5, 3.0, 20.0, 600.0 })
                {
                    Assert.AreEqual(drift0, Breakaway.Drift(side, settings.SlowDownSeconds, t, settings), 1e-12, side + " " + t);
                    RinkPoint start = Breakaway.ShotStart(side, t, settings);
                    Assert.AreEqual(start0.X, start.X, 1e-12);
                    Assert.AreEqual(start0.Z, start.Z, 1e-12);
                }
            }
            var normal = new RoundSettings();
            Assert.Greater(Breakaway.Drift(AttackSide.Left, normal.SlowDownSeconds, 2, normal), Breakaway.Drift(AttackSide.Left, normal.SlowDownSeconds, 0, normal),
                "in a Round the shooter still drifts on in the window");
        }

        [Test]
        public void The_shot_fade_never_darkens_the_screen_in_Test_Mode()
        {
            ScreenFlow flow = NewTestModeFlow();
            var fade = new ShotFade();
            flow.SubmitShot(50, 90, 100, false);
            flow.Round.WhistleNow();
            for (double t = 0; t < flow.Round.Settings.TestModeHoldSeconds + 0.5; t += 0.05)
            {
                flow.Tick(0.05);
                Assert.AreEqual(0.0, fade.Step(flow.Round, 0.05), 1e-12, "t " + t);
            }
        }

        [Test]
        public void The_window_ring_and_ticks_are_off_in_Test_Mode()
        {
            ScreenFlow flow = NewTestModeFlow();
            var cues = new RoundSoundCues(new SaveData { TickOn = true });
            Assert.AreEqual(RoundPhase.ShootingWindow, flow.Round.Phase);
            for (int i = 0; i < 300; i++)
            {
                flow.Tick(0.1);
                foreach (SoundCue cue in cues.Update(flow.Round))
                    Assert.IsFalse(cue == SoundCue.Tick || cue == SoundCue.FastTick, "no window tick in Test Mode, at " + i * 0.1 + " s");
            }
        }

        [Test]
        public void In_a_Round_the_window_still_ticks()
        {
            ScreenFlow flow = NewFlow();
            flow.Press();
            flow.Tick(flow.Round.Settings.SkateInSeconds + flow.Round.Settings.SlowDownSeconds);
            var cues = new RoundSoundCues(new SaveData { TickOn = true });
            bool ticked = false;
            for (int i = 0; i < 25; i++)
            {
                flow.Tick(0.1);
                foreach (SoundCue cue in cues.Update(flow.Round))
                    ticked |= cue == SoundCue.Tick || cue == SoundCue.FastTick;
            }
            Assert.IsTrue(ticked);
        }
    }
}
