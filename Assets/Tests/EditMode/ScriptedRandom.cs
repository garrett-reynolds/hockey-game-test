using System;
using System.Collections.Generic;
using HockeyGame.Core;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Test random source: plays the queued values first, then cycles through a fallback list forever.
    /// The helpers write values in Round's published roll order (see Round): per Round the Set-up deck (its 4th mistake type,
    /// then 5 shuffle rolls) and the Attack Side shuffle (5 rolls); per Shot the post side, then the Reaction Time wobble.
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

        /// <summary>A source that always rolls 0.5 (no wobble; whatever Set-ups and sides that deals).</summary>
        public static ScriptedRandom Halves() => new ScriptedRandom(new[] { 0.5 });

        /// <summary>The post-side roll that picks this post (Next() &lt; 0.5 is the left post).</summary>
        public static double PostValue(Post post) => post == Post.Left ? 0.25 : 0.75;

        /// <summary>
        /// The 6 rolls that deal a Set-up deck starting with these kinds (the rest in a legal order): the 4th mistake type,
        /// then the 5 shuffle rolls. Throws if no legal deck (4 mistakes, each type at least once, 2 Clean) starts that way.
        /// </summary>
        public static double[] DeckValues(params SetUpKind[] first)
        {
            var mistakes = new List<SetUpKind> { SetUpKind.Cheat, SetUpKind.Depth, SetUpKind.LateSettle };
            SetUpKind fourth = SetUpKind.Cheat;
            foreach (SetUpKind kind in first)
            {
                if (kind == SetUpKind.Clean) continue;
                if (!mistakes.Remove(kind)) { fourth = kind; break; }
            }
            var values = new List<double> { ((int)fourth - 1 + 0.5) / 3.0 };
            var cards = new List<SetUpKind> { SetUpKind.Cheat, SetUpKind.Depth, SetUpKind.LateSettle, fourth, SetUpKind.Clean, SetUpKind.Clean };
            values.AddRange(ShuffleValues(cards, first));
            return values.ToArray();
        }

        /// <summary>The 5 rolls that shuffle the Attack Sides to start with these sides (the rest in a legal order, 2-2-2).</summary>
        public static double[] SideValues(params AttackSide[] first) =>
            ShuffleValues(new List<AttackSide> { AttackSide.Left, AttackSide.Left, AttackSide.Middle, AttackSide.Middle, AttackSide.Right, AttackSide.Right }, first);

        /// <summary>Inverts Round's shuffle (card i swaps with card i + (int)(Next() * (6 - i)), i = 0..4) to put `first` first.</summary>
        private static double[] ShuffleValues<T>(List<T> cards, T[] first)
        {
            var values = new List<double>();
            for (int i = 0; i < cards.Count - 1; i++)
            {
                int j = i;
                if (i < first.Length)
                {
                    j = cards.FindIndex(i, c => EqualityComparer<T>.Default.Equals(c, first[i]));
                    if (j < 0) throw new ArgumentException($"No legal deal starts {string.Join(", ", first)}.");
                }
                values.Add((j - i + 0.5) / (cards.Count - i));
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
            if (first.Length == cards.Count && !EqualityComparer<T>.Default.Equals(cards[cards.Count - 1], first[cards.Count - 1]))
                throw new ArgumentException($"No legal deal is {string.Join(", ", first)}.");
            return values.ToArray();
        }

        /// <summary>
        /// A whole scripted Round: the Shots start with these Attack Sides and these Set-ups (the rest dealt in a legal order),
        /// each Shot's post side from its Set-up (left when it has none) and this wobble roll. Later Rounds roll 0.5 throughout.
        /// </summary>
        public static ScriptedRandom Dealing(AttackSide[] sides, SetUp[] setUps, double wobble = NoWobble)
        {
            var kinds = new SetUpKind[setUps.Length];
            for (int i = 0; i < setUps.Length; i++) kinds[i] = setUps[i].Kind;
            var values = new List<double>(DeckValues(kinds));
            values.AddRange(SideValues(sides));
            for (int shot = 0; shot < Round.ShotsPerRound; shot++)
            {
                Post post = shot < setUps.Length && setUps[shot].Toward.HasValue ? setUps[shot].Toward.Value : Post.Left;
                values.Add(PostValue(post));
                values.Add(wobble);
            }
            return new ScriptedRandom(new[] { 0.5 }, values.ToArray());
        }

        /// <summary>The Shots come from these Attack Sides in order; the first two are Clean Set-ups (the rest as dealt).</summary>
        public static ScriptedRandom Picking(params AttackSide[] sides) =>
            Dealing(sides, sides.Length > 1 ? new[] { SetUp.Clean, SetUp.Clean } : new[] { SetUp.Clean });

        /// <summary>The first Shot comes from this Attack Side in this Set-up (Clean unless given), with this wobble roll.</summary>
        public static ScriptedRandom Forcing(AttackSide side, SetUp? setUp = null, double wobble = NoWobble) =>
            Dealing(new[] { side }, new[] { setUp ?? SetUp.Clean }, wobble);
    }
}
