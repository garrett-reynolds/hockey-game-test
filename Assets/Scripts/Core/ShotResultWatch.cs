namespace HockeyGame.Core
{
    /// <summary>
    /// Tells a frame-by-frame watcher (effects, sound) when the Round has recorded a new Shot result.
    /// Call TakeNew once per frame; a new Round starts the count again.
    /// </summary>
    public sealed class ShotResultWatch
    {
        private int _seen;

        /// <summary>True once for each new Shot result (then Round.LastResult holds it).</summary>
        public bool TakeNew(Round round)
        {
            int count = round.Results.Count;
            bool isNew = count > _seen && round.LastResult.HasValue;
            _seen = count;
            return isNew;
        }
    }
}
