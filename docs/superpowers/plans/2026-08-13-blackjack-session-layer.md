# Blackjack Session Layer Implementation Plan (Plan 2a)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the layer that drives the blackjack engine from Unity and plays its event stream back over time, with input locked while playback runs.

**Architecture:** The engine resolves each step instantly and synchronously. This layer drains `Round.DrainEvents()` into a queue and plays each event through an `IEventPresenter` seam, one at a time, waiting for each to finish. Player input is accepted only when the queue is drained and the engine is waiting. The presenter seam means every behaviour here is testable with a recording fake — no scene, no art, no prefabs.

**Tech Stack:** Unity 6.3 LTS (`6000.3.22f1`), URP 17.3.0, C# 9, .NET Standard 2.1, Unity Test Framework 1.6.0 (EditMode + PlayMode), NUnit.

**Source spec:** `docs/superpowers/specs/2026-08-13-blackjack-design.md` (§6 presentation and data flow, §8 persistence)

**Scope:** This plan covers the session and playback layer only. Views, the card atlas, prefabs, the URP scene, and the Android device build are Plan 2b, written after this lands against the real `BlackjackSession` API.

## Global Constraints

- Unity `6000.3.22f1`, URP 17.3.0, C# 9, .NET Standard 2.1 target.
- The Core assembly `HouseRules.Blackjack` keeps `"noEngineReferences": true`, `"overrideReferences": true`, and an empty `references` array. **Task 1 is the only task that touches Core**, and it must not weaken that boundary.
- The new assembly `HouseRules.Blackjack.Presentation` references `HouseRules.Blackjack` and may use UnityEngine.
- **Do not use C# `record` types or `init`-only setters** — Unity's .NET Standard 2.1 profile lacks `System.Runtime.CompilerServices.IsExternalInit` and both fail to compile. Use sealed classes with constructor-assigned get-only properties.
- Money is `long`. Never `float`, `double`, or `decimal` for chips.
- Naming: PascalCase for public members; `_camelCase` for private fields, including `[SerializeField]` ones.
- Commit messages follow conventional commits (`feat:`, `fix:`, `test:`, `chore:`).
- Never edit files under `Library/`, `Temp/`, `obj/`, or `Logs/`.
- `.meta` files are committed alongside their assets.

## Working Loop

The Unity Editor must be running. The `unity` CLI is NOT on PATH in a fresh shell — it lives at `C:\Users\bensi\AppData\Local\Unity\bin\unity.exe`. Prefix your shell:

```bash
$env:PATH = "C:\Users\bensi\AppData\Local\Unity\bin;" + $env:PATH
```

After writing or changing any `.cs`, `.asmdef`, or `.json` file:

```bash
unity command recompile
```

Poll until it reports `completed`:

```bash
unity command recompile_status
```

Only then run tests. A stale assembly produces misleading pass/fail results.

- EditMode: `unity command run_tests --mode editor --filter <TestClass>`
- PlayMode: `unity command run_tests --mode playmode --filter <TestClass>`

## The Engine API You Are Building On

This is the real, shipped surface of `HouseRules.Blackjack` — verified against the source, not predicted.

