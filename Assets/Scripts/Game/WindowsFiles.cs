using System;
using System.IO;
using System.Runtime.InteropServices;
using HockeyGame.Core;

namespace HockeyGame
{
    /// <summary>
    /// The Windows player's file box for CHANGE LOGO (#229): the Windows open-file box (comdlg32 GetOpenFileNameW) with a
    /// pictures filter, owned by the game window so it comes on top. Cancel gives null. A file that cannot be read gives empty
    /// bytes (not null), so the menu says "not a picture". The box blocks the game until it closes. This class compiles in every
    /// build; only the call into comdlg32 is Windows-player-only. Tests give their own path chooser.
    /// </summary>
    public sealed class WindowsPicturePicker : IPicturePicker
    {
        /// <summary>The box's filter: name, NUL, patterns, NUL, then one more NUL to end the list.</summary>
        public const string Filter = "Pictures (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0\0";

        readonly Func<string> _choosePath;

        /// <param name="choosePath">Shows the box and returns the chosen path, or null on Cancel. Null: the real Windows box.</param>
        public WindowsPicturePicker(Func<string> choosePath = null)
        {
            _choosePath = choosePath ?? ShowBox;
        }

        public void Ask(Action<byte[]> done)
        {
            string path = _choosePath();
            if (string.IsNullOrEmpty(path)) { done(null); return; }
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception) { bytes = new byte[0]; }
            done(bytes);
        }

        static string ShowBox()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.owner = GetActiveWindow();
            ofn.filter = Filter;
            const int chars = 32768;
            IntPtr buffer = Marshal.AllocHGlobal(chars * 2);
            try
            {
                Marshal.WriteInt16(buffer, 0, 0);
                ofn.file = buffer;
                ofn.maxFile = chars;
                ofn.title = "Choose a picture";
                ofn.flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnHideReadOnly;
                if (!GetOpenFileNameW(ref ofn)) return null;
                return Marshal.PtrToStringUni(buffer);
            }
            finally { Marshal.FreeHGlobal(buffer); }
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int OfnHideReadOnly = 0x4, OfnNoChangeDir = 0x8, OfnPathMustExist = 0x800, OfnFileMustExist = 0x1000, OfnExplorer = 0x80000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName
        {
            public int structSize;
            public IntPtr owner;
            public IntPtr instance;
            public string filter;
            public string customFilter;
            public int maxCustomFilter;
            public int filterIndex;
            public IntPtr file;
            public int maxFile;
            public string fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData;
            public IntPtr hook;
            public string templateName;
            public IntPtr reserved;
            public int reserved2;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetOpenFileNameW(ref OpenFileName ofn);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();
#endif
    }
}
