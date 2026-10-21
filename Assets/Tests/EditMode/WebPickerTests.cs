using System;
using System.Runtime.InteropServices;
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
