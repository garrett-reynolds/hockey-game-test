using System.Collections.Generic;
using System.Linq;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The per-Round Set-up deck and Attack Side pick (RoundDecks), on scripted and seeded sources.</summary>
    public class RoundDecksTests
    {
        static readonly SetUpKind[] Mistakes = { SetUpKind.Cheat, SetUpKind.Depth, SetUpKind.LateSettle };

        /// <summary>Counts how many rolls were taken, playing the given values then 0.</summary>
        sealed class CountingRandom : IRandomSource
        {
            readonly Queue<double> _values;
            public int Rolls;
            public CountingRandom(params double[] values) { _values = new Queue<double>(values); }
            public double Next() { Rolls++; return _values.Count > 0 ? _values.Dequeue() : 0; }
        }

        static void AssertLegalDeck(SetUpKind[] deck, string because)
        {
            Assert.AreEqual(Round.ShotsPerRound, deck.Length, because);
            Assert.AreEqual(2, deck.Count(k => k == SetUpKind.Clean), because + ": 2 Clean Set-ups");
            Assert.AreEqual(4, deck.Count(k => k != SetUpKind.Clean), because + ": 4 Set-up Mistakes");
            foreach (SetUpKind mistake in Mistakes)
                Assert.That(deck, Has.Member(mistake), because + ": each mistake type at least once");
        }

        [TestCase(0.0, SetUpKind.Cheat)]
        [TestCase(0.34, SetUpKind.Depth)]
        [TestCase(0.99, SetUpKind.LateSettle)]
        public void The_deck_has_4_mistakes_each_type_once_plus_a_4th_from_the_first_roll_and_2_Clean(double roll, SetUpKind fourth)
        {
            SetUpKind[] deck = RoundDecks.SetUps(new CountingRandom(roll, 0.7, 0.2, 0.9, 0.4, 0.6));
            AssertLegalDeck(deck, "4th roll " + roll);
            Assert.AreEqual(2, deck.Count(k => k == fourth), "the 4th mistake is a second " + fourth);
        }

        [Test]
        public void The_deck_takes_one_roll_for_the_4th_mistake_and_5_for_the_shuffle()
        {
            var random = new CountingRandom();
            RoundDecks.SetUps(random);
            Assert.AreEqual(6, random.Rolls);
        }

        [Test]
        public void The_shuffle_follows_the_rolls_in_the_published_order()
        {
            // Unshuffled: Cheat, Depth, Late Settle, the 4th, Clean, Clean. Card i swaps with card i + (int)(roll * (6 - i)).
            CollectionAssert.AreEqual(
                new[] { SetUpKind.Cheat, SetUpKind.Depth, SetUpKind.LateSettle, SetUpKind.Depth, SetUpKind.Clean, SetUpKind.Clean },
                RoundDecks.SetUps(new CountingRandom(0.5, 0, 0, 0, 0, 0)));
            CollectionAssert.AreEqual(
                new[] { SetUpKind.Clean, SetUpKind.Depth, SetUpKind.LateSettle, SetUpKind.Clean, SetUpKind.Cheat, SetUpKind.Cheat },
                RoundDecks.SetUps(new CountingRandom(0.0, 0.9, 0, 0, 0.5, 0)));
        }

        [Test]
        public void Seeded_decks_are_always_legal_and_any_kind_can_come_first()
        {
            var first = new HashSet<SetUpKind>();
            for (int seed = 1; seed <= 200; seed++)
            {
                SetUpKind[] deck = RoundDecks.SetUps(new SeededRandom(seed));
                AssertLegalDeck(deck, "seed " + seed);
                first.Add(deck[0]);
            }
            Assert.AreEqual(4, first.Count, "Clean and every mistake type come up first");
        }

        [Test]
        public void The_Attack_Sides_come_up_left_middle_and_right_twice_each_from_5_rolls()
        {
            var random = new CountingRandom(0.9, 0, 0, 0, 0);
            CollectionAssert.AreEqual(
                new[] { AttackSide.Right, AttackSide.Left, AttackSide.Middle, AttackSide.Middle, AttackSide.Right, AttackSide.Left },
                RoundDecks.AttackSides(random));
            Assert.AreEqual(5, random.Rolls);

            var first = new HashSet<AttackSide>();
            for (int seed = 1; seed <= 200; seed++)
            {
                AttackSide[] sides = RoundDecks.AttackSides(new SeededRandom(seed));
                Assert.AreEqual(Round.ShotsPerRound, sides.Length);
                foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
                    Assert.AreEqual(2, sides.Count(s => s == side), "seed " + seed + ": " + side + " twice");
                first.Add(sides[0]);
            }
            Assert.AreEqual(3, first.Count, "any side can come first");
        }
    }
}
