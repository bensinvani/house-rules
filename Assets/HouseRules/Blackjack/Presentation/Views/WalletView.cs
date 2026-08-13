using HouseRules.Blackjack;
using UnityEngine;
using UnityEngine.UI;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Displays the chip balance. Polls, because the wallet raises no events. This is the
    /// SOLE writer of its target Text — the bootstrap must never assign _status.text itself,
    /// or the two writers race and the balance renders doubled/overlapping.
    /// </summary>
    public sealed class WalletView : MonoBehaviour
    {
        private static readonly string AccentHex = ColorUtility.ToHtmlStringRGB(Palette.Accent);

        private Wallet _wallet;
        private Text _target;
        private long _lastShown = long.MinValue;

        public void Bind(Wallet wallet, Text target)
        {
            _wallet = wallet;
            _target = target;
            _lastShown = long.MinValue;

            // Paint once immediately so the balance is never blank before the first Update tick.
            Refresh();
        }

        private void Update() => Refresh();

        private void Refresh()
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
            // The amount is what players care about, so it carries the Accent token; the
            // "Chips:" label stays TextPrimary (the Text component's own colour).
            _target.text = $"Chips: <color=#{AccentHex}>{_wallet.Balance}</color>";
        }
    }
}
