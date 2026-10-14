namespace HockeyGame.Core
{
    /// <summary>
    /// What the screen shows around Test Mode (#157): the TEST MODE label, and the rules of the clean view (no Shot dots; the lower
    /// third is gated in LowerThird). Pure, so the screen only follows it.
    /// </summary>
    public static class TestModeHud
    {
        public const string LabelText = "TEST MODE";

        /// <summary>True when the TEST MODE label shows: in Test Mode only.</summary>
        public static bool LabelShown(ScreenFlow flow) => flow.TestMode;

        /// <summary>True when the Shot dots (one per Shot of a Round) draw: not on the start screen, and never in Test Mode.</summary>
        public static bool DotsShown(ScreenFlow flow) => flow.Screen != FlowScreen.Start && !flow.TestMode;

        /// <summary>The Score Box's words in Test Mode: the visit's Goals and Shots, e.g. "GOALS 2 · SHOTS 5" (Round.Results holds only the Shot showing, so the totals count).</summary>
        public static string CountText(int goals, int shots) => "GOALS " + goals + " · SHOTS " + shots;
    }
}
