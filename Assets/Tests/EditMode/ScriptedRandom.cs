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

        /// <summary>The two values that make Round pick this pose (side = (int)(v*3), index = (int)(v*2)).</summary>
        public static double[] PickValues(GoaliePose pose)
        {
            int side = (int)pose / 2;
            int index = (int)pose % 2;
            return new[] { (side + 0.5) / 3.0, (index + 0.5) / 2.0 };
        }

        /// <summary>Alternates LeftButterfly and RightButterfly: a Shot at (50, 90) is Open in both.</summary>
        public static double[] AlternatingButterflies()
        {
            var left = PickValues(GoaliePose.LeftButterfly);
            var right = PickValues(GoaliePose.RightButterfly);
            return new[] { left[0], left[1], right[0], right[1] };
        }

        /// <summary>Queue: the pick values (side, then pose) for each given pose in order. Then the alternating fallback.</summary>
        public static ScriptedRandom Picking(params GoaliePose[] poses)
        {
            var values = new List<double>();
            foreach (var pose in poses)
                values.AddRange(PickValues(pose));
            return new ScriptedRandom(AlternatingButterflies(), values.ToArray());
        }

        /// <summary>Forces the first pose, then queues the rolls, then the alternating fallback.</summary>
        public static ScriptedRandom Forcing(GoaliePose pose, params double[] rolls)
        {
            var values = new List<double>(PickValues(pose));
            values.AddRange(rolls);
            return new ScriptedRandom(AlternatingButterflies(), values.ToArray());
        }

        /// <summary>Forces the first pose, then the rolls, then the second pose's pick values, then the alternating fallback.</summary>
        public static ScriptedRandom ForcingThen(GoaliePose first, double[] rolls, GoaliePose second)
        {
            var values = new List<double>(PickValues(first));
            values.AddRange(rolls);
            values.AddRange(PickValues(second));
            return new ScriptedRandom(AlternatingButterflies(), values.ToArray());
        }
    }
}
