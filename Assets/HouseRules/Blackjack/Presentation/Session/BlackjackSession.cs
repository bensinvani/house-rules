using System;
using System.Collections.Generic;
using HouseRules.Blackjack;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// The single bridge between the rules engine and Unity. Owns the round, pumps
    /// its event stream into the sequencer, and refuses input while playback runs.
    /// </summary>
    public sealed class BlackjackSession : MonoBehaviour
    {
        private static readonly PlayerAction[] NoActions = new PlayerAction[0];

        private BlackjackRules _rules;
        private IShoe _shoe;
        private EventSequencer _sequencer;
        private bool _completionAnnounced;

        /// <summary>Raised once when a round reaches Complete and playback has finished.</summary>
        public event Action RoundCompleted;

        public Wallet Wallet { get; private set; }

        public Round CurrentRound { get; private set; }

        public RoundState State => CurrentRound?.State ?? RoundState.Complete;

        /// <summary>True while the sequencer is still playing events back.</summary>
        public bool IsBusy
        {
            get
            {
                bool busy = _sequencer != null && !_sequencer.IsIdle;

                // A caller that polls IsBusy in its own coroutine can observe the
                // sequencer going idle in the very frame EventSequencer's playback
                // coroutine finishes — one frame before this component's own Update()
                // would next run. Checking completion here, at the read that drives
                // that polling, keeps RoundCompleted from lagging a frame behind
                // whoever is actually watching for idle.
                if (!busy)
                {
                    TryAnnounceCompletion();
                }

                return busy;
            }
        }

        public bool CanAcceptInput => CurrentRound != null && !IsBusy;

        /// <summary>
        /// Empty while animating. The UI renders buttons from this and nothing else,
        /// so an empty list is what physically prevents a double-tap mid-deal.
        /// </summary>
        public IReadOnlyList<PlayerAction> LegalActions =>
            CanAcceptInput ? CurrentRound.LegalActions : NoActions;

        public void Configure(BlackjackRules rules, IShoe shoe, Wallet wallet, EventSequencer sequencer)
        {
            _rules = rules;
            _shoe = shoe ?? throw new ArgumentNullException(nameof(shoe));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _sequencer = sequencer;
        }

        public void BeginRound()
        {
            if (_shoe == null || Wallet == null)
            {
                throw new InvalidOperationException("Configure must be called before BeginRound.");
            }

            CurrentRound = new Round(_rules, _shoe, Wallet);
            _completionAnnounced = false;
            Pump();
        }

        public void PlaceBet(int boxIndex, long wager)
        {
            if (!CanAcceptInput || CurrentRound.State != RoundState.Betting)
            {
                return;
            }

            CurrentRound.PlaceBet(boxIndex, wager);
            Pump();
        }

        public void Deal()
        {
            if (!CanAcceptInput || CurrentRound.State != RoundState.Betting)
            {
                return;
            }

            CurrentRound.Deal();
            Pump();
        }

        public void Apply(PlayerAction action)
        {
            if (!CanAcceptInput)
            {
                return;
            }

            if (!Contains(CurrentRound.LegalActions, action))
            {
                return;
            }

            CurrentRound.Apply(action);
            Pump();
        }

        public void AbandonRound()
        {
            if (CurrentRound == null || CurrentRound.State == RoundState.Complete)
            {
                return;
            }

            CurrentRound.Abandon();
            Pump();
        }

        private void Pump()
        {
            if (CurrentRound == null)
            {
                return;
            }

            IReadOnlyList<GameEvent> drained = CurrentRound.DrainEvents();

            if (_sequencer != null && drained.Count > 0)
            {
                _sequencer.Enqueue(drained);
            }
        }

        private void Update()
        {
            // Backstop for a caller that never reads IsBusy directly (e.g. only
            // reacts to the RoundCompleted event). IsBusy's getter already handles
            // the common case of a caller polling it in a coroutine.
            if (!IsBusy)
            {
                TryAnnounceCompletion();
            }
        }

        /// <summary>
        /// Announce completion only once playback has caught up, so a listener that
        /// shows a result screen does not pre-empt the settlement animation.
        /// </summary>
        private void TryAnnounceCompletion()
        {
            if (_completionAnnounced || CurrentRound == null)
            {
                return;
            }

            if (CurrentRound.State == RoundState.Complete)
            {
                _completionAnnounced = true;
                RoundCompleted?.Invoke();
            }
        }

        private static bool Contains(IReadOnlyList<PlayerAction> actions, PlayerAction action)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] == action)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
