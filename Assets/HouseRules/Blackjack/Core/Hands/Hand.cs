using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed class Hand
    {
        private readonly List<Card> _cards = new List<Card>();

        public Hand(long wager, bool isFromSplit = false)
        {
            Wager = wager;
            IsFromSplit = isFromSplit;
        }

        public IReadOnlyList<Card> Cards => _cards;

        public long Wager { get; private set; }

        /// <summary>True when this hand was produced by splitting another.</summary>
        public bool IsFromSplit { get; }

        public bool IsDoubled { get; private set; }

        /// <summary>True once the hand can take no further action (stood, doubled, busted, or a split ace).</summary>
        public bool IsClosed { get; private set; }

        internal void Add(Card card) => _cards.Add(card);

        internal void SetWager(long wager) => Wager = wager;

        internal void MarkDoubled() => IsDoubled = true;

        internal void Close() => IsClosed = true;

        public HandValue Value
        {
            get
            {
                int total = 0;
                int aces = 0;

                foreach (var card in _cards)
                {
                    total += card.BaseValue;
                    if (card.Rank == Rank.Ace)
                    {
                        aces++;
                    }
                }

                // Demote aces from 11 to 1 until the hand fits, or we run out of aces.
                while (total > 21 && aces > 0)
                {
                    total -= 10;
                    aces--;
                }

                return new HandValue(total, aces > 0);
            }
        }

        public bool IsBust => Value.IsBust;

        /// <summary>A natural 21 on the first two cards. A 21 formed after a split does not count.</summary>
        public bool IsBlackjack => _cards.Count == 2 && Value.Total == 21 && !IsFromSplit;

        public bool IsPair => _cards.Count == 2 && _cards[0].Rank == _cards[1].Rank;

        public override string ToString() => string.Join(", ", _cards);
    }
}
