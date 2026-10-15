using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Where the lower third (#141, look A) and its parts sit on any screen size (no drawing here, so a test can check it): a full-width bar
    /// along the very bottom with a gold stripe on top; left to right the red tab (and its gold edge), the tally, the stat, the blue cap.
    /// Sizes are for a 1280 x 720 screen, times Scale.
    /// </summary>
    public static class LowerThirdLayout
    {
        public const float BarHeight = 40f, StripeHeight = 3f, TabWidth = 200f, TabEdge = 8f, TallyWidth = 316f, CapWidth = 180f;

        /// <summary>The size against 1280 x 720, kept by the narrower side so the parts fit a narrow screen too.</summary>
        public static float Scale(float w, float h) => Mathf.Min(w / 1280f, h / 720f);

        public static Rect Bar(float w, float h)
        {
            float barH = BarHeight * Scale(w, h);
            return new Rect(0f, h - barH, w, barH);
        }

        public static Rect Stripe(float w, float h)
        {
            float t = StripeHeight * Scale(w, h);
            return new Rect(0f, Bar(w, h).y - t, w, t);
        }

        public static Rect Tab(float w, float h) => Part(w, h, 0f, TabWidth * Scale(w, h));

        public static Rect Tally(float w, float h) => Part(w, h, (TabWidth + TabEdge) * Scale(w, h), TallyWidth * Scale(w, h));

        public static Rect Stat(float w, float h)
        {
            float x = Tally(w, h).xMax;
            return Part(w, h, x, Cap(w, h).x - x);
        }

        public static Rect Cap(float w, float h)
        {
            float capW = CapWidth * Scale(w, h);
            return Part(w, h, w - capW, capW);
        }

        static Rect Part(float w, float h, float x, float width)
        {
            Rect bar = Bar(w, h);
            return new Rect(x, bar.y, width, bar.height);
        }
    }
}
