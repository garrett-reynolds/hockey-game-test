using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The five colours of look C "TV graphic" (#137), in one place (#151): the Shootout logo, the lower third and the Video Board's start
    /// card take them from here. The Settings page's style sheet (Resources/UI/LookC.uss) keeps the same values as USS text, because USS
    /// cannot read C#; a test (LookCTests) checks that the two lists match.
    /// </summary>
    public static class LookC
    {
        public static readonly Color32 Navy = new Color32(10, 16, 34, 255);
        public static readonly Color32 Blue = new Color32(20, 60, 150, 255);
        public static readonly Color32 Gold = new Color32(232, 190, 40, 255);
        public static readonly Color32 Red = new Color32(200, 30, 45, 255);
        public static readonly Color32 White = new Color32(255, 255, 255, 255);
    }
}
