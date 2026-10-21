using System;
using System.Runtime.InteropServices;
using AOT;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>Which file box CHANGE LOGO uses in this build (#228).</summary>
    public enum PickerKind { None, Editor, Web }

    /// <summary>
    /// The web build's file box for CHANGE LOGO (#228): the browser plugin (Assets/Plugins/WebGL/HockeyWebFiles.jslib) opens a hidden
    /// file input and calls Deliver with the picture bytes, or with length -1 on Cancel. The plugin frees its memory right after
    /// Deliver returns, so Deliver copies the bytes. This class compiles in every build; only the calls into the plugin are web-only.
    /// </summary>
    public sealed class WebPicturePicker : IPicturePicker
    {
        static Action<byte[]> _waiting;

        public void Ask(Action<byte[]> done)
        {
            _waiting = done;
#if UNITY_WEBGL && !UNITY_EDITOR
            HockeyPickPicture(OnPicked);
#endif
        }

        /// <summary>length -1: Cancel (null). length 0: an empty or unreadable file (not null, so the menu says "not a picture").</summary>
        public static void Deliver(IntPtr data, int length)
        {
            Action<byte[]> done = _waiting;
            _waiting = null;
            if (done == null) return;
            if (length < 0) { done(null); return; }
            var bytes = new byte[length];
            if (length > 0) Marshal.Copy(data, bytes, 0, length);
            done(bytes);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void HockeyPickPicture(Action<IntPtr, int> callback);

        [MonoPInvokeCallback(typeof(Action<IntPtr, int>))]
        static void OnPicked(IntPtr data, int length) => Deliver(data, length);
#endif
    }

    /// <summary>
    /// Makes the browser write the saved files (the logo pictures and Save Data under persistentDataPath) to IndexedDB (#228).
    /// Unity's file writes land in memory first; FS.syncfs(false) copies them to the browser's stored files, and that is
    /// asynchronous. Request starts it after each write; Pending says how many are still running. Does nothing outside the web build.
    /// </summary>
    public static class WebFlush
    {
        /// <summary>Starts one sync and calls back with true when it worked. Null where there is no browser. Tests set it.</summary>
        public static Action<Action<bool>> Start =
#if UNITY_WEBGL && !UNITY_EDITOR
            StartInBrowser;
#else
            null;
#endif

        public static int Pending { get; private set; }

        /// <summary>True until a sync fails (a failed one logs a warning).</summary>
        public static bool LastOk { get; private set; } = true;

        public static void Request()
        {
            Action<Action<bool>> start = Start;
            if (start == null) return;
            Pending++;
            start(ok =>
            {
                Pending--;
                LastOk = ok;
                if (!ok) Debug.LogWarning("The saved files could not be stored in the browser (IndexedDB sync failed).");
            });
        }

        public static void Reset()
        {
            Pending = 0;
            LastOk = true;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static readonly System.Collections.Generic.Queue<Action<bool>> Running = new System.Collections.Generic.Queue<Action<bool>>();

        static void StartInBrowser(Action<bool> done)
        {
            Running.Enqueue(done);
            HockeyFlushFiles(OnFlushed);
        }

        [DllImport("__Internal")]
        static extern void HockeyFlushFiles(Action<int> callback);

        [MonoPInvokeCallback(typeof(Action<int>))]
        static void OnFlushed(int ok)
        {
            if (Running.Count > 0) Running.Dequeue()(ok != 0);
        }
#endif
    }
}
