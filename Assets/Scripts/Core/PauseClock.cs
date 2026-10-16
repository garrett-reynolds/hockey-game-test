namespace HockeyGame.Core
{
    /// <summary>
    /// A clock that stops while the game is paused (#195 review). Feed it the real time (Time.unscaledTimeAsDouble); Now gives a time
    /// that does not count the paused stretches, so the ribbons, boards and crowd carry on from the same moment after Resume.
    /// </summary>
    public sealed class PauseClock
    {
        private bool _paused;
        private double _pausedAt;
        private double _pausedTotal; // real seconds spent paused so far, before the current pause

        /// <summary>Stops the clock at this real time. Does nothing when it is already stopped.</summary>
        public void Pause(double realNow)
        {
            if (_paused) return;
            _paused = true;
            _pausedAt = realNow;
        }

        /// <summary>Starts the clock again at this real time. Does nothing when it is not stopped.</summary>
        public void Resume(double realNow)
        {
            if (!_paused) return;
            _paused = false;
            _pausedTotal += realNow - _pausedAt;
        }

        /// <summary>The clock's time at this real time: real time minus every paused stretch.</summary>
        public double Now(double realNow) => (_paused ? _pausedAt : realNow) - _pausedTotal;
    }
}
