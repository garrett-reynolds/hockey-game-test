using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// The per-Round shuffles (pure; they roll only on the given source, in the order Round publishes): the Set-up deck, one
    /// Set-up per Shot, and the Attack Side pick, one side per Shot.
    /// </summary>
    public static class RoundDecks
    {
        /// <summary>
        /// The Round's Set-up deck, in Shot order: 4 Set-up Mistakes (Cheat, Depth and Late Settle once each, and a 4th of a random
        /// type) and 2 Clean Set-ups, shuffled. Rolls 6 times: one Next() for the 4th type (Cheat, Depth, Late Settle by
        /// (int)(Next() * 3)), then the 5 shuffle rolls (Shuffle) on Cheat, Depth, Late Settle, the 4th, Clean, Clean.
        /// </summary>
        public static SetUpKind[] SetUps(IRandomSource random)
        {
            var fourth = (SetUpKind)(1 + Pick(random, 3));
            var deck = new[] { SetUpKind.Cheat, SetUpKind.Depth, SetUpKind.LateSettle, fourth, SetUpKind.Clean, SetUpKind.Clean };
            Shuffle(deck, random);
            return deck;
        }

        /// <summary>
        /// The Round's Attack Sides, in Shot order: left, middle and right twice each, shuffled. Rolls 5 times (Shuffle) on
        /// Left, Left, Middle, Middle, Right, Right.
        /// </summary>
        public static AttackSide[] AttackSides(IRandomSource random)
        {
            var sides = new[] { AttackSide.Left, AttackSide.Left, AttackSide.Middle, AttackSide.Middle, AttackSide.Right, AttackSide.Right };
            Shuffle(sides, random);
            return sides;
        }

        /// <summary>For card i = 0 to n - 2 in turn: one Next(), and card i swaps with card i + (int)(Next() * (n - i)).</summary>
        private static void Shuffle<T>(T[] cards, IRandomSource random)
        {
            for (int i = 0; i < cards.Length - 1; i++)
            {
                int j = i + Pick(random, cards.Length - i);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        /// <summary>(int)(Next() * count), kept in 0 to count - 1.</summary>
        private static int Pick(IRandomSource random, int count) => Math.Max(0, Math.Min((int)(random.Next() * count), count - 1));
    }
}
