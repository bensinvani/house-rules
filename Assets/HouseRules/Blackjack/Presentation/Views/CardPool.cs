using System.Collections.Generic;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Recycles card views. Instantiating and destroying primitives every deal
    /// produces avoidable GC churn on a phone; a handful of reused objects does not.
    /// </summary>
    public sealed class CardPool : MonoBehaviour
    {
        private readonly Stack<CardView> _idle = new Stack<CardView>();
        private readonly List<CardView> _live = new List<CardView>();

        public int LiveCount => _live.Count;

        public int IdleCount => _idle.Count;

        public CardView Rent()
        {
            CardView view = _idle.Count > 0 ? _idle.Pop() : CardView.Create();
            view.transform.SetParent(transform, false);
            view.gameObject.SetActive(true);
            _live.Add(view);
            return view;
        }

        public void Return(CardView view)
        {
            if (view == null || !_live.Remove(view))
            {
                return;
            }

            view.gameObject.SetActive(false);
            _idle.Push(view);
        }

        public void ReturnAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                CardView view = _live[i];
                view.gameObject.SetActive(false);
                _idle.Push(view);
            }

            _live.Clear();
        }
    }
}
