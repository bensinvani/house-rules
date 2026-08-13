using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class EventTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        [Test]
        public void Deal_EmitsRoundStartedThenCardsInDealOrder()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Nine), C(Rank.Seven), C(Rank.Four)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();

            List<GameEvent> events = round.DrainEvents().ToList();

            Assert.IsInstanceOf<RoundStarted>(events[0]);

            var cards = events.OfType<CardDealt>().ToList();
            Assert.AreEqual(4, cards.Count);
            Assert.AreEqual(C(Rank.Ten), cards[0].Card);
            Assert.AreEqual(C(Rank.Nine), cards[1].Card);
            Assert.AreEqual(C(Rank.Seven), cards[2].Card);
            Assert.AreEqual(C(Rank.Four), cards[3].Card);
        }

        [Test]
        public void HoleCard_IsEmittedFaceDown()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Nine), C(Rank.Seven), C(Rank.Four)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();

            var cards = round.DrainEvents().OfType<CardDealt>().ToList();
            Assert.IsTrue(cards[1].FaceUp, "Upcard should be face up.");
            Assert.IsFalse(cards[3].FaceUp, "Hole card should be face down.");
            Assert.AreEqual(CardDealt.DealerBoxIndex, cards[3].BoxIndex);
        }

        [Test]
        public void DrainEvents_ClearsTheBuffer()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Nine), C(Rank.Seven), C(Rank.Four)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.IsNotEmpty(round.DrainEvents());
            Assert.IsEmpty(round.DrainEvents());
        }

        [Test]
        public void Bust_EmitsHandBustedBeforeDealerRevealed()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Four), C(Rank.King)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();
            round.DrainEvents();

            round.Apply(PlayerAction.Hit);
            List<GameEvent> events = round.DrainEvents().ToList();

            int bustIndex = events.FindIndex(e => e is HandBusted);
            int revealIndex = events.FindIndex(e => e is DealerRevealed);

            Assert.Greater(bustIndex, -1, "Expected a HandBusted event.");
            Assert.Greater(revealIndex, bustIndex, "Dealer must be revealed after the bust.");
        }

        [Test]
        public void RoundSettled_IsTheFinalEvent()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Eight)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Stand);

            List<GameEvent> events = round.DrainEvents().ToList();
            Assert.IsInstanceOf<RoundSettled>(events[events.Count - 1]);
        }

        [Test]
        public void EverySettledHand_EmitsAHandSettledEvent()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Eight)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.PlaceBet(1, 10);
            round.Deal();
            round.Apply(PlayerAction.Stand);
            round.Apply(PlayerAction.Stand);

            int settled = round.DrainEvents().OfType<HandSettled>().Count();
            Assert.AreEqual(round.Settlements.Count, settled);
        }
    }
}
