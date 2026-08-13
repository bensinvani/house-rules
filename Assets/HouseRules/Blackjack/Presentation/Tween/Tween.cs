using System;
using System.Collections;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Minimal coroutine tweens. Deliberately not a DOTween dependency: card motion
    /// here is move-and-rotate with easing, which does not earn a third-party package.
    /// </summary>
    public static class Tween
    {
        public static IEnumerator Move(
            Transform target,
            Vector3 to,
            float duration,
            Func<float, float> ease = null)
        {
            if (target == null)
            {
                yield break;
            }

            ease = ease ?? Easing.OutCubic;
            Vector3 from = target.position;

            if (duration <= 0f)
            {
                target.position = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = ease(Easing.Clamp01(elapsed / duration));
                target.position = Vector3.LerpUnclamped(from, to, progress);
                yield return null;
            }

            target.position = to;
        }

        public static IEnumerator MoveAndRotate(
            Transform target,
            Vector3 toPosition,
            Quaternion toRotation,
            float duration,
            Func<float, float> ease = null)
        {
            if (target == null)
            {
                yield break;
            }

            ease = ease ?? Easing.OutCubic;
            Vector3 fromPosition = target.position;
            Quaternion fromRotation = target.rotation;

            if (duration <= 0f)
            {
                target.SetPositionAndRotation(toPosition, toRotation);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = ease(Easing.Clamp01(elapsed / duration));
                target.SetPositionAndRotation(
                    Vector3.LerpUnclamped(fromPosition, toPosition, progress),
                    Quaternion.SlerpUnclamped(fromRotation, toRotation, progress));
                yield return null;
            }

            target.SetPositionAndRotation(toPosition, toRotation);
        }

        public static IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
