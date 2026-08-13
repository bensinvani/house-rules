using System;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        /// <summary>
        /// Apply a player intent. Always validated against <see cref="LegalActions"/> —
        /// a bug in a caller surfaces as a rejected intent, never a corrupted round.
        /// </summary>
        public void Apply(PlayerAction action)
        {
            if (!Contains(LegalActions, action))
            {
                throw new InvalidOperationException(
                    $"{action} is not legal in state {State}.");
            }

            switch (action)
            {
                case PlayerAction.Hit:
                    ApplyHit();
                    break;
                case PlayerAction.Stand:
                    ApplyStand();
                    break;
                case PlayerAction.Double:
                    ApplyDouble();
                    break;
                case PlayerAction.Split:
                    ApplySplit();
                    break;
                case PlayerAction.TakeInsurance:
                    ApplyInsurance(taken: true);
                    break;
                case PlayerAction.DeclineInsurance:
                    ApplyInsurance(taken: false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<PlayerAction> list, PlayerAction action)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == action)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyHit()
        {
            Hand hand = CurrentHand;
            DealTo(CurrentBoxIndex, CurrentHandIndex, hand, faceUp: true);

            if (hand.IsBust)
            {
                hand.Close();
                Emit(new HandBusted(CurrentBoxIndex, CurrentHandIndex));
                AdvanceTurn();
            }
        }

        private void ApplyStand()
        {
            CurrentHand.Close();
            Emit(new HandStood(CurrentBoxIndex, CurrentHandIndex));
            AdvanceTurn();
        }

        private void ApplyDouble()
        {
            Hand hand = CurrentHand;

            _wallet.Debit(hand.Wager);
            hand.SetWager(hand.Wager * 2);
            hand.MarkDoubled();
            Emit(new HandDoubled(CurrentBoxIndex, CurrentHandIndex, hand.Wager));

            DealTo(CurrentBoxIndex, CurrentHandIndex, hand, faceUp: true);

            if (hand.IsBust)
            {
                Emit(new HandBusted(CurrentBoxIndex, CurrentHandIndex));
            }

            // A doubled hand receives exactly one card and is finished either way.
            hand.Close();
            AdvanceTurn();
        }

        private void AdvanceTurn()
        {
            if (AdvanceToNextPlayableHand())
            {
                Emit(new PlayerTurnStarted(CurrentBoxIndex, CurrentHandIndex));
            }
            else
            {
                BeginDealerTurn();
            }
        }

        /// <summary>
        /// Walks boxes left-to-right and, within a box, hands in order. A split mid-turn
        /// simply extends the walk, because the new hand is inserted after the current one.
        /// </summary>
        private bool AdvanceToNextPlayableHand()
        {
            for (int b = 0; b < _boxes.Count; b++)
            {
                Box box = _boxes[b];
                if (!box.IsActive)
                {
                    continue;
                }

                for (int h = 0; h < box.Hands.Count; h++)
                {
                    Hand hand = box.Hands[h];
                    if (hand.IsClosed || hand.IsBust)
                    {
                        continue;
                    }

                    // A natural stands automatically — the player never acts on it.
                    if (hand.IsBlackjack)
                    {
                        hand.Close();
                        continue;
                    }

                    CurrentBoxIndex = b;
                    CurrentHandIndex = h;

                    // A split ace has no legal actions; close it and keep walking.
                    if (LegalActions.Count == 0)
                    {
                        hand.Close();
                        continue;
                    }

                    return true;
                }
            }

            CurrentBoxIndex = -1;
            CurrentHandIndex = -1;
            return false;
        }
    }
}
