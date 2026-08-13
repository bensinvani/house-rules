using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        private readonly List<Settlement> _settlements = new List<Settlement>();

        public IReadOnlyList<Settlement> Settlements => _settlements;

        public long TotalDelta { get; private set; }

        /// <summary>
        /// Called when the dealer peeks a natural. Player hands settle immediately;
        /// nobody gets to act, so only naturals push.
        /// </summary>
        private void RevealAndSettleDealerBlackjack()
        {
            SetState(RoundState.DealerTurn);
            Emit(new DealerRevealed(DealerHand.Cards[1]));
            Settle();
        }

        private void Settle()
        {
            SetState(RoundState.Settlement);

            bool dealerBlackjack = DealerHasBlackjack;
            int dealerTotal = DealerHand.Value.Total;
            bool dealerBust = DealerHand.IsBust;

            foreach (Box box in _boxes)
            {
                if (!box.IsActive)
                {
                    continue;
                }

                SettleInsurance(box, dealerBlackjack);

                for (int h = 0; h < box.Hands.Count; h++)
                {
                    Hand hand = box.Hands[h];
                    Settlement settlement = SettleHand(box.Index, h, hand, dealerBlackjack, dealerBust, dealerTotal);

                    _settlements.Add(settlement);
                    TotalDelta += settlement.Delta;

                    if (settlement.Payout > 0)
                    {
                        _wallet.Credit(settlement.Payout);
                    }

                    Emit(new HandSettled(settlement));
                }
            }

            Emit(new RoundSettled(TotalDelta));
            SetState(RoundState.Complete);
        }

        private void SettleInsurance(Box box, bool dealerBlackjack)
        {
            if (box.InsuranceBet <= 0)
            {
                return;
            }

            if (dealerBlackjack)
            {
                // Pays 2:1 — the premium plus twice the premium in winnings.
                _wallet.Credit(box.InsuranceBet * 3);
                TotalDelta += box.InsuranceBet * 2;
            }
            else
            {
                TotalDelta -= box.InsuranceBet;
            }
        }

        private static Settlement SettleHand(
            int boxIndex,
            int handIndex,
            Hand hand,
            bool dealerBlackjack,
            bool dealerBust,
            int dealerTotal)
        {
            long wager = hand.Wager;

            if (hand.IsBust)
            {
                return new Settlement(boxIndex, handIndex, HandOutcome.Bust, wager, 0);
            }

            if (hand.IsBlackjack)
            {
                if (dealerBlackjack)
                {
                    return new Settlement(boxIndex, handIndex, HandOutcome.Push, wager, wager);
                }

                // 3:2. Wagers are always even, so this division is exact.
                long winnings = wager * 3 / 2;
                return new Settlement(boxIndex, handIndex, HandOutcome.Blackjack, wager, wager + winnings);
            }

            if (dealerBlackjack)
            {
                return new Settlement(boxIndex, handIndex, HandOutcome.Lose, wager, 0);
            }

            if (dealerBust)
            {
                return new Settlement(boxIndex, handIndex, HandOutcome.Win, wager, wager * 2);
            }

            int playerTotal = hand.Value.Total;

            if (playerTotal > dealerTotal)
            {
                return new Settlement(boxIndex, handIndex, HandOutcome.Win, wager, wager * 2);
            }

            if (playerTotal == dealerTotal)
            {
                return new Settlement(boxIndex, handIndex, HandOutcome.Push, wager, wager);
            }

            return new Settlement(boxIndex, handIndex, HandOutcome.Lose, wager, 0);
        }
    }
}
