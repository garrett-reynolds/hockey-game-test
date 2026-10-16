using UnityEngine;

namespace HockeyGame
{
    /// <summary>The PAUSED box's two buttons, in the order they sit (PauseLayout.Button takes the number).</summary>
    public enum PauseButton
    {
        Resume,
        QuitToStart
    }

    /// <summary>
    /// Where the PAUSED box (#196, look C "TV graphic") and its two buttons sit on any screen size (no drawing here, so a test can check it).
    /// Sizes are for a 1280 x 720 screen, times LowerThirdLayout.Scale. The box is centred; it stays clear of the Score Box (top right) and the
    /// lower third (bottom).
    /// </summary>
    public static class PauseLayout
    {
        /// <summary>Resume and Quit to start: the PauseButton values.</summary>
        public const int ButtonCount = 2;

        public const float BoxWidth = 460f, BoxHeight = 260f, HeaderHeight = 72f, StripeHeight = 5f;
        public const float ButtonWidth = 380f, ButtonHeight = 58f, ButtonGap = 16f;

        public static float Scale(float w, float h) => LowerThirdLayout.Scale(w, h);

        public static Rect Box(float w, float h)
        {
            float s = Scale(w, h), bw = BoxWidth * s, bh = BoxHeight * s;
            return new Rect((w - bw) / 2f, (h - bh) / 2f, bw, bh);
        }

        /// <summary>The red header with the word PAUSED, inside the box under its gold stripe.</summary>
        public static Rect Header(float w, float h)
        {
            Rect box = Box(w, h);
            float s = Scale(w, h);
            return new Rect(box.x, box.y + StripeHeight * s, box.width, HeaderHeight * s);
        }

        public static Rect Stripe(float w, float h)
        {
            Rect box = Box(w, h);
            return new Rect(box.x, box.y, box.width, StripeHeight * Scale(w, h));
        }

        /// <summary>Button 0 is Resume, button 1 is Quit to start.</summary>
        public static Rect Button(float w, float h, int index)
        {
            Rect box = Box(w, h), header = Header(w, h);
            float s = Scale(w, h), bw = ButtonWidth * s, bh = ButtonHeight * s, gap = ButtonGap * s;
            float y = header.yMax + gap + index * (bh + gap);
            return new Rect(box.x + (box.width - bw) / 2f, y, bw, bh);
        }

        /// <summary>The button under this point (GUI coordinates, y down), or -1 for none.</summary>
        public static int ButtonAt(float w, float h, Vector2 point)
        {
            for (int i = 0; i < ButtonCount; i++)
                if (Button(w, h, i).Contains(point)) return i;
            return -1;
        }
    }
}
