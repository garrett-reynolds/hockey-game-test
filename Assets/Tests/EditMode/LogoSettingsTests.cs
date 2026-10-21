using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>CHANGE LOGO and USE DEFAULT on the Settings page's brand rows (#226), driven with a fake picker, codec and file store.</summary>
    public class LogoSettingsTests
    {
        sealed class FakePicker : IPicturePicker
        {
            public byte[] Next;
            public int Asked;
            public void Ask(Action<byte[]> done) { Asked++; done(Next); }
        }

        // A made-up picture file: "FAKE", width, height, then the RGBA bytes. Anything else is "not a picture".
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
            public bool WriteWorks = true;
            public bool Write(string id, byte[] png) { if (!WriteWorks) return false; Saved[id] = png; return true; }
            public void Delete(string id) => Saved.Remove(id);
            public bool Exists(string id) => Saved.ContainsKey(id);
            public byte[] Read(string id) => Saved.TryGetValue(id, out var b) ? b : null;
        }

        SaveData _save;
        FakePicker _picker;
        FakeFiles _files;
        SettingsMenu _menu;
        readonly List<string> _logoSaved = new List<string>();
        Brands.Brand _brand;
        int _row;

        [SetUp]
        public void Open()
        {
            _brand = Brands.All[4];
            _save = new SaveData();
            _picker = new FakePicker { Next = Good() };
            _files = new FakeFiles();
            _menu = new SettingsMenu(_save, null, null, _picker, new FakeCodec(), _files);
            _menu.LogoSaved += _logoSaved.Add;
            _menu.ShowPart(SettingsPart.Brands);
            for (_row = 0; _row < _menu.RowCount; _row++) if (_menu.Label(_row) == _brand.Name) break;
        }

        static byte[] Good() => FakeCodec.File(LogoArtTests.LogoOnWhite(300, 200, 120, 50));

        [Test]
        public void Every_brand_row_has_the_two_buttons_next_to_its_tick_box_and_other_rows_do_not()
        {
            for (int i = 0; i < _menu.RowCount; i++)
            {
                Assert.IsTrue(_menu.HasLogoButtons(i), _menu.Label(i));
                Assert.AreEqual(i < 2 ? SettingsRowKind.Picture : SettingsRowKind.Toggle, _menu.Kind(i), "the tick box stays (the two picture rows at the top have none, #230)");
            }
            _menu.ShowPart(SettingsPart.Player);
            for (int i = 0; i < _menu.RowCount; i++) Assert.IsFalse(_menu.HasLogoButtons(i), _menu.Label(i));
        }

        [Test]
        public void A_pick_waits_for_Save_sets_the_unsaved_state_and_Save_writes_the_picture_and_lists_the_brand()
        {
            _menu.ChangeLogo(_row);
            Assert.AreEqual(1, _picker.Asked);
            Assert.IsTrue(_menu.IsLogoCustom(_row), "the row shows the pick");
            Assert.IsTrue(_menu.HasUnsavedChanges);
            Assert.IsTrue(_menu.IsRowUnsaved(_row));
            Assert.IsFalse(_save.IsLogoCustom(_brand.Id), "nothing in Save Data before SAVE");
            Assert.AreEqual(0, _files.Saved.Count, "no file before SAVE");
            Assert.IsEmpty(_logoSaved);

            Assert.IsTrue(_menu.Save());
            Assert.IsTrue(_save.IsLogoCustom(_brand.Id));
            Assert.IsTrue(_files.Exists(_brand.Id));
            CollectionAssert.AreEqual(new[] { _brand.Id }, _logoSaved);
            Assert.IsFalse(_menu.HasUnsavedChanges);
            Assert.IsTrue(_menu.IsLogoCustom(_row));
        }

        [Test]
        public void Discard_drops_the_pick()
        {
            _menu.ChangeLogo(_row);
            _menu.Discard();
            Assert.IsFalse(_menu.IsLogoCustom(_row));
            Assert.IsFalse(_menu.HasUnsavedChanges);
            Assert.IsFalse(_menu.Save());
            Assert.AreEqual(0, _files.Saved.Count);
            Assert.IsFalse(_save.IsLogoCustom(_brand.Id));
        }

        [Test]
        public void Cancel_in_the_file_box_changes_nothing()
        {
            _picker.Next = null;
            _menu.ChangeLogo(_row);
            Assert.AreEqual(1, _picker.Asked);
            Assert.IsFalse(_menu.IsLogoCustom(_row));
            Assert.IsFalse(_menu.HasUnsavedChanges);
        }

        [Test]
        public void A_broken_file_a_too_big_picture_and_a_picture_with_no_logo_are_refused_with_a_note_in_the_Hint()
        {
            var cases = new Dictionary<string, byte[]>
            {
                ["not a picture"] = new byte[] { 1, 2, 3 },
                ["too big"] = FakeCodec.File(new RgbaPicture(4097, 1, new byte[4097 * 4])),
                ["no logo"] = FakeCodec.File(LogoArtTests.Make(40, 30, (x, y) => (9, 9, 9, 0))),
            };
            foreach (var pair in cases)
            {
                _picker.Next = pair.Value;
                _menu.ChangeLogo(_row);
                Assert.IsFalse(string.IsNullOrEmpty(_menu.Hint(_row)), pair.Key + ": a note in the Hint");
                if (pair.Key == "too big") StringAssert.Contains("4096", _menu.Hint(_row));
                Assert.IsFalse(_menu.IsLogoCustom(_row), pair.Key);
                Assert.IsFalse(_menu.HasUnsavedChanges, pair.Key);
                Assert.AreEqual("", _save.CustomLogos, pair.Key + ": Save Data does not change");
            }
            _picker.Next = Good();
            _menu.ChangeLogo(_row);
            Assert.IsNull(_menu.Hint(_row), "a good pick clears the note");
        }

        [Test]
        public void A_picture_over_2048_px_is_shrunk_before_it_is_kept()
        {
            _picker.Next = FakeCodec.File(LogoArtTests.LogoOnWhite(3000, 600, 2400, 300));
            _menu.ChangeLogo(_row);
            Assert.IsTrue(_menu.IsLogoCustom(_row), "3000 px is allowed");
            _menu.Save();
            Assert.IsTrue(new FakeCodec().TryDecode(_files.Read(_brand.Id), out int w, out int h, out _));
            Assert.LessOrEqual(Math.Max(w, h), 2048);
        }

        [Test]
        public void USE_DEFAULT_then_Save_takes_the_brand_off_the_list_and_deletes_its_picture()
        {
            _menu.ChangeLogo(_row);
            _menu.Save();
            _logoSaved.Clear();

            Assert.IsTrue(_menu.UseDefaultLogo(_row));
            Assert.IsFalse(_menu.IsLogoCustom(_row));
            Assert.IsTrue(_menu.HasUnsavedChanges);
            Assert.IsTrue(_save.IsLogoCustom(_brand.Id), "waits for SAVE");
            Assert.IsTrue(_menu.Save());
            Assert.IsFalse(_save.IsLogoCustom(_brand.Id));
            Assert.IsFalse(_files.Exists(_brand.Id));
            CollectionAssert.AreEqual(new[] { _brand.Id }, _logoSaved);
        }

        [Test]
        public void USE_DEFAULT_on_a_brand_with_the_shipped_art_does_nothing_and_a_new_pick_after_a_pick_replaces_it()
        {
            Assert.IsFalse(_menu.UseDefaultLogo(_row));
            Assert.IsFalse(_menu.HasUnsavedChanges);
            _menu.ChangeLogo(_row);
            _menu.UseDefaultLogo(_row);
            Assert.IsFalse(_menu.HasUnsavedChanges, "a pick that is taken back leaves nothing waiting");
        }

        [Test]
        public void TICK_EVERY_BRAND_keeps_the_logos()
        {
            _menu.ChangeLogo(_row);
            _menu.Save();
            _menu.ResetPart();
            Assert.IsTrue(_menu.IsLogoCustom(_row));
        }

        [Test]
        public void A_file_that_cannot_be_written_leaves_the_default_and_a_note()
        {
            _files.WriteWorks = false;
            _menu.ChangeLogo(_row);
            _menu.Save();
            Assert.IsFalse(_save.IsLogoCustom(_brand.Id));
            Assert.IsFalse(string.IsNullOrEmpty(_menu.Hint(_row)));
        }

        [Test]
        public void Save_Data_writes_and_reads_the_custom_logo_list()
        {
            string path = Path.Combine(Path.GetTempPath(), "hockey-t226-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                var save = new SaveData();
                save.SetLogoCustom("bell", true);
                save.SetLogoCustom("bauer", true);
                Assert.IsTrue(save.Save(path));
                var back = SaveData.Load(path);
                Assert.IsTrue(back.IsLogoCustom("bell"));
                Assert.IsTrue(back.IsLogoCustom("bauer"));
                Assert.IsFalse(back.IsLogoCustom("ccm"));
                Assert.AreEqual(save.CustomLogos, back.CustomLogos);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void A_listed_id_with_no_picture_file_falls_back_to_default_with_a_log_line_and_is_dropped()
        {
            var save = new SaveData();
            save.SetLogoCustom("bell", true);
            save.SetLogoCustom("bauer", true);
            var log = new List<string>();
            Assert.IsTrue(save.DropMissingLogos(id => id == "bauer", log.Add));
            Assert.IsFalse(save.IsLogoCustom("bell"));
            Assert.IsTrue(save.IsLogoCustom("bauer"));
            Assert.AreEqual(1, log.Count);
            StringAssert.Contains("bell", log[0]);
            Assert.IsFalse(save.DropMissingLogos(id => true, log.Add));
        }
    }
}
