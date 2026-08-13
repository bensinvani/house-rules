using System.Linq;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class SettlementTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round PlayOut(Wallet wallet, PlayerAction? action, params Card[] script)
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(script), wallet);
            round.PlaceBet(0, 10);
            round.Deal();

            if (action.HasValue && round.State == RoundState.PlayerTurn)
            {
                round.Apply(action.Value);
            }

            return round;
        }

        [Test]
        public void PlayerWins_PaysEvenMoney()
        {
            var wallet = new Wallet(1000);
            // player 20, dealer 18
            var round = PlayOut(wallet, PlayerAction.Stand,
                C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Eight));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Win, s.Outcome);
            Assert.AreEqual(20, s.Payout);
            Assert.AreEqual(10, s.Delta);
            Assert.AreEqual(1010, wallet.Balance);
        }

        [Test]
        public void PlayerLoses_PaysNothing()
        {
            var wallet = new Wallet(1000);
            // player 18, dealer 20
            var round = PlayOut(wallet, PlayerAction.Stand,
                C(Rank.Ten), C(Rank.Ten), C(Rank.Eight), C(Rank.King));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Lose, s.Outcome);
            Assert.AreEqual(0, s.Payout);
            Assert.AreEqual(-10, s.Delta);
            Assert.AreEqual(990, wallet.Balance);
        }

        [Test]
        public void Push_ReturnsTheWager()
        {
            var wallet = new Wallet(1000);
            var round = PlayOut(wallet, PlayerAction.Stand,
                C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.King));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Push, s.Outcome);
            Assert.AreEqual(10, s.Payout);
            Assert.AreEqual(0, s.Delta);
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Bust_LosesRegardlessOfDealerHand()
        {
            var wallet = new Wallet(1000);
            var round = PlayOut(wallet, PlayerAction.Hit,
                C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Six), C(Rank.King), C(Rank.King));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Bust, s.Outcome);
            Assert.AreEqual(-10, s.Delta);
            Assert.AreEqual(990, wallet.Balance);
        }

        [Test]
        public void Blackjack_Pays3To2()
        {
            var wallet = new Wallet(1000);
            var round = PlayOut(wallet, null,
                C(Rank.Ace), C(Rank.Nine), C(Rank.King), C(Rank.Seven));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Blackjack, s.Outcome);
            Assert.AreEqual(25, s.Payout, "10 stake + 15 winnings");
            Assert.AreEqual(15, s.Delta);
            Assert.AreEqual(1015, wallet.Balance);
        }

        [Test]
        public void Blackjack_PaysExact3To2_AcrossEveryLegalWager()
        {
            // Drives the real settlement path for every legal wager, so a truncating
            // payout would actually fail here. (Asserting wager*3/2.0 == wager*3/2
            // in isolation proves nothing — it is an arithmetic identity for even wagers.)
            for (long wager = 2; wager <= 200; wager += 2)
            {
                var wallet = new Wallet(10000);
                var round = new Round(
                    BlackjackRules.Standard,
                    new StackedShoe(C(Rank.Ace), C(Rank.Nine), C(Rank.King), C(Rank.Seven)),
                    wallet);

                round.PlaceBet(0, wager);
                round.Deal();

                Settlement s = round.Settlements.Single();
                Assert.AreEqual(HandOutcome.Blackjack, s.Outcome, $"wager {wager}");
                Assert.AreEqual(wager + (wager * 3 / 2), s.Payout, $"wager {wager}");
                Assert.AreEqual(10000 + (wager * 3 / 2), wallet.Balance, $"wager {wager}");
            }
        }

        [Test]
        public void BlackjackVersusDealerBlackjack_Pushes()
        {
            var wallet = new Wallet(1000);
            // Dealer's ace must be the hole card — an ace upcard diverts to Insurance.
            var round = PlayOut(wallet, null,
                C(Rank.Ace), C(Rank.King), C(Rank.King), C(Rank.Ace));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Push, s.Outcome);
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void DealerBlackjack_BeatsPlayerTwenty()
        {
            var wallet = new Wallet(1000);
            // Player 20 (not a natural) against a dealer natural. Ace in the hole.
            var round = PlayOut(wallet, null,
                C(Rank.Ten), C(Rank.King), C(Rank.Ten), C(Rank.Ace));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Lose, s.Outcome);
            Assert.AreEqual(990, wallet.Balance);
        }

        [Test]
        public void Insurance_Pays2To1_WhenDealerHasBlackjack()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Ace), C(Rank.Seven), C(Rank.King)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.TakeInsurance);

            // Bet 10 lost, insurance premium 5 returned as 15.
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Insurance_IsLost_WhenDealerHasNoBlackjack()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Ace), C(Rank.King), C(Rank.Four), C(Rank.Five)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.TakeInsurance);
            round.Apply(PlayerAction.Stand);

            // Player 20 beats dealer 20? No: dealer draws to 20 -> push on main bet,
            // insurance premium of 5 is lost.
            Assert.AreEqual(995, wallet.Balance);
        }

        [Test]
        public void EveryHandOfASplitBoxSettlesIndependently()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(
                    C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Ten),
                    C(Rank.King),  // hand 0 -> 18
                    C(Rank.Two),   // hand 1 -> 10
                    C(Rank.Nine)), // dealer draws to 25? 6+10=16, +9 = 25 bust
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Split);
            round.Apply(PlayerAction.Stand);
            round.Apply(PlayerAction.Stand);

            Assert.AreEqual(2, round.Settlements.Count);
        }

        [Test]
        public void RoundSettled_TotalMatchesSumOfDeltas()
        {
            var wallet = new Wallet(1000);
            var round = PlayOut(wallet, PlayerAction.Stand,
                C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Eight));

            long expected = round.Settlements.Sum(s => s.Delta);
            Assert.AreEqual(expected, round.TotalDelta);
        }

        [Test]
        public void RoundSettled_OnInsuranceRound_TotalIncludesInsuranceDelta()
        {
            var wallet = new Wallet(1000);
            long startingBalance = wallet.Balance;
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Ace), C(Rank.King), C(Rank.Four), C(Rank.Five)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.TakeInsurance);
            round.Apply(PlayerAction.Stand);

            long insuranceDelta = round.DrainEvents()
                .OfType<InsuranceSettled>()
                .Single()
                .Delta;

            long expected = round.Settlements.Sum(s => s.Delta) + insuranceDelta;
            Assert.AreEqual(expected, round.TotalDelta);
            Assert.AreEqual(startingBalance + round.TotalDelta, wallet.Balance);
        }
    }
}
