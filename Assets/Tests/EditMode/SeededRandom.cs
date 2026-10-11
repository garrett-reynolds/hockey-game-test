using System;

namespace HockeyGame.Core.Tests
{
    /// <summary>Test random source from a seeded System.Random: many different but repeatable Rounds.</summary>
    internal sealed class SeededRandom : IRandomSource
    {
        private readonly Random _random;
        public SeededRandom(int seed) { _random = new Random(seed); }
        public double Next() => _random.NextDouble();
    }
}
