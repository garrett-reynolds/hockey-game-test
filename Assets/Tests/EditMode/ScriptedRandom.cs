using System.Collections.Generic;
using HockeyGame.Core;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Test random source: plays the queued values first, then cycles through a fallback list forever,
    /// so a Round can never loop on a constant source.
    /// </summary>
    internal sealed class ScriptedRandom : IRandomSource
    {
        private readonly Queue<double> _queue = new Queue<double>();
        private readonly double[] _fallback;
        private int _fallbackIndex;

        public ScriptedRandom(double[] fallback, params double[] queued)
        {
            _fallback = fallback;
            foreach (var value in queued)
                _queue.Enqueue(value);
        }

        public double Next()
        {
            if (_queue.Count > 0)
                return _queue.Dequeue();
            double value = _fallback[_fallbackIndex];
            _fallbackIndex = (_fallbackIndex + 1) % _fallback.Length;
            return value;
        }

        /// <summary>The Reaction Time wobble roll that leaves the Reaction Time at RoundSettings.ReactionSeconds.</summary>
        public const double NoWobble = 0.5;

        /// <summary>The two values that make Round pick this pose (side = (int)(v*3), index = (int)(v*2)).</summary>
        public static double[] PickValues(GoaliePose pose)
        {
            int side = (int)pose / 2;
            int index = (int)pose % 2;
            return new[] { (side + 0.5) / 3.0, (index + 0.5) / 2.0 };
        }

        /// <summary>The values one Shot start takes, in Round's roll order: the pose pick, then the Reaction Time wobble.</summary>
        public static double[] ShotValues(GoaliePose pose, double wobble = NoWobble)
        {
            var values = new List<double>(PickValues(pose)) { wobble };
            return values.ToArray();
        }

        /// <summary>Alternates LeftButterfly and RightButterfly Shots (no wobble), so the pose never repeats.</summary>
        public static double[] AlternatingButterflies()
        {
            var values = new List<double>(ShotValues(GoaliePose.LeftButterfly));
            values.AddRange(ShotValues(GoaliePose.RightButterfly));
            return values.ToArray();
        }

        /// <summary>Queue: the Shot start values (pose, no wobble) for each given pose in order. Then the alternating fallback.</summary>
        public static ScriptedRandom Picking(params GoaliePose[] poses)
        {
            var values = new List<double>();
            foreach (var pose in poses)
                values.AddRange(ShotValues(pose));
            return new ScriptedRandom(AlternatingButterflies(), values.ToArray());
        }

        /// <summary>Forces the first Shot's pose (no wobble), then the alternating fallback.</summary>
        public static ScriptedRandom Forcing(GoaliePose pose) => Picking(pose);
    }
}
