using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>#198: leaving the Settings page with unsaved changes asks "Save changes?" (Save / Discard) (pick #168).</summary>
    public class SettingsMenuLeaveTests
    {
        private sealed class Fixture
        {
            public SaveData Save = new SaveData();
            public SettingsMenu Menu;
            public int Closed, TestModes;
            public double VolumeAtTestMode = -1;

            public Fixture()
            {
                Menu = new SettingsMenu(Save, new string[0], () => { TestModes++; VolumeAtTestMode = Save.MasterVolume; });
                Menu.CloseApproved += () => Closed++;
            }

            public int RowOf(string label)
            {
                for (int i = 0; i < Menu.RowCount; i++)
                    if (Menu.Label(i) == label) return i;
                Assert.Fail("No row " + label);
                return -1;
            }

            public void ChangeVolume()
            {
                Menu.ShowPart(SettingsPart.Player);
                Menu.Select(RowOf("Master volume"));
                Menu.Change(-1);
            }

            public void PressTestMode()
            {
                Menu.ShowPart(SettingsPart.Dev);
                Menu.Select(RowOf("Test Mode"));
                Menu.Activate();
            }
        }

        [Test]
        public void With_nothing_changed_closing_leaves_at_once_with_no_box()
        {
            var f = new Fixture();
            f.Menu.RequestClose();
            Assert.IsFalse(f.Menu.ConfirmOpen);
            Assert.AreEqual(1, f.Closed);
        }

        [Test]
        public void With_unsaved_changes_closing_asks_and_does_not_leave()
        {
            var f = new Fixture();
            f.ChangeVolume();
            f.Menu.RequestClose();
            Assert.IsTrue(f.Menu.ConfirmOpen);
            Assert.AreEqual(0, f.Closed);
            Assert.AreEqual(ConfirmButton.Save, f.Menu.ConfirmChoice, "Save is the first choice");
        }

        [Test]
        public void Save_in_the_box_saves_and_leaves()
        {
            var f = new Fixture();
            f.ChangeVolume();
            f.Menu.RequestClose();
            f.Menu.ConfirmSave();
            Assert.IsFalse(f.Menu.ConfirmOpen);
            Assert.AreEqual(0.9, f.Save.MasterVolume, 1e-9);
            Assert.IsFalse(f.Menu.HasUnsavedChanges);
            Assert.AreEqual(1, f.Closed);
        }

        [Test]
        public void Discard_in_the_box_puts_every_value_back_and_leaves()
        {
            var f = new Fixture();
            f.ChangeVolume();
            f.Menu.Select(f.RowOf("Mute"));
            f.Menu.Activate();
            f.Menu.RequestClose();
            f.Menu.ConfirmDiscard();
            Assert.IsFalse(f.Menu.ConfirmOpen);
            Assert.AreEqual(1.0, f.Save.MasterVolume, 1e-9);
            Assert.IsFalse(f.Save.Mute);
            Assert.AreEqual("100%", f.Menu.Value(f.RowOf("Master volume")));
            Assert.IsFalse(f.Menu.HasUnsavedChanges);
            Assert.AreEqual(1, f.Closed);
        }

        [Test]
        public void Esc_in_the_box_goes_back_to_the_page_with_no_change()
        {
            var f = new Fixture();
            f.ChangeVolume();
            f.Menu.RequestClose();
            f.Menu.Press(SettingsKey.Escape);
            Assert.IsFalse(f.Menu.ConfirmOpen);
            Assert.AreEqual(0, f.Closed);
            Assert.IsTrue(f.Menu.HasUnsavedChanges);
            Assert.AreEqual("90%", f.Menu.Value(f.RowOf("Master volume")));
            Assert.AreEqual(1.0, f.Save.MasterVolume, 1e-9);
        }

        [Test]
        public void Keys_in_the_box_pick_with_Left_Right_and_Enter()
        {
            var f = new Fixture();
            f.ChangeVolume();
            f.Menu.RequestClose();
            f.Menu.Press(SettingsKey.Right);
            Assert.AreEqual(ConfirmButton.Discard, f.Menu.ConfirmChoice, "Right goes to Discard");
            f.Menu.Press(SettingsKey.Left);
            Assert.AreEqual(ConfirmButton.Save, f.Menu.ConfirmChoice);
            f.Menu.Press(SettingsKey.Right);
            f.Menu.Press(SettingsKey.Enter);
            Assert.IsFalse(f.Menu.ConfirmOpen);
            Assert.AreEqual(1.0, f.Save.MasterVolume, 1e-9, "Enter on Discard drops the change");
            Assert.AreEqual(1, f.Closed);
        }

        [Test]
        public void Page_keys_do_nothing_while_the_box_is_open()
        {
            var f = new Fixture();
            f.ChangeVolume();
            f.Menu.RequestClose();
            Assert.IsFalse(f.Menu.Press(SettingsKey.Reset));
            f.Menu.Press(SettingsKey.Tab);
            Assert.AreEqual(SettingsPart.Player, f.Menu.Part);
            Assert.IsTrue(f.Menu.ConfirmOpen);
        }

        [Test]
        public void Esc_on_the_page_asks_to_close()
        {
            var f = new Fixture();
            f.Menu.Press(SettingsKey.Escape);
            Assert.AreEqual(1, f.Closed);
            f = new Fixture();
            f.ChangeVolume();
            f.Menu.Press(SettingsKey.Escape);
            Assert.IsTrue(f.Menu.ConfirmOpen);
            Assert.AreEqual(0, f.Closed);
        }

        [Test]
        public void Test_Mode_with_nothing_changed_opens_at_once()
        {
            var f = new Fixture();
            f.PressTestMode();
            Assert.IsFalse(f.Menu.ConfirmOpen);
            Assert.AreEqual(1, f.TestModes);
        }

        [Test]
        public void Test_Mode_with_unsaved_changes_asks_first()
        {
            var f = new Fixture();
            f.ChangeVolume();
            f.PressTestMode();
            Assert.IsTrue(f.Menu.ConfirmOpen);
            Assert.AreEqual(0, f.TestModes);
            Assert.AreEqual(0, f.Closed);
        }

        [Test]
        public void Test_Mode_runs_with_the_saved_values_after_Save_in_the_box()
        {
            var f = new Fixture();
            f.ChangeVolume();
            f.PressTestMode();
            f.Menu.ConfirmSave();
            Assert.AreEqual(1, f.TestModes);
            Assert.AreEqual(0.9, f.VolumeAtTestMode, 1e-9, "Test Mode starts after the values are saved");
            Assert.AreEqual(0, f.Closed, "Test Mode itself closes the page");
        }

        [Test]
        public void Test_Mode_runs_with_the_old_values_after_Discard_in_the_box()
        {
            var f = new Fixture();
            f.ChangeVolume();
            f.PressTestMode();
            f.Menu.ConfirmDiscard();
            Assert.AreEqual(1, f.TestModes);
            Assert.AreEqual(1.0, f.VolumeAtTestMode, 1e-9);
        }

        [Test]
        public void Esc_in_the_box_after_Test_Mode_asked_stays_on_the_page()
        {
            var f = new Fixture();
            f.ChangeVolume();
            f.PressTestMode();
            f.Menu.Press(SettingsKey.Escape);
            Assert.IsFalse(f.Menu.ConfirmOpen);
            Assert.AreEqual(0, f.TestModes);
            Assert.AreEqual(0, f.Closed);
            Assert.IsTrue(f.Menu.HasUnsavedChanges);
        }

        [Test]
        public void Save_in_the_box_raises_SaveDataChanged_so_the_file_is_written()
        {
            var f = new Fixture();
            int written = 0;
            f.Menu.SaveDataChanged += () => written++;
            f.ChangeVolume();
            f.Menu.RequestClose();
            f.Menu.ConfirmSave();
            Assert.AreEqual(1, written);
        }
    }
}
