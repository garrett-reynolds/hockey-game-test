using System;

namespace HockeyGame.Core
{
    /// <summary>The green "SAVED" on the Settings page shows for SavedSeconds, then fades out over the last FadeSeconds (#195 review).</summary>
    public static class SavedFade
    {
        public const double SavedSeconds = 1.5, FadeSeconds = 0.4;

        /// <summary>How solid "SAVED" is (1 solid, 0 gone) with this many seconds left.</summary>
        public static double Opacity(double secondsLeft) => Math.Max(0, Math.Min(1, secondsLeft / FadeSeconds));
    }
}
