using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    /// <summary>
    /// One betting position. Holds the original bet and one or more hands —
    /// splitting is what turns a single hand into several.
    /// </summary>
    public sealed class Box
    {
        private readonly List<Hand> _hands = new List<Hand>();

        public Box(int index)
        {
            Index = index;
        }

        public int Index { get; }

        public long InitialBet { get; private set; }

        public IReadOnlyList<Hand> Hands => _hands;

        public bool IsActive => InitialBet > 0;

        /// <summary>Number of splits performed. Hand count is always SplitCount + 1.</summary>
        public int SplitCount => _hands.Count - 1;

        /// <summary>Insurance side bet, or 0 if none was taken.</summary>
        public long InsuranceBet { get; private set; }

        internal void SetInitialBet(long wager) => InitialBet = wager;

        internal void SetInsuranceBet(long amount) => InsuranceBet = amount;

        internal void AddHand(Hand hand) => _hands.Add(hand);

        internal void InsertHandAfter(int index, Hand hand) => _hands.Insert(index + 1, hand);

        internal void ReplaceHand(int index, Hand hand) => _hands[index] = hand;
    }
}
