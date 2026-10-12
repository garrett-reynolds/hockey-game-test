using UnityEngine;

namespace HockeyGame
{
    /// <summary>The game's frame lock (#85): 60 fps with VSync on, set at game start (RoundDriver), not in the project settings.</summary>
    public static class FrameRate
    {
        public const int TargetFps = 60;
        /// <summary>Vertical blanks per frame: 1 is VSync on.</summary>
        public const int VSyncCount = 1;

        /// <summary>Locks the frame rate: VSync on, and a 60 fps cap where VSync is not honoured.</summary>
        public static void Lock()
        {
            QualitySettings.vSyncCount = VSyncCount;
            Application.targetFrameRate = TargetFps;
        }
    }
}
