namespace HockeyGame.Core
{
    /// <summary>What the goalie character makes a Save with (see GLOSSARY: Save Reaction).</summary>
    public enum SaveReaction { Glove, Blocker, Pads, Stick, Body }

    /// <summary>
    /// Chooses the Save Reaction from where the Shot meets the Net Face (0..100 from the bottom-left, shooter's view). Only
    /// the point counts, never the Goalie Pose, so the same point always gets the same reaction. The catching glove is on the
    /// shooter's right (the goalie's left hand faces the shooter on that side), the blocker on the shooter's left.
    /// </summary>
    public static class SaveReactions
    {
        /// <summary>Below this height (Net Face units) a Shot is low: the pads take it (0.49 m, about a butterfly's pad top).</summary>
        public const double PadsBelow = 40.0;
        /// <summary>Below this height a Shot is along the ice: through the middle the stick takes it (0.15 m).</summary>
        public const double StickBelow = 12.0;
        /// <summary>The middle strip (Net Face x) the body and the stick cover: about the goalie's chest width, 0.55 m.</summary>
        public const double MiddleFrom = 35.0, MiddleTo = 65.0;
        /// <summary>The body takes middle Shots up to this height (0.92 m, the shoulders); above it a hand does.</summary>
        public const double BodyBelow = 75.0;

        public static SaveReaction For(double x, double y)
        {
            bool middle = x >= MiddleFrom && x < MiddleTo;
            if (y < StickBelow && middle) return SaveReaction.Stick;
            if (y < PadsBelow) return SaveReaction.Pads;
            if (middle && y < BodyBelow) return SaveReaction.Body;
            return x >= 50.0 ? SaveReaction.Glove : SaveReaction.Blocker;
        }

        /// <summary>The save sound of the reaction's gear: wood for the blocker and the stick, a thud for pads and body.</summary>
        public static SoundCue Sound(SaveReaction reaction)
        {
            switch (reaction)
            {
                case SaveReaction.Glove: return SoundCue.GloveCatch;
                case SaveReaction.Blocker: case SaveReaction.Stick: return SoundCue.BlockerHit;
                default: return SoundCue.SaveImpact;
            }
        }
    }
}
