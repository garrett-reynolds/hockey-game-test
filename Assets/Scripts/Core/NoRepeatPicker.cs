using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>
    /// Picks from one pool of clip names, never the one it played last (Garrett: "you never get the same celebration or
    /// upset twice in a row"). Next only chooses; Remember records the clip that really played, so a pick that never showed
    /// (no room for a clip, a missing clip) does not move the memory. A pool of one has nothing else and repeats it. Add a
    /// clip to a pool by adding its name to the pool's array.
    /// </summary>
    public sealed class NoRepeatPicker
    {
        readonly string[] _pool;
        readonly IRandomSource _random;

        /// <summary>The clip that played last from this pool; null before the first.</summary>
        public string Last { get; private set; }

        public NoRepeatPicker(IReadOnlyList<string> pool, IRandomSource random)
        {
            _pool = new string[pool.Count];
            for (int i = 0; i < pool.Count; i++) _pool[i] = pool[i];
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>The next clip: the preferred one when it is in the pool and was not the last; else one of the others at
        /// random. Null for an empty pool.</summary>
        public string Next(string preferred = null)
        {
            if (_pool.Length == 0) return null;
            if (_pool.Length == 1) return _pool[0];
            if (preferred != null && preferred != Last && Array.IndexOf(_pool, preferred) >= 0) return preferred;
            int others = 0;
            foreach (string clip in _pool) if (clip != Last) others++;
            int k = Math.Min(others - 1, (int)(_random.Next() * others));
            foreach (string clip in _pool)
                if (clip != Last && k-- == 0) return clip;
            return _pool[0];
        }

        /// <summary>Records the clip that played.</summary>
        public void Remember(string clip) { Last = clip; }
    }
}
