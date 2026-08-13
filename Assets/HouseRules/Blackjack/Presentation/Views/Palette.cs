using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Visual Quality Bar palette tokens (docs/superpowers/plans/2026-08-13-blackjack-visuals.md).
    /// One source of truth for the felt, cards, and HUD so the three never drift out of the
    /// same palette. No pure black and no pure white anywhere in the game.
    /// </summary>
    internal static class Palette
    {
        public static readonly Color FeltGreen = new Color(0.086f, 0.294f, 0.180f, 1f);
        public static readonly Color FeltShadow = new Color(0.043f, 0.157f, 0.098f, 1f);
        public static readonly Color Surround = new Color(0.078f, 0.086f, 0.094f, 1f);
        public static readonly Color PanelDark = new Color(0.118f, 0.133f, 0.145f, 0.902f);
        public static readonly Color TextPrimary = new Color(0.949f, 0.957f, 0.945f, 1f);
        public static readonly Color TextMuted = new Color(0.639f, 0.678f, 0.655f, 1f);
        public static readonly Color Accent = new Color(0.847f, 0.706f, 0.325f, 1f);
        public static readonly Color Danger = new Color(0.788f, 0.267f, 0.243f, 1f);
        public static readonly Color CardRed = new Color(0.729f, 0.129f, 0.129f, 1f);
        public static readonly Color CardBack = new Color(0.145f, 0.227f, 0.396f, 1f);
        public static readonly Color CardInk = new Color(0.106f, 0.106f, 0.118f, 1f);
        public static readonly Color CardFace = new Color(0.976f, 0.973f, 0.957f, 1f);

        /// <summary>Lifts a colour toward white by roughly the given fraction. Used for button hover.</summary>
        public static Color Lift(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r + (1f - c.r) * amount),
                Mathf.Clamp01(c.g + (1f - c.g) * amount),
                Mathf.Clamp01(c.b + (1f - c.b) * amount),
                c.a);
        }

        /// <summary>Darkens a colour by roughly the given fraction. Used for the pressed button state.</summary>
        public static Color Darken(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r * (1f - amount)),
                Mathf.Clamp01(c.g * (1f - amount)),
                Mathf.Clamp01(c.b * (1f - amount)),
                c.a);
        }
    }
}
