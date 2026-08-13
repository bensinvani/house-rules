using System.Collections.Generic;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class ShoeTests
    {
        [Test]
        public void SixDeckShoe_Holds312Cards()
        {
            var shoe = new Shoe(6, 0.75, new SeededRandom(1));
            Assert.AreEqual(312, shoe.Remaining);
        }

        [Test]
        public void Shoe_DealsEveryCardExactlyOnce()
        {
            var shoe = new Shoe(6, 1.0, new SeededRandom(42));
            var counts = new Dictionary<Card, int>();

            while (shoe.Remaining > 0)
            {
                var card = shoe.Deal();
                counts.TryGetValue(card, out int seen);
                counts[card] = seen + 1;
            }

            Assert.AreEqual(52, counts.Count, "Expected all 52 distinct cards.");
            foreach (var pair in counts)
            {
                Assert.AreEqual(6, pair.Value, $"{pair.Key} should appear exactly 6 times in a 6-deck shoe.");
            }
        }

        [Test]
        public void SameSeed_ProducesSameOrder()
        {
            var a = new Shoe(6, 1.0, new SeededRandom(7));
            var b = new Shoe(6, 1.0, new SeededRandom(7));

            for (int i = 0; i < 312; i++)
            {
                Assert.AreEqual(a.Deal(), b.Deal(), $"Divergence at card {i}.");
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentOrder()
        {
            var a = new Shoe(6, 1.0, new SeededRandom(1));
            var b = new Shoe(6, 1.0, new SeededRandom(2));

            bool anyDifference = false;
            for (int i = 0; i < 312; i++)
            {
                if (!a.Deal().Equals(b.Deal()))
                {
                    anyDifference = true;
                }
            }

            Assert.IsTrue(anyDifference);
        }

        [Test]
        public void NeedsReshuffle_TripsAtPenetration()
        {
            var shoe = new Shoe(6, 0.75, new SeededRandom(1));

            // 75% of 312 is 234.
            for (int i = 0; i < 233; i++)
            {
                shoe.Deal();
                Assert.IsFalse(shoe.NeedsReshuffle, $"Tripped early at card {i + 1}.");
            }

            shoe.Deal();
            Assert.IsTrue(shoe.NeedsReshuffle);
        }

        [Test]
        public void Reshuffle_RestoresFullShoe()
        {
            var shoe = new Shoe(6, 0.75, new SeededRandom(1));
            for (int i = 0; i < 240; i++)
            {
                shoe.Deal();
            }

            shoe.Reshuffle();

            Assert.AreEqual(312, shoe.Remaining);
            Assert.IsFalse(shoe.NeedsReshuffle);
        }

        [Test]
        public void StackedShoe_DealsScriptedOrder()
        {
            var shoe = new StackedShoe(
                new Card(Rank.Eight, Suit.Clubs),
                new Card(Rank.Six, Suit.Hearts));

            Assert.AreEqual(new Card(Rank.Eight, Suit.Clubs), shoe.Deal());
            Assert.AreEqual(new Card(Rank.Six, Suit.Hearts), shoe.Deal());
        }
    }
}
