using System.Linq;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>One real brand per Round (#223): the deck that picks it, the taglines, and the Round start that switches it.</summary>
    public class BrandRoundTests
    {
        Brands.Brand _before;

        [SetUp]
        public void Keep() => _before = Brands.Current;

        [TearDown]
        public void PutBack() => Brands.Use(_before);

        [Test]
        public void The_deck_gives_every_brand_once_before_any_repeats()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var deck = new BrandDeck(Brands.All, new System.Random(seed));
                Brands.Brand[] first = Enumerable.Range(0, Brands.All.Length).Select(_ => deck.Next()).ToArray();
                CollectionAssert.AreEquivalent(Brands.All, first, "seed " + seed + ": the whole pool, no repeats");
                Brands.Brand[] second = Enumerable.Range(0, Brands.All.Length).Select(_ => deck.Next()).ToArray();
                CollectionAssert.AreEquivalent(Brands.All, second, "seed " + seed + ": then the whole pool again");
                Assert.AreNotSame(first[first.Length - 1], second[0], "seed " + seed + ": no brand twice in a row across the reshuffle");
            }
        }

        [Test]
        public void Two_seeds_give_different_orders_and_one_seed_the_same_order()
        {
            string[] Order(int seed)
            {
                var deck = new BrandDeck(Brands.All, new System.Random(seed));
                return Enumerable.Range(0, Brands.All.Length).Select(_ => deck.Next().Id).ToArray();
            }
            CollectionAssert.AreNotEqual(Order(1), Order(2));
            CollectionAssert.AreEqual(Order(1), Order(1));
        }

        [Test]
        public void Each_brand_has_its_approved_tagline_split_into_black_words_and_script_words()
        {
            Brands.Brand Of(string id) => Brands.All.Single(b => b.Id == id);
            Assert.AreEqual("COMFORTABLE IN THE CREASE", Of("td").Tagline);
            Assert.AreEqual("THE FUTURE IS FRIENDLY. THE FORECHECK ISN'T.", Of("telus").Tagline);
            Assert.AreEqual("15 MINUTES COULD SAVE YOU A SHUTOUT", Of("geico").Tagline);
            Assert.AreEqual("I'M LOVIN' THIS POWER PLAY", Of("mcdonalds").Tagline);
            Assert.AreEqual("WE'LL PICK YOU UP AFTER THE GAME", Of("enterprise").Tagline);
            Assert.AreEqual(("ALWAYS FRESH.", "ALWAYS HOCKEY."), Brands.SplitTagline(Of("tim-hortons").Tagline));
            Assert.AreEqual(("YOUR AMBITION.", "OUR ASSIST."), Brands.SplitTagline(Of("cibc").Tagline));
            Assert.AreEqual(("ALL IN", "ON HOCKEY"), Brands.SplitTagline(Of("sport-chek").Tagline));
            foreach (Brands.Brand b in Brands.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(b.Tagline), b.Name);
                (string first, string last) = Brands.SplitTagline(b.Tagline);
                Assert.IsNotEmpty(first, b.Name);
                Assert.IsNotEmpty(last, b.Name);
                Assert.AreEqual(b.Tagline, first + " " + last, b.Name);
                Assert.IsTrue(ReservedColours.IsSafe(b.Colour), b.Name + " colour");
            }
            Assert.AreEqual(44, Brands.All.Select(b => b.Tagline).Distinct().Count(), "44 different lines");
        }

        [Test]
        public void Starting_a_Round_switches_to_the_next_brand_of_the_deck()
        {
            Brands.NewLoad(new System.Random(3));
            var expected = new BrandDeck(Brands.All, new System.Random(3));
            Assert.AreSame(expected.Next(), Brands.Current, "the load's first brand (the start screen)");
            var flow = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches());
            flow.Press();
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            Assert.AreSame(expected.Next(), Brands.Current, "the Round's brand");
        }

        [Test]
        public void A_new_load_shuffles_again()
        {
            Brands.NewLoad(new System.Random(1));
            string[] one = Enumerable.Range(0, 10).Select(_ => { Brands.NextRound(); return Brands.Current.Id; }).ToArray();
            Brands.NewLoad(new System.Random(2));
            string[] two = Enumerable.Range(0, 10).Select(_ => { Brands.NextRound(); return Brands.Current.Id; }).ToArray();
            CollectionAssert.AreNotEqual(one, two);
        }
    }
}
