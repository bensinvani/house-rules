using System;

namespace HouseRules.Blackjack
{
    public readonly struct Card : IEquatable<Card>
    {
        public Rank Rank { get; }
        public Suit Suit { get; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        /// <summary>
        /// Value before any ace demotion. Aces count 11 here; <see cref="Hand"/> demotes them.
        /// </summary>
        public int BaseValue
        {
            get
            {
                switch (Rank)
                {
                    case Rank.Ace:
                        return 11;
                    case Rank.Jack:
                    case Rank.Queen:
                    case Rank.King:
                        return 10;
                    default:
                        return (int)Rank;
                }
            }
        }

        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;

        public override bool Equals(object obj) => obj is Card other && Equals(other);

        public override int GetHashCode() => ((int)Rank * 4) + (int)Suit;

        public override string ToString() => $"{Rank} of {Suit}";
    }
}
