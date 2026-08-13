using System;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class WalletTests
    {
        [Test]
        public void NewWallet_HasStartingBalance()
        {
            Assert.AreEqual(1000, new Wallet(1000).Balance);
        }

        [Test]
        public void Debit_ReducesBalance()
        {
            var wallet = new Wallet(1000);
            wallet.Debit(250);
            Assert.AreEqual(750, wallet.Balance);
        }

        [Test]
        public void Credit_IncreasesBalance()
        {
            var wallet = new Wallet(1000);
            wallet.Credit(250);
            Assert.AreEqual(1250, wallet.Balance);
        }

        [Test]
        public void Debit_BeyondBalance_Throws()
        {
            var wallet = new Wallet(100);
            Assert.Throws<InvalidOperationException>(() => wallet.Debit(101));
        }

        [Test]
        public void Debit_NeverLeavesNegativeBalance()
        {
            var wallet = new Wallet(100);
            try
            {
                wallet.Debit(500);
            }
            catch (InvalidOperationException)
            {
                // expected
            }

            Assert.AreEqual(100, wallet.Balance);
        }

        [Test]
        public void Debit_NonPositiveAmount_Throws()
        {
            var wallet = new Wallet(100);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Debit(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Debit(-5));
        }

        [Test]
        public void Credit_NonPositiveAmount_Throws()
        {
            var wallet = new Wallet(100);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Credit(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Credit(-5));
        }

        [Test]
        public void CanAfford_ReflectsBalance()
        {
            var wallet = new Wallet(100);
            Assert.IsTrue(wallet.CanAfford(100));
            Assert.IsFalse(wallet.CanAfford(101));
        }

        [Test]
        public void StandardRules_MatchTheSpec()
        {
            var rules = BlackjackRules.Standard;

            Assert.AreEqual(6, rules.DeckCount);
            Assert.IsFalse(rules.DealerHitsSoft17);
            Assert.IsTrue(rules.DoubleAfterSplit);
            Assert.AreEqual(3, rules.MaxSplitsPerBox);
            Assert.IsFalse(rules.ResplitAces);
            Assert.IsFalse(rules.HitSplitAces);
            Assert.IsFalse(rules.SurrenderAllowed);
            Assert.IsTrue(rules.InsuranceOffered);
            Assert.AreEqual(0.75, rules.Penetration, 0.0001);
            Assert.AreEqual(2, rules.MinimumBet);
            Assert.AreEqual(2, rules.BetIncrement);
            Assert.AreEqual(3, rules.MaxBoxes);
        }

        [Test]
        public void Construction_OddMinimumBet_Throws()
        {
            Assert.Throws<ArgumentException>(() => new BlackjackRules(
                deckCount: 6,
                dealerHitsSoft17: false,
                doubleAfterSplit: true,
                maxSplitsPerBox: 3,
                resplitAces: false,
                hitSplitAces: false,
                surrenderAllowed: false,
                insuranceOffered: true,
                penetration: 0.75,
                minimumBet: 5,
                betIncrement: 2,
                maxBoxes: 3));
        }

        [Test]
        public void Construction_OddBetIncrement_Throws()
        {
            Assert.Throws<ArgumentException>(() => new BlackjackRules(
                deckCount: 6,
                dealerHitsSoft17: false,
                doubleAfterSplit: true,
                maxSplitsPerBox: 3,
                resplitAces: false,
                hitSplitAces: false,
                surrenderAllowed: false,
                insuranceOffered: true,
                penetration: 0.75,
                minimumBet: 2,
                betIncrement: 1,
                maxBoxes: 3));
        }
    }
}
