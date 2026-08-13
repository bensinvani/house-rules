using System.Collections;
using HouseRules.Blackjack;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Turns the engine's event stream into card motion. Every method here performs
    /// something the engine already decided — no rule is evaluated in this file.
    /// </summary>
    public sealed class TableCardPresenter : MonoBehaviour, IEventPresenter
    {
        private const float DealDuration = 0.26f;
        private const float FlipDuration = 0.22f;
        private const float BeatDuration = 0.35f;

        private TableView _table;
        private CardPool _pool;
        private TextEventPresenter _log;

        public void Bind(TableView table, CardPool pool, TextEventPresenter log)
        {
            _table = table;
            _pool = pool;
            _log = log;
        }

        public IEnumerator Present(GameEvent gameEvent)
        {
            // The text log stays alive as a secondary readout, but it must never consume
            // sequencer time itself — Log() is the non-yielding entry point for exactly that.
            _log?.Log(gameEvent);

            switch (gameEvent)
            {
                case RoundStarted _:
                    _table.ClearAll();
                    _pool.ReturnAll();
                    yield break;

                case CardDealt dealt:
                    yield return DealCard(dealt);
                    yield break;

                case DealerRevealed _:
                    yield return RevealHoleCard();
                    yield break;

                case HandSplit split:
                    yield return MoveSplitCard(split);
                    yield break;

                case RoundSettled _:
                    yield return Tween.Wait(BeatDuration * 2f);
                    yield break;

                case HandBusted _:
                case HandSettled _:
                    yield return Tween.Wait(BeatDuration);
                    yield break;

                default:
                    yield break;
            }
        }

        private HandView HandFor(int boxIndex, int handIndex)
        {
            return boxIndex == CardDealt.DealerBoxIndex
                ? _table.DealerHand
                : _table.BoxAt(boxIndex).HandAt(handIndex);
        }

        private IEnumerator DealCard(CardDealt dealt)
        {
            HandView hand = HandFor(dealt.BoxIndex, dealt.HandIndex);

            // The betting-circle marker sits exactly where hand 0's first card lands (that's the
            // box's betting spot). Hide it the instant that card is dealt, before it starts
            // occluding the marker unreliably — see TableView.HideBettingMarker's remarks.
            if (dealt.BoxIndex != CardDealt.DealerBoxIndex && dealt.HandIndex == 0 && hand.Count == 0)
            {
                _table.HideBettingMarker(dealt.BoxIndex);
            }

            CardView view = _pool.Rent();
            view.transform.position = _table.ShoePosition;
            view.transform.rotation = Quaternion.identity;
            view.Show(dealt.Card, faceUp: false);

            Vector3 destination = hand.SlotPosition(hand.Count);
            hand.Add(view);

            yield return Tween.Move(view.transform, destination, DealDuration, Easing.OutCubic);

            if (dealt.FaceUp)
            {
                yield return view.Flip(FlipDuration);
            }
        }

        private IEnumerator RevealHoleCard()
        {
            HandView dealer = _table.DealerHand;

            foreach (CardView view in dealer.Cards)
            {
                if (!view.IsFaceUp)
                {
                    yield return view.Flip(FlipDuration);
                    yield break;
                }
            }
        }

        private IEnumerator MoveSplitCard(HandSplit split)
        {
            BoxView box = _table.BoxAt(split.BoxIndex);
            HandView source = box.HandAt(split.HandIndex);
            HandView target = box.HandAt(split.NewHandIndex);

            if (source.Count < 2)
            {
                yield break;
            }

            CardView moved = source.Cards[source.Count - 1];
            source.Remove(moved);
            target.Add(moved);

            yield return Tween.Move(
                moved.transform, target.SlotPosition(0), DealDuration, Easing.OutCubic);
        }
    }
}
