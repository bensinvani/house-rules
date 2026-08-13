# Blackjack — Design Spec

**Date:** 2026-08-13
**Project:** HouseRules (Unity 6.3 LTS `6000.3.22f1`, URP 17.3.0)
**Status:** Approved, ready for implementation planning

---

## 1. Context

HouseRules is a mobile game bundling casino games (blackjack, roulette) and
board games (backgammon, checkers, chess). Five games is not one project — it is
a shared foundation plus five independent rule engines that share little beyond
UI chrome.

**Decomposition:** build one game end-to-end first and let the shared foundation
emerge from real work, rather than designing a universal game framework before a
single game exists. Blackjack is first because it yields the most reusable
groundwork per unit of effort: a wallet and chip economy, a card deck and shoe,
dealing animation, and betting UI — most of which roulette reuses directly.

This spec covers **blackjack only**.

## 2. Settled decisions

| Decision | Choice | Rationale |
|---|---|---|
| Money model | Play-money only | No cash in or out. Removes gambling licensing, certified RNG, server-authoritative play, and geo-gating. Ships anywhere; real-money would be blocked on Google Play from Israel regardless. |
| Rules | Fixed standard ruleset | "HouseRules" is a product name, not a configurable-rules hook. Values live in one struct so tests can vary them; there is no settings UI. |
| Presentation | 3D URP scene | Felt table, card meshes, lighting. |
| Art | Placeholder now, Blender later | Keeps code on the critical path. Real assets swap in behind a stable prefab interface. |
| Seats | Player multi-bet, 2–3 boxes | No bot players. Splitting already forces multi-hand plumbing, so boxes and splits together define the domain. |
| Language | English only | All player-facing strings route through one localisation table from day one, so adding Hebrew later is translation, not refactor. |
| Architecture | Pure C# core + event-driven presentation | See §5. |

## 3. Ruleset

Encoded as a single `BlackjackRules` struct with a `Standard` preset.

| Rule | Value |
|---|---|
| Deck count | 6 |
| Dealer on soft 17 | Stands |
| Blackjack payout | 3:2 |
| Double after split | Allowed |
| Max splits per box | 3 (up to 4 hands) |
| Split aces | Receive one card each, cannot be resplit or hit |
| Surrender | Not allowed |
| Insurance | Offered when dealer upcard is an ace; pays 2:1 |
| Shoe penetration | Reshuffle at 75% |

## 4. Domain model

A round owns one dealer hand and 2–3 player *boxes*. A box holds a wager and
**one or more hands** — splitting turns one hand into several, so hands are a
list from the start.

```
Round
├── Shoe          6 decks, seeded RNG, cut card at 75% penetration
├── DealerHand
└── Box[2..3]
    ├── Wager
    └── Hand[1..4]     ← split grows this list
        └── Card[]
```

- `Card` — readonly struct of `Rank` and `Suit`.
- `Hand` — cards plus value computation. Aces count 11 and demote to 1 while the
  total exceeds 21. Exposes `IsSoft`, `IsBust`, `IsBlackjack` (natural 21 on the
  first two cards only; a 21 formed after a split is not a blackjack).
- `Box` — index, wager, and its hands.
- `Shoe` — 312 cards, shuffled through `IRandom`, reshuffles at penetration.
- `IRandom` — the randomness seam. Seeded per round.

## 5. Architecture

Two assemblies with a compiler-enforced boundary.

```
HouseRules.Blackjack           (asmdef: NO UnityEngine reference)
  Card, Shoe, Hand, Box, Round, Resolver, Wallet, IRandom
  state machine + events out, intents in
         │  events                                 ▲ intents
         ▼                                         │
HouseRules.Blackjack.Presentation   (MonoBehaviours, URP scene, tweens)
```

The core assembly physically cannot reference `UnityEngine`. The compiler, not
developer discipline, enforces that payout logic never depends on frame timing
or scene state. That is what makes exhaustive headless testing possible.

### 5.1 Round state machine

One explicit enum, one transition method. No coroutines, no timers in the core.

```
Betting ──▶ Dealing ──▶ [Insurance?] ──▶ PlayerTurn ──▶ DealerTurn ──▶ Settlement ──┐
   ▲                                     (per box,                                  │
   └─────────────────────────────────────  per hand)  ◀─────────────────────────────┘
```

