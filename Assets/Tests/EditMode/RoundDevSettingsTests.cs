using System.IO;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Settings page's Dev part, Round timing and Set-up Mistakes (#148): ranges, linked limits, saving, Reset, and when a Round takes them.</summary>
    public class RoundDevSettingsTests
    {
        static readonly string[] Labels = { "Skate-in time", "Shooting window", "Result hold", "Cheat size", "Late Settle size", "Late Settle time" };

        static SettingsMenu DevMenu(SaveData save)
        {
            var menu = new SettingsMenu(save, new string[0]);
            menu.ShowPart(SettingsPart.Dev);
            return menu;
        }

        static int RowOf(SettingsMenu menu, string label)
        {
            for (int i = 0; i < menu.RowCount; i++)
                if (menu.Label(i) == label) return i;
            Assert.Fail("No Dev row named " + label);
            return -1;
        }

        static void Step(SettingsMenu menu, string label, int dir, int times)
        {
            menu.Select(RowOf(menu, label));
            for (int i = 0; i < times; i++) menu.Change(dir);
        }

        [Test]
        public void The_Dev_part_lists_the_Round_values_and_the_Gap_reach_before_the_Test_Mode_button()
        {
            var menu = DevMenu(new SaveData());
            Assert.AreEqual(17, menu.RowCount, "the six values, the Gap reach (#160) and the two bottle rows (#164) sit before the Test Mode button (#156), which stays last");
            Assert.AreEqual("Gap thin-gear reach", menu.Label(13));
            Assert.AreEqual("Test Mode", menu.Label(16));
            Assert.AreEqual("Show the block goalie", menu.Label(0));
            Assert.AreEqual("Reaction time", menu.Label(1));
            Assert.AreEqual("Shuffle max", menu.Label(6));
            for (int i = 0; i < Labels.Length; i++) Assert.AreEqual(Labels[i], menu.Label(7 + i));
            foreach (var label in Labels)
            {
                int row = RowOf(menu, label);
                Assert.AreEqual(SettingsRowKind.Slider, menu.Kind(row), label);
                Assert.AreEqual(label == "Result hold" ? "(no Replay)" : "(next Round)", menu.Hint(row), label);
            }
            Assert.AreEqual("ROUND TIMING", menu.Group(RowOf(menu, "Skate-in time")));
            Assert.AreEqual("ROUND TIMING", menu.Group(RowOf(menu, "Result hold")));
            Assert.AreEqual("SET-UP MISTAKES", menu.Group(RowOf(menu, "Cheat size")));
            Assert.AreEqual("SET-UP MISTAKES", menu.Group(RowOf(menu, "Late Settle time")));
            Assert.AreEqual("2.5 s", menu.Value(RowOf(menu, "Skate-in time")));
            Assert.AreEqual("3.0 s", menu.Value(RowOf(menu, "Shooting window")));
            Assert.AreEqual("2.5 s", menu.Value(RowOf(menu, "Result hold")));
            Assert.AreEqual("0.30 m", menu.Value(RowOf(menu, "Cheat size")));
            Assert.AreEqual("0.40 m", menu.Value(RowOf(menu, "Late Settle size")));
            Assert.AreEqual("1.0 s", menu.Value(RowOf(menu, "Late Settle time")));
        }

        [Test]
        public void The_saved_defaults_are_the_code_defaults_of_RoundSettings()
        {
            var save = new SaveData();
            var code = new RoundSettings();
            Assert.AreEqual(code.SkateInSeconds, save.SkateInSeconds);
            Assert.AreEqual(code.ShootingWindowSeconds, save.ShootingWindowSeconds);
            Assert.AreEqual(code.ResultHoldSeconds, save.ResultHoldSeconds);
            Assert.AreEqual(code.CheatMetres, save.CheatMetres);
            Assert.AreEqual(code.LateSettleMetres, save.LateSettleMetres);
            Assert.AreEqual(code.LateSettleSeconds, save.LateSettleSeconds);
        }

        [Test]
        public void Each_value_stops_at_the_ends_of_its_range()
        {
            var save = new SaveData();
            var menu = DevMenu(save);
            Step(menu, "Skate-in time", +1, 100);
            Assert.AreEqual(5.0, save.SkateInSeconds, 1e-9);
            Step(menu, "Skate-in time", -1, 100);
            Assert.AreEqual(1.5, save.SkateInSeconds, 1e-9);
            Step(menu, "Shooting window", +1, 100);
            Assert.AreEqual(6.0, save.ShootingWindowSeconds, 1e-9);
            Step(menu, "Result hold", -1, 100);
            Assert.AreEqual(2.4, save.ResultHoldSeconds, 1e-9);
            Step(menu, "Result hold", +1, 100);
            Assert.AreEqual(5.0, save.ResultHoldSeconds, 1e-9);
            Step(menu, "Cheat size", -1, 100);
            Assert.AreEqual(0.1, save.CheatMetres, 1e-9);
            Step(menu, "Cheat size", +1, 100);
            Assert.AreEqual(0.6, save.CheatMetres, 1e-9);
            Step(menu, "Late Settle size", -1, 100);
            Assert.AreEqual(0.2, save.LateSettleMetres, 1e-9);
            Step(menu, "Late Settle size", +1, 100);
            Assert.AreEqual(0.6, save.LateSettleMetres, 1e-9);
            Step(menu, "Late Settle time", -1, 100);
            Assert.AreEqual(0.4, save.LateSettleSeconds, 1e-9);
            Step(menu, "Late Settle time", +1, 100);
            Assert.AreEqual(2.0, save.LateSettleSeconds, 1e-9);
        }

        [Test]
        public void The_mouse_clamps_to_the_range_too_and_steps_in_the_step_size()
        {
            var save = new SaveData();
            var menu = DevMenu(save);
            menu.SetFraction(RowOf(menu, "Skate-in time"), 1);
            Assert.AreEqual(5.0, save.SkateInSeconds, 1e-9);
            menu.SetFraction(RowOf(menu, "Result hold"), 0);
            Assert.AreEqual(2.4, save.ResultHoldSeconds, 1e-9);
            menu.SetFraction(RowOf(menu, "Cheat size"), 0.5); // 0.1 + 0.5 * 0.5 = 0.35
            Assert.AreEqual(0.35, save.CheatMetres, 1e-9);
            menu.SetFraction(RowOf(menu, "Shooting window"), 0); // 1.5, but the Late Settle time (1.0) holds the window at 2.0
            Assert.AreEqual(2.0, save.ShootingWindowSeconds, 1e-9);
        }

        [Test]
        public void Late_Settle_time_stays_below_the_shooting_window_when_the_window_changes()
        {
            var save = new SaveData();
            var menu = DevMenu(save);
            Step(menu, "Late Settle time", +1, 100); // 2.0 needs a window of 3.0 or more: the default window already is
            Assert.AreEqual(2.0, save.LateSettleSeconds, 1e-9);
            Step(menu, "Shooting window", -1, 100);
            Assert.AreEqual(3.0, save.ShootingWindowSeconds, 1e-9, "the window stops 1.0 s above the Late Settle time");
            Step(menu, "Late Settle time", -1, 100); // 0.4
            Step(menu, "Shooting window", -1, 100);
            Assert.AreEqual(1.5, save.ShootingWindowSeconds, 1e-9, "its own floor");
            Assert.Less(save.LateSettleSeconds, save.ShootingWindowSeconds);
        }

        [Test]
        public void Late_Settle_time_stays_below_the_shooting_window_when_the_time_changes()
        {
            var save = new SaveData();
            var menu = DevMenu(save);
            Step(menu, "Shooting window", -1, 100); // toward 1.5 s, but the default Late Settle time 1.0 holds the window up
            Assert.AreEqual(2.0, save.ShootingWindowSeconds, 1e-9);
            Step(menu, "Late Settle time", +1, 100);
            Assert.AreEqual(1.0, save.LateSettleSeconds, 1e-9, "the time stops 1.0 s under the window");
            Step(menu, "Shooting window", +1, 100); // 6.0
            Step(menu, "Late Settle time", +1, 100);
            Assert.AreEqual(2.0, save.LateSettleSeconds, 1e-9, "and at its own top, 2.0");
            Assert.Less(save.LateSettleSeconds, save.ShootingWindowSeconds);
        }

        [Test]
        public void Late_Settle_size_never_goes_above_Max_push_whichever_one_changes()
        {
            var save = new SaveData();
            var menu = DevMenu(save);
            Step(menu, "Max push", -1, 100); // down to 0.3 (the shuffle): the Late Settle size comes down with it
            Assert.AreEqual(0.3, save.MaxPushMetres, 1e-9);
            Assert.AreEqual(0.3, save.LateSettleMetres, 1e-9, "the Late Settle size is held down to the push");
            Step(menu, "Late Settle size", +1, 100);
            Assert.AreEqual(0.3, save.LateSettleMetres, 1e-9, "and stops at the push");
            Step(menu, "Max push", +1, 100);
            Step(menu, "Late Settle size", +1, 100);
            Assert.AreEqual(0.6, save.LateSettleMetres, 1e-9, "its own top, 0.6");
            Step(menu, "Max push", -1, 100);
            Assert.LessOrEqual(save.LateSettleMetres, save.MaxPushMetres);
        }

        [Test]
        public void With_Max_push_below_the_size_floor_the_size_is_held_to_the_push_and_its_slider_stays_on_the_track()
        {
            string path = TempFile("goalieMaxPushMetres=0.1\ngoalieShuffleMaxMetres=0.1\nsetUpLateSettleMetres=0.5\n");
            try
            {
                var save = SaveData.Load(path);
                Assert.AreEqual(0.1, save.LateSettleMetres, 1e-9, "held to Max push, under its own floor 0.2");
                var menu = DevMenu(save);
                int row = RowOf(menu, "Late Settle size");
                Assert.GreaterOrEqual(menu.Fraction(row), 0);
                Assert.LessOrEqual(menu.Fraction(row), 1);
                Step(menu, "Late Settle size", +1, 100);
                Assert.AreEqual(0.1, save.LateSettleMetres, 1e-9, "cannot go above the push");
                Step(menu, "Late Settle size", -1, 100);
                Assert.AreEqual(0.1, save.LateSettleMetres, 1e-9);
                menu.SetFraction(row, 1);
                Assert.AreEqual(0.1, save.LateSettleMetres, 1e-9);
                Assert.GreaterOrEqual(menu.Fraction(row), 0);
                Assert.LessOrEqual(menu.Fraction(row), 1);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void A_file_with_wild_or_linked_wrong_values_loads_clamped_and_linked()
        {
            string path = TempFile(
                "roundSkateInSeconds=0\nroundShootingWindowSeconds=1.5\nroundResultHoldSeconds=1\nsetUpCheatMetres=9\n" +
                "setUpLateSettleMetres=0.6\nsetUpLateSettleSeconds=2\ngoalieMaxPushMetres=0.3\ngoalieShuffleMaxMetres=0.3\n");
            try
            {
                var save = SaveData.Load(path);
                Assert.AreEqual(1.5, save.SkateInSeconds, 1e-9);
                Assert.AreEqual(1.5, save.ShootingWindowSeconds, 1e-9);
                Assert.AreEqual(2.4, save.ResultHoldSeconds, 1e-9);
                Assert.AreEqual(0.6, save.CheatMetres, 1e-9);
                Assert.AreEqual(0.5, save.LateSettleSeconds, 1e-9, "held 1.0 s under the window");
                Assert.AreEqual(0.3, save.LateSettleMetres, 1e-9, "held down to Max push");
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void An_old_file_without_the_new_keys_loads_the_defaults()
        {
            string path = TempFile("goalieReactionSeconds=0.2\n");
            try
            {
                var save = SaveData.Load(path);
                var code = new RoundSettings();
                Assert.AreEqual(code.SkateInSeconds, save.SkateInSeconds);
                Assert.AreEqual(code.LateSettleMetres, save.LateSettleMetres);
                Assert.AreEqual(code.LateSettleSeconds, save.LateSettleSeconds);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void The_values_save_and_load_back()
        {
            string path = TempFile("");
            try
            {
                var save = new SaveData
                {
                    SkateInSeconds = 3.5, ShootingWindowSeconds = 4.5, ResultHoldSeconds = 3.1, CheatMetres = 0.45,
                    LateSettleMetres = 0.5, LateSettleSeconds = 1.7
                };
                Assert.IsTrue(save.Save(path));
                var back = SaveData.Load(path);
                Assert.AreEqual(3.5, back.SkateInSeconds, 1e-9);
                Assert.AreEqual(4.5, back.ShootingWindowSeconds, 1e-9);
                Assert.AreEqual(3.1, back.ResultHoldSeconds, 1e-9);
                Assert.AreEqual(0.45, back.CheatMetres, 1e-9);
                Assert.AreEqual(0.5, back.LateSettleMetres, 1e-9);
                Assert.AreEqual(1.7, back.LateSettleSeconds, 1e-9);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Reset_to_default_in_the_Dev_part_sets_the_six_values_back_and_leaves_the_Player_part_alone()
        {
            var save = new SaveData
            {
                SkateInSeconds = 4, ShootingWindowSeconds = 5, ResultHoldSeconds = 4, CheatMetres = 0.5,
                LateSettleMetres = 0.5, LateSettleSeconds = 1.5, MasterVolume = 0.3, BestScore = 4
            };
            var menu = DevMenu(save);
            Assert.IsTrue(menu.ResetPart());
            var defaults = new SaveData();
            Assert.AreEqual(defaults.SkateInSeconds, save.SkateInSeconds);
            Assert.AreEqual(defaults.ShootingWindowSeconds, save.ShootingWindowSeconds);
            Assert.AreEqual(defaults.ResultHoldSeconds, save.ResultHoldSeconds);
            Assert.AreEqual(defaults.CheatMetres, save.CheatMetres);
            Assert.AreEqual(defaults.LateSettleMetres, save.LateSettleMetres);
            Assert.AreEqual(defaults.LateSettleSeconds, save.LateSettleSeconds);
            Assert.AreEqual(0.3, save.MasterVolume, 1e-9);
            Assert.AreEqual(4, save.BestScore);
        }

        [Test]
        public void Reset_to_default_in_the_Player_part_leaves_the_six_values_alone()
        {
            var save = new SaveData { SkateInSeconds = 4, CheatMetres = 0.5, MasterVolume = 0.3 };
            var menu = new SettingsMenu(save, new string[0]);
            Assert.IsTrue(menu.ResetPart());
            Assert.AreEqual(1.0, save.MasterVolume, 1e-9);
            Assert.AreEqual(4, save.SkateInSeconds, 1e-9);
            Assert.AreEqual(0.5, save.CheatMetres, 1e-9);
        }

        [Test]
        public void A_changed_value_acts_from_the_next_Round_and_not_the_current_one()
        {
            var save = new SaveData { SkateInSeconds = 3, ShootingWindowSeconds = 4, ResultHoldSeconds = 3, CheatMetres = 0.4, LateSettleMetres = 0.5, LateSettleSeconds = 1.5 };
            var settings = new RoundSettings();
            var flow = new ScreenFlow(ScriptedRandom.Halves(), save, FakeReach.NeverReaches(), settings);
            Assert.AreEqual(2.5, settings.SkateInSeconds, "nothing is applied before a Round starts");

            flow.Press(); // Round 1 starts
            Assert.AreEqual(3, settings.SkateInSeconds, 1e-9);
            Assert.AreEqual(4, settings.ShootingWindowSeconds, 1e-9);
            Assert.AreEqual(3, settings.ResultHoldSeconds, 1e-9);
            Assert.AreEqual(0.4, settings.CheatMetres, 1e-9);
            Assert.AreEqual(0.5, settings.LateSettleMetres, 1e-9);
            Assert.AreEqual(1.5, settings.LateSettleSeconds, 1e-9);

            save.SkateInSeconds = 2; // changed during the Round (cannot happen on the page, but the seam must hold)
            save.CheatMetres = 0.2;
            flow.Tick(0.5);
            Assert.AreEqual(3, settings.SkateInSeconds, 1e-9, "still Round 1's value");
            Assert.AreEqual(0.4, settings.CheatMetres, 1e-9);
        }

        [Test]
        public void A_Round_applies_the_values_clamped_and_linked_even_when_the_save_holds_bad_ones()
        {
            var save = new SaveData { ShootingWindowSeconds = 1.5, LateSettleSeconds = 2.0, MaxPushMetres = 0.3, ShuffleMaxMetres = 0.3, LateSettleMetres = 0.6, SkateInSeconds = 99 };
            var settings = new RoundSettings();
            var flow = new ScreenFlow(ScriptedRandom.Halves(), save, FakeReach.NeverReaches(), settings);
            flow.Press();
            Assert.AreEqual(5.0, settings.SkateInSeconds, 1e-9);
            Assert.AreEqual(0.5, settings.LateSettleSeconds, 1e-9);
            Assert.Less(settings.LateSettleSeconds, settings.ShootingWindowSeconds);
            Assert.LessOrEqual(settings.LateSettleMetres, settings.MaxPushMetres);
        }

        [Test]
        public void Tests_and_tools_get_code_defaults_even_when_the_save_file_holds_tuned_values()
        {
            string path = TempFile("roundSkateInSeconds=4\nroundShootingWindowSeconds=5\nsetUpCheatMetres=0.5\n");
            try
            {
                Assert.AreEqual(4, SaveData.Load(path).SkateInSeconds, 1e-9, "the file holds tuned values");
                var settings = new RoundSettings();
                var flow = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches(), settings);
                flow.Press();
                Assert.AreEqual(2.5, settings.SkateInSeconds);
                Assert.AreEqual(3.0, settings.ShootingWindowSeconds);
                Assert.AreEqual(0.3, settings.CheatMetres);
            }
            finally { File.Delete(path); }
        }

        // ---- Gap thin-gear reach (#160) ----

        [Test]
        public void The_Gap_thin_gear_reach_row_is_a_slider_from_0_to_0_08_m_defaulting_to_0_04_m()
        {
            Assert.AreEqual(0.0, SaveData.GapThinGearReachRange.Min, 1e-9);
            Assert.AreEqual(0.08, SaveData.GapThinGearReachRange.Max, 1e-9);
            Assert.AreEqual(new RoundSettings().GapThinGearReachMetres, new SaveData().GapThinGearReachMetres, "the saved default is the code default");
            Assert.AreEqual(0.04, new SaveData().GapThinGearReachMetres, 1e-9);
            var save = new SaveData();
            var menu = DevMenu(save);
            int row = RowOf(menu, "Gap thin-gear reach");
            Assert.AreEqual(SettingsRowKind.Slider, menu.Kind(row));
            Assert.AreEqual("GAPS", menu.Group(row));
            Assert.AreEqual("0.04 m", menu.Value(row));
            Step(menu, "Gap thin-gear reach", +1, 100);
            Assert.AreEqual(0.08, save.GapThinGearReachMetres, 1e-9);
            Step(menu, "Gap thin-gear reach", -1, 100);
            Assert.AreEqual(0.0, save.GapThinGearReachMetres, 1e-9);
        }

        [Test]
        public void The_Gap_thin_gear_reach_saves_and_loads_and_an_old_file_without_it_loads_0_04()
        {
            string path = TempFile("");
            try
            {
                Assert.IsTrue(new SaveData { GapThinGearReachMetres = 0.02 }.Save(path));
                Assert.AreEqual(0.02, SaveData.Load(path).GapThinGearReachMetres, 1e-9);
            }
            finally { File.Delete(path); }
            string old = TempFile("roundSkateInSeconds=4\n");
            try { Assert.AreEqual(0.04, SaveData.Load(old).GapThinGearReachMetres, 1e-9); }
            finally { File.Delete(old); }
            string wild = TempFile("gapThinGearReachMetres=5\n");
            try { Assert.AreEqual(0.08, SaveData.Load(wild).GapThinGearReachMetres, 1e-9, "clamped to the range"); }
            finally { File.Delete(wild); }
        }

        [Test]
        public void A_Round_takes_the_Gap_thin_gear_reach_and_Reset_brings_back_0_04()
        {
            var save = new SaveData { GapThinGearReachMetres = 0.01 };
            var settings = new RoundSettings();
            new ScreenFlow(ScriptedRandom.Halves(), save, FakeReach.NeverReaches(), settings).Press();
            Assert.AreEqual(0.01, settings.GapThinGearReachMetres, 1e-9, "the Gap measure reads it from the Round's settings");

            var menu = DevMenu(save);
            menu.Press(SettingsKey.Reset);
            Assert.AreEqual(0.04, save.GapThinGearReachMetres, 1e-9);
        }

        // ---- Water Bottle (#164) ----

        [Test]
        public void The_bottle_rows_are_sliders_in_the_Water_Bottle_card_with_ranges_and_defaults()
        {
            Assert.AreEqual(60, SaveData.BottleKnockSpeedRange.Min, 1e-9);
            Assert.AreEqual(160, SaveData.BottleKnockSpeedRange.Max, 1e-9);
            Assert.AreEqual(5, SaveData.BottleKnockSpeedRange.Step, 1e-9);
            Assert.AreEqual(50, SaveData.BottleJumpRange.Min, 1e-9);
            Assert.AreEqual(200, SaveData.BottleJumpRange.Max, 1e-9);
            Assert.AreEqual(10, SaveData.BottleJumpRange.Step, 1e-9);
            Assert.AreEqual(BottleKnock.KnockSpeedKmh, new SaveData().BottleKnockSpeedKmh, "the saved default is the old fixed speed");
            Assert.AreEqual(100.0, new SaveData().BottleJumpPercent, 1e-9);

            var save = new SaveData();
            var menu = DevMenu(save);
            Assert.AreEqual(17, menu.RowCount);
            Assert.AreEqual("Bottle knock speed", menu.Label(14));
            Assert.AreEqual("Bottle jump", menu.Label(15));
            Assert.AreEqual("Test Mode", menu.Label(16), "the Test Mode button stays last");
            foreach (var label in new[] { "Bottle knock speed", "Bottle jump" })
            {
                int row = RowOf(menu, label);
                Assert.AreEqual(SettingsRowKind.Slider, menu.Kind(row), label);
                Assert.AreEqual("WATER BOTTLE", menu.Group(row), label);
                Assert.AreEqual("(next Shot)", menu.Hint(row), label);
            }
            Assert.AreEqual("80 km/h", menu.Value(RowOf(menu, "Bottle knock speed")));
            Assert.AreEqual("100%", menu.Value(RowOf(menu, "Bottle jump")));
            Step(menu, "Bottle knock speed", +1, 100);
            Assert.AreEqual(160, save.BottleKnockSpeedKmh, 1e-9);
            Step(menu, "Bottle knock speed", -1, 100);
            Assert.AreEqual(60, save.BottleKnockSpeedKmh, 1e-9);
            Step(menu, "Bottle knock speed", +1, 1);
            Assert.AreEqual(65, save.BottleKnockSpeedKmh, 1e-9, "5 km/h a step");
            Step(menu, "Bottle jump", +1, 100);
            Assert.AreEqual(200, save.BottleJumpPercent, 1e-9);
            Step(menu, "Bottle jump", -1, 100);
            Assert.AreEqual(50, save.BottleJumpPercent, 1e-9);
        }

        [Test]
        public void The_bottle_values_save_load_clamp_and_Reset_brings_back_80_and_100()
        {
            string path = Path.Combine(Path.GetTempPath(), "hockey-t164-" + System.Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                Assert.IsTrue(new SaveData { BottleKnockSpeedKmh = 125, BottleJumpPercent = 150 }.Save(path));
                var back = SaveData.Load(path);
                Assert.AreEqual(125, back.BottleKnockSpeedKmh, 1e-9);
                Assert.AreEqual(150, back.BottleJumpPercent, 1e-9);
            }
            finally { File.Delete(path); }

            string old = TempFile("shotsPerRound=6\n");
            try
            {
                Assert.AreEqual(80, SaveData.Load(old).BottleKnockSpeedKmh, 1e-9, "an old file loads the defaults");
                Assert.AreEqual(100, SaveData.Load(old).BottleJumpPercent, 1e-9);
            }
            finally { File.Delete(old); }

            string wild = TempFile("bottleKnockSpeedKmh=900\nbottleJumpPercent=1\n");
            try
            {
                Assert.AreEqual(160, SaveData.Load(wild).BottleKnockSpeedKmh, 1e-9, "clamped to the range");
                Assert.AreEqual(50, SaveData.Load(wild).BottleJumpPercent, 1e-9);
            }
            finally { File.Delete(wild); }

            var save = new SaveData { BottleKnockSpeedKmh = 140, BottleJumpPercent = 60 };
            DevMenu(save).Press(SettingsKey.Reset);
            Assert.AreEqual(80, save.BottleKnockSpeedKmh, 1e-9);
            Assert.AreEqual(100, save.BottleJumpPercent, 1e-9);
        }

        static string TempFile(string text)
        {
            string path = Path.Combine(Path.GetTempPath(), "hockey-t148-" + System.Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, text);
            return path;
        }
    }
}
