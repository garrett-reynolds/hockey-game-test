using System;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The game's frame lock (#85): 60 fps with VSync on, set at game start (RoundDriver), not in the project settings. VSync
    /// waits as many vertical blanks a frame as keep a fast screen at 60 fps (2 on 120 Hz); targetFrameRate caps at 60 too.
    /// </summary>
    public static class FrameRate
    {
        public const int TargetFps = 60;
        /// <summary>The most vertical blanks Unity waits a frame (QualitySettings.vSyncCount).</summary>
        public const int MaxVSyncCount = 4;

        /// <summary>Locks the frame rate to 60 fps on this screen.</summary>
        public static void Lock() => Lock(Screen.currentResolution.refreshRateRatio.value);

        /// <summary>Locks the frame rate to 60 fps on a screen refreshing refreshHz times a second.</summary>
        public static void Lock(double refreshHz)
        {
            QualitySettings.vSyncCount = VSyncCountFor(refreshHz);
            Application.targetFrameRate = TargetFps;
        }

        /// <summary>
        /// Vertical blanks a frame so the frame rate is about 60: refreshHz / 60, rounded, at least 1 (VSync stays on, also when
        /// the refresh rate is not reported) and at most MaxVSyncCount. 60 Hz: 1; 120 and 144 Hz: 2; 240 Hz: 4.
        /// </summary>
        public static int VSyncCountFor(double refreshHz)
        {
            if (double.IsNaN(refreshHz) || refreshHz <= 0) return 1;
            return Math.Max(1, Math.Min(MaxVSyncCount, (int)Math.Round(refreshHz / TargetFps)));
        }
    }
}
