namespace HouseRules.Blackjack
{
    public interface IShoe
    {
        Card Deal();
        int Remaining { get; }
        bool NeedsReshuffle { get; }
        void Reshuffle();
    }
}