```csharp
// Round
public sealed partial class Round
{
    public Round(BlackjackRules rules, IShoe shoe, Wallet wallet);
    public RoundState State { get; }                       // Betting, Dealing, Insurance, PlayerTurn, DealerTurn, Settlement, Complete
    public IReadOnlyList<Box> Boxes { get; }
    public Hand DealerHand { get; }
    public Card DealerUpcard { get; }                      // throws if the dealer has no cards yet
    public BlackjackRules Rules { get; }
    public bool DealerHasBlackjack { get; }
    public int CurrentBoxIndex { get; }                    // -1 when no hand is current
    public int CurrentHandIndex { get; }
    public Box CurrentBox { get; }                         // null when none
    public Hand CurrentHand { get; }                       // null when none
    public IReadOnlyList<PlayerAction> LegalActions { get; }
    public IReadOnlyList<Settlement> Settlements { get; }
    public long TotalDelta { get; }
    public void PlaceBet(int boxIndex, long wager);
    public void Deal();
    public void Apply(PlayerAction action);
    public IReadOnlyList<GameEvent> DrainEvents();         // returns and clears
}

public enum RoundState { Betting, Dealing, Insurance, PlayerTurn, DealerTurn, Settlement, Complete }
public enum PlayerAction { Hit, Stand, Double, Split, TakeInsurance, DeclineInsurance }
public enum HandOutcome { Win, Lose, Push, Blackjack, Bust }

public sealed class Wallet { public Wallet(long startingBalance); public long Balance { get; } public bool CanAfford(long); public void Debit(long); public void Credit(long); }
public sealed class Shoe : IShoe { public Shoe(int deckCount, double penetration, IRandom random); }
public sealed class SeededRandom : IRandom { public SeededRandom(int seed); public int Seed { get; } }
public readonly struct BlackjackRules { public static BlackjackRules Standard { get; } /* DeckCount, Penetration, MinimumBet, BetIncrement, MaxBoxes, ... */ }
public sealed class Box { public int Index { get; } public long InitialBet { get; } public IReadOnlyList<Hand> Hands { get; } public bool IsActive { get; } public int SplitCount { get; } public long InsuranceBet { get; } }
public sealed class Hand { public IReadOnlyList<Card> Cards { get; } public long Wager { get; } public bool IsFromSplit { get; } public bool IsDoubled { get; } public bool IsClosed { get; } public HandValue Value { get; } public bool IsBust { get; } public bool IsBlackjack { get; } public bool IsPair { get; } }
public readonly struct Card : IEquatable<Card> { public Rank Rank { get; } public Suit Suit { get; } public int BaseValue { get; } }
public sealed class Settlement { public int BoxIndex { get; } public int HandIndex { get; } public HandOutcome Outcome { get; } public long Wager { get; } public long Payout { get; } public long Delta { get; } }
```

**Important:** `Hand` and `Box` mutators are `internal` to Core. The presentation layer reads them and never mutates them — all state change goes through `Round`.

### The full event set

Every one of these can appear in `DrainEvents()`. A presenter must handle all of them.

| Event | Payload |
|---|---|
| `RoundStarted` | — |
| `ShoeReshuffled` | — |
| `CardDealt` | `BoxIndex` (`CardDealt.DealerBoxIndex == -1` means the dealer), `HandIndex`, `Card`, `FaceUp` |
| `PlayerTurnStarted` | `BoxIndex`, `HandIndex` |
| `HandStood` | `BoxIndex`, `HandIndex` |
| `HandBusted` | `BoxIndex`, `HandIndex` |
| `HandDoubled` | `BoxIndex`, `HandIndex`, `NewWager` |
| `HandSplit` | `BoxIndex`, `HandIndex`, `NewHandIndex` |
| `InsuranceOffered` | — |
| `InsuranceTaken` | `BoxIndex`, `Amount` |
| `InsuranceDeclined` | — |
| `InsuranceSettled` | `BoxIndex`, `DealerHadBlackjack`, `Delta` |
| `DealerRevealed` | `HoleCard` |
| `HandSettled` | `Settlement` |
| `RoundSettled` | `TotalDelta` |

---

## File Structure

```
Assets/HouseRules/Blackjack/Core/Round/
  Round.Abandon.cs                      Task 1 — refund + RoundAbandoned event

Assets/HouseRules/Blackjack/Presentation/
  HouseRules.Blackjack.Presentation.asmdef
  Tween/
    Easing.cs                           pure float->float, no Unity types
    Tween.cs                            coroutine move/rotate helpers
  Session/
    IEventPresenter.cs                  the seam views plug into
    EventSequencer.cs                   drains a queue, plays one event at a time
    BlackjackSession.cs                 owns the Round, pumps events, gates input
  Persistence/
    WalletStore.cs                      JSON load/save in persistentDataPath

Assets/HouseRules/Blackjack/Tests/EditMode/
  EasingTests.cs                        Task 2
  WalletStoreTests.cs                   Task 6

Assets/HouseRules/Blackjack/Tests/PlayMode/
  HouseRules.Blackjack.PlayModeTests.asmdef
  RecordingPresenter.cs                 test double for IEventPresenter
  EventSequencerTests.cs                Task 4
  BlackjackSessionTests.cs              Task 5
```

