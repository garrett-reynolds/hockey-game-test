using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class SettingsMenuTests
    {
        private static SettingsMenu NewMenu(SaveData save, params string[] ports) => new SettingsMenu(save, ports);

        // Moves the selection to the row with this label (fails the test if there is none).
        private static void Select(SettingsMenu menu, string label)
        {
            for (int i = 0; i < menu.RowCount; i++)
            {
                if (menu.Label(menu.Selected) == label) return;
                menu.MoveSelection(1);
            }
            Assert.Fail("No settings row named " + label);
        }

        [Test]
        public void There_is_one_row_per_setting_plus_reset_best_score()
        {
            var menu = NewMenu(new SaveData());
            // The page groups the rows into cards (Sound, Round, Operator), so each card's rows sit together (#143).
            string[] labels =
            {
                "Master volume", "Mute", "Tick",
                "Easy mode", "Test Shots on at start",
                "USB port", "End screen input-ignore time", "End screen auto-return time", "Reset best score"
            };
            Assert.AreEqual(labels.Length, menu.RowCount);
            for (int i = 0; i < labels.Length; i++)
                Assert.AreEqual(labels[i], menu.Label(i));
        }

        [Test]
        public void Rows_show_their_current_values()
        {
            var menu = NewMenu(new SaveData { BestScore = 3 }, "COM3");
            Assert.AreEqual("100%", menu.Value(0));
            Assert.AreEqual("OFF", menu.Value(1));
            Assert.AreEqual("ON", menu.Value(2));
            Assert.AreEqual("OFF", menu.Value(3));
            Assert.AreEqual("ON", menu.Value(4));
            Assert.AreEqual("Auto", menu.Value(5));
            Assert.AreEqual("2.0 s", menu.Value(6));
            Assert.AreEqual("15.0 s", menu.Value(7));
            Assert.AreEqual("best 3", menu.Value(8));
        }

        // ---- Two parts, Tab, keys, Reset (#143) ----

        [Test]
        public void The_page_opens_on_the_Player_part_and_the_Dev_part_holds_Show_the_block_goalie()
        {
            var menu = NewMenu(new SaveData());
            Assert.AreEqual(SettingsPart.Player, menu.Part);
            menu.ShowPart(SettingsPart.Dev);
            Assert.AreEqual(SettingsPart.Dev, menu.Part);
            Assert.AreEqual(1, menu.RowCount);
            Assert.AreEqual("Show the block goalie", menu.Label(0));
            Assert.AreEqual("OFF", menu.Value(0));
            Assert.AreEqual(SettingsRowKind.Toggle, menu.Kind(0));
        }

        [Test]
        public void Tab_switches_between_the_parts_and_each_part_keeps_its_selected_row()
        {
            var menu = NewMenu(new SaveData());
            menu.MoveSelection(1);
            menu.MoveSelection(1);
            Assert.AreEqual(2, menu.Selected);
            Assert.IsFalse(menu.Press(SettingsKey.Tab), "switching the part changes no setting");
            Assert.AreEqual(SettingsPart.Dev, menu.Part);
            Assert.AreEqual(0, menu.Selected);
            menu.Press(SettingsKey.Tab);
            Assert.AreEqual(SettingsPart.Player, menu.Part);
            Assert.AreEqual(2, menu.Selected);
        }

        [Test]
        public void Show_the_block_goalie_toggles_and_is_saved_in_the_SaveData()
        {
            var save = new SaveData();
            var menu = NewMenu(save);
            menu.Press(SettingsKey.Tab);
            Assert.IsTrue(menu.Press(SettingsKey.Enter));
            Assert.IsTrue(save.ForceGoalieBlocks);
            Assert.AreEqual("ON", menu.Value(0));
            Assert.IsTrue(menu.Press(SettingsKey.Left));
            Assert.IsFalse(save.ForceGoalieBlocks);
        }

        [Test]
        public void The_keys_move_change_and_flip_on_the_current_part()
        {
            var save = new SaveData { MasterVolume = 0.5 };
            var menu = NewMenu(save);
            Assert.IsFalse(menu.Press(SettingsKey.Down));
            Assert.AreEqual(1, menu.Selected);
            Assert.IsFalse(menu.Press(SettingsKey.Up));
            Assert.AreEqual(0, menu.Selected);
            Assert.IsTrue(menu.Press(SettingsKey.Right));
            Assert.AreEqual(0.6, save.MasterVolume, 1e-9);
            Assert.IsTrue(menu.Press(SettingsKey.Left));
            Assert.AreEqual(0.5, save.MasterVolume, 1e-9);
            menu.Press(SettingsKey.Down);
            Assert.IsTrue(menu.Press(SettingsKey.Enter));
            Assert.IsTrue(save.Mute);
        }

        [Test]
        public void Rows_say_how_they_are_drawn_and_which_card_they_sit_in()
        {
            var menu = NewMenu(new SaveData(), "COM3");
            Assert.AreEqual(SettingsRowKind.Slider, menu.Kind(0));
            Assert.AreEqual(SettingsRowKind.Toggle, menu.Kind(1));
            Assert.AreEqual(SettingsRowKind.Choice, menu.Kind(5));
            Assert.AreEqual(SettingsRowKind.Button, menu.Kind(8));
            Assert.AreEqual("SOUND", menu.Group(0));
            Assert.AreEqual("SOUND", menu.Group(2));
            Assert.AreEqual("ROUND", menu.Group(3));
            Assert.AreEqual("OPERATOR", menu.Group(5));
        }

        [Test]
        public void A_slider_reports_its_place_and_the_mouse_sets_it_snapped_to_the_step()
        {
            var save = new SaveData { MasterVolume = 0.5 };
            var menu = NewMenu(save);
            Assert.AreEqual(0.5, menu.Fraction(0), 1e-9);
            Assert.IsTrue(menu.SetFraction(0, 0.83));
            Assert.AreEqual(0.8, save.MasterVolume, 1e-9, "snaps to the 10% step");
            Assert.IsFalse(menu.SetFraction(0, 0.8), "same value: nothing to save");
            menu.SetFraction(0, 1.7);
            Assert.AreEqual(1.0, save.MasterVolume, 1e-9, "dragged past the end stays at the end");
            menu.SetFraction(0, -0.4);
            Assert.AreEqual(0.0, save.MasterVolume, 1e-9);
            menu.SetFraction(7, 0.3);
            Assert.AreEqual(37.0, save.AutoReturnSeconds, 1e-9, "a slider with its own range: 1 to 120 s, 30% along snaps to the whole second 37");
            Assert.AreEqual(7, menu.Selected, "the mouse selects the row it works");
        }

        [Test]
        public void The_mouse_clicks_a_toggle_a_choice_and_a_button_on_any_row()
        {
            var save = new SaveData { BestScore = 4 };
            var menu = NewMenu(save, "COM3");
            menu.Select(1);
            Assert.IsTrue(menu.Activate());
            Assert.IsTrue(save.Mute);
            menu.Select(5);
            Assert.IsTrue(menu.Change(1));
            Assert.AreEqual("COM3", save.UsbPort);
            menu.Select(8);
            Assert.IsTrue(menu.Activate());
            Assert.AreEqual(0, save.BestScore);
        }

        [Test]
        public void Reset_to_default_in_the_Player_part_sets_every_player_value_back_and_keeps_the_best_score()
        {
            var save = new SaveData
            {
                MasterVolume = 0.2, Mute = true, TickOn = false, EasyMode = true, TestShotOnAtStart = false,
                UsbPort = "COM9", InputIgnoreSeconds = 9, AutoReturnSeconds = 99, BestScore = 5, ForceGoalieBlocks = true
            };
            var menu = NewMenu(save);
            Assert.IsTrue(menu.Press(SettingsKey.Reset));
            var d = new SaveData();
            Assert.AreEqual(d.MasterVolume, save.MasterVolume);
            Assert.AreEqual(d.Mute, save.Mute);
            Assert.AreEqual(d.TickOn, save.TickOn);
            Assert.AreEqual(d.EasyMode, save.EasyMode);
            Assert.AreEqual(d.TestShotOnAtStart, save.TestShotOnAtStart);
            Assert.AreEqual(d.UsbPort, save.UsbPort);
            Assert.AreEqual(d.InputIgnoreSeconds, save.InputIgnoreSeconds);
            Assert.AreEqual(d.AutoReturnSeconds, save.AutoReturnSeconds);
            Assert.AreEqual(5, save.BestScore, "the best score has its own Reset row");
            Assert.IsTrue(save.ForceGoalieBlocks, "the Dev part is not reset from the Player part");
        }

        [Test]
        public void Reset_to_default_in_the_Dev_part_resets_only_the_Dev_part()
        {
            var save = new SaveData { ForceGoalieBlocks = true, MasterVolume = 0.2 };
            var menu = NewMenu(save);
            menu.ShowPart(SettingsPart.Dev);
            Assert.IsTrue(menu.ResetPart());
            Assert.IsFalse(save.ForceGoalieBlocks);
            Assert.AreEqual(0.2, save.MasterVolume, 1e-9);
        }

        [Test]
        public void Selection_moves_and_wraps_at_both_ends()
        {
            var menu = NewMenu(new SaveData());
            Assert.AreEqual(0, menu.Selected);
            menu.MoveSelection(-1);
            Assert.AreEqual(menu.RowCount - 1, menu.Selected);
            menu.MoveSelection(1);
            Assert.AreEqual(0, menu.Selected);
            menu.MoveSelection(1);
            Assert.AreEqual(1, menu.Selected);
        }

        [Test]
        public void Reset_best_score_sets_the_best_score_to_0()
        {
            var save = new SaveData { BestScore = 5 };
            var menu = NewMenu(save);
            Select(menu, "Reset best score");
            Assert.IsTrue(menu.Activate());
            Assert.AreEqual(0, save.BestScore);
        }

        [Test]
        public void Left_or_right_does_not_reset_the_best_score()
        {
            var save = new SaveData { BestScore = 5 };
            var menu = NewMenu(save);
            Select(menu, "Reset best score");
            Assert.IsFalse(menu.Change(1));
            Assert.IsFalse(menu.Change(-1));
            Assert.AreEqual(5, save.BestScore);
        }

        [Test]
        public void On_off_rows_flip_with_enter_or_left_right()
        {
            var save = new SaveData();
            var menu = NewMenu(save);

            Select(menu, "Easy mode");
            Assert.IsTrue(menu.Activate());
            Assert.IsTrue(save.EasyMode);
            Assert.IsTrue(menu.Change(1));
            Assert.IsFalse(save.EasyMode);

            Select(menu, "Test Shots on at start");
            menu.Activate();
            Assert.IsFalse(save.TestShotOnAtStart);

            Select(menu, "Tick");
            menu.Change(-1);
            Assert.IsFalse(save.TickOn);

            Select(menu, "Mute");
            menu.Activate();
            Assert.IsTrue(save.Mute);
        }

        [Test]
        public void End_screen_times_step_and_never_go_below_their_minimum()
        {
            var save = new SaveData { InputIgnoreSeconds = 0.5, AutoReturnSeconds = 2 };
            var menu = NewMenu(save);

            Select(menu, "End screen input-ignore time");
            menu.Change(1);
            Assert.AreEqual(1.0, save.InputIgnoreSeconds, 1e-9);
            for (int i = 0; i < 5; i++) menu.Change(-1);
            Assert.AreEqual(0.0, save.InputIgnoreSeconds, 1e-9);

            Select(menu, "End screen auto-return time");
            menu.Change(1);
            Assert.AreEqual(3.0, save.AutoReturnSeconds, 1e-9);
            for (int i = 0; i < 5; i++) menu.Change(-1);
            Assert.AreEqual(1.0, save.AutoReturnSeconds, 1e-9);
        }

        [Test]
        public void Master_volume_steps_by_10_percent_between_0_and_100_percent()
        {
            var save = new SaveData { MasterVolume = 0.1 };
            var menu = NewMenu(save);
            Select(menu, "Master volume");
            menu.Change(-1);
            Assert.AreEqual(0.0, save.MasterVolume, 1e-9);
            menu.Change(-1);
            Assert.AreEqual(0.0, save.MasterVolume, 1e-9);
            for (int i = 0; i < 12; i++) menu.Change(1);
            Assert.AreEqual(1.0, save.MasterVolume, 1e-9);
            Assert.AreEqual("100%", menu.Value(menu.Selected));
        }

        [Test]
        public void USB_port_cycles_Auto_then_each_listed_port()
        {
            var save = new SaveData();
            var menu = NewMenu(save, "COM3", "COM4");
            Select(menu, "USB port");
            menu.Change(1);
            Assert.AreEqual("COM3", save.UsbPort);
            menu.Change(1);
            Assert.AreEqual("COM4", save.UsbPort);
            menu.Change(1);
            Assert.AreEqual(SaveData.AutoUsbPort, save.UsbPort);
            menu.Change(-1);
            Assert.AreEqual("COM4", save.UsbPort);
        }

        [Test]
        public void A_saved_port_that_is_not_plugged_in_stays_in_the_list()
        {
            var save = new SaveData { UsbPort = "COM9" };
            var menu = NewMenu(save, "COM3");
            Select(menu, "USB port");
            Assert.AreEqual("COM9", menu.Value(menu.Selected));
            menu.Change(1);
            Assert.AreEqual(SaveData.AutoUsbPort, save.UsbPort);
            menu.Change(-1);
            Assert.AreEqual("COM9", save.UsbPort);
        }

        [Test]
        public void With_no_ports_the_USB_port_row_stays_on_Auto()
        {
            var save = new SaveData();
            var menu = NewMenu(save);
            Select(menu, "USB port");
            Assert.IsFalse(menu.Change(1));
            Assert.AreEqual(SaveData.AutoUsbPort, save.UsbPort);
        }
    }
}
