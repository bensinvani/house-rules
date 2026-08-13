using System.Collections.Generic;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// One betting position. Owns up to four hand views, because a box can split
    /// three times — they are created lazily as splits occur.
    /// </summary>
    public sealed class BoxView : MonoBehaviour
    {
        // Widened alongside CardView.CardSize so split hands stay clearly separated at the
        // larger card scale.
        private const float SplitOffsetX = 1.7f;

        private readonly List<HandView> _hands = new List<HandView>();

        public int HandCount => _hands.Count;

        public HandView HandAt(int handIndex)
        {
            while (_hands.Count <= handIndex)
            {
                var go = new GameObject($"Hand{_hands.Count}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(_hands.Count * SplitOffsetX, 0f, 0f);
                _hands.Add(go.AddComponent<HandView>());
            }

            return _hands[handIndex];
        }

        public void Clear()
        {
            foreach (HandView hand in _hands)
            {
                hand.Clear();
            }
        }
    }
}
