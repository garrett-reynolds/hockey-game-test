using System.Linq;
using System.Reflection;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>#197: Settings page changes wait until Save (pick #168).</summary>
    public class SettingsMenuSaveTests
    {
        private static SettingsMenu NewMenu(SaveData save, System.Action openTestMode = null) =>
            new SettingsMenu(save, new string[0], openTestMode);

        private static int RowOf(SettingsMenu menu, string label)
        {
            for (int i = 0; i < menu.RowCount; i++)
                if (menu.Label(i) == label) return i;
            Assert.Fail("No row " + label);
            return -1;
        }

        [Test]
        public void A_change_does_not_reach_the_save_data_until_Save()
        {
            var save = new SaveData();
            var menu = NewMenu(save);
            menu.Select(RowOf(menu, "Master volume"));
            Assert.IsTrue(menu.Change(-1));
            Assert.AreEqual("90%", menu.Value(menu.Selected));
            Assert.AreEqual(1.0, save.MasterVolume, 1e-9);
            menu.Save();
            Assert.AreEqual(0.9, save.MasterVolume, 1e-9);
        }

        [Test]
        public void A_toggle_and_a_mouse_slider_also_wait_for_Save()
        {
            var save = new SaveData();
            var menu = NewMenu(save);
            menu.Select(RowOf(menu, "Mute"));
            menu.Activate();
            menu.SetFraction(RowOf(menu, "Music"), 0.5);
            Assert.IsFalse(save.Mute);
            Assert.AreEqual(1.0, save.MusicVolume, 1e-9);
            menu.Save();
            Assert.IsTrue(save.Mute);
            Assert.AreEqual(0.5, save.MusicVolume, 1e-9);
        }

        [Test]
        public void Is_row_unsaved_is_true_after_a_change_and_false_after_changing_it_back()
        {
            var menu = NewMenu(new SaveData());
            int row = RowOf(menu, "Master volume");
            menu.Select(row);
            Assert.IsFalse(menu.IsRowUnsaved(row));
            menu.Change(-1);
            Assert.IsTrue(menu.IsRowUnsaved(row));
            Assert.IsFalse(menu.IsRowUnsaved(RowOf(menu, "Crowd")));
            menu.Change(1);
            Assert.IsFalse(menu.IsRowUnsaved(row));
            Assert.IsFalse(menu.HasUnsavedChanges);
        }

        [Test]
        public void Save_clears_all_marks_and_Has_unsaved_changes_is_false_after_it()
        {
            var menu = NewMenu(new SaveData());
            int a = RowOf(menu, "Master volume");
            int b = RowOf(menu, "Easy mode");
            menu.Select(a); menu.Change(-1);
            menu.Select(b); menu.Activate();
            Assert.IsTrue(menu.HasUnsavedChanges);
            Assert.IsTrue(menu.IsRowUnsaved(a));
            Assert.IsTrue(menu.IsRowUnsaved(b));
            Assert.IsTrue(menu.Save());
            Assert.IsFalse(menu.HasUnsavedChanges);
            Assert.IsFalse(menu.IsRowUnsaved(a));
            Assert.IsFalse(menu.IsRowUnsaved(b));
            Assert.IsFalse(menu.Save(), "nothing left to save");
        }

        [Test]
        public void The_marks_cover_both_parts()
        {
            var menu = NewMenu(new SaveData());
            menu.ShowPart(SettingsPart.Dev);
            int dev = RowOf(menu, "Reaction time");
            menu.Select(dev);
            menu.Change(1);
            Assert.IsTrue(menu.IsRowUnsaved(dev));
            menu.ShowPart(SettingsPart.Player);
            Assert.IsTrue(menu.HasUnsavedChanges, "a Dev change counts while the Player part shows");
            menu.Save();
            Assert.IsFalse(menu.HasUnsavedChanges);
        }

        [Test]
        public void Reset_to_default_waits_for_Save()
        {
            var save = new SaveData { MasterVolume = 0.3, Mute = true };
            var menu = NewMenu(save);
            menu.ResetPart();
            Assert.AreEqual("100%", menu.Value(RowOf(menu, "Master volume")));
            Assert.AreEqual(0.3, save.MasterVolume, 1e-9);
            Assert.IsTrue(save.Mute);
            Assert.IsTrue(menu.IsRowUnsaved(RowOf(menu, "Master volume")));
            menu.Save();
            Assert.AreEqual(1.0, save.MasterVolume, 1e-9);
            Assert.IsFalse(save.Mute);
        }

        [Test]
        public void Reset_best_score_acts_at_once()
        {
            var save = new SaveData { BestScore = 4 };
            var menu = NewMenu(save);
            menu.Select(RowOf(menu, "Reset best score"));
            Assert.IsTrue(menu.Activate());
            Assert.AreEqual(0, save.BestScore);
            Assert.IsFalse(menu.HasUnsavedChanges);
        }

        [Test]
        public void Reset_best_score_does_not_save_other_waiting_changes()
        {
            var save = new SaveData { BestScore = 4 };
            var menu = NewMenu(save);
            menu.Select(RowOf(menu, "Master volume"));
            menu.Change(-1);
            menu.Select(RowOf(menu, "Reset best score"));
            menu.Activate();
            Assert.AreEqual(1.0, save.MasterVolume, 1e-9);
            Assert.IsTrue(menu.HasUnsavedChanges);
        }

        [Test]
        public void Open_Test_Mode_acts_at_once_and_marks_nothing()
        {
            int opened = 0;
            var menu = NewMenu(new SaveData(), () => opened++);
            menu.ShowPart(SettingsPart.Dev);
            menu.Select(RowOf(menu, "Test Mode"));
            menu.Activate();
            Assert.AreEqual(1, opened);
            Assert.IsFalse(menu.HasUnsavedChanges);
        }

        [Test]
        public void Discard_drops_the_waiting_changes()
        {
            var save = new SaveData();
            var menu = NewMenu(save);
            int row = RowOf(menu, "Master volume");
            menu.Select(row);
            menu.Change(-1);
            menu.Discard();
            Assert.IsFalse(menu.HasUnsavedChanges);
            Assert.AreEqual("100%", menu.Value(row));
            Assert.AreEqual(1.0, save.MasterVolume, 1e-9);
        }

        [Test]
        public void A_Dev_slider_limit_follows_the_waiting_partner_value()
        {
            // Wobble may not pass Reaction time: the limit reads the working copy, not the saved file.
            var save = new SaveData { ReactionSeconds = 0.10, ReactionWobbleSeconds = 0.05 };
            var menu = NewMenu(save);
            menu.ShowPart(SettingsPart.Dev);
            menu.Select(RowOf(menu, "Reaction time"));
            for (int i = 0; i < 10; i++) menu.Change(-1);
            Assert.AreEqual("0.05 s", menu.Value(menu.Selected), "Reaction stops at the waiting Wobble");
        }

        [Test]
        public void Copy_from_copies_every_setting()
        {
            var from = new SaveData();
            foreach (var f in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(bool)) f.SetValue(from, !(bool)f.GetValue(from));
                else if (f.FieldType == typeof(double)) f.SetValue(from, (double)f.GetValue(from) + 0.01);
                else if (f.FieldType == typeof(string)) f.SetValue(from, "x" + f.Name);
            }
            from.ShotsPerRound = 8;
            from.SetBest(4, 3);
            from.SetBest(8, 5);
            var to = new SaveData();
            to.SetBest(7, 9);
            to.CopyFrom(from);
            foreach (var f in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.AreEqual(f.GetValue(from), f.GetValue(to), f.Name);
            Assert.AreEqual(8, to.ShotsPerRound);
            Assert.AreEqual(3, to.BestFor(4));
            Assert.AreEqual(5, to.BestFor(8));
            Assert.AreEqual(0, to.BestFor(7), "bests are replaced, not merged");
            Assert.IsTrue(typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance).Any(), "the loop ran");
        }
    }
}
