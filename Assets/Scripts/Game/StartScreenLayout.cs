using UnityEngine;

namespace HockeyGame
{
    /// <summary>Where the start screen's parts go on a screen w x h (#140): the Shootout logo on top, the text lines under it.</summary>
    public static class StartScreenLayout
    {
        public static Rect Logo(float w, float h)
        {
            float height = Mathf.Min(0.30f * h, 0.62f * w / StartLogoArt.Aspect);
            float width = height * StartLogoArt.Aspect;
            return new Rect((w - width) / 2f, 0.04f * h, width, height);
        }

        static float TextTop(float w, float h) => Logo(w, h).yMax + 0.03f * h;

        public static Rect BestScore(float w, float h) => new Rect(0, TextTop(w, h), w, 40);
        public static Rect Prompt(float w, float h) => new Rect(0, TextTop(w, h) + 50, w, 40);
        public static Rect UsbStatus(float w, float h) => new Rect(0, TextTop(w, h) + 100, w, 40);
        public static Rect SettingsHint(float w, float h) => new Rect(10, h - 64, w, 30);
        public static Rect TestShots(float w, float h) => new Rect(10, h - 34, w, 30);
    }
}
