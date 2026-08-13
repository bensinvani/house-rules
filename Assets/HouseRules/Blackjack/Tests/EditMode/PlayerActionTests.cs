using System;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class PlayerActionTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round Dealt(Wallet wallet, params Card[] script)
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(script), wallet);
            round.PlaceBet(0, 10);
            round.Deal();
            return round;
        }

        [Test]
        public void Hit_AddsACardToCurrentHand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Five), C(Rank.Six), C(Rank.Four), C(Rank.Four), C(Rank.Three));

            round.Apply(PlayerAction.Hit);

            Assert.AreEqual(3, round.Boxes[0].Hands[0].Cards.Count);
            Assert.AreEqual(C(Rank.Three), round.Boxes[0].Hands[0].Cards[2]);
        }

        [Test]
        public void Hit_ToBust_ClosesHandAndMovesOn()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Four), C(Rank.King));

            round.Apply(PlayerAction.Hit);

            Assert.IsTrue(round.Boxes[0].Hands[0].IsBust);
            Assert.IsTrue(round.Boxes[0].Hands[0].IsClosed);
            Assert.AreEqual(RoundState.DealerTurn, round.State);
        }

        [Test]
        public void Stand_ClosesHand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Four));

            round.Apply(PlayerAction.Stand);

            Assert.IsTrue(round.Boxes[0].Hands[0].IsClosed);
            Assert.AreEqual(RoundState.DealerTurn, round.State);
        }

        [Test]
        public void TurnAdvances_AcrossBoxes_LeftToRight()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(
                    C(Rank.Ten),   // box0 c1
                    C(Rank.Nine),  // box2 c1
                    C(Rank.Six),   // dealer up
                    C(Rank.Seven), // box0 c2
                    C(Rank.Eight), // box2 c2
                    C(Rank.Four)), // dealer hole
                wallet);

            round.PlaceBet(0, 10);
            round.PlaceBet(2, 10);
            round.Deal();

            Assert.AreEqual(0, round.CurrentBoxIndex);
            round.Apply(PlayerAction.Stand);
            Assert.AreEqual(2, round.CurrentBoxIndex);
            round.Apply(PlayerAction.Stand);
            Assert.AreEqual(RoundState.DealerTurn, round.State);
        }

        [Test]
        public void Apply_IllegalAction_Throws()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Four));

            Assert.Throws<InvalidOperationException>(() => round.Apply(PlayerAction.Split));
        }

        [Test]
        public void Apply_OutsidePlayerTurn_Throws()
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), new Wallet(1000));
            Assert.Throws<InvalidOperationException>(() => round.Apply(PlayerAction.Hit));
        }

        [Test]
        public void Hit_To21_DoesNotAutoStand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ten), C(Rank.Six), C(Rank.Five), C(Rank.Four), C(Rank.Six));

            round.Apply(PlayerAction.Hit);

            Assert.AreEqual(21, round.Boxes[0].Hands[0].Value.Total);
            Assert.AreEqual(RoundState.PlayerTurn, round.State);
            Assert.IsFalse(round.Boxes[0].Hands[0].IsClosed);
        }

        [Test]
        public void AfterHitting_DoubleIsNoLongerLegal()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Five), C(Rank.Six), C(Rank.Four), C(Rank.Four), C(Rank.Two));

            round.Apply(PlayerAction.Hit);

            CollectionAssert.Contains(round.LegalActions, PlayerAction.Hit);
            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.Double);
        }

        [Test]
        public void NaturalBlackjack_AutoClosesAndIsNeverOfferedActions()
        {
            // Player is dealt A,K — a natural. It stands automatically, so with no
            // other live hand the round goes straight to the dealer.
            var round = Dealt(new Wallet(1000),
                C(Rank.Ace), C(Rank.Nine), C(Rank.King), C(Rank.Seven));

            Assert.IsTrue(round.Boxes[0].Hands[0].IsBlackjack);
            Assert.IsTrue(round.Boxes[0].Hands[0].IsClosed);
            Assert.AreNotEqual(RoundState.PlayerTurn, round.State);
        }

        [Test]
        public void NaturalOnOneBox_DoesNotSkipTheOtherBox()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(
                    C(Rank.Ace),   // box0 c1
                    C(Rank.Ten),   // box1 c1
                    C(Rank.Nine),  // dealer up
                    C(Rank.King),  // box0 c2  -> natural
                    C(Rank.Six),   // box1 c2  -> 16
                    C(Rank.Seven)),// dealer hole
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.PlaceBet(1, 10);
            round.Deal();

            Assert.AreEqual(RoundState.PlayerTurn, round.State);
            Assert.AreEqual(1, round.CurrentBoxIndex, "Play should skip the natural and land on box 1.");
        }

        [Test]
        public void Double_DebitsWallet_DoublesWager_DealsExactlyOneCard_AndCloses()
        {
            var wallet = new Wallet(1000);
            var round = Dealt(wallet,
                C(Rank.Six), C(Rank.Six), C(Rank.Five), C(Rank.Four), C(Rank.Nine));

            Assert.AreEqual(990, wallet.Balance);

            round.Apply(PlayerAction.Double);

            Hand hand = round.Boxes[0].Hands[0];
            Assert.AreEqual(20, hand.Wager);
            Assert.IsTrue(hand.IsDoubled);
            Assert.AreEqual(3, hand.Cards.Count);
            Assert.IsTrue(hand.IsClosed);
            Assert.AreEqual(980, wallet.Balance);
            Assert.AreEqual(RoundState.DealerTurn, round.State);
        }

        [Test]
        public void Double_ThatBusts_StillClosesTheHand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ten), C(Rank.Six), C(Rank.Six), C(Rank.Four), C(Rank.King));

            round.Apply(PlayerAction.Double);

            Hand hand = round.Boxes[0].Hands[0];
            Assert.IsTrue(hand.IsBust);
            Assert.IsTrue(hand.IsClosed);
        }
    }
}
