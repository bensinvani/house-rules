using System;
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        private readonly BlackjackRules _rules;
        private readonly IShoe _shoe;
        private readonly Wallet _wallet;
        private readonly List<Box> _boxes = new List<Box>();
        private readonly List<GameEvent> _events = new List<GameEvent>();

        public Round(BlackjackRules rules, IShoe shoe, Wallet wallet)
        {
            _rules = rules;
            _shoe = shoe ?? throw new ArgumentNullException(nameof(shoe));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

            for (int i = 0; i < rules.MaxBoxes; i++)
            {
                _boxes.Add(new Box(i));
            }

            State = RoundState.Betting;
            DealerHand = new Hand(0);
        }

        public RoundState State { get; private set; }

        public IReadOnlyList<Box> Boxes => _boxes;

        public Hand DealerHand { get; }

        public BlackjackRules Rules => _rules;

        /// <summary>The dealer's face-up card. Only meaningful once dealing has completed.</summary>
        public Card DealerUpcard => DealerHand.Cards.Count > 0
            ? DealerHand.Cards[0]
            : throw new InvalidOperationException("Dealer has no cards yet.");

        public void PlaceBet(int boxIndex, long wager)
        {
            if (State != RoundState.Betting)
            {
                throw new InvalidOperationException($"Cannot bet in state {State}.");
            }

            if (boxIndex < 0 || boxIndex >= _boxes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(boxIndex));
            }

            if (wager < _rules.MinimumBet)
            {
                throw new ArgumentException(
                    $"Wager {wager} is below the minimum of {_rules.MinimumBet}.", nameof(wager));
            }

            if (wager % _rules.BetIncrement != 0)
            {
                throw new ArgumentException(
                    $"Wager {wager} must be a multiple of {_rules.BetIncrement} so a 3:2 payout is exact.",
                    nameof(wager));
            }

            Box box = _boxes[boxIndex];
            if (box.IsActive)
            {
                throw new InvalidOperationException($"Box {boxIndex} already has a bet.");
            }

            if (!_wallet.CanAfford(wager))
            {
                throw new InvalidOperationException(
                    $"Cannot afford {wager} with a balance of {_wallet.Balance}.");
            }

            _wallet.Debit(wager);
            box.SetInitialBet(wager);
            box.AddHand(new Hand(wager));
        }

        private void SetState(RoundState state) => State = state;

        private void Emit(GameEvent gameEvent) => _events.Add(gameEvent);
    }
}
