using System;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class RoundDealTests
    {
        private static Round NewRound(Wallet wallet = null, IShoe shoe = null)
        {
            return new Round(
                BlackjackRules.Standard,
                shoe ?? new StackedShoe(),
                wallet ?? new Wallet(1000));
        }

        [Test]
        public void NewRound_StartsInBettingState()
        {
            Assert.AreEqual(RoundState.Betting, NewRound().State);
        }

        [Test]
        public void NewRound_HasMaxBoxes_AllInactive()
        {
            var round = NewRound();
            Assert.AreEqual(3, round.Boxes.Count);
            foreach (var box in round.Boxes)
            {
                Assert.IsFalse(box.IsActive);
            }
        }

        [Test]
        public void PlaceBet_ActivatesBox_AndDebitsWallet()
        {
            var wallet = new Wallet(1000);
            var round = NewRound(wallet);

            round.PlaceBet(0, 10);

            Assert.IsTrue(round.Boxes[0].IsActive);
            Assert.AreEqual(10, round.Boxes[0].InitialBet);
            Assert.AreEqual(10, round.Boxes[0].Hands[0].Wager);
            Assert.AreEqual(990, wallet.Balance);
        }

        [Test]
        public void PlaceBet_OddWager_Throws()
        {
            var round = NewRound();
            Assert.Throws<ArgumentException>(() => round.PlaceBet(0, 5));
        }

        [Test]
        public void PlaceBet_BelowMinimum_Throws()
        {
            var round = NewRound();
            Assert.Throws<ArgumentException>(() => round.PlaceBet(0, 0));
        }

        [Test]
        public void PlaceBet_BeyondBalance_Throws()
        {
            var round = NewRound(new Wallet(8));
            Assert.Throws<InvalidOperationException>(() => round.PlaceBet(0, 10));
        }

        [Test]
        public void PlaceBet_OutOfRangeBox_Throws()
        {
            var round = NewRound();
            Assert.Throws<ArgumentOutOfRangeException>(() => round.PlaceBet(3, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.PlaceBet(-1, 10));
        }

        [Test]
        public void PlaceBet_Twice_OnSameBox_Throws()
        {
            var round = NewRound();
            round.PlaceBet(0, 10);
            Assert.Throws<InvalidOperationException>(() => round.PlaceBet(0, 10));
        }

        [Test]
        public void PlaceBet_OnMultipleBoxes_IsAllowed()
        {
            var wallet = new Wallet(1000);
            var round = NewRound(wallet);

            round.PlaceBet(0, 10);
            round.PlaceBet(2, 20);

            Assert.IsTrue(round.Boxes[0].IsActive);
            Assert.IsFalse(round.Boxes[1].IsActive);
            Assert.IsTrue(round.Boxes[2].IsActive);
            Assert.AreEqual(970, wallet.Balance);
        }

        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        [Test]
        public void Deal_GivesTwoCardsToEachActiveBoxAndDealer()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.Nine),   // box0 first, dealer upcard
                C(Rank.Seven), C(Rank.Four), // box0 second, dealer hole
                C(Rank.Two)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.AreEqual(2, round.Boxes[0].Hands[0].Cards.Count);
            Assert.AreEqual(2, round.DealerHand.Cards.Count);
        }

        [Test]
        public void Deal_UsesCasinoOrder_BoxThenDealerThenBoxThenHole()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten),    // box0 card 1
                C(Rank.Nine),   // dealer upcard
                C(Rank.Seven),  // box0 card 2
                C(Rank.Four))); // dealer hole

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.AreEqual(C(Rank.Ten), round.Boxes[0].Hands[0].Cards[0]);
            Assert.AreEqual(C(Rank.Seven), round.Boxes[0].Hands[0].Cards[1]);
            Assert.AreEqual(C(Rank.Nine), round.DealerHand.Cards[0]);
            Assert.AreEqual(C(Rank.Four), round.DealerHand.Cards[1]);
        }

        [Test]
        public void Deal_SkipsInactiveBoxes()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten),    // box0 card 1
                C(Rank.Six),    // box2 card 1
                C(Rank.Nine),   // dealer upcard
                C(Rank.Seven),  // box0 card 2
                C(Rank.Three),  // box2 card 2
                C(Rank.Four))); // dealer hole

            round.PlaceBet(0, 10);
            round.PlaceBet(2, 10);
            round.Deal();

            Assert.AreEqual(2, round.Boxes[0].Hands[0].Cards.Count);
            Assert.AreEqual(0, round.Boxes[1].Hands.Count);
            Assert.AreEqual(2, round.Boxes[2].Hands[0].Cards.Count);
        }

        [Test]
        public void Deal_WithNoBets_Throws()
        {
            var round = NewRound();
            Assert.Throws<InvalidOperationException>(() => round.Deal());
        }

        [Test]
        public void Deal_EntersPlayerTurn_OnOrdinaryUpcard()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.Nine), C(Rank.Seven), C(Rank.Four)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.AreEqual(RoundState.PlayerTurn, round.State);
            Assert.AreEqual(0, round.CurrentBoxIndex);
            Assert.AreEqual(0, round.CurrentHandIndex);
        }

        [Test]
        public void Deal_EntersInsurance_OnAceUpcard()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.Ace), C(Rank.Seven), C(Rank.Four)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.AreEqual(RoundState.Insurance, round.State);
        }

        [Test]
        public void Deal_GoesStraightToSettlement_WhenDealerPeeksBlackjackOnTen()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.King), C(Rank.Seven), C(Rank.Ace)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.IsTrue(round.DealerHasBlackjack);
            Assert.AreEqual(RoundState.Complete, round.State);
        }

        [Test]
        public void Deal_DoesNotPeek_OnLowUpcard()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.Six), C(Rank.Seven), C(Rank.Four)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.AreEqual(RoundState.PlayerTurn, round.State);
        }

        [Test]
        public void Deal_Twice_Throws()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.Nine), C(Rank.Seven), C(Rank.Four)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.Throws<InvalidOperationException>(() => round.Deal());
        }
    }
}
