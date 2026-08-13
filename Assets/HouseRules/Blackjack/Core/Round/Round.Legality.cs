using System;
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        // Wrapped in ReadOnlyCollection, not returned as bare arrays. A bare
        // static array handed out as IReadOnlyList is still mutable via a
        // downcast, and a single stray write would corrupt legality for every
        // Round in the process. The runtime type enforces what the interface
        // only suggests.
        private static readonly IReadOnlyList<PlayerAction> NoActions =
            Array.AsReadOnly(Array.Empty<PlayerAction>());

        private static readonly IReadOnlyList<PlayerAction> InsuranceActions =
            Array.AsReadOnly(new[]
            {
                PlayerAction.TakeInsurance,
                PlayerAction.DeclineInsurance
            });

        /// <summary>
        /// Everything the player may legally do right now. All rule knowledge about
        /// permitted actions lives here — consumers render from this list and nothing else.
        /// </summary>
        public IReadOnlyList<PlayerAction> LegalActions
        {
            get
            {
                if (State == RoundState.Insurance)
                {
                    return InsuranceActions;
                }

                if (State != RoundState.PlayerTurn)
                {
                    return NoActions;
                }

                Hand hand = CurrentHand;
                Box box = CurrentBox;
                if (hand == null || box == null || hand.IsClosed)
                {
                    return NoActions;
                }

                var actions = new List<PlayerAction>(4);

                bool isSplitAce = hand.IsFromSplit
                                  && hand.Cards.Count > 0
                                  && hand.Cards[0].Rank == Rank.Ace;

                // A split ace receives exactly one card and cannot act further.
                if (isSplitAce && !_rules.HitSplitAces)
                {
                    return NoActions;
                }

                actions.Add(PlayerAction.Hit);
                actions.Add(PlayerAction.Stand);

                bool isFirstDecision = hand.Cards.Count == 2 && !hand.IsDoubled;
                bool doubleAllowedHere = !hand.IsFromSplit || _rules.DoubleAfterSplit;

                if (isFirstDecision && doubleAllowedHere && _wallet.CanAfford(hand.Wager))
                {
                    actions.Add(PlayerAction.Double);
                }

                bool underSplitLimit = box.SplitCount < _rules.MaxSplitsPerBox;
                bool acesResplitAllowed = !isSplitAce || _rules.ResplitAces;

                if (isFirstDecision
                    && hand.IsPair
                    && underSplitLimit
                    && acesResplitAllowed
                    && _wallet.CanAfford(hand.Wager))
                {
                    actions.Add(PlayerAction.Split);
                }

                return actions;
            }
        }
    }
}
