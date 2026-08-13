using System;
using System.Collections.Generic;
using HouseRules.Blackjack;
using UnityEngine;
using UnityEngine.UI;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Renders one button per player action and enables them strictly from
    /// session.LegalActions. It holds ZERO rules of its own — if an action is not
    /// in that list the button is not interactable, which is what makes a
    /// double-tap during a deal animation impossible.
    /// </summary>
    public sealed class ActionBarView : MonoBehaviour
    {
        private readonly Dictionary<PlayerAction, Button> _buttons = new Dictionary<PlayerAction, Button>();
        private BlackjackSession _session;
        private Button _dealButton;
        private Button _betButton;

        public void Bind(BlackjackSession session) => _session = session;

        public void Register(PlayerAction action, Button button)
        {
            _buttons[action] = button;
            PlayerAction captured = action;
            button.onClick.AddListener(() => _session?.Apply(captured));
        }

        public void RegisterDeal(Button button, Action onDeal)
        {
            _dealButton = button;
            button.onClick.AddListener(() => onDeal?.Invoke());
        }

        public void RegisterBet(Button button, Action onBet)
        {
            _betButton = button;
            button.onClick.AddListener(() => onBet?.Invoke());
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            IReadOnlyList<PlayerAction> legal = _session.LegalActions;

            foreach (KeyValuePair<PlayerAction, Button> pair in _buttons)
            {
                pair.Value.interactable = Contains(legal, pair.Key);
            }

            bool betting = _session.CanAcceptInput && _session.State == RoundState.Betting;
            bool anyBet = betting && AnyBoxActive();

            if (_betButton != null)
            {
                _betButton.interactable = betting;
            }

            if (_dealButton != null)
            {
                _dealButton.interactable = anyBet;
            }
        }

        private bool AnyBoxActive()
        {
            Round round = _session.CurrentRound;
            if (round == null)
            {
                return false;
            }

            foreach (Box box in round.Boxes)
            {
                if (box.IsActive)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(IReadOnlyList<PlayerAction> list, PlayerAction action)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == action)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
