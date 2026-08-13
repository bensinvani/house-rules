using System.Collections;
using System.Linq;
using HouseRules.Blackjack;
using HouseRules.Blackjack.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HouseRules.Blackjack.PlayModeTests
{
    public class EventSequencerTests
    {
        private GameObject _host;
        private EventSequencer _sequencer;
        private RecordingPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Sequencer");
            _sequencer = _host.AddComponent<EventSequencer>();
            _presenter = new RecordingPresenter();
            _sequencer.SetPresenter(_presenter);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_host);
        }

        private static GameEvent[] ThreeEvents()
        {
            return new GameEvent[]
            {
                new RoundStarted(),
                new CardDealt(0, 0, new Card(Rank.Ace, Suit.Spades), true),
                new PlayerTurnStarted(0, 0)
            };
        }

        [UnityTest]
        public IEnumerator NewSequencer_IsIdle()
        {
            Assert.IsTrue(_sequencer.IsIdle);
            Assert.AreEqual(0, _sequencer.PendingCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Enqueue_PresentsEveryEvent_InOrder()
        {
            _sequencer.Enqueue(ThreeEvents());

            while (!_sequencer.IsIdle)
            {
                yield return null;
            }

            Assert.AreEqual(3, _presenter.Presented.Count);
            Assert.IsInstanceOf<RoundStarted>(_presenter.Presented[0]);
            Assert.IsInstanceOf<CardDealt>(_presenter.Presented[1]);
            Assert.IsInstanceOf<PlayerTurnStarted>(_presenter.Presented[2]);
        }

        [UnityTest]
        public IEnumerator Sequencer_IsNotIdle_WhilePlayingBack()
        {
            _presenter.SecondsPerEvent = 0.05f;
            _sequencer.Enqueue(ThreeEvents());

            yield return null;

            Assert.IsFalse(_sequencer.IsIdle, "Should be busy immediately after enqueueing.");

            while (!_sequencer.IsIdle)
            {
                yield return null;
            }

            Assert.IsTrue(_sequencer.IsIdle);
        }

        [UnityTest]
        public IEnumerator Sequencer_PresentsOneEventAtATime()
        {
            _presenter.SecondsPerEvent = 0.02f;
            _sequencer.Enqueue(ThreeEvents());

            while (!_sequencer.IsIdle)
            {
                yield return null;
            }

            Assert.AreEqual(1, _presenter.MaxConcurrent,
                "Two events were presented concurrently — playback must be serial.");
        }

        [UnityTest]
        public IEnumerator Enqueue_WhileBusy_AppendsRatherThanRestarting()
        {
            _presenter.SecondsPerEvent = 0.02f;
            _sequencer.Enqueue(new GameEvent[] { new RoundStarted() });
            _sequencer.Enqueue(new GameEvent[] { new PlayerTurnStarted(0, 0) });

            while (!_sequencer.IsIdle)
            {
                yield return null;
            }

            Assert.AreEqual(2, _presenter.Presented.Count);
            Assert.IsInstanceOf<RoundStarted>(_presenter.Presented[0]);
            Assert.IsInstanceOf<PlayerTurnStarted>(_presenter.Presented[1]);
        }

        [UnityTest]
        public IEnumerator Enqueue_EmptyCollection_LeavesSequencerIdle()
        {
            _sequencer.Enqueue(new GameEvent[0]);
            yield return null;
            Assert.IsTrue(_sequencer.IsIdle);
        }

        [UnityTest]
        public IEnumerator PendingCount_DrainsToZero()
        {
            _presenter.SecondsPerEvent = 0.02f;
            _sequencer.Enqueue(ThreeEvents());

            Assert.Greater(_sequencer.PendingCount, 0);

            while (!_sequencer.IsIdle)
            {
                yield return null;
            }

            Assert.AreEqual(0, _sequencer.PendingCount);
        }

        [UnityTest]
        public IEnumerator WithNoPresenter_EventsStillDrain()
        {
            var host = new GameObject("Bare");
            var bare = host.AddComponent<EventSequencer>();

            bare.Enqueue(ThreeEvents());

            while (!bare.IsIdle)
            {
                yield return null;
            }

            Assert.IsTrue(bare.IsIdle);
            Object.Destroy(host);
        }
    }
}
