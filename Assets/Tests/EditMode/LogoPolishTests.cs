using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>Brands part polish (#231): CUSTOM tag, USE EVERY DEFAULT LOGO, keyboard on the picture rows, hover tips on the two buttons.</summary>
    public class LogoPolishTests
    {
        sealed class FakePicker : IPicturePicker
        {
            public byte[] Next;
            public int Asked;
            public void Ask(Action<byte[]> done) { Asked++; done(Next); }
        }

        sealed class FakeCodec : IPictureCodec
        {
            public static byte[] File(RgbaPicture p)
            {
                var bytes = new byte[12 + p.Rgba.Length];
                bytes[0] = (byte)'F'; bytes[1] = (byte)'A'; bytes[2] = (byte)'K'; bytes[3] = (byte)'E';
                BitConverter.GetBytes(p.Width).CopyTo(bytes, 4);
                BitConverter.GetBytes(p.Height).CopyTo(bytes, 8);
                p.Rgba.CopyTo(bytes, 12);
                return bytes;
            }

            public bool TryDecode(byte[] file, out int width, out int height, out byte[] rgba)
            {
                width = height = 0; rgba = null;
                if (file == null || file.Length < 12 || file[0] != 'F' || file[1] != 'A' || file[2] != 'K' || file[3] != 'E') return false;
                width = BitConverter.ToInt32(file, 4); height = BitConverter.ToInt32(file, 8);
                rgba = new byte[file.Length - 12];
                Array.Copy(file, 12, rgba, 0, rgba.Length);
                return rgba.Length == width * height * 4;
            }

            public byte[] EncodePng(int width, int height, byte[] rgba) => File(new RgbaPicture(width, height, rgba));
        }

        sealed class FakeFiles : ILogoFiles
        {
            public readonly Dictionary<string, byte[]> Saved = new Dictionary<string, byte[]>();
            public bool Write(string id, byte[] png) { Saved[id] = png; return true; }
            public void Delete(string id) => Saved.Remove(id);
            public bool Exists(string id) => Saved.ContainsKey(id);
            public byte[] Read(string id) => Saved.TryGetValue(id, out var b) ? b : null;
        }

        const string EveryDefault = "USE EVERY DEFAULT LOGO";

        SaveData _save;
        FakePicker _picker;
        FakeFiles _files;
        SettingsMenu _menu;
        readonly List<string> _logoSaved = new List<string>();

        [SetUp]
        public void Open()
        {
            _logoSaved.Clear(); // NUnit keeps one fixture instance for every test
            _save = new SaveData();
            _picker = new FakePicker { Next = FakeCodec.File(LogoArtTests.LogoOnWhite(300, 200, 120, 50)) };
            _files = new FakeFiles();
            _menu = new SettingsMenu(_save, null, null, _picker, new FakeCodec(), _files);
            _menu.LogoSaved += _logoSaved.Add;
            _menu.ShowPart(SettingsPart.Brands);
        }

        [TearDown]
        public void Close() { Brands.UseOff(""); }

        int Row(string label)
        {
            for (int i = 0; i < _menu.RowCount; i++) if (_menu.Label(i) == label) return i;
            Assert.Fail("No row " + label);
            return -1;
        }

        // Picks a picture on every logo row and saves, so each has a saved custom logo.
        void MakeEveryLogoCustom()
        {
            for (int i = 0; i < _menu.RowCount; i++) if (_menu.HasLogoButtons(i)) _menu.ChangeLogo(i);
            Assert.IsTrue(_menu.Save());
            _logoSaved.Clear();
        }

        // ---- preview and CUSTOM tag ----

        [Test]
        public void Each_logo_row_names_the_logo_its_preview_shows_and_only_a_custom_one_has_the_CUSTOM_tag()
        {
            int centre = Row("CENTRE ICE"), game = Row("GAME LOGO"), brand = Row(Brands.All[0].Name);
            Assert.AreEqual(LogoArt.CentreIceId, _menu.LogoId(centre));
            Assert.AreEqual(LogoArt.GameLogoId, _menu.LogoId(game));
            Assert.AreEqual(Brands.All[0].Id, _menu.LogoId(brand));
            Assert.IsNull(_menu.LogoId(Row(EveryDefault)), "the button row has no logo");
            foreach (int row in new[] { centre, game, brand }) Assert.IsNull(_menu.CustomTag(row), "shipped logo: no tag");

            _menu.ChangeLogo(game);
            Assert.AreEqual("CUSTOM", _menu.CustomTag(game));
            Assert.IsNull(_menu.CustomTag(centre));
            _menu.ChangeLogo(brand);
            Assert.AreEqual("CUSTOM", _menu.CustomTag(brand));
            _menu.UseDefaultLogo(game);
            Assert.IsNull(_menu.CustomTag(game));
        }

        // ---- USE EVERY DEFAULT LOGO ----

        [Test]
        public void USE_EVERY_DEFAULT_LOGO_is_a_button_row_in_the_Brands_part_under_the_two_picture_rows_with_a_tip()
        {
            int row = Row(EveryDefault);
            Assert.AreEqual(2, row);
            Assert.AreEqual(SettingsRowKind.Button, _menu.Kind(row));
            Assert.IsFalse(_menu.HasLogoButtons(row));
            Assert.IsFalse(string.IsNullOrEmpty(_menu.Tip(row)));
            Assert.AreEqual(Brands.All.Length + 3, _menu.RowCount);
        }

        [Test]
        public void USE_EVERY_DEFAULT_LOGO_makes_every_logo_default_as_a_waiting_change_and_SAVE_applies_it()
        {
            MakeEveryLogoCustom();
            _menu.Select(Row(EveryDefault));
            _menu.Activate();

            for (int i = 0; i < _menu.RowCount; i++)
                if (_menu.HasLogoButtons(i)) { Assert.IsFalse(_menu.IsLogoCustom(i), _menu.Label(i)); Assert.IsTrue(_menu.IsRowUnsaved(i), _menu.Label(i) + " waits"); }
            Assert.IsTrue(_menu.HasUnsavedChanges);
            Assert.AreEqual(Brands.All.Length + 2, _files.Saved.Count, "nothing is deleted before SAVE");

            Assert.IsTrue(_menu.Save());
            Assert.AreEqual(0, _files.Saved.Count, "every kept picture is gone");
            Assert.AreEqual(Brands.All.Length + 2, _logoSaved.Count, "every logo's art is forgotten once");
            Assert.IsFalse(_menu.HasUnsavedChanges);
            Assert.AreEqual("", _save.CustomLogos);
        }

        [Test]
        public void Discard_drops_USE_EVERY_DEFAULT_LOGO_and_the_custom_logos_stay()
        {
            MakeEveryLogoCustom();
            _menu.Select(Row(EveryDefault));
            _menu.Activate();
            _menu.Discard();
            for (int i = 0; i < _menu.RowCount; i++)
                if (_menu.HasLogoButtons(i)) Assert.IsTrue(_menu.IsLogoCustom(i), _menu.Label(i));
            Assert.IsFalse(_menu.HasUnsavedChanges);
            Assert.AreEqual(Brands.All.Length + 2, _files.Saved.Count);
        }

        [Test]
        public void USE_EVERY_DEFAULT_LOGO_also_drops_a_picked_logo_that_was_never_saved()
        {
            _menu.ChangeLogo(Row("CENTRE ICE"));
            Assert.IsTrue(_menu.HasUnsavedChanges);
            _menu.Select(Row(EveryDefault));
            _menu.Activate();
            Assert.IsFalse(_menu.HasUnsavedChanges, "back to what is saved");
            Assert.IsFalse(_menu.Save());
            Assert.AreEqual(0, _files.Saved.Count);
        }

        [Test]
        public void USE_EVERY_DEFAULT_LOGO_leaves_the_tick_boxes_alone()
        {
            int off = Row(Brands.All[1].Name), on = Row(Brands.All[0].Name);
            _menu.Select(off);
            _menu.Activate(); // untick one brand
            MakeEveryLogoCustom();
            Assert.AreEqual("OFF", _menu.Value(off));
            _menu.Select(Row(EveryDefault));
            _menu.Activate();
            Assert.AreEqual("OFF", _menu.Value(off), "still unticked");
            _menu.Save();
            Assert.AreEqual("OFF", _menu.Value(off), "still unticked after SAVE");
            Assert.AreEqual("ON", _menu.Value(on));
        }

        [Test]
        public void TICK_EVERY_BRAND_leaves_the_logos_alone()
        {
            MakeEveryLogoCustom();
            int off = Row(Brands.All[1].Name);
            _menu.Select(off);
            _menu.Activate(); // untick one brand
            Assert.AreEqual("OFF", _menu.Value(off));
            _menu.ResetPart();
            Assert.AreEqual("ON", _menu.Value(off), "ticked again");
            for (int i = 0; i < _menu.RowCount; i++)
                if (_menu.HasLogoButtons(i)) Assert.IsTrue(_menu.IsLogoCustom(i), _menu.Label(i));
        }

        // ---- keyboard ----

        [Test]
        public void Up_and_Down_reach_the_picture_rows_and_the_chosen_button_starts_on_CHANGE_LOGO()
        {
            _menu.Select(0);
            _menu.Press(SettingsKey.Down);
            Assert.AreEqual(1, _menu.Selected);
            Assert.AreEqual(SettingsRowKind.Picture, _menu.Kind(_menu.Selected));
            Assert.AreEqual(0, _menu.LogoButton(_menu.Selected), "CHANGE LOGO first");
            _menu.Press(SettingsKey.Up);
            Assert.AreEqual(0, _menu.Selected);
        }

        [Test]
        public void Right_and_Left_move_between_CHANGE_LOGO_and_USE_DEFAULT_and_change_nothing_else()
        {
            int row = Row("GAME LOGO");
            _menu.Select(row);
            Assert.IsFalse(_menu.Press(SettingsKey.Right), "no setting changed");
            Assert.AreEqual(1, _menu.LogoButton(row));
            _menu.Press(SettingsKey.Right);
            Assert.AreEqual(1, _menu.LogoButton(row), "stops at the last button");
            _menu.Press(SettingsKey.Left);
            Assert.AreEqual(0, _menu.LogoButton(row));
            _menu.Press(SettingsKey.Left);
            Assert.AreEqual(0, _menu.LogoButton(row));
            Assert.IsFalse(_menu.HasUnsavedChanges);
            Assert.AreEqual(0, _picker.Asked);
        }

        [Test]
        public void Enter_presses_the_chosen_button_CHANGE_LOGO_opens_the_picker_USE_DEFAULT_puts_the_shipped_logo_back()
        {
            int row = Row("CENTRE ICE");
            _menu.Select(row);
            _menu.Press(SettingsKey.Enter);
            Assert.AreEqual(1, _picker.Asked);
            Assert.AreEqual("CUSTOM", _menu.CustomTag(row));
            Assert.IsTrue(_menu.IsRowUnsaved(row));

            _menu.Press(SettingsKey.Right);
            _menu.Press(SettingsKey.Enter);
            Assert.AreEqual(1, _picker.Asked, "USE DEFAULT does not open the picker");
            Assert.IsNull(_menu.CustomTag(row));
            Assert.IsFalse(_menu.HasUnsavedChanges);
        }

        [Test]
        public void Each_picture_row_keeps_its_own_chosen_button()
        {
            _menu.Select(0);
            _menu.Press(SettingsKey.Right);
            _menu.Press(SettingsKey.Down);
            Assert.AreEqual(0, _menu.LogoButton(1));
            Assert.AreEqual(1, _menu.LogoButton(0));
        }

        [Test]
        public void The_keys_on_a_brand_row_and_the_button_row_work_as_before()
        {
            int brand = Row(Brands.All[0].Name);
            _menu.Select(brand);
            Assert.AreEqual("ON", _menu.Value(brand));
            Assert.IsTrue(_menu.Press(SettingsKey.Enter), "Enter still flips the tick box");
            Assert.AreEqual("OFF", _menu.Value(brand));
            Assert.IsTrue(_menu.Press(SettingsKey.Left));
            Assert.AreEqual("ON", _menu.Value(brand));
            Assert.AreEqual(0, _picker.Asked);

            _menu.Select(Row(EveryDefault));
            Assert.IsFalse(_menu.Press(SettingsKey.Enter), "an action row changes no saved setting");
            Assert.IsFalse(_menu.Press(SettingsKey.Right));
        }

        // ---- hover tips ----

        [Test]
        public void Hover_tips_on_the_two_buttons_say_what_CHANGE_LOGO_makes_and_what_USE_DEFAULT_does()
        {
            int brand = Row(Brands.All[0].Name);
            string change = _menu.LogoButtonTip(brand, 0), use = _menu.LogoButtonTip(brand, 1);
            StringAssert.Contains("boards", change);
            StringAssert.Contains("ice", change);
            StringAssert.Contains("Ribbon", change);
            StringAssert.Contains("one picture", change);
            Assert.IsFalse(string.IsNullOrEmpty(use));
            Assert.AreNotEqual(change, use);
            StringAssert.Contains("SAVE", use);
        }

        [Test]
        public void The_picture_rows_have_button_tips_of_their_own()
        {
            foreach (string label in new[] { "CENTRE ICE", "GAME LOGO" })
            {
                int row = Row(label);
                string change = _menu.LogoButtonTip(row, 0), use = _menu.LogoButtonTip(row, 1);
                Assert.IsFalse(string.IsNullOrEmpty(change), label);
                Assert.IsFalse(string.IsNullOrEmpty(use), label);
                Assert.AreNotEqual(change, use, label);
                Assert.AreNotEqual(_menu.LogoButtonTip(Row(Brands.All[0].Name), 0), change, label + " says what it makes");
            }
        }

        [Test]
        public void A_row_without_logo_buttons_has_no_button_tip()
        {
            Assert.IsNull(_menu.LogoButtonTip(Row(EveryDefault), 0));
            Assert.IsNull(_menu.LogoButtonTip(Row(EveryDefault), 1));
        }
    }
}
