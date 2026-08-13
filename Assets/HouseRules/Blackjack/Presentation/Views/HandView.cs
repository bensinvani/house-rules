using System.Collections.Generic;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>Fans cards within one hand. Slot positions are a pure function of index.</summary>
    public sealed class HandView : MonoBehaviour
    {
        private const float FanStepX = 0.22f;
        private const float FanStepZ = -0.06f;
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
