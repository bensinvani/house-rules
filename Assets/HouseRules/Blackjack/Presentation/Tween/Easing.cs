namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Normalized easing curves: each maps t in [0,1] to a progress value that is
    /// 0 at t=0 and 1 at t=1. Deliberately free of UnityEngine types so the curve
    /// maths can be unit-tested without a scene.
    /// </summary>
    public static class Easing
    {
        private const float BackOvershoot = 1.70158f;

        public static float Clamp01(float t)
        {
            if (t < 0f)
            {
                return 0f;
            }

            return t > 1f ? 1f : t;
        }

        public static float Linear(float t) => Clamp01(t);

        public static float OutCubic(float t)
        {
            float clamped = Clamp01(t);
            float inverted = 1f - clamped;
            return 1f - (inverted * inverted * inverted);
        }

        public static float InOutCubic(float t)
        {
            float clamped = Clamp01(t);

            if (clamped < 0.5f)
            {
                return 4f * clamped * clamped * clamped;
            }

            float shifted = (-2f * clamped) + 2f;
            return 1f - ((shifted * shifted * shifted) / 2f);
        }

        /// <summary>Overshoots past 1 then settles back — gives a card a bit of snap on landing.</summary>
        public static float OutBack(float t)
        {
            float clamped = Clamp01(t);
            float inverted = clamped - 1f;
            const float C = BackOvershoot + 1f;
            return 1f + (C * inverted * inverted * inverted) + (BackOvershoot * inverted * inverted);
        }
    }
}
