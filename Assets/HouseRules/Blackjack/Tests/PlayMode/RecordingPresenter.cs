using System.Collections;
using System.Collections.Generic;
using HouseRules.Blackjack;
using HouseRules.Blackjack.Presentation;
using UnityEngine;

namespace HouseRules.Blackjack.PlayModeTests
{
    /// <summary>
    /// Test double for <see cref="IEventPresenter"/>. Records what it was asked to
    /// present, in order, and takes a controllable amount of time doing it — so a
    /// test can observe the sequencer mid-playback rather than only at the end.
    /// </summary>
    public sealed class RecordingPresenter : IEventPresenter
    {
        private readonly List<GameEvent> _presented = new List<GameEvent>();

        public RecordingPresenter(float secondsPerEvent = 0f)
        {
            SecondsPerEvent = secondsPerEvent;
        }

        public float SecondsPerEvent { get; set; }

        public IReadOnlyList<GameEvent> Presented => _presented;

        /// <summary>True while an event is being presented — proves the sequencer waits.</summary>
        public bool IsPresenting { get; private set; }

        /// <summary>Highest number of concurrent presentations seen. Must never exceed 1.</summary>
        public int MaxConcurrent { get; private set; }

        private int _concurrent;

        public IEnumerator Present(GameEvent gameEvent)
        {
            _concurrent++;
            if (_concurrent > MaxConcurrent)
            {
                MaxConcurrent = _concurrent;
            }

            IsPresenting = true;
            _presented.Add(gameEvent);

            if (SecondsPerEvent > 0f)
            {
                yield return Tween.Wait(SecondsPerEvent);
            }
            else
            {
                yield return null;
            }

            IsPresenting = false;
            _concurrent--;
        }
    }
}
