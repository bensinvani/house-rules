using System;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        /// <summary>
        /// Discard the round and return every staked chip. A round is atomic: a player
        /// who quits mid-hand is made whole rather than settled against.
        /// </summary>
        public void Abandon()
        {
            if (State == RoundState.Complete)
            {
                throw new InvalidOperationException("Cannot abandon a completed round.");
            }

            long refunded = 0;

            foreach (Box box in _boxes)
            {
                if (!box.IsActive)
                {
                    continue;
                }

                // Refund from each hand's CURRENT wager, not the box's initial bet:
                // a doubled hand's wager is already 2x, and a split box has one wager per hand.
                foreach (Hand hand in box.Hands)
                {
                    refunded += hand.Wager;
                }

                refunded += box.InsuranceBet;
            }

            if (refunded > 0)
            {
                _wallet.Credit(refunded);
            }

            Emit(new RoundAbandoned(refunded));
            SetState(RoundState.Complete);
        }
    }
}
