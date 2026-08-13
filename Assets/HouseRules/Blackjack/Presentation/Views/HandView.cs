using System.Collections.Generic;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>Fans cards within one hand. Slot positions are a pure function of index.</summary>
    public sealed class HandView : MonoBehaviour
    {
        // Widened alongside CardSize (0.63->0.95, ~1.5x) so each card's rank corner stays
        // clear of the next card instead of overlapping into a smudge.
        private const float FanStepX = 0.42f;
        private const float FanStepZ = -0.09f;
        private const float StackLift = 0.025f;

        private readonly List<CardView> _cards = new List<CardView>();

        public int Count => _cards.Count;

        public IReadOnlyList<CardView> Cards => _cards;

        public Vector3 SlotPosition(int cardIndex)
        {
            return transform.position + new Vector3(
                cardIndex * FanStepX,
                cardIndex * StackLift,
                cardIndex * FanStepZ);
        }

        public void Add(CardView view) => _cards.Add(view);

        public void Remove(CardView view) => _cards.Remove(view);

        public void Clear() => _cards.Clear();
    }
}
