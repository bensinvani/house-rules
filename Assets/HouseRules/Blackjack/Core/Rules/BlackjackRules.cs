using System;

namespace HouseRules.Blackjack
{
    /// <summary>
    /// The ruleset the engine plays under. Fixed at construction; there is no settings UI.
    /// Tests vary these values to exercise rule-dependent branches.
    /// </summary>
    public readonly struct BlackjackRules
    {
        public int DeckCount { get; }
        public bool DealerHitsSoft17 { get; }
        public bool DoubleAfterSplit { get; }
        public int MaxSplitsPerBox { get; }
        public bool ResplitAces { get; }
        public bool HitSplitAces { get; }
        public bool SurrenderAllowed { get; }
        public bool InsuranceOffered { get; }
        public double Penetration { get; }
        public long MinimumBet { get; }
        public long BetIncrement { get; }
        public int MaxBoxes { get; }

        public BlackjackRules(
            int deckCount,
            bool dealerHitsSoft17,
            bool doubleAfterSplit,
            int maxSplitsPerBox,
            bool resplitAces,
            bool hitSplitAces,
            bool surrenderAllowed,
            bool insuranceOffered,
            double penetration,
            long minimumBet,
            long betIncrement,
            int maxBoxes)
        {
            if (minimumBet <= 0 || minimumBet % 2 != 0)
            {
                throw new ArgumentException(
                    "Minimum bet must be positive and even so a 3:2 blackjack payout is exact.",
                    nameof(minimumBet));
            }

            if (betIncrement <= 0 || betIncrement % 2 != 0)
            {
                throw new ArgumentException(
                    "Bet increment must be positive and even so a 3:2 blackjack payout is exact.",
                    nameof(betIncrement));
            }

            DeckCount = deckCount;
            DealerHitsSoft17 = dealerHitsSoft17;
            DoubleAfterSplit = doubleAfterSplit;
            MaxSplitsPerBox = maxSplitsPerBox;
            ResplitAces = resplitAces;
            HitSplitAces = hitSplitAces;
            SurrenderAllowed = surrenderAllowed;
            InsuranceOffered = insuranceOffered;
            Penetration = penetration;
            MinimumBet = minimumBet;
            BetIncrement = betIncrement;
            MaxBoxes = maxBoxes;
        }

        /// <summary>
        /// 6 decks, dealer stands soft 17, 3:2 blackjack, double after split allowed,
        /// up to 3 splits, split aces get one card and cannot be resplit, no surrender.
        /// Minimum bet and increment are 2 so that a 3:2 payout is always exact integer math.
        /// </summary>
        public static BlackjackRules Standard => new BlackjackRules(
            deckCount: 6,
            dealerHitsSoft17: false,
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
    }
}
