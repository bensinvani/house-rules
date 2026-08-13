namespace HouseRules.Blackjack
{
    /// <summary>
    /// The result of one hand. The wager was debited when the bet was placed, so
    /// <see cref="Payout"/> is what gets credited back and <see cref="Delta"/> is the net change.
    /// </summary>
    public sealed class Settlement
    {
        public Settlement(int boxIndex, int handIndex, HandOutcome outcome, long wager, long payout)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
            Outcome = outcome;
            Wager = wager;
            Payout = payout;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
        public HandOutcome Outcome { get; }
        public long Wager { get; }
        public long Payout { get; }
        public long Delta => Payout - Wager;
    }
}
