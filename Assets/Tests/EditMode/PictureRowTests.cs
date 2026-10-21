using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The CENTRE ICE and GAME LOGO picture rows at the top of the Brands part (#230): rows, pending / SAVE / Discard, art lookup, round trip.</summary>
    public class PictureRowTests
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

        static readonly string[] Labels = { "CENTRE ICE", "GAME LOGO" };
        static readonly string[] Ids = { LogoArt.CentreIceId, LogoArt.GameLogoId };

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

        [Test]
        public void The_Brands_part_starts_with_the_CENTRE_ICE_and_GAME_LOGO_picture_rows_that_have_both_buttons_and_no_tick_box()
        {
            Assert.AreEqual("CENTRE ICE", _menu.Label(0));
            Assert.AreEqual("GAME LOGO", _menu.Label(1));
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(SettingsRowKind.Picture, _menu.Kind(i), "no tick box");
                Assert.IsTrue(_menu.HasLogoButtons(i));
                Assert.IsFalse(_menu.IsLogoCustom(i), "the shipped art at first");
                Assert.IsFalse(string.IsNullOrEmpty(_menu.Tip(i)), "a hover tip");
            }
        }

        [Test]
        public void The_rows_are_not_brands_so_the_tick_box_count_and_the_pool_stay_as_they_were()
        {
            int ticks = 0;
            for (int i = 0; i < _menu.RowCount; i++) if (_menu.Kind(i) == SettingsRowKind.Toggle) ticks++;
            Assert.AreEqual(Brands.All.Length, ticks, "one tick box per pool brand");
            Assert.AreEqual(Brands.All.Length + 2, _menu.RowCount);
            foreach (string id in Ids)
                Assert.IsFalse(Brands.All.Any(b => b.Id == id), id + ": a reserved id no brand can have");
            _picker.Next = FakeCodec.File(LogoArtTests.LogoOnWhite(300, 200, 120, 50));
            _menu.ChangeLogo(0);
            _menu.ChangeLogo(1);
            _menu.Save();
            Assert.AreEqual("", _save.BrandsOff, "a picture row changes no tick box");
            CollectionAssert.AreEqual(Brands.All, Brands.Pool(_save.BrandsOff), "the pool is every brand");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void A_pick_waits_for_Save_then_the_file_is_kept_under_the_reserved_id_and_the_lookup_is_told(int which)
        {
            int row = Row(Labels[which]);
            _menu.ChangeLogo(row);
            Assert.AreEqual(1, _picker.Asked);
            Assert.IsTrue(_menu.IsLogoCustom(row));
            Assert.IsTrue(_menu.HasUnsavedChanges);
            Assert.IsTrue(_menu.IsRowUnsaved(row));
            Assert.IsFalse(_save.IsLogoCustom(Ids[which]), "nothing in Save Data before SAVE");
            Assert.AreEqual(0, _files.Saved.Count);
            Assert.IsEmpty(_logoSaved);

            Assert.IsTrue(_menu.Save());
            Assert.IsTrue(_save.IsLogoCustom(Ids[which]));
            Assert.IsTrue(_files.Exists(Ids[which]));
            CollectionAssert.AreEqual(new[] { Ids[which] }, _logoSaved);
            Assert.IsFalse(_menu.HasUnsavedChanges);
            Assert.IsFalse(_save.IsLogoCustom(Ids[1 - which]), "the other row is untouched");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Discard_drops_the_pick_and_Cancel_changes_nothing(int which)
        {
            int row = Row(Labels[which]);
            _picker.Next = null; // Cancel
            _menu.ChangeLogo(row);
            Assert.IsFalse(_menu.HasUnsavedChanges);
            Assert.IsFalse(_menu.IsLogoCustom(row));

            _picker.Next = FakeCodec.File(LogoArtTests.LogoOnWhite(300, 200, 120, 50));
            _menu.ChangeLogo(row);
            Assert.IsTrue(_menu.HasUnsavedChanges);
            _menu.Discard();
            Assert.IsFalse(_menu.HasUnsavedChanges);
            Assert.IsFalse(_menu.IsLogoCustom(row));
            Assert.IsFalse(_menu.Save());
            Assert.AreEqual(0, _files.Saved.Count);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void A_refused_file_shows_in_the_hint_and_changes_nothing(int which)
        {
            int row = Row(Labels[which]);
            _picker.Next = new byte[] { 1, 2, 3 };
            _menu.ChangeLogo(row);
            Assert.AreEqual("(not a picture)", _menu.Hint(row));
            Assert.IsFalse(_menu.HasUnsavedChanges);
            Assert.IsFalse(_menu.IsLogoCustom(row));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void USE_DEFAULT_and_Save_delete_the_file_and_bring_the_shipped_art_back(int which)
        {
            int row = Row(Labels[which]);
            _menu.ChangeLogo(row);
            _menu.Save();
            _logoSaved.Clear();
            Assert.IsTrue(_menu.UseDefaultLogo(row));
            Assert.IsTrue(_menu.HasUnsavedChanges);
            Assert.IsTrue(_files.Exists(Ids[which]), "the file stays until SAVE");
            Assert.IsTrue(_menu.Save());
            Assert.IsFalse(_save.IsLogoCustom(Ids[which]));
            Assert.IsFalse(_files.Exists(Ids[which]));
            CollectionAssert.AreEqual(new[] { Ids[which] }, _logoSaved);
        }

        [Test]
        public void TICK_EVERY_BRAND_keeps_the_pictures()
        {
            _menu.ChangeLogo(0);
            _menu.ChangeLogo(1);
            _menu.Save();
            _menu.ShowPart(SettingsPart.Brands);
            _menu.ResetPart();
            Assert.IsTrue(_menu.IsLogoCustom(0));
            Assert.IsTrue(_menu.IsLogoCustom(1));
        }

        [Test]
        public void Both_pictures_stay_after_a_restart_and_a_missing_file_is_dropped()
        {
            string path = Path.Combine(Path.GetTempPath(), "pictrow-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                _menu.ChangeLogo(0);
                _menu.ChangeLogo(1);
                _menu.Save();
                _save.Save(path);
                SaveData back = SaveData.Load(path);
                Assert.IsTrue(back.IsLogoCustom(LogoArt.CentreIceId));
                Assert.IsTrue(back.IsLogoCustom(LogoArt.GameLogoId));
                _files.Delete(LogoArt.GameLogoId);
                var logged = new List<string>();
                Assert.IsTrue(back.DropMissingLogos(_files.Exists, logged.Add));
                Assert.IsTrue(back.IsLogoCustom(LogoArt.CentreIceId));
                Assert.IsFalse(back.IsLogoCustom(LogoArt.GameLogoId));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void The_game_logo_picture_is_fitted_in_the_start_logo_frame_with_clear_round_it()
        {
            RgbaPicture logo = LogoArt.MakeLogo(LogoArtTests.LogoOnWhite(300, 200, 120, 50));
            RgbaPicture art = LogoArt.MakeGameLogo(logo);
            Assert.AreEqual(1200, art.Width);
            Assert.AreEqual(330, art.Height);
            Assert.AreEqual(0, art.Rgba[3], "top-left corner clear");
            Assert.AreEqual(0, art.Rgba[(art.Width * art.Height - 1) * 4 + 3], "bottom-right corner clear");
            Assert.AreEqual(255, art.Rgba[((art.Height / 2) * art.Width + art.Width / 2) * 4 + 3], "the logo is solid in the middle");
            int top = art.Height, bottom = -1, left = art.Width, right = -1;
            for (int y = 0; y < art.Height; y++)
                for (int x = 0; x < art.Width; x++)
                    if (art.Rgba[(y * art.Width + x) * 4 + 3] > 8) { top = Math.Min(top, y); bottom = Math.Max(bottom, y); left = Math.Min(left, x); right = Math.Max(right, x); }
            Assert.Greater(top, 0, "room above");
            Assert.Less(bottom, art.Height - 1, "room below");
            Assert.Greater(left, 0);
            Assert.Less(right, art.Width - 1);
            Assert.AreEqual(logo.Width / (double)logo.Height, (right - left + 1) / (double)(bottom - top + 1), 0.05, "the shape is kept");
        }

        // The art lookup: a fake stands for the made pictures.
        Func<string, string, Texture2D> _spotWas;
        readonly List<Texture2D> _fakes = new List<Texture2D>();

        Texture2D Fake()
        {
            var t = new Texture2D(4, 4);
            _fakes.Add(t);
            return t;
        }

        void KeepSpot() { _spotWas = ArtLookup.CustomSpot; }

        void PutSpotBack()
        {
            ArtLookup.CustomSpot = _spotWas;
            ArtLookup.Clear(LogoArt.CentreIceId);
            ArtLookup.Clear(LogoArt.GameLogoId);
            foreach (Texture2D t in _fakes) UnityEngine.Object.DestroyImmediate(t);
            _fakes.Clear();
        }

        [Test]
        public void The_lookup_gives_the_picked_pictures_after_the_ids_are_cleared_and_the_shipped_art_when_they_are_not_custom()
        {
            KeepSpot();
            try
            {
                ArtLookup.Clear(LogoArt.CentreIceId); ArtLookup.Clear(LogoArt.GameLogoId);
                Texture2D leafs = ArtLookup.CentreIce();
                Assert.AreSame(Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + RinkAds.LeafsCentreArt), leafs);
                Assert.IsNull(ArtLookup.GameLogo(), "the drawn logo");

                Texture2D centre = Fake(), game = Fake();
                ArtLookup.CustomSpot = (id, spot) => id == LogoArt.CentreIceId && spot == "centre" ? centre : id == LogoArt.GameLogoId && spot == "game-logo" ? game : null;
                Assert.AreSame(leafs, ArtLookup.CentreIce(), "the cache holds the old art until SAVE clears it");
                ArtLookup.Clear(LogoArt.CentreIceId); ArtLookup.Clear(LogoArt.GameLogoId);
                Assert.AreSame(centre, ArtLookup.CentreIce());
                Assert.AreSame(game, ArtLookup.GameLogo());

                ArtLookup.CustomSpot = (id, spot) => null;
                ArtLookup.Clear(LogoArt.CentreIceId); ArtLookup.Clear(LogoArt.GameLogoId);
                Assert.AreSame(leafs, ArtLookup.CentreIce(), "USE DEFAULT and SAVE: the Leafs art is back");
                Assert.IsNull(ArtLookup.GameLogo(), "USE DEFAULT and SAVE: the drawn logo is back");
            }
            finally { PutSpotBack(); }
        }

        [Test]
        public void A_custom_centre_ice_does_not_touch_the_brand_art()
        {
            KeepSpot();
            try
            {
                Texture2D centre = Fake();
                Brands.Brand b = Brands.All[0];
                ArtLookup.Clear(b.Id);
                Texture2D shipped = ArtLookup.Panel(b, false);
                ArtLookup.CustomSpot = (id, spot) => id == LogoArt.CentreIceId ? centre : null;
                ArtLookup.Clear(LogoArt.CentreIceId);
                Assert.AreSame(centre, ArtLookup.CentreIce());
                Assert.AreSame(shipped, ArtLookup.Panel(b, false));
            }
            finally { PutSpotBack(); ArtLookup.Clear(Brands.All[0].Id); }
        }
    }
}
