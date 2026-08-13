using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class DealerTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round PlayerStands(params Card[] script)
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(script), new Wallet(1000));
            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Stand);
            return round;
        }

        [Test]
        public void Dealer_HitsUntil17()
        {
            // player 20; dealer 5 + 6 = 11, then draws 9 for 20.
            var round = PlayerStands(
                C(Rank.Ten), C(Rank.Five), C(Rank.King), C(Rank.Six), C(Rank.Nine));

            Assert.AreEqual(20, round.DealerHand.Value.Total);
        }

        [Test]
        public void Dealer_StandsOnHard17()
        {
            var round = PlayerStands(
                C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Seven));

            Assert.AreEqual(17, round.DealerHand.Value.Total);
            Assert.AreEqual(2, round.DealerHand.Cards.Count);
        }

        [Test]
        public void Dealer_StandsOnSoft17_UnderStandardRules()
        {
            // Dealer 6 up, ace in the hole = soft 17, must stand.
            // The ace must be the HOLE card: an ace upcard diverts to the Insurance state.
            var round = PlayerStands(
                C(Rank.Ten), C(Rank.Six), C(Rank.King), C(Rank.Ace));

            Assert.AreEqual(17, round.DealerHand.Value.Total);
            Assert.IsTrue(round.DealerHand.Value.IsSoft);
            Assert.AreEqual(2, round.DealerHand.Cards.Count);
        }

        [Test]
        public void Dealer_HitsSoft17_WhenRulesSaySo()
        {
            var rules = new BlackjackRules(
                deckCount: 6,
                dealerHitsSoft17: true,
                doubleAfterSplit: true,
                maxSplitsPerBox: 3,
                resplitAces: false,
                hitSplitAces: false,
                surrenderAllowed: false,
                insuranceOffered: true,
                penetration: 0.75,
                minimumBet: 2,
                betIncrement: 2,
                maxBoxes: 3);

            // Dealer 6 up, ace in the hole = soft 17, hits under H17, draws a 2 for 19.
            var round = new Round(
                rules,
                new StackedShoe(C(Rank.Ten), C(Rank.Six), C(Rank.King), C(Rank.Ace), C(Rank.Two)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Stand);

            Assert.AreEqual(3, round.DealerHand.Cards.Count);
            Assert.AreEqual(19, round.DealerHand.Value.Total);
        }

        [Test]
        public void Dealer_CanBust()
        {
            var round = PlayerStands(
                C(Rank.Ten), C(Rank.Six), C(Rank.King), C(Rank.Six), C(Rank.King));

            Assert.IsTrue(round.DealerHand.IsBust);
        }

        [Test]
        public void DealerTurn_EndsWithRoundComplete()
        {
            var round = PlayerStands(
                C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Seven));

            Assert.AreEqual(RoundState.Complete, round.State);
        }

        [Test]
        public void Dealer_DoesNotDraw_WhenEveryPlayerHandBusted()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Four), C(Rank.King)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Hit); // busts with 29

            // Dealer has 10 and must not draw, because there is nothing left to beat.
            Assert.AreEqual(2, round.DealerHand.Cards.Count);
        }
    }
}
