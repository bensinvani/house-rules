using System;
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        public int CurrentBoxIndex { get; private set; } = -1;

        public int CurrentHandIndex { get; private set; } = -1;

        public Box CurrentBox => CurrentBoxIndex >= 0 ? _boxes[CurrentBoxIndex] : null;

        public Hand CurrentHand
        {
            get
            {
                Box box = CurrentBox;
                if (box == null || CurrentHandIndex < 0 || CurrentHandIndex >= box.Hands.Count)
                {
                    return null;
                }

                return box.Hands[CurrentHandIndex];
            }
        }

        public bool DealerHasBlackjack => DealerHand.IsBlackjack;

        private IEnumerable<Box> ActiveBoxes()
        {
            foreach (Box box in _boxes)
            {
                if (box.IsActive)
                {
                    yield return box;
                }
            }
        }

        private bool AnyBoxActive()
        {
            foreach (Box box in _boxes)
            {
                if (box.IsActive)
                {
                    return true;
                }
            }

            return false;
        }

        public void Deal()
        {
            if (State != RoundState.Betting)
            {
                throw new InvalidOperationException($"Cannot deal in state {State}.");
            }

            if (!AnyBoxActive())
            {
                throw new InvalidOperationException("At least one box must have a bet before dealing.");
            }

            SetState(RoundState.Dealing);

            Emit(new RoundStarted());

            if (_shoe.NeedsReshuffle)
            {
                _shoe.Reshuffle();
                Emit(new ShoeReshuffled());
            }

            // First card to each active box, then the dealer's upcard.
            foreach (Box box in ActiveBoxes())
            {
                DealTo(box.Index, 0, box.Hands[0], faceUp: true);
            }

            DealToDealer(faceUp: true);

            // Second card to each active box, then the dealer's hole card.
            foreach (Box box in ActiveBoxes())
            {
                DealTo(box.Index, 0, box.Hands[0], faceUp: true);
            }

            DealToDealer(faceUp: false);

            // US peek rules: the dealer checks for blackjack on an ace or ten upcard,
            // so the player never loses double or split money to a dealer natural.
            bool upcardTriggersPeek =
                DealerUpcard.Rank == Rank.Ace || DealerUpcard.BaseValue == 10;

            if (DealerUpcard.Rank == Rank.Ace && _rules.InsuranceOffered)
            {
                SetState(RoundState.Insurance);
                Emit(new InsuranceOffered());
                return;
            }

            if (upcardTriggersPeek && DealerHasBlackjack)
            {
                RevealAndSettleDealerBlackjack();
                return;
            }

            BeginPlayerTurn();
        }

        private void DealTo(int boxIndex, int handIndex, Hand hand, bool faceUp)
        {
            Card card = _shoe.Deal();
            hand.Add(card);
            Emit(new CardDealt(boxIndex, handIndex, card, faceUp));
        }

        private void DealToDealer(bool faceUp)
        {
            Card card = _shoe.Deal();
            DealerHand.Add(card);
            Emit(new CardDealt(CardDealt.DealerBoxIndex, 0, card, faceUp));
        }

        private void BeginPlayerTurn()
        {
            SetState(RoundState.PlayerTurn);
            CurrentBoxIndex = -1;
            CurrentHandIndex = -1;

            if (AdvanceToNextPlayableHand())
            {
                Emit(new PlayerTurnStarted(CurrentBoxIndex, CurrentHandIndex));
            }
            else
            {
                BeginDealerTurn();
            }
        }
    }
}
