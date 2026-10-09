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
            string[] labels =
            {
                "USB port", "Easy mode", "Contested chance at 0 km/h", "Contested chance at top speed", "Top speed",
                "Test Shots on at start", "End screen input-ignore time", "End screen auto-return time",
                "Tick", "Master volume", "Mute", "Reset best score"
            };
            Assert.AreEqual(labels.Length, menu.RowCount);
            for (int i = 0; i < labels.Length; i++)
                Assert.AreEqual(labels[i], menu.Label(i));
        }

        [Test]
        public void Rows_show_their_current_values()
        {
            var menu = NewMenu(new SaveData { BestScore = 3 }, "COM3");
            Assert.AreEqual("Auto", menu.Value(0));
            Assert.AreEqual("OFF", menu.Value(1));
            Assert.AreEqual("50%", menu.Value(2));
            Assert.AreEqual("85%", menu.Value(3));
            Assert.AreEqual("120 km/h", menu.Value(4));
            Assert.AreEqual("ON", menu.Value(5));
            Assert.AreEqual("2.0 s", menu.Value(6));
            Assert.AreEqual("15.0 s", menu.Value(7));
            Assert.AreEqual("ON", menu.Value(8));
            Assert.AreEqual("100%", menu.Value(9));
            Assert.AreEqual("OFF", menu.Value(10));
            Assert.AreEqual("best 3", menu.Value(11));
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
        public void Contested_chances_step_by_5_percent_and_stay_between_0_and_100_percent()
        {
            var save = new SaveData { ContestedLowChance = 0.05, ContestedTopChance = 0.95 };
            var menu = NewMenu(save);

            Select(menu, "Contested chance at 0 km/h");
            Assert.IsTrue(menu.Change(-1));
            Assert.AreEqual(0.0, save.ContestedLowChance, 1e-9);
            Assert.IsFalse(menu.Change(-1));
            Assert.AreEqual(0.0, save.ContestedLowChance, 1e-9);

            Select(menu, "Contested chance at top speed");
            menu.Change(1);
            Assert.AreEqual(1.0, save.ContestedTopChance, 1e-9);
            menu.Change(1);
            Assert.AreEqual(1.0, save.ContestedTopChance, 1e-9);
            menu.Change(-1);
            Assert.AreEqual(0.95, save.ContestedTopChance, 1e-9);
            Assert.AreEqual("95%", menu.Value(menu.Selected));
        }

        [Test]
        public void Top_speed_steps_by_5_kmh_and_never_reaches_0()
        {
            var save = new SaveData { TopSpeedKmh = 15 };
            var menu = NewMenu(save);
            Select(menu, "Top speed");
            menu.Change(1);
            Assert.AreEqual(20, save.TopSpeedKmh, 1e-9);
            for (int i = 0; i < 10; i++) menu.Change(-1);
            Assert.AreEqual(10, save.TopSpeedKmh, 1e-9);
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
