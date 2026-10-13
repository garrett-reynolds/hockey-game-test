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
                "Master volume", "Crowd", "Game sounds", "Music", "Mute", "Tick",
                "Shots per Round", "Easy mode", "Test Shots on at start", "Replay",
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
            Assert.AreEqual("100%", menu.Value(1));
            Assert.AreEqual("100%", menu.Value(2));
            Assert.AreEqual("100%", menu.Value(3));
            Assert.AreEqual("OFF", menu.Value(4));
            Assert.AreEqual("ON", menu.Value(5));
            Assert.AreEqual("6", menu.Value(6)); // Shots per Round (#146)
            Assert.AreEqual("OFF", menu.Value(7));
            Assert.AreEqual("ON", menu.Value(8));
            Assert.AreEqual("ON", menu.Value(9));
            Assert.AreEqual("Auto", menu.Value(10));
            Assert.AreEqual("2.0 s", menu.Value(11));
            Assert.AreEqual("15.0 s", menu.Value(12));
            Assert.AreEqual("best 3", menu.Value(13));
        }

        // ---- Two parts, Tab, keys, Reset (#143) ----

        [Test]
        public void The_page_opens_on_the_Player_part_and_the_Dev_part_holds_Show_the_block_goalie()
        {
            var menu = NewMenu(new SaveData());
            Assert.AreEqual(SettingsPart.Player, menu.Part);
            menu.ShowPart(SettingsPart.Dev);
            Assert.AreEqual(SettingsPart.Dev, menu.Part);
            Assert.AreEqual(2, menu.RowCount); // the block goalie, then the Test Mode button (#156)
            Assert.AreEqual("Show the block goalie", menu.Label(0));
            Assert.AreEqual("OFF", menu.Value(0));
            Assert.AreEqual(SettingsRowKind.Toggle, menu.Kind(0));
            Assert.AreEqual("(next launch)", menu.Hint(0), "the goalie is built when the game starts");
        }

        // ---- Test Mode button (#156) ----

        [Test]
        public void The_Dev_part_ends_with_a_Test_Mode_button_and_the_Player_part_has_none()
        {
            var menu = NewMenu(new SaveData());
            for (int i = 0; i < menu.RowCount; i++)
                Assert.AreNotEqual("Test Mode", menu.Label(i), "not in the Player part");
            menu.ShowPart(SettingsPart.Dev);
            Assert.AreEqual("Test Mode", menu.Label(menu.RowCount - 1));
            Assert.AreEqual(SettingsRowKind.Button, menu.Kind(menu.RowCount - 1));
            Assert.IsNull(menu.Hint(menu.RowCount - 1));
        }

        [Test]
        public void Pressing_the_Test_Mode_button_opens_Test_Mode_and_changes_no_setting()
        {
            var save = new SaveData();
            int opened = 0;
            var menu = new SettingsMenu(save, new string[0], () => opened++);
            menu.ShowPart(SettingsPart.Dev);
            Select(menu, "Test Mode");
            Assert.IsFalse(menu.Activate(), "nothing to save");
            Assert.AreEqual(1, opened);
            Assert.IsFalse(menu.Press(SettingsKey.Left));
            Assert.IsFalse(menu.Press(SettingsKey.Right));
            Assert.AreEqual(1, opened, "only Enter or a click opens it");
        }

        [Test]
        public void The_Test_Mode_button_does_nothing_without_a_game_to_open_it_in()
        {
            var menu = NewMenu(new SaveData());
            menu.ShowPart(SettingsPart.Dev);
            Select(menu, "Test Mode");
            Assert.IsFalse(menu.Activate());
        }

        [Test]
        public void Reset_to_default_in_the_Dev_part_does_not_press_the_Test_Mode_button()
        {
            int opened = 0;
            var menu = new SettingsMenu(new SaveData(), new string[0], () => opened++);
            menu.ShowPart(SettingsPart.Dev);
            menu.ResetPart();
            Assert.AreEqual(0, opened);
        }

        [Test]
        public void Rows_that_act_at_once_have_no_hint()
        {
            var menu = NewMenu(new SaveData());
            for (int i = 0; i < menu.RowCount; i++)
            {
                if (menu.Label(i) == "Reset best score") continue; // says it clears every Shots-per-Round number (#146)
                Assert.IsNull(menu.Hint(i), menu.Label(i));
            }
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
            for (int i = 0; i < 4; i++) menu.Press(SettingsKey.Down); // past Crowd, Game sounds and Music to Mute
            Assert.IsTrue(menu.Press(SettingsKey.Enter));
            Assert.IsTrue(save.Mute);
        }

        [Test]
        public void Rows_say_how_they_are_drawn_and_which_card_they_sit_in()
        {
            var menu = NewMenu(new SaveData(), "COM3");
            Assert.AreEqual(SettingsRowKind.Slider, menu.Kind(0));
            Assert.AreEqual(SettingsRowKind.Slider, menu.Kind(1));
            Assert.AreEqual(SettingsRowKind.Slider, menu.Kind(2));
            Assert.AreEqual(SettingsRowKind.Slider, menu.Kind(3));
            Assert.AreEqual(SettingsRowKind.Toggle, menu.Kind(4));
            Assert.AreEqual(SettingsRowKind.Slider, menu.Kind(6)); // Shots per Round
            Assert.AreEqual(SettingsRowKind.Toggle, menu.Kind(9));
            Assert.AreEqual(SettingsRowKind.Choice, menu.Kind(10));
            Assert.AreEqual(SettingsRowKind.Button, menu.Kind(13));
            Assert.AreEqual("SOUND", menu.Group(0));
            Assert.AreEqual("SOUND", menu.Group(5));
            Assert.AreEqual("ROUND", menu.Group(6));
            Assert.AreEqual("ROUND", menu.Group(9));
            Assert.AreEqual("OPERATOR", menu.Group(10));
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
            menu.SetFraction(12, 0.3);
            Assert.AreEqual(37.0, save.AutoReturnSeconds, 1e-9, "a slider with its own range: 1 to 120 s, 30% along snaps to the whole second 37");
            Assert.AreEqual(12, menu.Selected, "the mouse selects the row it works");
        }

        [Test]
        public void The_mouse_clicks_a_toggle_a_choice_and_a_button_on_any_row()
        {
            var save = new SaveData { BestScore = 4 };
            var menu = NewMenu(save, "COM3");
            menu.Select(4);
            Assert.IsTrue(menu.Activate());
            Assert.IsTrue(save.Mute);
            menu.Select(9);
            Assert.IsTrue(menu.Activate());
            Assert.IsFalse(save.ReplayOn);
            menu.Select(10);
            Assert.IsTrue(menu.Change(1));
            Assert.AreEqual("COM3", save.UsbPort);
            menu.Select(13);
            Assert.IsTrue(menu.Activate());
            Assert.AreEqual(0, save.BestScore);
        }

        [Test]
        public void Reset_to_default_in_the_Player_part_sets_every_player_value_back_and_keeps_the_best_score()
        {
            var save = new SaveData
            {
                MasterVolume = 0.2, CrowdVolume = 0.1, GameVolume = 0.3, MusicVolume = 0.4, Mute = true, TickOn = false, EasyMode = true, TestShotOnAtStart = false,
                UsbPort = "COM9", InputIgnoreSeconds = 9, AutoReturnSeconds = 99, BestScore = 5, ForceGoalieBlocks = true,
                ReplayOn = false
            };
            var menu = NewMenu(save);
            Assert.IsTrue(menu.Press(SettingsKey.Reset));
            var d = new SaveData();
            Assert.IsTrue(save.ReplayOn, "Reset to default turns the Replay back on (#145)");
            Assert.AreEqual(d.ReplayOn, save.ReplayOn);
            Assert.AreEqual(d.MasterVolume, save.MasterVolume);
            Assert.AreEqual(d.CrowdVolume, save.CrowdVolume);
            Assert.AreEqual(d.GameVolume, save.GameVolume);
            Assert.AreEqual(d.MusicVolume, save.MusicVolume);
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

        [TestCase("Crowd")]
        [TestCase("Game sounds")]
        [TestCase("Music")]
        public void A_group_volume_slider_steps_by_10_percent_and_changes_only_its_own_volume(string label)
        {
            var save = new SaveData { CrowdVolume = 0.5, GameVolume = 0.5, MusicVolume = 0.5, MasterVolume = 0.7 };
            var menu = NewMenu(save);
            Select(menu, label);
            Assert.IsTrue(menu.Change(1));
            double[] now = { save.CrowdVolume, save.GameVolume, save.MusicVolume };
            double[] want = { 0.5, 0.5, 0.5 };
            want[label == "Crowd" ? 0 : label == "Game sounds" ? 1 : 2] = 0.6;
            for (int i = 0; i < 3; i++) Assert.AreEqual(want[i], now[i], 1e-9);
            Assert.AreEqual(0.7, save.MasterVolume, 1e-9);
            Assert.AreEqual("60%", menu.Value(menu.Selected));
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