`Insurance` is entered only when the dealer's upcard is an ace. `PlayerTurn`
walks boxes left-to-right and, within a box, walks hands — so a split mid-turn
simply extends the walk.

### 5.2 Intents and legality

Player intents: `Hit`, `Stand`, `Double`, `Split`, `TakeInsurance`,
`DeclineInsurance`.

The engine exposes `IReadOnlyList<PlayerAction> LegalActions` for the current
hand. The UI enables buttons from that list and holds no rules of its own.
"Can I double after splitting aces?" is a rules question; if the UI answers it,
the rules exist in two places and will eventually disagree.

The engine validates every incoming intent against `LegalActions` and rejects
illegal ones rather than trusting the caller.

### 5.3 Events

Emitted synchronously, in order, as the round advances:

`RoundStarted`, `CardDealt(box, hand, card, faceUp)`, `HandChanged`,
`InsuranceOffered`, `PlayerTurnStarted(box, hand)`, `HandBusted`,
`DealerRevealed`, `DealerCardDealt`, `HandSettled(box, hand, outcome, delta)`,
`RoundSettled(totalDelta)`, `ShoeReshuffled`.

### 5.4 Money

Chips are `long`. Blackjack pays 3:2 through integer math as `wager * 3 / 2`.

For that division to be exact, every wager must be even. The betting UI
therefore enforces a minimum bet of 2 chips and increments of 2, and the engine
rejects an odd wager as an invalid bet. Doubling and splitting derive their
wagers from the original, so they inherit evenness automatically.

Floating-point money loses a chip precisely on a 3:2 payout of an odd wager, and
the error compounds invisibly across thousands of rounds.

### 5.5 Determinism

All randomness flows through `IRandom`, seeded per round. Any round replays
exactly from its seed, which makes bug reports reproducible and lets tests
assert real distributions.

## 6. Presentation and data flow

The engine writes the score; the presentation performs it. The engine resolves
each step instantly and synchronously. Animation is playback of what already
happened, never a participant in deciding it.

```
   player taps Hit
        │
        ▼
  Session.Apply(Hit) ───────────────▶ Engine (instant, synchronous)
                                        │ emits, in order:
                                        │   CardDealt(box1,hand0,♠7)
                                        │   HandBusted(box1,hand0)
                                        │   PlayerTurnStarted(box2,hand0)
                                        ▼
                                    EventQueue
                                        │
                                        ▼
                                  Sequencer ── plays each event as a tween,
                                        │       waits for it to finish
                                        ▼
                            CardView / HandView / BoxView / ChipView
                                        │
                                        ▼
                     queue drained + engine awaiting input
                                        │
                                        ▼
                        ActionBar enables exactly LegalActions
```

Input is accepted **only** when the queue is drained and the engine is waiting.
That single rule eliminates the class of bugs where a double-tap during a deal
animation produces two cards.

### 6.1 Components

| Component | Responsibility |
|---|---|
| `BlackjackSession` | Owns the engine, pumps events into the queue. The only bridge between assemblies. |
| `Sequencer` | Drains the queue, plays one event at a time, reports idle. |
| `TableView` | Scene root; owns box, dealer, and shoe anchors. |
| `BoxView` | Positions its hands on the felt. |
| `HandView` | Fans cards within a hand. |
| `CardView` | One pooled prefab: thin box mesh, face texture from a `(Rank,Suit)` atlas, back material. |
| `ActionBarView` | Renders buttons from `LegalActions`. Holds zero rules. |
| `WalletView` | Balance readout. |

Card views are pooled at roughly 30 instances; only a handful are ever on the
felt at once despite the 312-card shoe.

### 6.2 UI and animation choices

- **HUD in uGUI**, not UI Toolkit. Chips animate between 3D felt positions and
  screen space, and uGUI makes that conversion routine.
- **In-house tween helper** (~100 lines) rather than a DOTween dependency. Card
  motion here is move-and-rotate with easing; the dependency is not earned.

## 7. Error handling

- Illegal intents cannot originate from the UI, because buttons are generated
  from `LegalActions`. The engine still validates and rejects, so a view bug
  surfaces as a rejected intent rather than a corrupted round.
