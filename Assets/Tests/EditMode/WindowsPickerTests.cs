using System.IO;
using HockeyGame;
using NUnit.Framework;

namespace HockeyGame.Tests
{
    /// <summary>The Windows file box for CHANGE LOGO (#229): the picker choice per build, and what the picker does with the path the box gives back.</summary>
    public class WindowsPickerTests
    {
        [Test]
        public void Windows_player_uses_the_Windows_box()
        {
            Assert.AreEqual(PickerKind.Windows, CustomLogos.ChooseKind(inEditor: false, onWeb: false, onWindows: true));
        }

        [Test]
        public void Editor_wins_over_Windows()
        {
            Assert.AreEqual(PickerKind.Editor, CustomLogos.ChooseKind(inEditor: true, onWeb: false, onWindows: true));
        }

        [Test]
        public void Web_is_unchanged_and_no_box_elsewhere()
        {
            Assert.AreEqual(PickerKind.Web, CustomLogos.ChooseKind(inEditor: false, onWeb: true, onWindows: false));
            Assert.AreEqual(PickerKind.None, CustomLogos.ChooseKind(inEditor: false, onWeb: false, onWindows: false));
        }

        [Test]
        public void Make_gives_a_box_for_every_kind_that_has_one()
        {
            Assert.IsNull(CustomLogos.Make(PickerKind.None));
            Assert.IsInstanceOf<WebPicturePicker>(CustomLogos.Make(PickerKind.Web));
            Assert.IsInstanceOf<WindowsPicturePicker>(CustomLogos.Make(PickerKind.Windows)); // #224: the Windows player got null, so no CHANGE LOGO
            Assert.IsNotNull(CustomLogos.Make(PickerKind.Editor)); // the Editor build only; the tests run in the Editor
        }

        [Test]
        public void Cancel_gives_null()
        {
            byte[] got = new byte[1];
            bool called = false;
            new WindowsPicturePicker(() => null).Ask(b => { called = true; got = b; });
            Assert.IsTrue(called);
            Assert.IsNull(got);
        }

        [Test]
        public void Chosen_file_gives_its_bytes()
        {
            string path = Path.Combine(Path.GetTempPath(), "windows-picker-test-" + System.Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            try
            {
                byte[] got = null;
                new WindowsPicturePicker(() => path).Ask(b => got = b);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, got);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Unreadable_file_gives_empty_bytes_not_null()
        {
            byte[] got = null;
            new WindowsPicturePicker(() => Path.Combine(Path.GetTempPath(), "no-such-folder-229", "x.png")).Ask(b => got = b);
            Assert.IsNotNull(got);
            Assert.AreEqual(0, got.Length);
        }

        [Test]
        public void Filter_lists_png_and_jpg_and_ends_with_two_nulls()
        {
            Assert.AreEqual("Pictures (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0\0", WindowsPicturePicker.Filter);
        }
    }
}
