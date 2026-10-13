using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Where the lower third (#141) and its words sit, on any screen size (no drawing here, so a test can check it). Picture space is the
    /// bar's art, Width x Height, y down; everything leans right by Slant per unit of height above the middle. Screen space is picture
    /// space times Scale (the bar is 80 % of the screen's width).
    /// </summary>
    public static class LowerThirdLayout
    {
        public const int Width = 1240, Height = 170;
        public const float Slant = 0.25f, MidY = 85f, Left = 16f;
        /// <summary>The words' rows in picture space, and the navy bar's words' sides.</summary>
        public const float TextY0 = 30f, TextY1 = 130f, BarX0 = 396f, BarX1 = 1128f;
        /// <summary>The red tab's sides (before leaning).</summary>
        public const float TabLeft = 40f, TabRight = 380f;
        /// <summary>The kicker's box: centred on the tab (as it leans at the middle), narrower than the tab; its words may fill TextFit of that.</summary>
        public const float KickerCentre = 227f, KickerWidth = 300f;
        public const float TextFit = 0.95f;
        /// <summary>The kicker's words must stay this far (picture units) inside the tab's edges, on the rows they fill.</summary>
        public const float KickerMargin = 12f;
        /// <summary>The kicker's words fill these rows (picture space): its biggest font size is 32.</summary>
        public const float KickerBandY0 = 60f, KickerBandY1 = 100f;

        public static float Scale(float screenWidth) => screenWidth * 0.8f / Width;

        /// <summary>The leaned point of the art at picture (x, y).</summary>
        public static Vector2 Lean(float x, float y) => new Vector2(x + (MidY - y) * Slant + Left, y);

        /// <summary>The tab's left and right edge at picture row y.</summary>
        public static float TabLeftAt(float y) => Lean(TabLeft, y).x;
        public static float TabRightAt(float y) => Lean(TabRight, y).x;

        /// <summary>The whole bar on a w x h screen, fully slid in (slide moves its x only).</summary>
        public static Rect Bar(float w, float h)
        {
            float barWidth = w * 0.8f, barHeight = Height * Scale(w);
            return new Rect(w * 0.05f, h - barHeight - h * 0.075f, barWidth, barHeight);
        }

        public static Rect Kicker(Rect bar, float s) => At(bar, s, KickerCentre - KickerWidth / 2f, TextY0, KickerWidth, TextY1 - TextY0);
        public static Rect Headline(Rect bar, float s) => At(bar, s, BarX0, TextY0, BarX1 - BarX0, 62f);
        public static Rect Speed(Rect bar, float s) => At(bar, s, BarX0, TextY0 + 64f, BarX1 - BarX0, 36f);

        static Rect At(Rect bar, float s, float x, float y, float w, float h) => new Rect(bar.x + x * s, bar.y + y * s, w * s, h * s);
    }
}
