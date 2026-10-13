using System.IO;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Settings page's Dev part, goalie values (#147): ranges, linked limits, Reset, and when a Round takes them.</summary>
    public class GoalieDevSettingsTests
    {
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

        // Steps a row `times` times in one direction.
        static void Step(SettingsMenu menu, string label, int dir, int times)
        {
            menu.Select(RowOf(menu, label));
            for (int i = 0; i < times; i++) menu.Change(dir);
        }

        [Test]
        public void The_Dev_part_lists_the_six_goalie_values()
        {
            var menu = DevMenu(new SaveData());
            foreach (var label in new[] { "Reaction time", "Reaction wobble", "Hand speed", "Leg speed", "Max push", "Shuffle max" })
            {
                int row = RowOf(menu, label);
                Assert.AreEqual(SettingsRowKind.Slider, menu.Kind(row), label);
                Assert.AreEqual("GOALIE", menu.Group(row), label);
                Assert.AreEqual("(next Round)", menu.Hint(row), label);
            }
            Assert.AreEqual("0.12 s", menu.Value(RowOf(menu, "Reaction time")));
            Assert.AreEqual("8.0 m/s", menu.Value(RowOf(menu, "Hand speed")));
            Assert.AreEqual("0.60 m", menu.Value(RowOf(menu, "Max push")));
        }

        [Test]
        public void The_saved_defaults_are_the_code_defaults_of_RoundSettings()
        {
            var save = new SaveData();
            var code = new RoundSettings();
            Assert.AreEqual(code.ReactionSeconds, save.ReactionSeconds);
            Assert.AreEqual(code.ReactionWobbleSeconds, save.ReactionWobbleSeconds);
            Assert.AreEqual(code.HandMetresPerSecond, save.HandMetresPerSecond);
            Assert.AreEqual(code.LegMetresPerSecond, save.LegMetresPerSecond);
            Assert.AreEqual(code.MaxPushMetres, save.MaxPushMetres);
            Assert.AreEqual(code.ShuffleMaxMetres, save.ShuffleMaxMetres);
        }

        [Test]
        public void Each_value_stops_at_the_ends_of_its_range()
        {
            var save = new SaveData();
            var menu = DevMenu(save);
            // Left and Right far past the ends: reaction 0.04..0.30, hand 6 (the leg) ..12, max push 0.3 (the shuffle) ..0.9.
            Step(menu, "Reaction time", +1, 100);
            Assert.AreEqual(0.30, save.ReactionSeconds, 1e-9);
            Step(menu, "Reaction time", -1, 100);
            Assert.AreEqual(0.04, save.ReactionSeconds, 1e-9);
            Step(menu, "Hand speed", +1, 100);
            Assert.AreEqual(12, save.HandMetresPerSecond, 1e-9);
            Step(menu, "Leg speed", -1, 100);
            Assert.AreEqual(3, save.LegMetresPerSecond, 1e-9);
            Step(menu, "Max push", +1, 100);
            Assert.AreEqual(0.9, save.MaxPushMetres, 1e-9);
            Step(menu, "Shuffle max", -1, 100);
            Assert.AreEqual(0.1, save.ShuffleMaxMetres, 1e-9);
            Step(menu, "Reaction wobble", -1, 100);
            Assert.AreEqual(0, save.ReactionWobbleSeconds, 1e-9);
        }

        [Test]
        public void The_mouse_clamps_to_the_range_too_and_steps_in_the_step_size()
        {
            var save = new SaveData();
            var menu = DevMenu(save);
            menu.SetFraction(RowOf(menu, "Reaction time"), 1);
            Assert.AreEqual(0.30, save.ReactionSeconds, 1e-9);
            menu.SetFraction(RowOf(menu, "Max push"), 0.5); // 0.1 + 0.5 * 0.8 = 0.5
            Assert.AreEqual(0.5, save.MaxPushMetres, 1e-9);
            menu.SetFraction(RowOf(menu, "Hand speed"), 1);
            Assert.AreEqual(12, save.HandMetresPerSecond, 1e-9);
            menu.SetFraction(RowOf(menu, "Hand speed"), 0.5); // 3 + 0.5 * 9 = 7.5, above the leg (6)
            Assert.AreEqual(7.5, save.HandMetresPerSecond, 1e-9);
            menu.SetFraction(RowOf(menu, "Leg speed"), 1);
            Assert.AreEqual(7.5, save.LegMetresPerSecond, 1e-9, "the leg stops at the hand");
        }

        [Test]
        public void Wobble_never_goes_above_Reaction_whichever_one_changes()
        {
            var save = new SaveData();
            var menu = DevMenu(save);
            Step(menu, "Reaction time", -1, 100); // 0.04
            Step(menu, "Reaction wobble", +1, 100);
            Assert.AreEqual(0.04, save.ReactionWobbleSeconds, 1e-9, "the wobble stops at the reaction");
            Step(menu, "Reaction time", +1, 6); // 0.10
            Step(menu, "Reaction wobble", +1, 100);
            Assert.AreEqual(0.10, save.ReactionWobbleSeconds, 1e-9, "and at its own top, 0.10");
            Step(menu, "Reaction time", -1, 100); // the wobble holds the reaction up
            Assert.AreEqual(0.10, save.ReactionSeconds, 1e-9, "the reaction stops at the wobble");
            Assert.LessOrEqual(save.ReactionWobbleSeconds, save.ReactionSeconds);
        }

        [Test]
        public void Leg_never_goes_above_Hand_whichever_one_changes()
        {
            var save = new SaveData();
            var menu = DevMenu(save);
            Step(menu, "Hand speed", -1, 100);
            Assert.AreEqual(6, save.HandMetresPerSecond, 1e-9, "the hand stops at the leg");
            Step(menu, "Leg speed", +1, 100);
            Assert.AreEqual(6, save.LegMetresPerSecond, 1e-9, "the leg stops at the hand");
            Step(menu, "Hand speed", +1, 100);
            Step(menu, "Leg speed", +1, 100);
            Assert.AreEqual(12, save.LegMetresPerSecond, 1e-9);
            Step(menu, "Hand speed", -1, 100);
            Assert.AreEqual(12, save.HandMetresPerSecond, 1e-9, "the hand stops at the leg");
        }

        [Test]
        public void ShuffleMax_never_goes_above_MaxPush_whichever_one_changes()
        {
            var save = new SaveData();
            var menu = DevMenu(save);
            Step(menu, "Max push", -1, 100);
            Assert.AreEqual(0.3, save.MaxPushMetres, 1e-9, "the push stops at the shuffle");
            Step(menu, "Shuffle max", +1, 100);
            Assert.AreEqual(0.3, save.ShuffleMaxMetres, 1e-9, "the shuffle stops at the push");
            Step(menu, "Max push", +1, 100);
            Step(menu, "Shuffle max", +1, 100);
            Assert.AreEqual(0.9, save.ShuffleMaxMetres, 1e-9);
            Step(menu, "Max push", -1, 100);
            Assert.AreEqual(0.9, save.MaxPushMetres, 1e-9);
        }

        [Test]
        public void A_file_with_wild_or_linked_wrong_values_loads_clamped_and_linked()
        {
            string path = TempFile(
                "goalieReactionSeconds=9\ngoalieReactionWobbleSeconds=5\ngoalieHandMetresPerSecond=1\ngoalieLegMetresPerSecond=11\n" +
                "goalieMaxPushMetres=0.2\ngoalieShuffleMaxMetres=0.8\n");
            try
            {
                var save = SaveData.Load(path);
                Assert.AreEqual(0.30, save.ReactionSeconds, 1e-9);
                Assert.AreEqual(0.10, save.ReactionWobbleSeconds, 1e-9);
                Assert.AreEqual(3, save.HandMetresPerSecond, 1e-9);
                Assert.AreEqual(3, save.LegMetresPerSecond, 1e-9, "the leg is held down to the hand");
                Assert.AreEqual(0.2, save.MaxPushMetres, 1e-9);
                Assert.AreEqual(0.2, save.ShuffleMaxMetres, 1e-9, "the shuffle is held down to the push");
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void The_goalie_values_save_and_load_back()
        {
            string path = TempFile("");
            try
            {
                var save = new SaveData { ReactionSeconds = 0.2, ReactionWobbleSeconds = 0.07, HandMetresPerSecond = 10, LegMetresPerSecond = 4.5, MaxPushMetres = 0.75, ShuffleMaxMetres = 0.25 };
                Assert.IsTrue(save.Save(path));
                var back = SaveData.Load(path);
                Assert.AreEqual(0.2, back.ReactionSeconds, 1e-9);
                Assert.AreEqual(0.07, back.ReactionWobbleSeconds, 1e-9);
                Assert.AreEqual(10, back.HandMetresPerSecond, 1e-9);
                Assert.AreEqual(4.5, back.LegMetresPerSecond, 1e-9);
                Assert.AreEqual(0.75, back.MaxPushMetres, 1e-9);
                Assert.AreEqual(0.25, back.ShuffleMaxMetres, 1e-9);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Reset_to_default_in_the_Dev_part_sets_every_goalie_value_back_and_leaves_the_Player_part_alone()
        {
            var save = new SaveData
            {
                ReactionSeconds = 0.25, ReactionWobbleSeconds = 0.09, HandMetresPerSecond = 11, LegMetresPerSecond = 4,
                MaxPushMetres = 0.85, ShuffleMaxMetres = 0.15, ForceGoalieBlocks = true,
                MasterVolume = 0.3, EasyMode = true, InputIgnoreSeconds = 5, BestScore = 4
            };
            var menu = DevMenu(save);
            Assert.IsTrue(menu.ResetPart());
            var defaults = new SaveData();
            Assert.AreEqual(defaults.ReactionSeconds, save.ReactionSeconds);
            Assert.AreEqual(defaults.ReactionWobbleSeconds, save.ReactionWobbleSeconds);
            Assert.AreEqual(defaults.HandMetresPerSecond, save.HandMetresPerSecond);
            Assert.AreEqual(defaults.LegMetresPerSecond, save.LegMetresPerSecond);
            Assert.AreEqual(defaults.MaxPushMetres, save.MaxPushMetres);
            Assert.AreEqual(defaults.ShuffleMaxMetres, save.ShuffleMaxMetres);
            Assert.IsFalse(save.ForceGoalieBlocks);
            Assert.AreEqual(0.3, save.MasterVolume, 1e-9);
            Assert.IsTrue(save.EasyMode);
            Assert.AreEqual(5, save.InputIgnoreSeconds, 1e-9);
            Assert.AreEqual(4, save.BestScore);
        }

        [Test]
        public void Reset_to_default_in_the_Player_part_leaves_the_goalie_values_alone()
        {
            var save = new SaveData { ReactionSeconds = 0.25, HandMetresPerSecond = 11, MaxPushMetres = 0.85, MasterVolume = 0.3 };
            var menu = new SettingsMenu(save, new string[0]); // opens on the Player part
            Assert.IsTrue(menu.ResetPart());
            Assert.AreEqual(1.0, save.MasterVolume, 1e-9);
            Assert.AreEqual(0.25, save.ReactionSeconds, 1e-9);
            Assert.AreEqual(11, save.HandMetresPerSecond, 1e-9);
            Assert.AreEqual(0.85, save.MaxPushMetres, 1e-9);
        }

        static SaveData Tuned() => new SaveData
        {
            ReactionSeconds = 0.2, ReactionWobbleSeconds = 0.07, HandMetresPerSecond = 10, LegMetresPerSecond = 4.5, MaxPushMetres = 0.75, ShuffleMaxMetres = 0.25
        };

        [Test]
        public void A_changed_value_acts_from_the_next_Round_and_not_the_current_one()
        {
            var save = Tuned();
            var settings = new RoundSettings();
            var flow = new ScreenFlow(ScriptedRandom.Halves(), save, FakeReach.NeverReaches(), settings);
            Assert.AreEqual(0.12, settings.ReactionSeconds, "nothing is applied before a Round starts");

            flow.Press(); // Round 1 starts
            Assert.AreEqual(0.2, settings.ReactionSeconds, 1e-9);
            Assert.AreEqual(10, settings.HandMetresPerSecond, 1e-9);
            Assert.AreEqual(0.75, settings.MaxPushMetres, 1e-9);

            save.ReactionSeconds = 0.1; // changed during the Round (cannot happen on the page, but the seam must hold)
            save.HandMetresPerSecond = 7;
            flow.Tick(3.7);
            flow.SubmitShot(50, 90, 100, false);
            flow.Round.WhistleNow();
            flow.Tick(flow.Round.ResultHoldLength);
            Assert.AreEqual(0.2, settings.ReactionSeconds, 1e-9, "still Round 1's value");
            Assert.AreEqual(10, settings.HandMetresPerSecond, 1e-9);

            for (int shot = 1; shot < Round.ShotsPerRound; shot++)
            {
                flow.Tick(3.7);
                flow.SubmitShot(50, 90, 100, false);
                flow.Round.WhistleNow();
                flow.Tick(flow.Round.ResultHoldLength);
            }
            Assert.AreEqual(FlowScreen.End, flow.Screen);
            Assert.AreEqual(0.2, settings.ReactionSeconds, 1e-9, "the end screen does not apply it either");
            flow.Tick(save.InputIgnoreSeconds + 0.1);
            flow.Press(); // back to the start screen
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
            flow.Press(); // Round 2 starts
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            Assert.AreEqual(0.1, settings.ReactionSeconds, 1e-9, "Round 2 takes the new value");
            Assert.AreEqual(7, settings.HandMetresPerSecond, 1e-9);
        }

        [Test]
        public void A_Round_leaves_the_other_RoundSettings_as_the_code_made_them()
        {
            var settings = new RoundSettings { SkateInSeconds = 4, ShootingWindowSeconds = 5 };
            var flow = new ScreenFlow(ScriptedRandom.Halves(), Tuned(), FakeReach.NeverReaches(), settings);
            flow.Press();
            Assert.AreEqual(4, settings.SkateInSeconds);
            Assert.AreEqual(5, settings.ShootingWindowSeconds);
            Assert.AreEqual(new RoundSettings().PushAccelMetresPerSecondSquared, settings.PushAccelMetresPerSecondSquared);
        }

        [Test]
        public void Tests_and_tools_get_code_defaults_even_when_the_save_file_holds_tuned_values()
        {
            string path = TempFile(
                "goalieReactionSeconds=0.25\ngoalieReactionWobbleSeconds=0.09\ngoalieHandMetresPerSecond=11\ngoalieLegMetresPerSecond=4\n" +
                "goalieMaxPushMetres=0.85\ngoalieShuffleMaxMetres=0.15\n");
            try
            {
                Assert.AreEqual(0.25, SaveData.Load(path).ReactionSeconds, 1e-9, "the file holds tuned values");

                // The way tests and editor tools build their settings: from code, never from the file.
                var settings = new RoundSettings();
                Assert.AreEqual(0.12, settings.ReactionSeconds);
                Assert.AreEqual(0.04, settings.ReactionWobbleSeconds);
                Assert.AreEqual(8, settings.HandMetresPerSecond);
                Assert.AreEqual(6, settings.LegMetresPerSecond);
                Assert.AreEqual(0.6, settings.MaxPushMetres);
                Assert.AreEqual(0.3, settings.ShuffleMaxMetres);

                // And a flow built on a fresh SaveData (what tests and tools use) starts its Round on the same defaults.
                var flow = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches(), settings);
                flow.Press();
                Assert.AreEqual(0.12, settings.ReactionSeconds);
                Assert.AreEqual(0.6, settings.MaxPushMetres);
            }
            finally { File.Delete(path); }
        }

        static string TempFile(string text)
        {
            string path = Path.Combine(Path.GetTempPath(), "hockey-t147-" + System.Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, text);
            return path;
        }
    }
}
