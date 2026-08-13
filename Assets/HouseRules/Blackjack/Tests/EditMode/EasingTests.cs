using NUnit.Framework;
using HouseRules.Blackjack.Presentation;

namespace HouseRules.Blackjack.Tests
{
    public class EasingTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Clamp01_ClampsBothEnds()
        {
            Assert.AreEqual(0f, Easing.Clamp01(-5f), Tolerance);
            Assert.AreEqual(1f, Easing.Clamp01(5f), Tolerance);
            Assert.AreEqual(0.25f, Easing.Clamp01(0.25f), Tolerance);
        }

        [Test]
        public void AllCurves_StartAtZeroAndEndAtOne()
        {
            Assert.AreEqual(0f, Easing.Linear(0f), Tolerance);
            Assert.AreEqual(1f, Easing.Linear(1f), Tolerance);

            Assert.AreEqual(0f, Easing.OutCubic(0f), Tolerance);
            Assert.AreEqual(1f, Easing.OutCubic(1f), Tolerance);

            Assert.AreEqual(0f, Easing.InOutCubic(0f), Tolerance);
            Assert.AreEqual(1f, Easing.InOutCubic(1f), Tolerance);

            Assert.AreEqual(0f, Easing.OutBack(0f), Tolerance);
            Assert.AreEqual(1f, Easing.OutBack(1f), Tolerance);
        }

        [Test]
        public void OutCubic_DeceleratesRatherThanAccelerating()
        {
            // Past the halfway point in time, an ease-out has already covered
            // more than half the distance.
            Assert.Greater(Easing.OutCubic(0.5f), 0.5f);
        }

        [Test]
        public void InOutCubic_IsSymmetricAboutTheMidpoint()
        {
            Assert.AreEqual(0.5f, Easing.InOutCubic(0.5f), Tolerance);
            Assert.AreEqual(1f - Easing.InOutCubic(0.25f), Easing.InOutCubic(0.75f), Tolerance);
        }

        [Test]
        public void OutBack_Overshoots_ThenSettles()
        {
            // The characteristic of a "back" ease: it passes 1 before returning to it.
            bool overshot = false;
            for (float t = 0.5f; t < 1f; t += 0.01f)
            {
                if (Easing.OutBack(t) > 1f)
                {
                    overshot = true;
                    break;
                }
            }

            Assert.IsTrue(overshot, "OutBack should exceed 1 before settling.");
            Assert.AreEqual(1f, Easing.OutBack(1f), Tolerance);
        }

        [Test]
        public void Curves_AreMonotonicExceptOutBack()
        {
            AssertMonotonic(Easing.Linear);
            AssertMonotonic(Easing.OutCubic);
            AssertMonotonic(Easing.InOutCubic);
        }

        private static void AssertMonotonic(System.Func<float, float> curve)
        {
            float previous = curve(0f);
            for (float t = 0.01f; t <= 1f; t += 0.01f)
            {
                float current = curve(t);
                Assert.GreaterOrEqual(current, previous - Tolerance, $"Went backwards at t={t}.");
                previous = current;
            }
        }
    }
}
