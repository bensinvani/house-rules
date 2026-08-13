using System.Collections;
using System.Collections.Generic;
using System.Text;
using HouseRules.Blackjack;
using UnityEngine;
using UnityEngine.UI;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Renders the engine's event stream as a scrolling text log. Deliberately the
    /// simplest possible IEventPresenter: it makes the game playable before any art
    /// exists, and proves the session/sequencer wiring independently of layout.
    /// </summary>
    public sealed class TextEventPresenter : MonoBehaviour, IEventPresenter
    {
        private const int MaxLines = 18;

        private readonly Queue<string> _lines = new Queue<string>();
        private Text _target;

        /// <summary>Seconds each event lingers, so a deal reads as a sequence rather than a blur.</summary>
        public float SecondsPerEvent { get; set; } = 0.18f;

        public void SetTarget(Text target) => _target = target;

        public IEnumerator Present(GameEvent gameEvent)
        {
            string line = Describe(gameEvent);

            if (!string.IsNullOrEmpty(line))
            {
                Append(line);
                yield return Tween.Wait(SecondsPerEvent);
            }
        }

        private void Append(string line)
        {
            _lines.Enqueue(line);

            while (_lines.Count > MaxLines)
            {
                _lines.Dequeue();
            }

            if (_target == null)
            {
                return;
            }

            var builder = new StringBuilder();
            foreach (string existing in _lines)
            {
                builder.AppendLine(existing);
            }

            _target.text = builder.ToString();
        }

        public void Clear()
        {
            _lines.Clear();
            if (_target != null)
            {
                _target.text = string.Empty;
            }
        }

        private static string Describe(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case RoundStarted _:
                    return "--- new round ---";
                case ShoeReshuffled _:
                    return "shoe reshuffled";
                case CardDealt dealt:
                    return DescribeCard(dealt);
                case PlayerTurnStarted turn:
                    return $"your turn: box {turn.BoxIndex + 1}, hand {turn.HandIndex + 1}";
                case HandStood stood:
                    return $"stand (box {stood.BoxIndex + 1})";
                case HandBusted busted:
                    return $"BUST (box {busted.BoxIndex + 1})";
                case HandDoubled doubled:
                    return $"double to {doubled.NewWager} (box {doubled.BoxIndex + 1})";
                case HandSplit split:
                    return $"split (box {split.BoxIndex + 1})";
                case InsuranceOffered _:
                    return "insurance offered";
                case InsuranceTaken taken:
                    return $"insurance taken: {taken.Amount}";
                case InsuranceDeclined _:
                    return "insurance declined";
                case InsuranceSettled insurance:
                    return $"insurance {(insurance.Delta >= 0 ? "+" : string.Empty)}{insurance.Delta}";
                case DealerRevealed revealed:
                    return $"dealer reveals {Short(revealed.HoleCard)}";
                case HandSettled settled:
                    return DescribeSettlement(settled.Settlement);
                case RoundSettled round:
                    return $"=== round net {(round.TotalDelta >= 0 ? "+" : string.Empty)}{round.TotalDelta} ===";
                case RoundAbandoned abandoned:
                    return $"round abandoned, {abandoned.Refunded} refunded";
                default:
                    return null;
            }
        }

        private static string DescribeCard(CardDealt dealt)
        {
            string who = dealt.BoxIndex == CardDealt.DealerBoxIndex
                ? "dealer"
                : $"box {dealt.BoxIndex + 1}";

            return dealt.FaceUp
                ? $"{who}: {Short(dealt.Card)}"
                : $"{who}: [face down]";
        }

        private static string DescribeSettlement(Settlement settlement)
        {
            string sign = settlement.Delta >= 0 ? "+" : string.Empty;
            return $"box {settlement.BoxIndex + 1}: {settlement.Outcome} {sign}{settlement.Delta}";
        }

        private static string Short(Card card)
        {
            string rank;
            switch (card.Rank)
            {
                case Rank.Ace: rank = "A"; break;
                case Rank.King: rank = "K"; break;
                case Rank.Queen: rank = "Q"; break;
                case Rank.Jack: rank = "J"; break;
                case Rank.Ten: rank = "10"; break;
                default: rank = ((int)card.Rank).ToString(); break;
            }

            string suit;
            switch (card.Suit)
            {
                case Suit.Clubs: suit = "♣"; break;
                case Suit.Diamonds: suit = "♦"; break;
                case Suit.Hearts: suit = "♥"; break;
                default: suit = "♠"; break;
            }

            return rank + suit;
        }
    }
}
