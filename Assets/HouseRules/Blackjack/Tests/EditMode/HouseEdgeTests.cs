using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class HouseEdgeTests
    {
        private const int Rounds = 100000;
        private const long Wager = 10;

        [Test]
        [Category("Statistical")]
        public void BasicStrategy_ProducesTheExpectedHouseEdge()
        {
            var rules = BlackjackRules.Standard;
            var random = new SeededRandom(20260813);
            var shoe = new Shoe(rules.DeckCount, rules.Penetration, random);

            // A large float balance would lose precision, so track staked and returned separately.
            long totalStaked = 0;
            long netDelta = 0;
            var wallet = new Wallet(long.MaxValue / 4);
            long startingBalance = wallet.Balance;

            for (int i = 0; i < Rounds; i++)
            {
                var round = new Round(rules, shoe, wallet);
                round.PlaceBet(0, Wager);
                totalStaked += Wager;

                round.Deal();

                while (round.State == RoundState.Insurance)
                {
                    // Basic strategy never takes insurance.
                    round.Apply(PlayerAction.DeclineInsurance);
                }

                int guard = 0;
                while (round.State == RoundState.PlayerTurn)
                {
                    PlayerAction action = BasicStrategy.Decide(
                        round.CurrentHand, round.DealerUpcard, round.LegalActions);

                    round.Apply(action);

                    if (++guard > 100)
                    {
                        Assert.Fail("Player turn failed to terminate — likely a turn-advancement bug.");
                    }
                }

                Assert.AreEqual(RoundState.Complete, round.State, $"Round {i} did not complete.");

                netDelta += round.TotalDelta;
                round.DrainEvents();
            }

            // Wallet must reconcile exactly with the deltas the engine reported.
            Assert.AreEqual(startingBalance + netDelta, wallet.Balance,
                "Wallet balance diverged from the sum of settlement deltas.");

            double houseEdgePercent = -100.0 * netDelta / totalStaked;

            TestContext.WriteLine(
                $"Rounds: {Rounds}, staked: {totalStaked}, net: {netDelta}, " +
                $"house edge: {houseEdgePercent:F3}%");

            // Published basic-strategy edge for this ruleset is roughly 0.4%.
            // The band absorbs sampling variance across 100k rounds; a result outside
            // it means a systemic payout or rule error, not bad luck.
            Assert.That(houseEdgePercent, Is.InRange(-1.0, 1.5),
                $"House edge {houseEdgePercent:F3}% is outside the plausible band.");
        }

        [Test]
        public void ThousandRounds_NeverThrowAndAlwaysComplete()
        {
            var rules = BlackjackRules.Standard;
            var shoe = new Shoe(rules.DeckCount, rules.Penetration, new SeededRandom(99));
            var wallet = new Wallet(long.MaxValue / 4);

            for (int i = 0; i < 1000; i++)
            {
                var round = new Round(rules, shoe, wallet);
                round.PlaceBet(0, 10);
                round.PlaceBet(1, 10);
                round.Deal();

                while (round.State == RoundState.Insurance)
                {
                    round.Apply(PlayerAction.DeclineInsurance);
                }

                while (round.State == RoundState.PlayerTurn)
                {
                    round.Apply(BasicStrategy.Decide(
                        round.CurrentHand, round.DealerUpcard, round.LegalActions));
                }

                Assert.AreEqual(RoundState.Complete, round.State);
            }
        }
    }
}