---

### Task 1: `Abandon()` and the refund path

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/Round/Round.Abandon.cs`
- Modify: `Assets/HouseRules/Blackjack/Core/Events/HandEvents.cs` (append `RoundAbandoned`)
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/AbandonTests.cs`

**Interfaces:**
- Consumes: `Round`, `Box`, `Hand`, `Wallet`, `RoundState`, `GameEvent`.
- Produces: `void Abandon()` on `Round`; `RoundAbandoned(long refunded)` event.

Spec §8 requires that quitting mid-round discards the round and refunds wagers. The engine currently has no way to do that, so a caller would have to compute the refund itself — which is rules arithmetic in the caller, exactly what §5.2 forbids.

**Refund rule:** every hand's current `Wager` across every active box, plus each box's `InsuranceBet`. Using `Hand.Wager` rather than `Box.InitialBet` is what makes doubles and splits refund correctly — a doubled hand's wager is already 2×, and a split box has a wager per hand.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/AbandonTests.cs`:

```csharp
using System;
using System.Linq;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class AbandonTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        [Test]
        public void Abandon_BeforeDealing_RefundsEveryBet()
        {
            var wallet = new Wallet(1000);
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), wallet);

            round.PlaceBet(0, 10);
            round.PlaceBet(1, 20);
            Assert.AreEqual(970, wallet.Balance);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
            Assert.AreEqual(RoundState.Complete, round.State);
        }

        [Test]
        public void Abandon_MidPlayerTurn_RefundsTheWager()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Six), C(Rank.Seven), C(Rank.Four)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            Assert.AreEqual(RoundState.PlayerTurn, round.State);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Abandon_AfterDoubling_RefundsTheDoubledWager()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Six), C(Rank.Six), C(Rank.Five), C(Rank.Four), C(Rank.Two)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Double);

            // 1000 - 10 (bet) - 10 (double) = 980. The hand's wager is now 20.
            Assert.AreEqual(980, wallet.Balance);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Abandon_AfterSplitting_RefundsBothHands()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(
                    C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                    C(Rank.Three), C(Rank.Two)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Split);
            Assert.AreEqual(980, wallet.Balance);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Abandon_AfterTakingInsurance_RefundsThePremiumToo()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Ace), C(Rank.Seven), C(Rank.Four)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.TakeInsurance);
            Assert.AreEqual(985, wallet.Balance);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Abandon_EmitsRoundAbandonedWithTheRefundedTotal()
        {
            var wallet = new Wallet(1000);
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), wallet);

            round.PlaceBet(0, 10);
            round.PlaceBet(1, 20);
            round.DrainEvents();

            round.Abandon();

            var abandoned = round.DrainEvents().OfType<RoundAbandoned>().Single();
            Assert.AreEqual(30, abandoned.Refunded);
        }

        [Test]
        public void Abandon_ProducesNoSettlements()
        {
            var wallet = new Wallet(1000);
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), wallet);

            round.PlaceBet(0, 10);
            round.Abandon();

            Assert.IsEmpty(round.Settlements);
            Assert.AreEqual(0, round.TotalDelta);
        }

        [Test]
        public void Abandon_OnACompleteRound_Throws()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Seven)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Stand);
            Assert.AreEqual(RoundState.Complete, round.State);

            Assert.Throws<InvalidOperationException>(() => round.Abandon());
        }

        [Test]
        public void Abandon_Twice_Throws()
        {
            var wallet = new Wallet(1000);
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), wallet);

            round.PlaceBet(0, 10);
            round.Abandon();

            Assert.Throws<InvalidOperationException>(() => round.Abandon());
        }

        [Test]
        public void Abandon_WithNoBets_IsAllowedAndRefundsNothing()
        {
            var wallet = new Wallet(1000);
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), wallet);

            round.Abandon();

            Assert.AreEqual(1000, wallet.Balance);
            Assert.AreEqual(RoundState.Complete, round.State);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
unity command recompile
```