- Engine invariants — the shoe never deals a duplicate, box wagers always
  reconcile against the wallet — are assertions that fail loudly in development
  builds.

## 8. Persistence

Only the wallet balance is saved, as JSON in `Application.persistentDataPath`.

A round is atomic: quitting mid-round discards the round and refunds wagers.
Persisting a half-played round is real complexity for a case players do not miss.

## 9. Testing strategy

The core assembly has no Unity dependency, so its tests are EditMode and run in
milliseconds.

The enabling technique is a **stacked shoe** — a scripted `IRandom`/shoe seam
that deals a known sequence. This turns "player holds 8,8 against a dealer 6,
splits, draws a 3" into a deterministic one-line test rather than a lottery.

| Layer | What it proves |
|---|---|
| Hand value | Soft/hard ace demotion. A,A,9 is 21, not 31. |
| Shoe integrity | 312 cards, zero duplicates dealt, reshuffle fires at penetration. |
| Legality | `LegalActions` exact in every state — no double after hit, split only on a pair, split aces draw one and stop. |
| Settlement | Every outcome × payout, including 3:2 exactness and both insurance branches. |
| Multi-box + split | Full scripted rounds; several hands from one box resolving against one dealer hand. |
| Statistical | 100k rounds under basic strategy; house edge lands in its expected band. |
| Wallet | Integer exactness, never negative, wagers always reconcile. |

The statistical test catches systemic payout errors that every individual unit
test happily passes.

**It carries a dependency worth naming up front:** driving 100k rounds requires
a basic-strategy table (hard totals, soft totals, pairs) as *test-only* code in
the EditMode assembly. That is a real artifact of a few hundred lines, not a
free byproduct, and the implementation plan must budget for it.

The asserted band is a return-to-player range, not a point value. Published
figures put the house edge for this exact ruleset — 6 decks, dealer stands soft
17, double after split, no surrender, 3:2 — at roughly 0.4%. That figure must be
confirmed against a published chart when the test is written rather than taken
from this document, and the band must be wide enough to absorb the sampling
variance of 100k rounds. A test that is too tight will flake; one that is too
loose proves nothing.

PlayMode tests are deliberately minimal — two only: the sequencer drains its
queue, and input stays locked while it does.

## 10. Project layout

Assembly-definition boundaries are the load-bearing part.

```
Assets/HouseRules/Blackjack/
  Core/          asmdef HouseRules.Blackjack            ← references NOTHING
    Cards/  Hands/  Round/  Rules/  Settlement/  Events/  Wallet/
  Presentation/  asmdef HouseRules.Blackjack.Presentation  → Core
    Session/  Views/  Tween/
  Tests/
    EditMode/    asmdef HouseRules.Blackjack.Tests          → Core only
    PlayMode/    asmdef HouseRules.Blackjack.PlayModeTests  → Core + Presentation
Assets/HouseRules/Shared/Localization/
Assets/Scenes/Blackjack.unity
Assets/Art/Placeholder/
```

Conventions follow the project standard: PascalCase for public members,
`_camelCase` for private fields with `[SerializeField]`.

## 11. Definition of done

Blackjack is playable on an Android device:

- Bet on 2–3 boxes
- Deal, hit, stand, double, split
- Insurance offered and resolved on a dealer ace
- Dealer plays out, settlement lands, balance updates
- Balance survives an app restart
- All EditMode tests green, including the 100k-round house-edge test
- 60fps with placeholder art

Note that the project's active build target is currently `StandaloneWindows64`.
The Android module is installed, so reaching this definition of done includes
switching the target and verifying on a real device — desktop play-testing does
not satisfy it.

## 12. Explicitly out of scope

This slice does **not** include:

- The other four games (roulette, backgammon, checkers, chess)
- Bot players at other seats
- Surrender
- A rules or settings screen
- Hebrew or RTL support
- Real 3D art
- Sound
- Online multiplayer, leaderboards, IAP, or ads
- Mid-round save/resume

## 13. Follow-on work

Once this slice ships, the natural next steps, in order:

1. Replace placeholder art with Blender-authored assets behind the existing
   prefab interface.
2. Roulette, reusing the wallet, chip economy, and betting UI.
3. Extract whatever the two casino games genuinely share into a common
   foundation — driven by two working games, not designed in advance.
