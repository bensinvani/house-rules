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
}
