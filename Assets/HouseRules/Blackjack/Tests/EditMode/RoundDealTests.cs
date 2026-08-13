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
    }
}
