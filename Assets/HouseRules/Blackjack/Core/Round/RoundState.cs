namespace HouseRules.Blackjack
{
    /// <summary>
    /// The lifecycle state of a <see cref="Round"/>.
    /// <para>
    /// <see cref="Dealing"/>, <see cref="DealerTurn"/>, and <see cref="Settlement"/> are
    /// transient: each is set and then replaced again within the same synchronous call
    /// (<see cref="Round.Deal"/> or the action that ends the player's turn), so an external
    /// caller observing <see cref="Round.State"/> between calls never sees them. Only
    /// <see cref="Betting"/>, <see cref="Insurance"/>, <see cref="PlayerTurn"/>, and
    /// <see cref="Complete"/> are externally visible states. A presentation layer writing a
    /// switch over this enum should treat the transient members as dead code.
    /// </para>
    /// </summary>
    public enum RoundState
    {
        Betting,
        Dealing,
        Insurance,
        PlayerTurn,
        DealerTurn,
        Settlement,
        Complete
    }
}
