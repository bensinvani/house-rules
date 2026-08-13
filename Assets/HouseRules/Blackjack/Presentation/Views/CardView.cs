using System.Collections;
using HouseRules.Blackjack;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// One card on the felt: a thin box with its rank and suit rendered on the face.
    /// Pooled — a 312-card shoe never puts more than a handful in play at once.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        public static readonly Vector3 CardSize = new Vector3(0.95f, 0.02f, 1.33f);

        private TextMesh _faceText;
        private Renderer _renderer;

        public Card Card { get; private set; }

        public bool IsFaceUp { get; private set; }

        public static CardView Create()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Card";
            root.transform.localScale = CardSize;

            Object.Destroy(root.GetComponent<BoxCollider>());

            var view = root.AddComponent<CardView>();
            view._renderer = root.GetComponent<Renderer>();

            var textGo = new GameObject("Face");
            textGo.transform.SetParent(root.transform, false);
            // Lift slightly above the top face and lie flat, readable from a top-down camera.
            textGo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            textGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // The card root carries a non-uniform scale (CardSize), and after the 90-degree
            // rotation above, local X still maps to world X (card width) but local Y now maps
            // to world Z (card length) instead of Y. Dividing by those two components cancels
            // the parent's non-uniform scale exactly, so the TextMesh below renders at its own
            // literal characterSize/fontSize in world units — undistorted and independent of
            // CardSize — instead of compounding with it (confirmed via capture: the previous
            // fixed 0.35 multiplier here silently relied on one specific CardSize and, combined
            // with it, sheared every digit into an unreadable diagonal smear).
            textGo.transform.localScale = new Vector3(1f / CardSize.x, 1f / CardSize.z, 1f / CardSize.x);

            var text = textGo.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            // characterSize renders in world units independent of CardSize (see the scale
            // comment above), so it does not grow for free when CardSize does. Scaled up by
            // the same ~1.51x CardSize grew (0.63->0.95) to keep the glyph filling the face.
            text.characterSize = 0.075f;
            view._faceText = text;

            return view;
        }

        public void Show(Card card, bool faceUp)
        {
            Card = card;
            SetFaceUp(faceUp);
        }

        public void SetFaceUp(bool faceUp)
        {
            IsFaceUp = faceUp;

            if (_faceText != null)
            {
                _faceText.gameObject.SetActive(faceUp);
                _faceText.text = Label(Card);
                _faceText.color = IsRed(Card.Suit) ? Palette.CardRed : Palette.CardInk;
            }

            if (_renderer != null)
            {
                // Face-down body uses the CardBack token, not CardInk — a face-down card
                // should read as navy card stock, not as a shadow.
                _renderer.material.color = faceUp ? Palette.CardFace : Palette.CardBack;
            }
        }

        /// <summary>Rotates 180 degrees about the long axis, swapping the face at the midpoint.</summary>
        public IEnumerator Flip(float duration)
        {
            Quaternion from = transform.rotation;
            Quaternion to = from * Quaternion.Euler(0f, 0f, 180f);

            float elapsed = 0f;
            bool swapped = false;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Easing.Clamp01(elapsed / duration);
                transform.rotation = Quaternion.SlerpUnclamped(from, to, Easing.InOutCubic(t));
                AlignFaceUpright();

                if (!swapped && t >= 0.5f)
                {
                    swapped = true;
                    SetFaceUp(!IsFaceUp);
                }

                yield return null;
            }

            transform.rotation = to;
            AlignFaceUpright();
        }

        /// <summary>
        /// The card body ends every other flip permanently rotated 180 degrees about Z — fine
        /// for the body, which is a uniformly-coloured box with no "back" mesh to reveal, but
        /// the Face child inherits that spin too, which mirrors its rank/suit into unreadable
        /// junk (a "2" mirrors into something that reads like "S"). Re-pinning the Face to a
        /// fixed world rotation each frame keeps it lying flat and right-reading regardless of
        /// how many times this card has been flipped.
        /// </summary>
        private void AlignFaceUpright()
        {
            if (_faceText != null)
            {
                _faceText.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private static bool IsRed(Suit suit) => suit == Suit.Diamonds || suit == Suit.Hearts;

        private static string Label(Card card)
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

            return rank + "\n" + suit;
        }
    }
}
