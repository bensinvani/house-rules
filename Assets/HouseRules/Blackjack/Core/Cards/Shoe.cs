using System;
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed class Shoe : IShoe
    {
        private readonly List<Card> _cards;
        private readonly IRandom _random;
        private readonly int _reshuffleAt;
        private int _index;

        public Shoe(int deckCount, double penetration, IRandom random)
        {
            if (deckCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deckCount), "Deck count must be positive.");
            }

            if (penetration <= 0.0 || penetration > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(penetration), "Penetration must be in (0, 1].");
            }

            _random = random ?? throw new ArgumentNullException(nameof(random));
            _cards = new List<Card>(deckCount * 52);

            for (int deck = 0; deck < deckCount; deck++)
            {
                foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                {
                    foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    {
                        _cards.Add(new Card(rank, suit));
                    }
                }
            }

            _reshuffleAt = (int)(_cards.Count * penetration);
            Shuffle();
        }

        public int Remaining => _cards.Count - _index;

        public bool NeedsReshuffle => _index >= _reshuffleAt;

        public Card Deal()
        {
            if (_index >= _cards.Count)
            {
                throw new InvalidOperationException("Shoe is exhausted. Reshuffle before dealing.");
            }

            return _cards[_index++];
        }

        public void Reshuffle()
        {
            _index = 0;
            Shuffle();
        }

        private void Shuffle()
        {
            // Fisher-Yates.
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                Card swap = _cards[i];
                _cards[i] = _cards[j];
                _cards[j] = swap;
            }
        }
    }
}
