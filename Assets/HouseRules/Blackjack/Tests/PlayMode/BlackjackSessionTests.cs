using System.Collections;
using System.Linq;
using HouseRules.Blackjack;
using HouseRules.Blackjack.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HouseRules.Blackjack.PlayModeTests
{
    public class BlackjackSessionTests
    {
        private GameObject _host;
        private BlackjackSession _session;
        private EventSequencer _sequencer;
        private RecordingPresenter _presenter;
        private Wallet _wallet;

        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private void Build(float secondsPerEvent, params Card[] script)
        {
            _host = new GameObject("Session");
            _sequencer = _host.AddComponent<EventSequencer>();
            _session = _host.AddComponent<BlackjackSession>();

            _presenter = new RecordingPresenter(secondsPerEvent);
            _sequencer.SetPresenter(_presenter);

            _wallet = new Wallet(1000);

            IShoe shoe = script.Length > 0
                ? (IShoe)new ScriptedShoe(script)
                : new Shoe(6, 0.75, new SeededRandom(1));

            _session.Configure(BlackjackRules.Standard, shoe, _wallet, _sequencer);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.Destroy(_host);
            }
        }

        [UnityTest]
        public IEnumerator BeginRound_StartsInBettingAndAcceptsInput()
        {
            Build(0f);
            _session.BeginRound();
            yield return null;

            Assert.AreEqual(RoundState.Betting, _session.State);
            Assert.IsTrue(_session.CanAcceptInput);
        }

        [UnityTest]
        public IEnumerator Deal_PumpsEventsIntoTheSequencer()
        {
            Build(0f, C(Rank.Nine), C(Rank.Six), C(Rank.Seven), C(Rank.Four));
            _session.BeginRound();
            _session.PlaceBet(0, 10);
            _session.Deal();

            while (_session.IsBusy)
            {
                yield return null;
            }

            Assert.IsTrue(_presenter.Presented.Any(e => e is RoundStarted));
            Assert.AreEqual(4, _presenter.Presented.OfType<CardDealt>().Count());
        }

        [UnityTest]
        public IEnumerator InputIsRefused_WhilePlaybackIsRunning()
        {
            Build(0.05f, C(Rank.Nine), C(Rank.Six), C(Rank.Seven), C(Rank.Four));
            _session.BeginRound();
            _session.PlaceBet(0, 10);
            _session.Deal();

            yield return null;

            Assert.IsTrue(_session.IsBusy);
            Assert.IsFalse(_session.CanAcceptInput);
            Assert.IsEmpty(_session.LegalActions,
                "LegalActions must be empty while animating, so a UI cannot offer a button.");

            while (_session.IsBusy)
            {
                yield return null;
            }

            Assert.IsTrue(_session.CanAcceptInput);
            CollectionAssert.Contains(_session.LegalActions, PlayerAction.Hit);
        }

        [UnityTest]
        public IEnumerator Apply_WhileBusy_IsIgnored()
        {
            Build(0.05f, C(Rank.Nine), C(Rank.Six), C(Rank.Seven), C(Rank.Four), C(Rank.Two));
            _session.BeginRound();
            _session.PlaceBet(0, 10);
            _session.Deal();

            yield return null;
            Assert.IsTrue(_session.IsBusy);

            int cardsBefore = _session.CurrentRound.Boxes[0].Hands[0].Cards.Count;
            _session.Apply(PlayerAction.Hit);
            _session.Apply(PlayerAction.Hit);

            while (_session.IsBusy)
            {
                yield return null;
            }

            Assert.AreEqual(cardsBefore, _session.CurrentRound.Boxes[0].Hands[0].Cards.Count,
                "A double-tap during playback must not deal extra cards.");
        }

        [UnityTest]
        public IEnumerator Apply_WhenIdle_AdvancesTheRound()
        {
            Build(0f, C(Rank.Nine), C(Rank.Six), C(Rank.Seven), C(Rank.Four), C(Rank.Two));
            _session.BeginRound();
            _session.PlaceBet(0, 10);
            _session.Deal();

            while (_session.IsBusy)
            {
                yield return null;
            }

            _session.Apply(PlayerAction.Hit);

            while (_session.IsBusy)
            {
                yield return null;
            }

            Assert.AreEqual(3, _session.CurrentRound.Boxes[0].Hands[0].Cards.Count);
        }

        [UnityTest]
        public IEnumerator RoundCompleted_FiresOnceWhenTheRoundEnds()
        {
            Build(0f, C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Seven));
            int fired = 0;
            _session.RoundCompleted += () => fired++;

            _session.BeginRound();
            _session.PlaceBet(0, 10);
            _session.Deal();

            while (_session.IsBusy)
            {
                yield return null;
            }

            _session.Apply(PlayerAction.Stand);

            while (_session.IsBusy)
            {
                yield return null;
            }

            Assert.AreEqual(RoundState.Complete, _session.State);
            Assert.AreEqual(1, fired);
        }

        [UnityTest]
        public IEnumerator AbandonRound_RefundsAndCompletes()
        {
            Build(0f, C(Rank.Nine), C(Rank.Six), C(Rank.Seven), C(Rank.Four));
            _session.BeginRound();
            _session.PlaceBet(0, 10);
            _session.Deal();

            while (_session.IsBusy)
            {
                yield return null;
            }

            Assert.AreEqual(990, _wallet.Balance);

            _session.AbandonRound();

            while (_session.IsBusy)
            {
                yield return null;
            }

            Assert.AreEqual(1000, _wallet.Balance);
            Assert.AreEqual(RoundState.Complete, _session.State);
        }

        [UnityTest]
        public IEnumerator PlaceBet_WhileBusy_IsIgnored()
        {
            Build(0.05f, C(Rank.Nine), C(Rank.Six), C(Rank.Seven), C(Rank.Four));
            _session.BeginRound();
            _session.PlaceBet(0, 10);
            _session.Deal();

            yield return null;

            long balanceDuringPlayback = _wallet.Balance;
            _session.PlaceBet(1, 10);

            Assert.AreEqual(balanceDuringPlayback, _wallet.Balance,
                "A bet placed during playback must not reach the wallet.");

            while (_session.IsBusy)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BeginRound_AfterCompletion_StartsAFreshRound()
        {
            Build(0f, C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Seven));
            _session.BeginRound();
            _session.PlaceBet(0, 10);
            _session.Deal();

            while (_session.IsBusy)
            {
                yield return null;
            }

            _session.Apply(PlayerAction.Stand);

            while (_session.IsBusy)
            {
                yield return null;
            }

            _session.BeginRound();
            yield return null;

            Assert.AreEqual(RoundState.Betting, _session.State);
            Assert.IsEmpty(_session.CurrentRound.Settlements);
        }
    }
}
