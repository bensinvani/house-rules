using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    // TEMPORARY: replaced in Tasks 9-13.
    public sealed partial class Round
    {
        private readonly List<GameEvent> _events = new List<GameEvent>();

        private void Emit(GameEvent gameEvent) => _events.Add(gameEvent);

        private void RevealAndSettleDealerBlackjack() => SetState(RoundState.Complete);

        private void BeginDealerTurn() => SetState(RoundState.DealerTurn);

        private void ApplyInsurance(bool taken) => throw new System.NotImplementedException();
    }
}
