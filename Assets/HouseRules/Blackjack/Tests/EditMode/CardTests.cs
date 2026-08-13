using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class CardTests
    {
        [Test]
        public void NumberCard_BaseValue_IsItsRank()
        {
            var card = new Card(Rank.Seven, Suit.Hearts);
            Assert.AreEqual(7, card.BaseValue);
        }

        [Test]
        public void FaceCards_BaseValue_IsTen()
        {
            Assert.AreEqual(10, new Card(Rank.Jack, Suit.Clubs).BaseValue);
            Assert.AreEqual(10, new Card(Rank.Queen, Suit.Clubs).BaseValue);
            Assert.AreEqual(10, new Card(Rank.King, Suit.Clubs).BaseValue);
        }

        [Test]
        public void Ace_BaseValue_IsEleven()
        {
            Assert.AreEqual(11, new Card(Rank.Ace, Suit.Spades).BaseValue);
        }

        [Test]
        public void Cards_WithSameRankAndSuit_AreEqual()
        {
            Assert.AreEqual(new Card(Rank.Nine, Suit.Diamonds), new Card(Rank.Nine, Suit.Diamonds));
            Assert.AreNotEqual(new Card(Rank.Nine, Suit.Diamonds), new Card(Rank.Nine, Suit.Clubs));
        }
    }
}
