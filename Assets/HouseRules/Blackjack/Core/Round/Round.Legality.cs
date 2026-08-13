using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        private static readonly PlayerAction[] NoActions = new PlayerAction[0];

        private static readonly PlayerAction[] InsuranceActions =
        {
            PlayerAction.TakeInsurance,
            PlayerAction.DeclineInsurance
        };

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
