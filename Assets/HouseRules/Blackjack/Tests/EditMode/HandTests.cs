using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class HandTests
    {
        private static Hand HandOf(params Card[] cards)
        {
            var hand = new Hand(10);
            foreach (var card in cards)
            {
                hand.Add(card);
            }
            return hand;
        }

        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        [Test]
        public void HardTotal_SumsBaseValues()
        {
            var hand = HandOf(C(Rank.Nine), C(Rank.Seven));
            Assert.AreEqual(16, hand.Value.Total);
            Assert.IsFalse(hand.Value.IsSoft);
        }

        [Test]
        public void SingleAce_CountsEleven_WhenItFits()
        {
            var hand = HandOf(C(Rank.Ace), C(Rank.Six));
            Assert.AreEqual(17, hand.Value.Total);
            Assert.IsTrue(hand.Value.IsSoft);
        }

        [Test]
        public void Ace_Demotes_WhenElevenWouldBust()
        {
            var hand = HandOf(C(Rank.Ace), C(Rank.Six), C(Rank.Nine));
            Assert.AreEqual(16, hand.Value.Total);
            Assert.IsFalse(hand.Value.IsSoft);
        }

        [Test]
        public void TwoAcesAndNine_Is21_Not31()
        {
            var hand = HandOf(C(Rank.Ace), C(Rank.Ace), C(Rank.Nine));
            Assert.AreEqual(21, hand.Value.Total);
            Assert.IsTrue(hand.Value.IsSoft);
        }

        [Test]
        public void FourAces_Is14()
        {
            var hand = HandOf(C(Rank.Ace), C(Rank.Ace), C(Rank.Ace), C(Rank.Ace));
            Assert.AreEqual(14, hand.Value.Total);
        }

        [Test]
        public void Bust_WhenOver21()
        {
            var hand = HandOf(C(Rank.King), C(Rank.Queen), C(Rank.Five));
            Assert.IsTrue(hand.IsBust);
        }

        [Test]
        public void Blackjack_IsNatural21_OnTwoCards()
        {
            var hand = HandOf(C(Rank.Ace), C(Rank.King));
            Assert.IsTrue(hand.IsBlackjack);
        }

        [Test]
        public void Blackjack_IsFalse_ForThreeCard21()
        {
            var hand = HandOf(C(Rank.Seven), C(Rank.Seven), C(Rank.Seven));
            Assert.AreEqual(21, hand.Value.Total);
            Assert.IsFalse(hand.IsBlackjack);
        }

        [Test]
        public void Blackjack_IsFalse_ForSplitHand()
        {
            var hand = new Hand(10, isFromSplit: true);
            hand.Add(C(Rank.Ace));
            hand.Add(C(Rank.King));
            Assert.AreEqual(21, hand.Value.Total);
            Assert.IsFalse(hand.IsBlackjack);
        }

        [Test]
        public void IsPair_ComparesRank_NotSuit()
        {
            var hand = new Hand(10);
            hand.Add(new Card(Rank.Eight, Suit.Clubs));
            hand.Add(new Card(Rank.Eight, Suit.Hearts));
            Assert.IsTrue(hand.IsPair);
        }

        [Test]
        public void IsPair_IsFalse_ForTenAndKing()
        {
            var hand = HandOf(C(Rank.Ten), C(Rank.King));
            Assert.IsFalse(hand.IsPair);
        }
    }
}
