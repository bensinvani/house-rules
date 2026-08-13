namespace HouseRules.Blackjack
{
    public sealed class HandBusted : GameEvent
    {
        public HandBusted(int boxIndex, int handIndex)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
    }

    public sealed class HandStood : GameEvent
    {
        public HandStood(int boxIndex, int handIndex)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
    }

    public sealed class HandDoubled : GameEvent
    {
        public HandDoubled(int boxIndex, int handIndex, long newWager)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
            NewWager = newWager;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
        public long NewWager { get; }
    }

    public sealed class HandSplit : GameEvent
    {
        public HandSplit(int boxIndex, int handIndex, int newHandIndex)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
            NewHandIndex = newHandIndex;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
        public int NewHandIndex { get; }
    }

    public sealed class InsuranceTaken : GameEvent
    {
        public InsuranceTaken(int boxIndex, long amount)
        {
            BoxIndex = boxIndex;
            Amount = amount;
        }

        public int BoxIndex { get; }
        public long Amount { get; }
    }

    public sealed class InsuranceDeclined : GameEvent
    {
    }

    public sealed class DealerRevealed : GameEvent
    {
        public DealerRevealed(Card holeCard)
        {
            HoleCard = holeCard;
        }

        public Card HoleCard { get; }
    }

    public sealed class HandSettled : GameEvent
    {
        public HandSettled(Settlement settlement)
        {
            Settlement = settlement;
        }

        public Settlement Settlement { get; }
    }

    public sealed class RoundSettled : GameEvent
    {
        public RoundSettled(long totalDelta)
        {
            TotalDelta = totalDelta;
        }

        public long TotalDelta { get; }
    }

    public sealed class InsuranceSettled : GameEvent
    {
        public InsuranceSettled(int boxIndex, bool dealerHadBlackjack, long delta)
        {
            BoxIndex = boxIndex;
            DealerHadBlackjack = dealerHadBlackjack;
            Delta = delta;
        }

        public int BoxIndex { get; }
        public bool DealerHadBlackjack { get; }

        /// <summary>Net change from the insurance side bet: +2x premium on a dealer natural, -premium otherwise.</summary>
        public long Delta { get; }
    }

    public sealed class RoundAbandoned : GameEvent
    {
        public RoundAbandoned(long refunded)
        {
            Refunded = refunded;
        }

        /// <summary>Total chips returned to the wallet: every hand's wager plus every insurance premium.</summary>
        public long Refunded { get; }
    }
}
