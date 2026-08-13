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
        public static readonly Vector3 CardSize = new Vector3(0.63f, 0.02f, 0.88f);

        // Visual Quality Bar palette tokens (docs/superpowers/plans/2026-08-13-blackjack-visuals.md).
        // No pure white/black anywhere in the game, cards included.
        private static readonly Color CardFace = new Color(0.976f, 0.973f, 0.957f, 1f);
        private static readonly Color CardInk = new Color(0.106f, 0.106f, 0.118f, 1f);
        private static readonly Color CardRed = new Color(0.729f, 0.129f, 0.129f, 1f);

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
            // Non-uniform because the child is rotated flat: local X stays the card's
            // width axis, local Y becomes the card's length axis after the rotation.
            // TextMesh's legacy runtime font scales its mesh by fontSize, not just
            // characterSize, so a small fontSize plus this modest localScale is what
            // keeps two lines of text inside the 0.63 x 0.88 face instead of ballooning
            // off the card (verified empirically via mesh bounds, not by eye alone).
            textGo.transform.localScale = new Vector3(1.0f, 0.35f, 1.0f);

            var text = textGo.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 20;
            text.characterSize = 0.5f;
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
                _faceText.color = IsRed(Card.Suit) ? CardRed : CardInk;
            }

            if (_renderer != null)
            {
                _renderer.material.color = faceUp ? CardFace : CardInk;
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

                if (!swapped && t >= 0.5f)
                {
                    swapped = true;
                    SetFaceUp(!IsFaceUp);
                }

                yield return null;
            }

            transform.rotation = to;
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
