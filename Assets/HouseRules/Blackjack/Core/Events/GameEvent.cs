namespace HouseRules.Blackjack
{
    public abstract class GameEvent
    {
    }

    public sealed class RoundStarted : GameEvent
    {
    }

    public sealed class ShoeReshuffled : GameEvent
    {
    }

    public sealed class InsuranceOffered : GameEvent
    {
    }

    public sealed class PlayerTurnStarted : GameEvent
    {
        public PlayerTurnStarted(int boxIndex, int handIndex)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
    }

    public sealed class CardDealt : GameEvent
    {
        /// <summary>Box index used to mean "the dealer" rather than a player box.</summary>
        public const int DealerBoxIndex = -1;

        public CardDealt(int boxIndex, int handIndex, Card card, bool faceUp)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
            Card = card;
            FaceUp = faceUp;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
        public Card Card { get; }
        public bool FaceUp { get; }
    }
}
