using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    // TEMPORARY: replaced in Tasks 7-13. Exists only so Task 6 compiles and tests run.
    public sealed partial class Round
    {
        private readonly List<GameEvent> _events = new List<GameEvent>();

        private void Emit(GameEvent gameEvent) => _events.Add(gameEvent);

        private void RevealAndSettleDealerBlackjack() => SetState(RoundState.Complete);

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
                    if (!box.Hands[h].IsClosed)
                    {
                        CurrentBoxIndex = b;
                        CurrentHandIndex = h;
                        return true;
                    }
                }
            }

            CurrentBoxIndex = -1;
            CurrentHandIndex = -1;
            return false;
        }

        private void BeginDealerTurn() => SetState(RoundState.DealerTurn);
    }
}
