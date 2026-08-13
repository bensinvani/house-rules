using System.Linq;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class SplitTests
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
        public void Split_CreatesTwoHands_EachWithOneOriginalCard()
        {
            var wallet = new Wallet(1000);
            var round = Dealt(wallet,
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                C(Rank.Three), C(Rank.Two));

            round.Apply(PlayerAction.Split);

            Box box = round.Boxes[0];
            Assert.AreEqual(2, box.Hands.Count);
            Assert.AreEqual(Rank.Eight, box.Hands[0].Cards[0].Rank);
            Assert.AreEqual(Rank.Eight, box.Hands[1].Cards[0].Rank);
        }

        [Test]
        public void Split_DebitsASecondWager()
        {
            var wallet = new Wallet(1000);
            var round = Dealt(wallet,
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                C(Rank.Three), C(Rank.Two));

            Assert.AreEqual(990, wallet.Balance);
            round.Apply(PlayerAction.Split);
            Assert.AreEqual(980, wallet.Balance);
            Assert.AreEqual(10, round.Boxes[0].Hands[1].Wager);
        }

        [Test]
        public void Split_DealsOneCardToEachNewHand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                C(Rank.Three), C(Rank.Two));

            round.Apply(PlayerAction.Split);

            Assert.AreEqual(2, round.Boxes[0].Hands[0].Cards.Count);
            Assert.AreEqual(2, round.Boxes[0].Hands[1].Cards.Count);
        }

        [Test]
        public void Split_MarksBothHandsAsFromSplit()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                C(Rank.Three), C(Rank.Two));

            round.Apply(PlayerAction.Split);

            Assert.IsTrue(round.Boxes[0].Hands[0].IsFromSplit);
            Assert.IsTrue(round.Boxes[0].Hands[1].IsFromSplit);
        }

        [Test]
        public void SplitHand_ThatMakes21_IsNotBlackjack()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ace), C(Rank.Six), C(Rank.Ace), C(Rank.Four),
                C(Rank.King), C(Rank.Queen));

            round.Apply(PlayerAction.Split);

            Hand first = round.Boxes[0].Hands[0];
            Assert.AreEqual(21, first.Value.Total);
            Assert.IsFalse(first.IsBlackjack);
        }

        [Test]
        public void SplitAces_ReceiveOneCardEach_AndCannotAct()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ace), C(Rank.Six), C(Rank.Ace), C(Rank.Four),
                C(Rank.Five), C(Rank.Seven));

            round.Apply(PlayerAction.Split);

            // Both split aces auto-close, so play passes straight to the dealer,
            // which plays out and the round completes.
            Assert.AreEqual(RoundState.Complete, round.State);
            Assert.AreEqual(2, round.Boxes[0].Hands[0].Cards.Count);
            Assert.AreEqual(2, round.Boxes[0].Hands[1].Cards.Count);
            Assert.IsTrue(round.Boxes[0].Hands[0].IsClosed);
            Assert.IsTrue(round.Boxes[0].Hands[1].IsClosed);
        }

        [Test]
        public void Split_RespectsMaxSplitsPerBox()
        {
            // Every card is an eight, so the player can keep splitting until the cap.
            var script = new Card[40];
            for (int i = 0; i < script.Length; i++)
            {
                script[i] = C(Rank.Eight);
            }

            var round = new Round(
                BlackjackRules.Standard,
                StackedShoe.WithFiller(C(Rank.Eight), script),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();

            int splits = 0;
            while (round.State == RoundState.PlayerTurn
                   && round.LegalActions.Contains(PlayerAction.Split))
            {
                round.Apply(PlayerAction.Split);
                splits++;
            }

            Assert.AreEqual(3, splits, "Standard rules allow at most 3 splits per box.");
            Assert.AreEqual(4, round.Boxes[0].Hands.Count);
        }

        [Test]
        public void AfterSplit_PlayContinuesOnTheFirstNewHand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                C(Rank.Three), C(Rank.Two));

            round.Apply(PlayerAction.Split);

            Assert.AreEqual(0, round.CurrentBoxIndex);
            Assert.AreEqual(0, round.CurrentHandIndex);
        }

        [Test]
        public void Split_OnANonFirstHand_InsertsAfterItAndLeavesEarlierHandsUntouched()
        {
            // Initial pair of eights (dealer 6 up, 5 in the hole — no peek trigger).
            var round = Dealt(new Wallet(1000),
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Five),
                // First split deals hand 0 a Three (no further pair) and hand 1 an Eight (a pair).
                C(Rank.Three), C(Rank.Eight),
                // Second split (on hand 1) deals its two children a Two and a Four.
                C(Rank.Two), C(Rank.Four));

            round.Apply(PlayerAction.Split);

            Hand originalHand0 = round.Boxes[0].Hands[0];
            Assert.AreEqual(2, originalHand0.Cards.Count, "hand 0 should have exactly its post-split card added so far");
            Assert.AreEqual(Rank.Three, originalHand0.Cards[1].Rank);

            round.Apply(PlayerAction.Stand);

            // Play now sits on hand 1, a fresh pair of eights.
            Assert.AreEqual(1, round.CurrentHandIndex);
            CollectionAssert.Contains(round.LegalActions, PlayerAction.Split);

            round.Apply(PlayerAction.Split);

            Box box = round.Boxes[0];
            Assert.AreEqual(3, box.Hands.Count, "splitting hand 1 should insert a third hand after it");

            Hand hand0 = box.Hands[0];
            Hand hand1 = box.Hands[1];
            Hand hand2 = box.Hands[2];

            // Hand 0 is completely untouched by the second split.
            Assert.IsTrue(hand0.IsClosed);
            Assert.AreEqual(2, hand0.Cards.Count);
            Assert.AreEqual(Rank.Eight, hand0.Cards[0].Rank);
            Assert.AreEqual(Rank.Three, hand0.Cards[1].Rank);

            // Hands 1 and 2 are the two children of the second split, in order.
            Assert.IsTrue(hand1.IsFromSplit);
            Assert.AreEqual(Rank.Eight, hand1.Cards[0].Rank);
            Assert.AreEqual(Rank.Two, hand1.Cards[1].Rank);

            Assert.IsTrue(hand2.IsFromSplit);
            Assert.AreEqual(Rank.Eight, hand2.Cards[0].Rank);
            Assert.AreEqual(Rank.Four, hand2.Cards[1].Rank);
        }
    }
}