Poll `unity command recompile_status` until `completed`, then:

```bash
unity command run_tests --mode editor --filter AbandonTests
```

Expected: FAIL — `Abandon` and `RoundAbandoned` do not exist.

- [ ] **Step 3: Add the event**

Append to `Assets/HouseRules/Blackjack/Core/Events/HandEvents.cs`:

```csharp
    public sealed class RoundAbandoned : GameEvent
    {
        public RoundAbandoned(long refunded)
        {
            Refunded = refunded;
        }

        /// <summary>Total chips returned to the wallet: every hand's wager plus every insurance premium.</summary>
        public long Refunded { get; }
    }
```

- [ ] **Step 4: Implement `Abandon`**

`Assets/HouseRules/Blackjack/Core/Round/Round.Abandon.cs`:

```csharp
using System;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        /// <summary>
        /// Discard the round and return every staked chip. A round is atomic: a player
        /// who quits mid-hand is made whole rather than settled against.
        /// </summary>
        public void Abandon()
        {
            if (State == RoundState.Complete)
            {
                throw new InvalidOperationException("Cannot abandon a completed round.");
            }

            long refunded = 0;

            foreach (Box box in _boxes)
            {
                if (!box.IsActive)
                {
                    continue;
                }

                // Refund from each hand's CURRENT wager, not the box's initial bet:
                // a doubled hand's wager is already 2x, and a split box has one wager per hand.
                foreach (Hand hand in box.Hands)
                {
                    refunded += hand.Wager;
                }

                refunded += box.InsuranceBet;
            }

            if (refunded > 0)
            {
                _wallet.Credit(refunded);
            }

            Emit(new RoundAbandoned(refunded));
            SetState(RoundState.Complete);
        }
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter AbandonTests
```

Expected: PASS, 10 tests.

- [ ] **Step 6: Confirm no regression and that the Core boundary still holds**

```bash
unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly --timeout 600
```

Expected: PASS, 124 tests (114 existing + 10 new).

Then confirm the asmdef is unchanged — it must still read `"references": []`, `"overrideReferences": true`, `"noEngineReferences": true`:

```bash
cat Assets/HouseRules/Blackjack/Core/HouseRules.Blackjack.asmdef
```

- [ ] **Step 7: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add Abandon with full wager and insurance refund"
```

---

### Task 2: Presentation assembly and easing

**Files:**
- Create: `Assets/HouseRules/Blackjack/Presentation/HouseRules.Blackjack.Presentation.asmdef`
- Create: `Assets/HouseRules/Blackjack/Presentation/Tween/Easing.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/EasingTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: assembly `HouseRules.Blackjack.Presentation`; `static class Easing` with `float Linear(float t)`, `float OutCubic(float t)`, `float InOutCubic(float t)`, `float OutBack(float t)`, and `float Clamp01(float t)`.

`Easing` deliberately contains no Unity types, so it is EditMode-testable without a scene.

- [ ] **Step 1: Create the presentation assembly definition**

`Assets/HouseRules/Blackjack/Presentation/HouseRules.Blackjack.Presentation.asmdef`:

```json
{
    "name": "HouseRules.Blackjack.Presentation",
    "rootNamespace": "HouseRules.Blackjack.Presentation",
    "references": [
        "HouseRules.Blackjack"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Note the deliberate asymmetry with Core: this assembly DOES reference UnityEngine (`noEngineReferences: false`), because it is the layer allowed to touch Unity. Core's boundary is what keeps the rules honest; this side has no such restriction.

- [ ] **Step 2: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/EasingTests.cs`:

```csharp
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
```

- [ ] **Step 3: Add the Presentation reference to the test assembly**

