using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// Before the Shot (#70): on the skate-in the goalie plays the angle in shuffles instead of gliding along with it. He waits
    /// until the angle is RoundSettings.ShuffleStartMetres across from him, then shuffles over in ShuffleSeconds (game seconds),
    /// pushing on to where the angle has got to. Across only: his depth follows the angle as it is. Off the skate-in (Shuffles
    /// false) he finishes the shuffle he is in without waiting, then moves with the angle, so nothing jumps; the Set-up spot is
    /// never touched. BreakawayView feeds what this shows into GoalieShuffle as the spot he is headed for.
    /// </summary>
    public sealed class SkateInShuffles
    {
        double _x, _from; // across: where he is headed now; where the shuffle he is in started
        double _t = 1;    // how far through that shuffle (1: not shuffling)
        bool _following;  // off the skate-in, once the last shuffle is done: moving with the angle

        public SkateInShuffles(RinkPoint angle) { Reset(angle); }

        /// <summary>Stands on the angle at once, not shuffling (a new skate-in starts on the angle).</summary>
        public void Reset(RinkPoint angle)
        {
            _x = _from = angle.X;
            _t = 1;
            _following = true; // nothing to finish: off the skate-in he moves with the angle; on it he waits
        }

        /// <summary>
        /// One step of dt game seconds with the angle at `angle`: the spot he is headed for, across in shuffles when shuffles
        /// is true (on the skate-in with SkateInShuffles on), at the angle's depth.
        /// </summary>
        public RinkPoint Step(RinkPoint angle, double dt, bool shuffles, RoundSettings settings)
        {
            if (shuffles) _following = false;
            if (_following)
                _x = angle.X;
            else
            {
                if (_t >= 1 && Math.Abs(angle.X - _x) > (shuffles ? settings.ShuffleStartMetres : 0))
                {
                    _from = _x;
                    _t = 0;
                }
                if (_t < 1)
                {
                    _t = Math.Min(1, _t + dt / Math.Max(0.01, settings.ShuffleSeconds));
                    double e = _t * _t * (3 - 2 * _t);
                    _x = _from + (angle.X - _from) * e; // pushes on to where the angle has got to
                }
                if (!shuffles && _t >= 1) _following = true;
            }
            return new RinkPoint(_x, 0, angle.Z);
        }
    }
}
