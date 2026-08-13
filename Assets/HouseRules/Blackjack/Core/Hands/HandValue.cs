namespace HouseRules.Blackjack
{
    public readonly struct HandValue
    {
        public int Total { get; }

        /// <summary>True when an ace is still counting as 11.</summary>
        public bool IsSoft { get; }

        public HandValue(int total, bool isSoft)
        {
            Total = total;
            IsSoft = isSoft;
        }

        public bool IsBust => Total > 21;

        public override string ToString() => IsSoft ? $"soft {Total}" : Total.ToString();
    }
}
