using HouseRules.Blackjack;
using UnityEngine;
using UnityEngine.UI;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>Displays the chip balance. Polls, because the wallet raises no events.</summary>
    public sealed class WalletView : MonoBehaviour
    {
        private Wallet _wallet;
        private Text _target;
        private long _lastShown = long.MinValue;

        public void Bind(Wallet wallet, Text target)
        {
            _wallet = wallet;
            _target = target;
            _lastShown = long.MinValue;
        }

        private void Update()
        {
            if (_wallet == null || _target == null)
            {
                return;
            }

            if (_wallet.Balance == _lastShown)
            {
                return;
            }

            _lastShown = _wallet.Balance;
            _target.text = $"Chips: {_wallet.Balance}";
        }
    }
}