Edit `Assets/HouseRules/Blackjack/Tests/EditMode/HouseRules.Blackjack.Tests.asmdef` and add `"HouseRules.Blackjack.Presentation"` to its `references` array, so it reads:

```json
    "references": [
        "HouseRules.Blackjack",
        "HouseRules.Blackjack.Presentation",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
```

Change nothing else in that file.

- [ ] **Step 4: Run the test to verify it fails**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter EasingTests
```

Expected: FAIL — `Easing` does not exist.

- [ ] **Step 5: Write the implementation**

`Assets/HouseRules/Blackjack/Presentation/Tween/Easing.cs`:

```csharp
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
```

- [ ] **Step 6: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter EasingTests
```

Expected: PASS, 6 tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add presentation assembly and easing curves"
```

---

### Task 3: Coroutine tween helpers

**Files:**
- Create: `Assets/HouseRules/Blackjack/Presentation/Tween/Tween.cs`

**Interfaces:**
- Consumes: `Easing`.
- Produces: `static class Tween` with
  `IEnumerator Move(Transform target, Vector3 to, float duration, Func<float,float> ease = null)`,
  `IEnumerator MoveAndRotate(Transform target, Vector3 toPosition, Quaternion toRotation, float duration, Func<float,float> ease = null)`,
  `IEnumerator Wait(float seconds)`.

There is no test for this task. Coroutine tweening against `Time.deltaTime` is exercised end-to-end by the PlayMode tests in Tasks 4 and 5, and a unit test here would assert Unity's own interpolation rather than our behaviour. The easing maths — the only real logic — is already covered by Task 2.

- [ ] **Step 1: Write the implementation**

`Assets/HouseRules/Blackjack/Presentation/Tween/Tween.cs`:

```csharp
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
```

`Vector3.LerpUnclamped` and `Quaternion.SlerpUnclamped` are deliberate: `OutBack` returns values above 1, and the clamped variants would silently flatten the overshoot that gives the motion its snap.

- [ ] **Step 2: Verify it compiles**

```bash
unity command recompile
```

Poll `unity command recompile_status` until `completed`. Expected: no errors.

Then confirm nothing regressed:

```bash
unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly --timeout 600
```

Expected: PASS, 130 tests.

- [ ] **Step 3: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add coroutine tween helpers"
```

---

### Task 4: The event sequencer

**Files:**
- Create: `Assets/HouseRules/Blackjack/Presentation/Session/IEventPresenter.cs`
- Create: `Assets/HouseRules/Blackjack/Presentation/Session/EventSequencer.cs`
- Create: `Assets/HouseRules/Blackjack/Tests/PlayMode/HouseRules.Blackjack.PlayModeTests.asmdef`
- Create: `Assets/HouseRules/Blackjack/Tests/PlayMode/RecordingPresenter.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/PlayMode/EventSequencerTests.cs`

**Interfaces:**
- Consumes: `GameEvent`.
- Produces: `interface IEventPresenter { IEnumerator Present(GameEvent gameEvent); }`; `sealed class EventSequencer : MonoBehaviour` with `bool IsIdle { get; }`, `int PendingCount { get; }`, `void Enqueue(IEnumerable<GameEvent> events)`, `void SetPresenter(IEventPresenter presenter)`.

The sequencer is the piece that turns an instant event stream into motion over time. It plays exactly one event at a time and reports idle only when the queue is empty and nothing is mid-presentation.

- [ ] **Step 1: Create the PlayMode test assembly**

`Assets/HouseRules/Blackjack/Tests/PlayMode/HouseRules.Blackjack.PlayModeTests.asmdef`:

```json
{
    "name": "HouseRules.Blackjack.PlayModeTests",
    "rootNamespace": "HouseRules.Blackjack.PlayModeTests",
    "references": [
        "HouseRules.Blackjack",
        "HouseRules.Blackjack.Presentation",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Note `includePlatforms` is empty here, unlike the EditMode test assembly which is `["Editor"]` — PlayMode tests must run on the player too.

- [ ] **Step 2: Write the recording presenter**

`Assets/HouseRules/Blackjack/Tests/PlayMode/RecordingPresenter.cs`:

```csharp
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
```

- [ ] **Step 3: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/PlayMode/EventSequencerTests.cs`:

