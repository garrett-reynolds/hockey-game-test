using System;
using System.Runtime.InteropServices;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Tests
{
    /// <summary>The web file box and the save flush (#228): the C# side, driven without a browser.</summary>
    public class WebPickerTests
    {
        [TearDown]
        public void Reset()
        {
            WebFlush.Start = null;
            WebFlush.Reset();
        }

        [Test]
        public void EditorBuildUsesTheEditorPicker()
        {
            Assert.AreEqual(PickerKind.Editor, CustomLogos.ChooseKind(inEditor: true, onWeb: false));
            Assert.AreEqual(PickerKind.Editor, CustomLogos.ChooseKind(inEditor: true, onWeb: true));
            Assert.AreEqual("EditorPicturePicker", CustomLogos.Picker.GetType().Name); // these tests run in the Editor
        }

        [Test]
        public void WebBuildUsesTheWebPicker()
        {
            Assert.AreEqual(PickerKind.Web, CustomLogos.ChooseKind(inEditor: false, onWeb: true));
        }

        [Test]
        public void OtherBuildsHaveNoPickerYet()
        {
            Assert.AreEqual(PickerKind.None, CustomLogos.ChooseKind(inEditor: false, onWeb: false));
        }

        [Test]
        public void PickedBytesAreCopiedAndHandedBack()
        {
            byte[] got = null;
            new WebPicturePicker().Ask(b => got = b);
            IntPtr mem = Marshal.AllocHGlobal(3);
            try
            {
                Marshal.Copy(new byte[] { 1, 2, 3 }, 0, mem, 3);
                WebPicturePicker.Deliver(mem, 3);
                Marshal.WriteByte(mem, 0, 9); // the browser side frees its memory after the call: our copy must not follow it
            }
            finally { Marshal.FreeHGlobal(mem); }
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, got);
        }

        [Test]
        public void CancelHandsBackNull()
        {
            bool called = false;
            byte[] got = new byte[1];
            new WebPicturePicker().Ask(b => { called = true; got = b; });
            WebPicturePicker.Deliver(IntPtr.Zero, -1);
            Assert.IsTrue(called);
            Assert.IsNull(got);
        }

        [Test]
        public void EmptyFileIsNotCancel()
        {
            byte[] got = null;
            new WebPicturePicker().Ask(b => got = b);
            WebPicturePicker.Deliver(IntPtr.Zero, 0);
            Assert.IsNotNull(got);
            Assert.AreEqual(0, got.Length);
        }

        [Test]
        public void AnswerComesOnlyOnce()
        {
            int calls = 0;
            new WebPicturePicker().Ask(b => calls++);
            WebPicturePicker.Deliver(IntPtr.Zero, -1);
            WebPicturePicker.Deliver(IntPtr.Zero, -1);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void ASecondAskWhileTheBoxIsOpenCancelsTheFirstAndTheAnswerGoesToTheSecond()
        {
            byte[] first = new byte[1], second = new byte[1];
            bool firstCalled = false;
            new WebPicturePicker().Ask(b => { firstCalled = true; first = b; });
            new WebPicturePicker().Ask(b => second = b);
            Assert.IsTrue(firstCalled);
            Assert.IsNull(first, "the first was cancelled");
            WebPicturePicker.Deliver(IntPtr.Zero, 0);
            Assert.IsNotNull(second, "the answer is the second Ask's");
            Assert.AreEqual(0, second.Length);
        }

        [Test]
        public void AnswerAfterDiscardOrCloseIsDropped()
        {
            // Through the menu: the web box answers late, after the page dropped its changes or closed.
            var menu = new Core.SettingsMenu(new SaveData(), null, null, new WebPicturePicker(), new LateCodec(), null);
            menu.ShowPart(Core.SettingsPart.Brands);
            menu.ChangeLogo(0);                       // the box opens (row 0 is CENTRE ICE)
            menu.Discard();                           // the page drops its changes
            WebPicturePicker.Deliver(IntPtr.Zero, 0); // the answer comes late
            Assert.IsFalse(menu.HasUnsavedChanges, "dropped after Discard");

            menu.ChangeLogo(0);
            menu.Abandon();                           // the page closes
            WebPicturePicker.Deliver(IntPtr.Zero, 0);
            Assert.IsFalse(menu.HasUnsavedChanges, "dropped after the page closed");

            menu.ChangeLogo(0);                       // a fresh Ask still works
            WebPicturePicker.Deliver(IntPtr.Zero, 0);
            Assert.IsTrue(menu.HasUnsavedChanges, "the control: an answer in time sets the pick");
        }

        // Says every file is a 300 x 200 picture with a logo, so a late answer WOULD set a pick if it were let in.
        sealed class LateCodec : Core.IPictureCodec
        {
            public bool TryDecode(byte[] file, out int width, out int height, out byte[] rgba)
            {
                Core.RgbaPicture p = Core.Tests.LogoArtTests.LogoOnWhite(300, 200, 120, 50);
                width = p.Width; height = p.Height; rgba = p.Rgba;
                return true;
            }

            public byte[] EncodePng(int width, int height, byte[] rgba) => rgba;
        }

        [Test]
        public void AFailedBrowserStorePutsTheOldPictureBackAndSaysSo()
        {
            string id = "web-flush-fail-test";
            string failedId = null; bool failedHadOld = false;
            Action<string, bool> seen = (i, old) => { failedId = i; failedHadOld = old; };
            CustomLogos.WriteFailed += seen;
            try
            {
                WebFlush.Start = done => done(true);
                Assert.IsTrue(CustomLogos.Files.Write(id, new byte[] { 1 }));
                Action<bool> finish = null;
                WebFlush.Start = done => finish = done;
                Assert.IsTrue(CustomLogos.Files.Write(id, new byte[] { 2 }));
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("could not be stored"));
                finish(false);
                Assert.AreEqual(id, failedId);
                Assert.IsTrue(failedHadOld);
                CollectionAssert.AreEqual(new byte[] { 1 }, CustomLogos.Files.Read(id), "the old art stays");

                failedId = null;
                CustomLogos.Files.Delete(id);
                WebFlush.Start = done => finish = done;
                Assert.IsTrue(CustomLogos.Files.Write(id, new byte[] { 3 }));
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("could not be stored"));
                finish(false);
                Assert.IsFalse(failedHadOld);
                Assert.IsFalse(CustomLogos.Files.Exists(id), "no old art: nothing stays");

                Action<bool> okFinish = null;
                WebFlush.Start = done => okFinish = done;
                failedId = null;
                Assert.IsTrue(CustomLogos.Files.Write(id, new byte[] { 4 }));
                okFinish(true);
                Assert.IsNull(failedId, "a good store says nothing");
            }
            finally
            {
                CustomLogos.WriteFailed -= seen;
                WebFlush.Start = null;
                CustomLogos.Files.Delete(id);
            }
        }

        [Test]
        public void TheMenuShowsTheNoteAndTakesTheSavedFlagBack()
        {
            var save = new SaveData();
            save.SetLogoCustom(Core.LogoArt.GameLogoId, true);
            var menu = new Core.SettingsMenu(save, null, null, null, null, null);
            menu.ShowPart(Core.SettingsPart.Brands);
            int saves = 0;
            menu.SaveDataChanged += () => saves++;
            menu.LogoWriteFailed(Core.LogoArt.GameLogoId, false);
            Assert.IsFalse(save.IsLogoCustom(Core.LogoArt.GameLogoId));
            int row = 1;
            Assert.AreEqual(Core.LogoArt.GameLogoId, menu.LogoId(row));
            Assert.AreEqual("(not saved on this browser)", menu.Hint(row));
            Assert.AreEqual(1, saves);
        }

        [Test]
        public void FlushStartsTheBrowserSyncAndCountsItUntilDone()
        {
            Action<bool> finish = null;
            WebFlush.Start = done => finish = done;
            WebFlush.Request();
            Assert.IsNotNull(finish, "the sync was not started");
            Assert.AreEqual(1, WebFlush.Pending);
            finish(true);
            Assert.AreEqual(0, WebFlush.Pending);
            Assert.IsTrue(WebFlush.LastOk);
        }

        [Test]
        public void FailedFlushIsKept()
        {
            Action<bool> finish = null;
            WebFlush.Start = done => finish = done;
            WebFlush.Request();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("could not be stored"));
            finish(false);
            Assert.IsFalse(WebFlush.LastOk);
            Assert.AreEqual(0, WebFlush.Pending);
        }

        [Test]
        public void FlushDoesNothingWhereThereIsNoBrowser()
        {
            WebFlush.Start = null;
            Assert.DoesNotThrow(WebFlush.Request); // Editor and Windows: no hook, no sync
            Assert.AreEqual(0, WebFlush.Pending);
        }

        [Test]
        public void LogoFileWriteAndDeleteRequestAFlush()
        {
            int starts = 0;
            WebFlush.Start = done => { starts++; done(true); };
            string id = "web-flush-test";
            try
            {
                Assert.IsTrue(CustomLogos.Files.Write(id, new byte[] { 1 }));
                Assert.AreEqual(1, starts);
                CustomLogos.Files.Delete(id);
                Assert.AreEqual(2, starts);
            }
            finally { CustomLogos.Files.Delete(id); }
        }
    }
}
