using System.Collections.Generic;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>Scene root: felt, box anchors, the dealer's hand, and the shoe position.</summary>
    public sealed class TableView : MonoBehaviour
    {
        private const float BoxSpacingX = 2.6f;
        private const float PlayerRowZ = -1.9f;
        private const float DealerRowZ = 1.7f;

        // Visual Quality Bar palette tokens (docs/superpowers/plans/2026-08-13-blackjack-visuals.md).
        // No pure white/black anywhere in the game.
        private static readonly Color FeltGreen = new Color(0.086f, 0.294f, 0.180f, 1f);
        private static readonly Color FeltShadow = new Color(0.043f, 0.157f, 0.098f, 1f);

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
                var go = new GameObject($"Box{i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(firstX + (i * BoxSpacingX), 0f, PlayerRowZ);
                _boxes.Add(go.AddComponent<BoxView>());
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
            shadow.transform.localScale = new Vector3(1.75f, 1f, 1.2f);
            Object.Destroy(shadow.GetComponent<MeshCollider>());
            shadow.GetComponent<Renderer>().material.color = FeltShadow;

            var felt = GameObject.CreatePrimitive(PrimitiveType.Plane);
            felt.name = "Felt";
            felt.transform.SetParent(transform, false);
            felt.transform.localScale = new Vector3(1.6f, 1f, 1.1f);
            Object.Destroy(felt.GetComponent<MeshCollider>());
            felt.GetComponent<Renderer>().material.color = FeltGreen;
        }
    }
}
