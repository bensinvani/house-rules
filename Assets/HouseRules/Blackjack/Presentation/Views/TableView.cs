using System.Collections.Generic;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>Scene root: felt, box anchors, the dealer's hand, and the shoe position.</summary>
    public sealed class TableView : MonoBehaviour
    {
        // BoxView.SplitOffsetX (1.15) is the distance between a box's split hands, and a box
        // can hold up to 4 of them (three splits), so a split-out box spans up to
        // SplitOffsetX * 3 = 3.45 units from its anchor. 4.2 gives that a 0.75 margin before
        // it reaches the neighbouring box's own anchor, so two fully split boxes never overlap.
        // (Previously 2.6, which is less than 3.45 — a confirmed overlap defect.)
        private const float BoxSpacingX = 4.2f;
        private const float PlayerRowZ = -1.9f;
        private const float DealerRowZ = 1.7f;

        private readonly List<BoxView> _boxes = new List<BoxView>();

        public HandView DealerHand { get; private set; }

        public Vector3 ShoePosition { get; private set; }

        public int BoxCount => _boxes.Count;

        public void Build(int boxCount)
        {
            CreateFelt();

            float firstX = -((boxCount - 1) * BoxSpacingX) / 2f;

            for (int i = 0; i < boxCount; i++)
            {
                var boxLocalPosition = new Vector3(firstX + (i * BoxSpacingX), 0f, PlayerRowZ);

                var go = new GameObject($"Box{i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = boxLocalPosition;
                _boxes.Add(go.AddComponent<BoxView>());

                CreateBettingCircle(boxLocalPosition, (i + 1).ToString());
            }

            var dealerGo = new GameObject("DealerHand");
            dealerGo.transform.SetParent(transform, false);
            dealerGo.transform.localPosition = new Vector3(-0.6f, 0f, DealerRowZ);
            DealerHand = dealerGo.AddComponent<HandView>();

            ShoePosition = transform.position + new Vector3(4.2f, 0.3f, DealerRowZ + 0.6f);
        }

        public BoxView BoxAt(int index) => _boxes[index];

        public void ClearAll()
        {
            foreach (BoxView box in _boxes)
            {
                box.Clear();
            }

            DealerHand.Clear();
        }

        private void CreateFelt()
        {
            // A second, larger, lower plane in FeltShadow behind the felt so the
            // table reads as a surface with depth rather than one flat quad.
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Plane);
            shadow.name = "FeltShadow";
            shadow.transform.SetParent(transform, false);
            shadow.transform.localPosition = new Vector3(0f, -0.01f, 0f);
            shadow.transform.localScale = new Vector3(2.15f, 1f, 1.2f);
            Object.Destroy(shadow.GetComponent<MeshCollider>());
            shadow.GetComponent<Renderer>().material.color = Palette.FeltShadow;

            var felt = GameObject.CreatePrimitive(PrimitiveType.Plane);
            felt.name = "Felt";
            felt.transform.SetParent(transform, false);
            felt.transform.localScale = new Vector3(2.0f, 1f, 1.1f);
            Object.Destroy(felt.GetComponent<MeshCollider>());
            felt.GetComponent<Renderer>().material.color = Palette.FeltGreen;
        }

        /// <summary>
        /// A flattened disc plus a numeral so the felt reads as a blackjack table with betting
        /// spots even before a single card lands. Purely decorative — it holds no game state.
        /// </summary>
        private void CreateBettingCircle(Vector3 boxLocalPosition, string numeral)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "BettingCircle";
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition = boxLocalPosition + new Vector3(0f, 0.008f, 0f);
            disc.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
            Object.Destroy(disc.GetComponent<CapsuleCollider>());
            disc.GetComponent<Renderer>().material.color = Palette.FeltShadow;

            var numeralGo = new GameObject("Numeral");
            numeralGo.transform.SetParent(transform, false);
            numeralGo.transform.localPosition = boxLocalPosition + new Vector3(0f, 0.02f, 0f);
            // Lie flat, readable from the top-down camera, same trick as CardView's face text.
            numeralGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            numeralGo.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            var text = numeralGo.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.5f;
            text.color = Palette.Accent;
            text.text = numeral;
        }
    }
}
