namespace HockeyGame.Core
{
    /// <summary>
    /// Tells a frame-by-frame watcher (effects, sound) when the Round has recorded a new Shot result (TakeNew: a Whistle's
    /// result, or a Too Late) or been given a new Shot (TakeNewShot). Call each once per frame; a new Round starts the count again.
    /// </summary>
    public sealed class ShotResultWatch
    {
        private int _seen;
        private int _seenShots;

        /// <summary>True once for each new Shot result (then Round.LastResult holds it).</summary>
        public bool TakeNew(Round round)
        {
            int count = round.Results.Count;
            bool isNew = count > _seen && round.LastResult.HasValue;
            _seen = count;
            return isNew;
        }

        /// <summary>True once for each new Shot submitted (its result comes later, at the Whistle; then Round.LastShot holds it).</summary>
        public bool TakeNewShot(Round round)
        {
            int count = round.ShotsSubmitted;
            bool isNew = count > _seenShots && round.LastShot.HasValue;
            _seenShots = count;
            return isNew;
        }
    }
}
