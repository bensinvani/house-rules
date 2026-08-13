namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        private void BeginDealerTurn()
        {
            SetState(RoundState.DealerTurn);
            Emit(new DealerRevealed(DealerHand.Cards[1]));

            if (AnyLiveHandRemains())
            {
                PlayDealerOut();
            }

            Settle();
        }

        /// <summary>
        /// The dealer only draws when some player hand can still be beaten.
        /// If every hand busted, the house already won and drawing is theatre.
        /// </summary>
        private bool AnyLiveHandRemains()
        {
            foreach (Box box in _boxes)
            {
                if (!box.IsActive)
                {
                    continue;
                }

                foreach (Hand hand in box.Hands)
                {
                    if (!hand.IsBust)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void PlayDealerOut()
        {
            while (true)
            {
                HandValue value = DealerHand.Value;

                if (value.Total > 17)
                {
                    break;
                }

                if (value.Total == 17)
                {
                    bool mustHit = value.IsSoft && _rules.DealerHitsSoft17;
                    if (!mustHit)
                    {
                        break;
                    }
                }

                DealToDealer(faceUp: true);
            }
        }
    }
}
