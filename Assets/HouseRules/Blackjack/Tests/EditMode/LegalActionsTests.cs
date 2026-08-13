using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class LegalActionsTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round Dealt(params Card[] script)
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(script), new Wallet(1000));
            round.PlaceBet(0, 10);
            round.Deal();
            return round;
        }

        [Test]
        public void FreshTwoCardHand_AllowsHitStandDouble()
        {
            // box: 9,7 = 16   dealer: 6, 4
            var round = Dealt(C(Rank.Nine), C(Rank.Six), C(Rank.Seven), C(Rank.Four));

            CollectionAssert.Contains(round.LegalActions, PlayerAction.Hit);
            CollectionAssert.Contains(round.LegalActions, PlayerAction.Stand);
            CollectionAssert.Contains(round.LegalActions, PlayerAction.Double);
            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.Split);
        }

        [Test]
        public void PairHand_AlsoAllowsSplit()
        {
            // box: 8,8   dealer: 6, 4
            var round = Dealt(C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four));
            CollectionAssert.Contains(round.LegalActions, PlayerAction.Split);
        }

        [Test]
        public void TenAndKing_IsNotSplittable()
        {
            var round = Dealt(C(Rank.Ten), C(Rank.Six), C(Rank.King), C(Rank.Four));
            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.Split);
        }

        [Test]
        public void Split_IsIllegal_WhenWalletCannotCoverSecondWager()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four)),
                new Wallet(10));

            round.PlaceBet(0, 10);
            round.Deal();

            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.Split);
        }

        [Test]
        public void Double_IsIllegal_WhenWalletCannotCoverIt()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Six), C(Rank.Two), C(Rank.Four)),
                new Wallet(10));

            round.PlaceBet(0, 10);
            round.Deal();

            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.Double);
        }

        [Test]
        public void InsuranceState_OffersOnlyInsuranceActions()
        {
            var round = Dealt(C(Rank.Nine), C(Rank.Ace), C(Rank.Seven), C(Rank.Four));

            Assert.AreEqual(RoundState.Insurance, round.State);
            CollectionAssert.AreEquivalent(
                new[] { PlayerAction.TakeInsurance, PlayerAction.DeclineInsurance },
                round.LegalActions);
        }

        [Test]
        public void NonPlayerStates_OfferNoActions()
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), new Wallet(1000));
            Assert.AreEqual(RoundState.Betting, round.State);
            Assert.IsEmpty(round.LegalActions);
        }
    }
}