```csharp
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
```

That last test matters: a sequencer with no presenter attached must drain rather than deadlock, or a scene missing a wiring reference would hang the game instead of merely showing nothing.

- [ ] **Step 4: Run the test to verify it fails**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode playmode --filter EventSequencerTests --timeout 600
```

Expected: FAIL — `EventSequencer` and `IEventPresenter` do not exist.

- [ ] **Step 5: Write the presenter seam**

`Assets/HouseRules/Blackjack/Presentation/Session/IEventPresenter.cs`:

```csharp
using System.Collections;
using HouseRules.Blackjack;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Turns one engine event into motion. The sequencer waits for the returned
    /// coroutine to finish before presenting the next event, so an implementation
    /// controls pacing simply by taking as long as it needs.
    /// </summary>
    public interface IEventPresenter
    {
        IEnumerator Present(GameEvent gameEvent);
    }
}
```

- [ ] **Step 6: Write the sequencer**

`Assets/HouseRules/Blackjack/Presentation/Session/EventSequencer.cs`:

```csharp
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
```

- [ ] **Step 7: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode playmode --filter EventSequencerTests --timeout 600
```

Expected: PASS, 8 tests.

- [ ] **Step 8: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add event sequencer and presenter seam"
```

---

### Task 5: The session bridge

**Files:**
- Create: `Assets/HouseRules/Blackjack/Presentation/Session/BlackjackSession.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/PlayMode/BlackjackSessionTests.cs`

**Interfaces:**
- Consumes: `Round`, `Wallet`, `Shoe`, `SeededRandom`, `BlackjackRules`, `PlayerAction`, `RoundState`, `EventSequencer`, `IEventPresenter`.
- Produces: `sealed class BlackjackSession : MonoBehaviour` with
  `Wallet Wallet { get; }`, `Round CurrentRound { get; }`, `RoundState State { get; }`,
  `bool IsBusy { get; }`, `bool CanAcceptInput { get; }`,
  `IReadOnlyList<PlayerAction> LegalActions { get; }`,
  `void Configure(BlackjackRules rules, IShoe shoe, Wallet wallet, EventSequencer sequencer)`,
  `void BeginRound()`, `void PlaceBet(int boxIndex, long wager)`, `void Deal()`,
  `void Apply(PlayerAction action)`, `void AbandonRound()`,
  and the event `event Action RoundCompleted`.

This is the only bridge between the two assemblies. It owns the `Round`, pumps drained events into the sequencer, and enforces the one rule that removes a whole class of bugs: **input is accepted only when the sequencer is idle.**

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/PlayMode/BlackjackSessionTests.cs`:

```csharp
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
```

- [ ] **Step 2: Add the scripted shoe helper the tests need**

The PlayMode assembly cannot see `StackedShoe`, which lives in the EditMode test assembly. Add an equivalent to the PlayMode assembly — `Assets/HouseRules/Blackjack/Tests/PlayMode/ScriptedShoe.cs`:

```csharp
using System;
using System.Collections.Generic;
using HouseRules.Blackjack;

namespace HouseRules.Blackjack.PlayModeTests
{
    /// <summary>
    /// Deals a scripted sequence, then a repeating filler card. Mirrors the EditMode
    /// StackedShoe; duplicated rather than shared because the two test assemblies
    /// cannot reference one another.
    /// </summary>
    public sealed class ScriptedShoe : IShoe
    {
        private readonly List<Card> _scripted;
        private readonly Card _filler = new Card(Rank.Five, Suit.Clubs);
        private int _index;

        public ScriptedShoe(params Card[] scripted)
        {
            _scripted = new List<Card>(scripted ?? Array.Empty<Card>());
        }

        public int Remaining => int.MaxValue;

        public bool NeedsReshuffle => false;

        public Card Deal()
        {
            if (_index < _scripted.Count)
            {
                return _scripted[_index++];
            }

            _index++;
            return _filler;
        }

        public void Reshuffle() => _index = 0;
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode playmode --filter BlackjackSessionTests --timeout 600
```

