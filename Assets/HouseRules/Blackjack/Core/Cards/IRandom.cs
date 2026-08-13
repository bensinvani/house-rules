namespace HouseRules.Blackjack
{
    /// <summary>
    /// The single source of randomness in the engine. Seeded so any round replays exactly.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Returns a value in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }
}
