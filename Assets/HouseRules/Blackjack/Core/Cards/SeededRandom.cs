using System;

namespace HouseRules.Blackjack
{
    public sealed class SeededRandom : IRandom
    {
        private readonly Random _random;

        public SeededRandom(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Seed { get; }

        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }
}
