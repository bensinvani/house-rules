using System.Collections;
using System.Collections.Generic;
using HouseRules.Blackjack;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Plays an engine event stream back over time, one event at a time.
    /// The engine has already decided everything; this only performs it.
    /// </summary>
    public sealed class EventSequencer : MonoBehaviour
    {
        private readonly Queue<GameEvent> _pending = new Queue<GameEvent>();
        private IEventPresenter _presenter;
        private Coroutine _pump;

        /// <summary>True when nothing is queued and nothing is mid-presentation.</summary>
        public bool IsIdle => _pending.Count == 0 && _pump == null;

        public int PendingCount => _pending.Count;

        public void SetPresenter(IEventPresenter presenter) => _presenter = presenter;

        public void Enqueue(IEnumerable<GameEvent> events)
        {
            if (events == null)
            {
                return;
            }

            foreach (GameEvent gameEvent in events)
            {
                _pending.Enqueue(gameEvent);
            }

            if (_pump == null && _pending.Count > 0 && isActiveAndEnabled)
            {
                _pump = StartCoroutine(Pump());
            }
        }

        private IEnumerator Pump()
        {
            while (_pending.Count > 0)
            {
                GameEvent next = _pending.Dequeue();

                if (_presenter != null)
                {
                    // A missing presenter must not deadlock playback: without one we
                    // simply drain, so a mis-wired scene shows nothing rather than hanging.
                    yield return _presenter.Present(next);
                }
            }

            _pump = null;
        }

        private void OnDisable()
        {
            if (_pump != null)
            {
                StopCoroutine(_pump);
                _pump = null;
            }

            _pending.Clear();
        }
    }
}