Expected: FAIL — `BlackjackSession` does not exist.

- [ ] **Step 4: Write the session**

`Assets/HouseRules/Blackjack/Presentation/Session/BlackjackSession.cs`:

```csharp
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
        public bool IsBusy => _sequencer != null && !_sequencer.IsIdle;

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
            // Announce completion only once playback has caught up, so a listener
            // that shows a result screen does not pre-empt the settlement animation.
            if (_completionAnnounced || CurrentRound == null)
            {
                return;
            }

            if (CurrentRound.State == RoundState.Complete && !IsBusy)
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
```

- [ ] **Step 5: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode playmode --filter BlackjackSessionTests --timeout 600
```

Expected: PASS, 9 tests.

- [ ] **Step 6: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add session bridge with input gating during playback"
```

---

### Task 6: Wallet persistence

**Files:**
- Create: `Assets/HouseRules/Blackjack/Presentation/Persistence/WalletStore.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/WalletStoreTests.cs`

**Interfaces:**
- Consumes: `Wallet`.
- Produces: `sealed class WalletStore` with constructor `WalletStore(string filePath)`, plus
  `long StartingBalanceDefault { get; }`, `Wallet Load()`, `void Save(Wallet wallet)`, `void Delete()`,
  and `static string DefaultPath { get; }`.

Spec §8: only the wallet balance is saved, as JSON in `Application.persistentDataPath`. A round is atomic and is never persisted mid-play.

