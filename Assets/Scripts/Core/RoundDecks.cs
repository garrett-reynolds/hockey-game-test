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
        public static SetUpKind[] SetUps(IRandomSource random) => SetUps(random, Round.ShotsPerRound);

        /// <summary>
        /// The Set-up deck for a Round of `shots` Shots (#146): shots / 3 Clean Set-ups (2 of 6; 1 of 3; 3 of 10) and the rest Set-up
        /// Mistakes, taken in turn from Cheat, Depth, Late Settle, the 4th type, Cheat ... (so 3 Shots hold Cheat and Depth only).
        /// Rolls exactly as the 6-Shot deck does: one Next() for the 4th type, then the shuffle.
        /// </summary>
        public static SetUpKind[] SetUps(IRandomSource random, int shots)
        {
            var fourth = (SetUpKind)(1 + Pick(random, 3));
            var types = new[] { SetUpKind.Cheat, SetUpKind.Depth, SetUpKind.LateSettle, fourth };
            int clean = shots / 3;
            var deck = new SetUpKind[shots];
            for (int i = 0; i < shots; i++)
                deck[i] = i < shots - clean ? types[i % types.Length] : SetUpKind.Clean;
            Shuffle(deck, random);
            return deck;
        }

        /// <summary>
        /// The Round's Attack Sides, in Shot order: left, middle and right twice each, shuffled. Rolls 5 times (Shuffle) on
        /// Left, Left, Middle, Middle, Right, Right.
        /// </summary>
        public static AttackSide[] AttackSides(IRandomSource random) => AttackSides(random, Round.ShotsPerRound);

        /// <summary>
        /// The Attack Sides for a Round of `shots` Shots (#146): Left, Middle and Right in equal runs (shots 3 and 6 an equal share;
        /// 10 gives 4, 3, 3), shuffled. Rolls shots - 1 times (Shuffle).
        /// </summary>
        public static AttackSide[] AttackSides(IRandomSource random, int shots)
        {
            var order = new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right };
            var sides = new AttackSide[shots];
            for (int i = 0; i < shots; i++)
                sides[i] = order[i * order.Length / shots];
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
