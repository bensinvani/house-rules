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
}