The constructor takes an explicit path so tests can write to a temp file rather than the real save location.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/WalletStoreTests.cs`:

```csharp
using System.IO;
using HouseRules.Blackjack.Presentation;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class WalletStoreTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"houserules-wallet-test-{Path.GetRandomFileName()}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        [Test]
        public void Load_WithNoSaveFile_ReturnsTheStartingBalance()
        {
            var store = new WalletStore(_path);
            Wallet wallet = store.Load();

            Assert.AreEqual(store.StartingBalanceDefault, wallet.Balance);
        }

        [Test]
        public void Save_ThenLoad_RoundTripsTheBalance()
        {
            var store = new WalletStore(_path);
            var wallet = new Wallet(4242);

            store.Save(wallet);
            Wallet reloaded = store.Load();

            Assert.AreEqual(4242, reloaded.Balance);
        }

        [Test]
        public void Save_WritesJsonContainingTheBalance()
        {
            var store = new WalletStore(_path);
            store.Save(new Wallet(777));

            Assert.IsTrue(File.Exists(_path));
            StringAssert.Contains("777", File.ReadAllText(_path));
        }

        [Test]
        public void Save_Twice_OverwritesRatherThanAppends()
        {
            var store = new WalletStore(_path);

            store.Save(new Wallet(100));
            store.Save(new Wallet(200));

            Assert.AreEqual(200, store.Load().Balance);
        }

        [Test]
        public void Load_WithCorruptFile_FallsBackToTheStartingBalance()
        {
            File.WriteAllText(_path, "this is not json {{{");

            var store = new WalletStore(_path);
            Wallet wallet = store.Load();

            Assert.AreEqual(store.StartingBalanceDefault, wallet.Balance);
        }

        [Test]
        public void Load_WithNegativeBalance_FallsBackToTheStartingBalance()
        {
            File.WriteAllText(_path, "{\"balance\":-500}");

            var store = new WalletStore(_path);

            Assert.AreEqual(store.StartingBalanceDefault, store.Load().Balance);
        }

        [Test]
        public void Load_WithZeroBalance_IsPreserved()
        {
            // Busting out is a real state, not corruption.
            var store = new WalletStore(_path);
            store.Save(new Wallet(0));

            Assert.AreEqual(0, store.Load().Balance);
        }

        [Test]
        public void Delete_RemovesTheSaveFile()
        {
            var store = new WalletStore(_path);
            store.Save(new Wallet(500));
            Assert.IsTrue(File.Exists(_path));

            store.Delete();

            Assert.IsFalse(File.Exists(_path));
            Assert.AreEqual(store.StartingBalanceDefault, store.Load().Balance);
        }

        [Test]
        public void Delete_WithNoFile_DoesNotThrow()
        {
            var store = new WalletStore(_path);
            Assert.DoesNotThrow(() => store.Delete());
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter WalletStoreTests
```

Expected: FAIL — `WalletStore` does not exist.

- [ ] **Step 3: Write the implementation**

`Assets/HouseRules/Blackjack/Presentation/Persistence/WalletStore.cs`:

```csharp
using System;
using System.IO;
using HouseRules.Blackjack;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Persists only the chip balance, as JSON. A round is atomic and is never saved
    /// mid-play: quitting mid-round abandons it and refunds the stake instead.
    /// </summary>
    public sealed class WalletStore
    {
        private const long DefaultStartingBalance = 1000;
        private const string FileName = "wallet.json";

        private readonly string _filePath;

        public WalletStore(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        public long StartingBalanceDefault => DefaultStartingBalance;

        public Wallet Load()
        {
            if (!File.Exists(_filePath))
            {
                return new Wallet(DefaultStartingBalance);
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                // A missing or negative balance means the file is damaged. Chips are
                // play money, so recovering to the default beats refusing to start.
                if (data == null || data.balance < 0)
                {
                    return new Wallet(DefaultStartingBalance);
                }

                return new Wallet(data.balance);
            }
            catch (Exception)
            {
                return new Wallet(DefaultStartingBalance);
            }
        }

        public void Save(Wallet wallet)
        {
            if (wallet == null)
            {
                throw new ArgumentNullException(nameof(wallet));
            }

            var data = new SaveData { balance = wallet.Balance };
            string directory = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonUtility.ToJson(data));
        }

        public void Delete()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        /// <summary>
        /// Serializable carrier. JsonUtility requires a concrete type with public
        /// fields — it cannot serialize properties or anonymous types.
        /// </summary>
        [Serializable]
        private sealed class SaveData
        {
            public long balance;
        }
    }
}
```

`JsonUtility.FromJson` returning malformed data throws, and a `null` result is possible for empty input — both are handled. The field is lowercase `balance` because `JsonUtility` matches field names verbatim, and the test asserts on that exact JSON shape.

- [ ] **Step 4: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter WalletStoreTests
```

Expected: PASS, 9 tests.

- [ ] **Step 5: Run the complete suite**

```bash
unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly --timeout 600
```

Expected: PASS, 139 EditMode tests.

```bash
unity command run_tests --mode playmode --timeout 600
```

Expected: PASS, 17 PlayMode tests.

- [ ] **Step 6: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add wallet JSON persistence with corrupt-file recovery"
```

---

## Completion Criteria

Plan 2a is done when:

- All EditMode tests pass: `unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly`
- All PlayMode tests pass: `unity command run_tests --mode playmode`
- `Round.Abandon()` refunds every hand wager plus insurance premiums, verified for the double, split, and insurance cases.
- `BlackjackSession.LegalActions` is empty whenever the sequencer is mid-playback, and applying an action in that window is a no-op.
- The Core asmdef still has `"references": []`, `"overrideReferences": true`, and `"noEngineReferences": true`.

At that point a round can be driven end-to-end from Unity with correct pacing and input gating, and Plan 2b can write views against a real `IEventPresenter` contract.

## Explicitly Out Of Scope (Plan 2b)

- `CardView`, `HandView`, `BoxView`, `TableView`, `ActionBarView`, `WalletView`
- The 52-card face atlas and placeholder materials
- Card view pooling
- The `Blackjack.unity` scene and its camera/lighting rig
- The real `IEventPresenter` implementation that drives those views
- Switching the build target to Android and verifying on a device
