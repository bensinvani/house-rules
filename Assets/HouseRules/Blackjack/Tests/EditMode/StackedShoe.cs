using System;
using System.Collections.Generic;

namespace HouseRules.Blackjack.Tests
{
    /// <summary>
    /// Deals a scripted sequence, so a test can arrange an exact scenario.
    /// Once the script runs out it deals a repeating filler rank, which keeps
    /// tests that only care about the opening cards short.
    /// </summary>
    public sealed class StackedShoe : IShoe
    {
        private readonly List<Card> _scripted;
        private readonly Card _filler;
        private int _index;

        public StackedShoe(params Card[] scripted)
            : this(new Card(Rank.Five, Suit.Clubs), scripted)
        {
        }

        public StackedShoe(Card filler, params Card[] scripted)
        {
            _scripted = new List<Card>(scripted ?? Array.Empty<Card>());
            _filler = filler;
        }

        public int Remaining => int.MaxValue;

        public bool NeedsReshuffle => false;

        public Card Deal()
        {
            if (_index < _scripted.Count)
            {
                return _scripted[_index++];
            }

            _index++;
            return _filler;
        }

        public void Reshuffle() => _index = 0;

        /// <summary>Number of cards dealt so far, including filler.</summary>
        public int DealtCount => _index;
    }
}
