using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class InsuranceTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round DealtWithAceUpcard(Wallet wallet, Card dealerHole)
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Ace), C(Rank.Seven), dealerHole),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            return round;
        }

        [Test]
        public void AceUpcard_EntersInsuranceState()
        {
            var round = DealtWithAceUpcard(new Wallet(1000), C(Rank.Four));
            Assert.AreEqual(RoundState.Insurance, round.State);
        }

        [Test]
        public void TakeInsurance_DebitsHalfTheOriginalBet()
        {
            var wallet = new Wallet(1000);
            var round = DealtWithAceUpcard(wallet, C(Rank.Four));

            Assert.AreEqual(990, wallet.Balance);
            round.Apply(PlayerAction.TakeInsurance);
            Assert.AreEqual(985, wallet.Balance);
            Assert.AreEqual(5, round.Boxes[0].InsuranceBet);
        }

        [Test]
        public void DeclineInsurance_CostsNothing()
        {
            var wallet = new Wallet(1000);
            var round = DealtWithAceUpcard(wallet, C(Rank.Four));

            round.Apply(PlayerAction.DeclineInsurance);

            Assert.AreEqual(990, wallet.Balance);
            Assert.AreEqual(0, round.Boxes[0].InsuranceBet);
        }

        [Test]
        public void AfterInsurance_NoDealerBlackjack_ProceedsToPlayerTurn()
        {
            var round = DealtWithAceUpcard(new Wallet(1000), C(Rank.Four));
            round.Apply(PlayerAction.DeclineInsurance);
            Assert.AreEqual(RoundState.PlayerTurn, round.State);
        }

        [Test]
        public void AfterInsurance_DealerBlackjack_EndsTheRound()
        {
            var round = DealtWithAceUpcard(new Wallet(1000), C(Rank.King));
            round.Apply(PlayerAction.DeclineInsurance);

            Assert.IsTrue(round.DealerHasBlackjack);
            Assert.AreEqual(RoundState.Complete, round.State);
        }

        [Test]
        public void Insurance_IsIllegal_WhenWalletCannotCoverIt()
        {
            var wallet = new Wallet(10);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Ace), C(Rank.Seven), C(Rank.Four)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();

            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.TakeInsurance);
            CollectionAssert.Contains(round.LegalActions, PlayerAction.DeclineInsurance);
        }
    }
}
