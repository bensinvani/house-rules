using System;
using System.Collections.Generic;
using HouseRules.Blackjack;

namespace HouseRules.Blackjack.PlayModeTests
{
    /// <summary>
    /// Deals a scripted sequence, then a repeating filler card. Mirrors the EditMode
    /// StackedShoe; duplicated rather than shared because the two test assemblies
    /// cannot reference one another.
    /// </summary>
    public sealed class ScriptedShoe : IShoe
    {
        private readonly List<Card> _scripted;
        private readonly Card _filler = new Card(Rank.Five, Suit.Clubs);
        private int _index;

        public ScriptedShoe(params Card[] scripted)
        {
            _scripted = new List<Card>(scripted ?? Array.Empty<Card>());
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
    }
}
