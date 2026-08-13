using System;
using System.Linq;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class AbandonTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        [Test]
        public void Abandon_BeforeDealing_RefundsEveryBet()
        {
            var wallet = new Wallet(1000);
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), wallet);

            round.PlaceBet(0, 10);
            round.PlaceBet(1, 20);
            Assert.AreEqual(970, wallet.Balance);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
            Assert.AreEqual(RoundState.Complete, round.State);
        }

        [Test]
        public void Abandon_MidPlayerTurn_RefundsTheWager()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Six), C(Rank.Seven), C(Rank.Four)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            Assert.AreEqual(RoundState.PlayerTurn, round.State);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Abandon_AfterDoubling_RefundsTheDoubledWager()
        {
            var wallet = new Wallet(1000);
            // A second box is bet purely to keep the round in PlayerTurn after box 0
            // doubles: with only one active box, closing its sole hand is the last
            // playable hand, so Round.Apply(Double) would cascade straight through
            // the dealer's turn and settlement in the same call, leaving nothing
            // mid-round to abandon. Box 0's cards are unchanged from the original
            // scenario (Six, Five -> double draws Two); box 1's cards are filler.
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(
                    C(Rank.Six), C(Rank.Two), C(Rank.Six), C(Rank.Five),
                    C(Rank.Three), C(Rank.Four), C(Rank.Two)),
                wallet);

            round.PlaceBet(0, 10);
            round.PlaceBet(1, 2);
            round.Deal();
            round.Apply(PlayerAction.Double);
            Assert.AreEqual(RoundState.PlayerTurn, round.State);

            // 1000 - 10 (box 0 bet) - 2 (box 1 bet) - 10 (double) = 978.
            // Box 0's hand wager is now 20; box 1's hand wager is still 2.
            Assert.AreEqual(978, wallet.Balance);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Abandon_AfterSplitting_RefundsBothHands()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(
                    C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                    C(Rank.Three), C(Rank.Two)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Split);
            Assert.AreEqual(980, wallet.Balance);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Abandon_AfterTakingInsurance_RefundsThePremiumToo()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Ace), C(Rank.Seven), C(Rank.Four)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.TakeInsurance);
            Assert.AreEqual(985, wallet.Balance);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Abandon_EmitsRoundAbandonedWithTheRefundedTotal()
        {
            var wallet = new Wallet(1000);
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), wallet);

            round.PlaceBet(0, 10);
            round.PlaceBet(1, 20);
            round.DrainEvents();

            round.Abandon();

            var abandoned = round.DrainEvents().OfType<RoundAbandoned>().Single();
            Assert.AreEqual(30, abandoned.Refunded);
        }

        [Test]
        public void Abandon_ProducesNoSettlements()
        {
            var wallet = new Wallet(1000);
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), wallet);

            round.PlaceBet(0, 10);
            round.Abandon();

            Assert.IsEmpty(round.Settlements);
            Assert.AreEqual(0, round.TotalDelta);
        }

        [Test]
        public void Abandon_OnACompleteRound_Throws()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Seven)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Stand);
            Assert.AreEqual(RoundState.Complete, round.State);

            Assert.Throws<InvalidOperationException>(() => round.Abandon());
        }

        [Test]
        public void Abandon_Twice_Throws()
        {
            var wallet = new Wallet(1000);
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), wallet);

            round.PlaceBet(0, 10);
            round.Abandon();

            Assert.Throws<InvalidOperationException>(() => round.Abandon());
        }

        [Test]
        public void Abandon_WithNoBets_IsAllowedAndRefundsNothing()
        {
            var wallet = new Wallet(1000);
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), wallet);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
            Assert.AreEqual(RoundState.Complete, round.State);
        }
    }
}
