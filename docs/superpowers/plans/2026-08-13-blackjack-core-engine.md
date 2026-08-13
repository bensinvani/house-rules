# Blackjack Core Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a fully tested, headless blackjack rules engine in pure C# with no UnityEngine dependency.

**Architecture:** A single assembly `HouseRules.Blackjack` with `noEngineReferences: true`, so the compiler forbids any Unity dependency. The engine is a synchronous state machine: callers place bets, deal, and apply player actions; the engine advances state, emits an ordered event stream, and settles against a wallet. All randomness flows through an `IRandom` seam, and all card supply through an `IShoe` seam, so tests can script exact scenarios.

**Tech Stack:** Unity 6.3 LTS (`6000.3.22f1`), C# 9, .NET Standard 2.1, Unity Test Framework 1.6.0 (EditMode), NUnit.

**Source spec:** `docs/superpowers/specs/2026-08-13-blackjack-design.md`

**Scope:** This plan covers the core engine only. The URP presentation layer (views, sequencer, scene, persistence, Android build) is Plan 2, written after this one lands.

## Global Constraints

- Unity `6000.3.22f1`, URP 17.3.0, C# 9, .NET Standard 2.1 target.
- `HouseRules.Blackjack` asmdef MUST set `"noEngineReferences": true` and have an empty `references` array. Any UnityEngine dependency in the core is a defect.
- Naming: PascalCase for public members; `_camelCase` for private fields.
- **Do not use C# `record` types or `init`-only setters.** Unity's .NET Standard 2.1 profile lacks `System.Runtime.CompilerServices.IsExternalInit`, so both fail to compile without a hand-written polyfill. Use sealed classes with constructor-assigned get-only properties.
- Chips are `long`. Never `float`, `double`, or `decimal` for money.
- Every wager must be even (see Task 4). `wager * 3 / 2` must be exact integer math.
- Commit messages follow conventional commits (`feat:`, `fix:`, `test:`, `chore:`).
- Never edit files under `Library/`, `Temp/`, `obj/`, or `Logs/`.
- `.meta` files are committed alongside their assets.

## Working Loop

The Unity Editor must be running. After writing or changing any `.cs` file, the Editor must recompile before tests can see the change:

```bash
unity command recompile
```

Then poll until it reports `completed`:

```bash
unity command recompile_status
```

Only then run tests. If `recompile_status` reports compile errors, fix them before running tests — a stale assembly will otherwise produce misleading pass/fail results.

## Deviations From The Spec — Flagged For Approval

1. **Box count.** The spec says the player bets on 2–3 boxes. This plan implements a maximum of 3 boxes and requires **at least one** bet to deal. Forcing a minimum of two is a UI constraint, not a rules constraint, and baking it into the engine would make single-box tests awkward. Flagging rather than silently changing.
2. **Dealer peek.** The spec does not mention it. This plan implements US peek rules: the dealer checks for blackjack on an ace or ten upcard before player action. Without peek, players lose double and split money to a dealer blackjack, which is wrong for the standard ruleset the spec specifies.

---

## File Structure

```
Assets/HouseRules/Blackjack/Core/
  HouseRules.Blackjack.asmdef
  Cards/
    Suit.cs             Suit enum
    Rank.cs             Rank enum
    Card.cs             readonly struct, base value
    IRandom.cs          randomness seam
    SeededRandom.cs     System.Random adapter
    IShoe.cs            card supply seam
    Shoe.cs             6-deck shoe, Fisher-Yates, penetration reshuffle
  Hands/
    HandValue.cs        total + soft flag
    Hand.cs             cards, wager, split/double state
    Box.cs              one betting box, owns 1..4 hands
  Rules/
    BlackjackRules.cs   ruleset struct + Standard preset
  Wallet/
    Wallet.cs           integer chip balance
  Round/
    RoundState.cs       state enum
    PlayerAction.cs     intent enum
    Round.cs            the state machine
  Settlement/
    HandOutcome.cs      outcome enum
    Settlement.cs       per-hand settlement record
  Events/
    GameEvent.cs        base + all event types

Assets/HouseRules/Blackjack/Tests/EditMode/
  HouseRules.Blackjack.Tests.asmdef
  StackedShoe.cs        test double: deals a scripted sequence
  BasicStrategy.cs      test-only strategy table
  CardTests.cs  HandTests.cs  ShoeTests.cs  WalletTests.cs
  RoundDealTests.cs  LegalActionsTests.cs  PlayerActionTests.cs
  SplitTests.cs  InsuranceTests.cs  DealerTests.cs
  SettlementTests.cs  EventTests.cs  HouseEdgeTests.cs
```

---

### Task 1: Assemblies and card primitives

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/HouseRules.Blackjack.asmdef`
- Create: `Assets/HouseRules/Blackjack/Core/Cards/Suit.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Cards/Rank.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Cards/Card.cs`
- Create: `Assets/HouseRules/Blackjack/Tests/EditMode/HouseRules.Blackjack.Tests.asmdef`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/CardTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `enum Suit { Clubs, Diamonds, Hearts, Spades }`; `enum Rank { Two=2 … Ace=14 }`; `readonly struct Card` with `Rank Rank`, `Suit Suit`, `int BaseValue`, constructor `Card(Rank, Suit)`.

- [ ] **Step 1: Create the core assembly definition**

`Assets/HouseRules/Blackjack/Core/HouseRules.Blackjack.asmdef`:

```json
{
    "name": "HouseRules.Blackjack",
    "rootNamespace": "HouseRules.Blackjack",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

`"noEngineReferences": true` is the load-bearing line. It makes `using UnityEngine;` a compile error in this assembly, which is what enforces the architecture rather than relying on discipline.

- [ ] **Step 2: Create the test assembly definition**

`Assets/HouseRules/Blackjack/Tests/EditMode/HouseRules.Blackjack.Tests.asmdef`:

```json
{
    "name": "HouseRules.Blackjack.Tests",
    "rootNamespace": "HouseRules.Blackjack.Tests",
    "references": [
        "HouseRules.Blackjack",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
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

- [ ] **Step 3: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/CardTests.cs`:

```csharp
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class CardTests
    {
        [Test]
        public void NumberCard_BaseValue_IsItsRank()
        {
            var card = new Card(Rank.Seven, Suit.Hearts);
            Assert.AreEqual(7, card.BaseValue);
        }

        [Test]
        public void FaceCards_BaseValue_IsTen()
        {
            Assert.AreEqual(10, new Card(Rank.Jack, Suit.Clubs).BaseValue);
            Assert.AreEqual(10, new Card(Rank.Queen, Suit.Clubs).BaseValue);
            Assert.AreEqual(10, new Card(Rank.King, Suit.Clubs).BaseValue);
        }

        [Test]
        public void Ace_BaseValue_IsEleven()
        {
            Assert.AreEqual(11, new Card(Rank.Ace, Suit.Spades).BaseValue);
        }

        [Test]
        public void Cards_WithSameRankAndSuit_AreEqual()
        {
            Assert.AreEqual(new Card(Rank.Nine, Suit.Diamonds), new Card(Rank.Nine, Suit.Diamonds));
            Assert.AreNotEqual(new Card(Rank.Nine, Suit.Diamonds), new Card(Rank.Nine, Suit.Clubs));
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

```bash
unity command recompile
```

Poll `unity command recompile_status` until `completed`, then:

```bash
unity command run_tests --mode editor --filter CardTests
```

Expected: FAIL. The compile will report that `Card`, `Rank`, and `Suit` do not exist.

- [ ] **Step 5: Write the implementation**

`Assets/HouseRules/Blackjack/Core/Cards/Suit.cs`:

```csharp
namespace HouseRules.Blackjack
{
    public enum Suit
    {
        Clubs,
        Diamonds,
        Hearts,
        Spades
    }
}
```

`Assets/HouseRules/Blackjack/Core/Cards/Rank.cs`:

```csharp
namespace HouseRules.Blackjack
{
    public enum Rank
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }
}
```

`Assets/HouseRules/Blackjack/Core/Cards/Card.cs`:

```csharp
using System;

namespace HouseRules.Blackjack
{
    public readonly struct Card : IEquatable<Card>
    {
        public Rank Rank { get; }
        public Suit Suit { get; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        /// <summary>
        /// Value before any ace demotion. Aces count 11 here; <see cref="Hand"/> demotes them.
        /// </summary>
        public int BaseValue
        {
            get
            {
                switch (Rank)
                {
                    case Rank.Ace:
                        return 11;
                    case Rank.Jack:
                    case Rank.Queen:
                    case Rank.King:
                        return 10;
                    default:
                        return (int)Rank;
                }
            }
        }

        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;

        public override bool Equals(object obj) => obj is Card other && Equals(other);

        public override int GetHashCode() => ((int)Rank * 4) + (int)Suit;

        public override string ToString() => $"{Rank} of {Suit}";
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter CardTests
```

Expected: PASS, 4 tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add card primitives and engine assembly definitions"
```

---

### Task 2: Hand value with ace demotion

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/Hands/HandValue.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Hands/Hand.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/HandTests.cs`

**Interfaces:**
- Consumes: `Card`, `Rank`, `Suit` from Task 1.
- Produces: `readonly struct HandValue` with `int Total`, `bool IsSoft`, `bool IsBust`. `sealed class Hand` with `IReadOnlyList<Card> Cards`, `long Wager`, `bool IsFromSplit`, `bool IsDoubled`, `bool IsClosed`, `HandValue Value`, `bool IsBust`, `bool IsBlackjack`, `bool IsPair`, methods `Add(Card)`, `SetWager(long)`, `MarkDoubled()`, `Close()`. Constructor `Hand(long wager, bool isFromSplit = false)`.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/HandTests.cs`:

```csharp
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class HandTests
    {
        private static Hand HandOf(params Card[] cards)
        {
            var hand = new Hand(10);
            foreach (var card in cards)
            {
                hand.Add(card);
            }
            return hand;
        }

        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        [Test]
        public void HardTotal_SumsBaseValues()
        {
            var hand = HandOf(C(Rank.Nine), C(Rank.Seven));
            Assert.AreEqual(16, hand.Value.Total);
            Assert.IsFalse(hand.Value.IsSoft);
        }

        [Test]
        public void SingleAce_CountsEleven_WhenItFits()
        {
            var hand = HandOf(C(Rank.Ace), C(Rank.Six));
            Assert.AreEqual(17, hand.Value.Total);
            Assert.IsTrue(hand.Value.IsSoft);
        }

        [Test]
        public void Ace_Demotes_WhenElevenWouldBust()
        {
            var hand = HandOf(C(Rank.Ace), C(Rank.Six), C(Rank.Nine));
            Assert.AreEqual(16, hand.Value.Total);
            Assert.IsFalse(hand.Value.IsSoft);
        }

        [Test]
        public void TwoAcesAndNine_Is21_Not31()
        {
            var hand = HandOf(C(Rank.Ace), C(Rank.Ace), C(Rank.Nine));
            Assert.AreEqual(21, hand.Value.Total);
            Assert.IsTrue(hand.Value.IsSoft);
        }

        [Test]
        public void FourAces_Is14()
        {
            var hand = HandOf(C(Rank.Ace), C(Rank.Ace), C(Rank.Ace), C(Rank.Ace));
            Assert.AreEqual(14, hand.Value.Total);
        }

        [Test]
        public void Bust_WhenOver21()
        {
            var hand = HandOf(C(Rank.King), C(Rank.Queen), C(Rank.Five));
            Assert.IsTrue(hand.IsBust);
        }

        [Test]
        public void Blackjack_IsNatural21_OnTwoCards()
        {
            var hand = HandOf(C(Rank.Ace), C(Rank.King));
            Assert.IsTrue(hand.IsBlackjack);
        }

        [Test]
        public void Blackjack_IsFalse_ForThreeCard21()
        {
            var hand = HandOf(C(Rank.Seven), C(Rank.Seven), C(Rank.Seven));
            Assert.AreEqual(21, hand.Value.Total);
            Assert.IsFalse(hand.IsBlackjack);
        }

        [Test]
        public void Blackjack_IsFalse_ForSplitHand()
        {
            var hand = new Hand(10, isFromSplit: true);
            hand.Add(C(Rank.Ace));
            hand.Add(C(Rank.King));
            Assert.AreEqual(21, hand.Value.Total);
            Assert.IsFalse(hand.IsBlackjack);
        }

        [Test]
        public void IsPair_ComparesRank_NotSuit()
        {
            var hand = new Hand(10);
            hand.Add(new Card(Rank.Eight, Suit.Clubs));
            hand.Add(new Card(Rank.Eight, Suit.Hearts));
            Assert.IsTrue(hand.IsPair);
        }

        [Test]
        public void IsPair_IsFalse_ForTenAndKing()
        {
            var hand = HandOf(C(Rank.Ten), C(Rank.King));
            Assert.IsFalse(hand.IsPair);
        }
    }
}
```

Note the last case: ten and king both score 10 but are not a pair. Casinos differ on this; this engine splits on rank only, which is the stricter and more common rule.

- [ ] **Step 2: Run the test to verify it fails**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter HandTests
```

Expected: FAIL — `Hand` and `HandValue` do not exist.

- [ ] **Step 3: Write the implementation**

`Assets/HouseRules/Blackjack/Core/Hands/HandValue.cs`:

```csharp
namespace HouseRules.Blackjack
{
    public readonly struct HandValue
    {
        public int Total { get; }

        /// <summary>True when an ace is still counting as 11.</summary>
        public bool IsSoft { get; }

        public HandValue(int total, bool isSoft)
        {
            Total = total;
            IsSoft = isSoft;
        }

        public bool IsBust => Total > 21;

        public override string ToString() => IsSoft ? $"soft {Total}" : Total.ToString();
    }
}
```

`Assets/HouseRules/Blackjack/Core/Hands/Hand.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed class Hand
    {
        private readonly List<Card> _cards = new List<Card>();

        public Hand(long wager, bool isFromSplit = false)
        {
            Wager = wager;
            IsFromSplit = isFromSplit;
        }

        public IReadOnlyList<Card> Cards => _cards;

        public long Wager { get; private set; }

        /// <summary>True when this hand was produced by splitting another.</summary>
        public bool IsFromSplit { get; }

        public bool IsDoubled { get; private set; }

        /// <summary>True once the hand can take no further action (stood, doubled, busted, or a split ace).</summary>
        public bool IsClosed { get; private set; }

        public void Add(Card card) => _cards.Add(card);

        public void SetWager(long wager) => Wager = wager;

        public void MarkDoubled() => IsDoubled = true;

        public void Close() => IsClosed = true;

        public HandValue Value
        {
            get
            {
                int total = 0;
                int aces = 0;

                foreach (var card in _cards)
                {
                    total += card.BaseValue;
                    if (card.Rank == Rank.Ace)
                    {
                        aces++;
                    }
                }

                // Demote aces from 11 to 1 until the hand fits, or we run out of aces.
                while (total > 21 && aces > 0)
                {
                    total -= 10;
                    aces--;
                }

                return new HandValue(total, aces > 0);
            }
        }

        public bool IsBust => Value.IsBust;

        /// <summary>A natural 21 on the first two cards. A 21 formed after a split does not count.</summary>
        public bool IsBlackjack => _cards.Count == 2 && Value.Total == 21 && !IsFromSplit;

        public bool IsPair => _cards.Count == 2 && _cards[0].Rank == _cards[1].Rank;

        public override string ToString() => string.Join(", ", _cards);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter HandTests
```

Expected: PASS, 11 tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add hand value with ace demotion"
```

---

### Task 3: Randomness and shoe seams

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/Cards/IRandom.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Cards/SeededRandom.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Cards/IShoe.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Cards/Shoe.cs`
- Create: `Assets/HouseRules/Blackjack/Tests/EditMode/StackedShoe.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/ShoeTests.cs`

**Interfaces:**
- Consumes: `Card`, `Rank`, `Suit`.
- Produces: `interface IRandom { int Next(int maxExclusive); }`; `sealed class SeededRandom : IRandom` with constructor `SeededRandom(int seed)`; `interface IShoe { Card Deal(); int Remaining { get; } bool NeedsReshuffle { get; } void Reshuffle(); }`; `sealed class Shoe : IShoe` with constructor `Shoe(int deckCount, double penetration, IRandom random)`; test double `StackedShoe : IShoe` with constructor `StackedShoe(params Card[] scripted)`.

`StackedShoe` is the technique the whole test suite rests on — it turns "player holds 8,8 against a dealer 6" from a lottery into a one-line arrangement.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/ShoeTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class ShoeTests
    {
        [Test]
        public void SixDeckShoe_Holds312Cards()
        {
            var shoe = new Shoe(6, 0.75, new SeededRandom(1));
            Assert.AreEqual(312, shoe.Remaining);
        }

        [Test]
        public void Shoe_DealsEveryCardExactlyOnce()
        {
            var shoe = new Shoe(6, 1.0, new SeededRandom(42));
            var counts = new Dictionary<Card, int>();

            while (shoe.Remaining > 0)
            {
                var card = shoe.Deal();
                counts.TryGetValue(card, out int seen);
                counts[card] = seen + 1;
            }

            Assert.AreEqual(52, counts.Count, "Expected all 52 distinct cards.");
            foreach (var pair in counts)
            {
                Assert.AreEqual(6, pair.Value, $"{pair.Key} should appear exactly 6 times in a 6-deck shoe.");
            }
        }

        [Test]
        public void SameSeed_ProducesSameOrder()
        {
            var a = new Shoe(6, 1.0, new SeededRandom(7));
            var b = new Shoe(6, 1.0, new SeededRandom(7));

            for (int i = 0; i < 312; i++)
            {
                Assert.AreEqual(a.Deal(), b.Deal(), $"Divergence at card {i}.");
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentOrder()
        {
            var a = new Shoe(6, 1.0, new SeededRandom(1));
            var b = new Shoe(6, 1.0, new SeededRandom(2));

            bool anyDifference = false;
            for (int i = 0; i < 312; i++)
            {
                if (!a.Deal().Equals(b.Deal()))
                {
                    anyDifference = true;
                }
            }

            Assert.IsTrue(anyDifference);
        }

        [Test]
        public void NeedsReshuffle_TripsAtPenetration()
        {
            var shoe = new Shoe(6, 0.75, new SeededRandom(1));

            // 75% of 312 is 234.
            for (int i = 0; i < 233; i++)
            {
                shoe.Deal();
                Assert.IsFalse(shoe.NeedsReshuffle, $"Tripped early at card {i + 1}.");
            }

            shoe.Deal();
            Assert.IsTrue(shoe.NeedsReshuffle);
        }

        [Test]
        public void Reshuffle_RestoresFullShoe()
        {
            var shoe = new Shoe(6, 0.75, new SeededRandom(1));
            for (int i = 0; i < 240; i++)
            {
                shoe.Deal();
            }

            shoe.Reshuffle();

            Assert.AreEqual(312, shoe.Remaining);
            Assert.IsFalse(shoe.NeedsReshuffle);
        }

        [Test]
        public void StackedShoe_DealsScriptedOrder()
        {
            var shoe = new StackedShoe(
                new Card(Rank.Eight, Suit.Clubs),
                new Card(Rank.Six, Suit.Hearts));

            Assert.AreEqual(new Card(Rank.Eight, Suit.Clubs), shoe.Deal());
            Assert.AreEqual(new Card(Rank.Six, Suit.Hearts), shoe.Deal());
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
unity command run_tests --mode editor --filter ShoeTests
```

Expected: FAIL — `Shoe`, `SeededRandom`, and `StackedShoe` do not exist.

- [ ] **Step 3: Write the seams**

`Assets/HouseRules/Blackjack/Core/Cards/IRandom.cs`:

```csharp
namespace HouseRules.Blackjack
{
    /// <summary>
    /// The single source of randomness in the engine. Seeded so any round replays exactly.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Returns a value in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }
}
```

`Assets/HouseRules/Blackjack/Core/Cards/SeededRandom.cs`:

```csharp
using System;

namespace HouseRules.Blackjack
{
    public sealed class SeededRandom : IRandom
    {
        private readonly Random _random;

        public SeededRandom(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Seed { get; }

        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }
}
```

`Assets/HouseRules/Blackjack/Core/Cards/IShoe.cs`:

```csharp
namespace HouseRules.Blackjack
{
    public interface IShoe
    {
        Card Deal();
        int Remaining { get; }
        bool NeedsReshuffle { get; }
        void Reshuffle();
    }
}
```

- [ ] **Step 4: Write the shoe**

`Assets/HouseRules/Blackjack/Core/Cards/Shoe.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed class Shoe : IShoe
    {
        private readonly List<Card> _cards;
        private readonly IRandom _random;
        private readonly int _reshuffleAt;
        private int _index;

        public Shoe(int deckCount, double penetration, IRandom random)
        {
            if (deckCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deckCount), "Deck count must be positive.");
            }

            if (penetration <= 0.0 || penetration > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(penetration), "Penetration must be in (0, 1].");
            }

            _random = random ?? throw new ArgumentNullException(nameof(random));
            _cards = new List<Card>(deckCount * 52);

            for (int deck = 0; deck < deckCount; deck++)
            {
                foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                {
                    foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    {
                        _cards.Add(new Card(rank, suit));
                    }
                }
            }

            _reshuffleAt = (int)(_cards.Count * penetration);
            Shuffle();
        }

        public int Remaining => _cards.Count - _index;

        public bool NeedsReshuffle => _index >= _reshuffleAt;

        public Card Deal()
        {
            if (_index >= _cards.Count)
            {
                throw new InvalidOperationException("Shoe is exhausted. Reshuffle before dealing.");
            }

            return _cards[_index++];
        }

        public void Reshuffle()
        {
            _index = 0;
            Shuffle();
        }

        private void Shuffle()
        {
            // Fisher-Yates.
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                Card swap = _cards[i];
                _cards[i] = _cards[j];
                _cards[j] = swap;
            }
        }
    }
}
```

- [ ] **Step 5: Write the stacked shoe test double**

`Assets/HouseRules/Blackjack/Tests/EditMode/StackedShoe.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace HouseRules.Blackjack.Tests
{
    /// <summary>
    /// Deals a scripted sequence, so a test can arrange an exact scenario.
    /// Once the script runs out it deals a repeating filler rank, which keeps
    /// tests that only care about the opening cards short.
    /// </summary>
    public sealed class StackedShoe : IShoe
    {
        private readonly List<Card> _scripted;
        private readonly Card _filler;
        private int _index;

        public StackedShoe(params Card[] scripted)
            : this(new Card(Rank.Five, Suit.Clubs), scripted)
        {
        }

        private StackedShoe(Card filler, Card[] scripted)
        {
            _scripted = new List<Card>(scripted ?? Array.Empty<Card>());
            _filler = filler;
        }

        /// <summary>
        /// Script with an explicit filler card.
        /// This is a named factory, not a second public constructor, on purpose:
        /// a public `StackedShoe(Card filler, params Card[] scripted)` would make
        /// `new StackedShoe(a, b)` bind to it — silently taking `a` as the filler
        /// when the caller meant a two-card script. A named factory cannot be
        /// picked by accident.
        /// </summary>
        public static StackedShoe WithFiller(Card filler, params Card[] scripted)
        {
            return new StackedShoe(filler, scripted);
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

        /// <summary>Number of cards dealt so far, including filler.</summary>
        public int DealtCount => _index;
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter ShoeTests
```

Expected: PASS, 7 tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add seeded shoe with penetration reshuffle and stacked test double"
```

---

### Task 4: Rules and wallet

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/Rules/BlackjackRules.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Wallet/Wallet.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/WalletTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `readonly struct BlackjackRules` with `int DeckCount`, `bool DealerHitsSoft17`, `bool DoubleAfterSplit`, `int MaxSplitsPerBox`, `bool ResplitAces`, `bool HitSplitAces`, `bool SurrenderAllowed`, `bool InsuranceOffered`, `double Penetration`, `long MinimumBet`, `long BetIncrement`, `int MaxBoxes`, and static property `Standard`. `sealed class Wallet` with `long Balance`, constructor `Wallet(long startingBalance)`, methods `Debit(long)`, `Credit(long)`, `bool CanAfford(long)`.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/WalletTests.cs`:

```csharp
using System;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class WalletTests
    {
        [Test]
        public void NewWallet_HasStartingBalance()
        {
            Assert.AreEqual(1000, new Wallet(1000).Balance);
        }

        [Test]
        public void Debit_ReducesBalance()
        {
            var wallet = new Wallet(1000);
            wallet.Debit(250);
            Assert.AreEqual(750, wallet.Balance);
        }

        [Test]
        public void Credit_IncreasesBalance()
        {
            var wallet = new Wallet(1000);
            wallet.Credit(250);
            Assert.AreEqual(1250, wallet.Balance);
        }

        [Test]
        public void Debit_BeyondBalance_Throws()
        {
            var wallet = new Wallet(100);
            Assert.Throws<InvalidOperationException>(() => wallet.Debit(101));
        }

        [Test]
        public void Debit_NeverLeavesNegativeBalance()
        {
            var wallet = new Wallet(100);
            try
            {
                wallet.Debit(500);
            }
            catch (InvalidOperationException)
            {
                // expected
            }

            Assert.AreEqual(100, wallet.Balance);
        }

        [Test]
        public void Debit_NonPositiveAmount_Throws()
        {
            var wallet = new Wallet(100);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Debit(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Debit(-5));
        }

        [Test]
        public void Credit_NonPositiveAmount_Throws()
        {
            var wallet = new Wallet(100);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Credit(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Credit(-5));
        }

        [Test]
        public void CanAfford_ReflectsBalance()
        {
            var wallet = new Wallet(100);
            Assert.IsTrue(wallet.CanAfford(100));
            Assert.IsFalse(wallet.CanAfford(101));
        }

        [Test]
        public void StandardRules_MatchTheSpec()
        {
            var rules = BlackjackRules.Standard;

            Assert.AreEqual(6, rules.DeckCount);
            Assert.IsFalse(rules.DealerHitsSoft17);
            Assert.IsTrue(rules.DoubleAfterSplit);
            Assert.AreEqual(3, rules.MaxSplitsPerBox);
            Assert.IsFalse(rules.ResplitAces);
            Assert.IsFalse(rules.HitSplitAces);
            Assert.IsFalse(rules.SurrenderAllowed);
            Assert.IsTrue(rules.InsuranceOffered);
            Assert.AreEqual(0.75, rules.Penetration, 0.0001);
            Assert.AreEqual(2, rules.MinimumBet);
            Assert.AreEqual(2, rules.BetIncrement);
            Assert.AreEqual(3, rules.MaxBoxes);
        }

        [Test]
        public void BlackjackPayout_IsExactIntegerMath_ForEveryLegalWager()
        {
            // Every legal wager is even, so wager * 3 / 2 must never truncate.
            for (long wager = 2; wager <= 1000; wager += 2)
            {
                Assert.AreEqual(wager * 3 / 2.0, wager * 3 / 2, $"Truncation at wager {wager}.");
            }
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
unity command run_tests --mode editor --filter WalletTests
```

Expected: FAIL — `Wallet` and `BlackjackRules` do not exist.

- [ ] **Step 3: Write the rules struct**

`Assets/HouseRules/Blackjack/Core/Rules/BlackjackRules.cs`:

```csharp
namespace HouseRules.Blackjack
{
    /// <summary>
    /// The ruleset the engine plays under. Fixed at construction; there is no settings UI.
    /// Tests vary these values to exercise rule-dependent branches.
    /// </summary>
    public readonly struct BlackjackRules
    {
        public int DeckCount { get; }
        public bool DealerHitsSoft17 { get; }
        public bool DoubleAfterSplit { get; }
        public int MaxSplitsPerBox { get; }
        public bool ResplitAces { get; }
        public bool HitSplitAces { get; }
        public bool SurrenderAllowed { get; }
        public bool InsuranceOffered { get; }
        public double Penetration { get; }
        public long MinimumBet { get; }
        public long BetIncrement { get; }
        public int MaxBoxes { get; }

        public BlackjackRules(
            int deckCount,
            bool dealerHitsSoft17,
            bool doubleAfterSplit,
            int maxSplitsPerBox,
            bool resplitAces,
            bool hitSplitAces,
            bool surrenderAllowed,
            bool insuranceOffered,
            double penetration,
            long minimumBet,
            long betIncrement,
            int maxBoxes)
        {
            DeckCount = deckCount;
            DealerHitsSoft17 = dealerHitsSoft17;
            DoubleAfterSplit = doubleAfterSplit;
            MaxSplitsPerBox = maxSplitsPerBox;
            ResplitAces = resplitAces;
            HitSplitAces = hitSplitAces;
            SurrenderAllowed = surrenderAllowed;
            InsuranceOffered = insuranceOffered;
            Penetration = penetration;
            MinimumBet = minimumBet;
            BetIncrement = betIncrement;
            MaxBoxes = maxBoxes;
        }

        /// <summary>
        /// 6 decks, dealer stands soft 17, 3:2 blackjack, double after split allowed,
        /// up to 3 splits, split aces get one card and cannot be resplit, no surrender.
        /// Minimum bet and increment are 2 so that a 3:2 payout is always exact integer math.
        /// </summary>
        public static BlackjackRules Standard => new BlackjackRules(
            deckCount: 6,
            dealerHitsSoft17: false,
            doubleAfterSplit: true,
            maxSplitsPerBox: 3,
            resplitAces: false,
            hitSplitAces: false,
            surrenderAllowed: false,
            insuranceOffered: true,
            penetration: 0.75,
            minimumBet: 2,
            betIncrement: 2,
            maxBoxes: 3);
    }
}
```

- [ ] **Step 4: Write the wallet**

`Assets/HouseRules/Blackjack/Core/Wallet/Wallet.cs`:

```csharp
using System;

namespace HouseRules.Blackjack
{
    /// <summary>
    /// Chip balance. Integer only — floating point money loses a chip on a 3:2 payout
    /// of an odd wager, and the error compounds invisibly across thousands of rounds.
    /// </summary>
    public sealed class Wallet
    {
        public Wallet(long startingBalance)
        {
            if (startingBalance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingBalance), "Balance cannot start negative.");
            }

            Balance = startingBalance;
        }

        public long Balance { get; private set; }

        public bool CanAfford(long amount) => amount <= Balance;

        public void Debit(long amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Debit must be positive.");
            }

            if (amount > Balance)
            {
                throw new InvalidOperationException($"Cannot debit {amount} from a balance of {Balance}.");
            }

            Balance -= amount;
        }

        public void Credit(long amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Credit must be positive.");
            }

            Balance += amount;
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
unity command run_tests --mode editor --filter WalletTests
```

Expected: PASS, 10 tests.

- [ ] **Step 6: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add standard ruleset and integer chip wallet"
```

---

### Task 5: Box, round skeleton, and betting

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/Hands/Box.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Round/RoundState.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Round/PlayerAction.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Round/Round.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/RoundDealTests.cs`

**Interfaces:**
- Consumes: `Hand`, `BlackjackRules`, `Wallet`, `IShoe`.
- Produces: `enum RoundState { Betting, Dealing, Insurance, PlayerTurn, DealerTurn, Settlement, Complete }`; `enum PlayerAction { Hit, Stand, Double, Split, TakeInsurance, DeclineInsurance }`; `sealed class Box` with `int Index`, `long InitialBet`, `IReadOnlyList<Hand> Hands`, `bool IsActive`, `int SplitCount`, methods `AddHand(Hand)`, `SetInitialBet(long)`; `sealed class Round` with constructor `Round(BlackjackRules rules, IShoe shoe, Wallet wallet)`, `RoundState State`, `IReadOnlyList<Box> Boxes`, `Hand DealerHand`, `Card DealerUpcard`, `void PlaceBet(int boxIndex, long wager)`.

This task delivers betting only. `Deal()` arrives in Task 6.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/RoundDealTests.cs`:

```csharp
using System;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class RoundDealTests
    {
        private static Round NewRound(Wallet wallet = null, IShoe shoe = null)
        {
            return new Round(
                BlackjackRules.Standard,
                shoe ?? new StackedShoe(),
                wallet ?? new Wallet(1000));
        }

        [Test]
        public void NewRound_StartsInBettingState()
        {
            Assert.AreEqual(RoundState.Betting, NewRound().State);
        }

        [Test]
        public void NewRound_HasMaxBoxes_AllInactive()
        {
            var round = NewRound();
            Assert.AreEqual(3, round.Boxes.Count);
            foreach (var box in round.Boxes)
            {
                Assert.IsFalse(box.IsActive);
            }
        }

        [Test]
        public void PlaceBet_ActivatesBox_AndDebitsWallet()
        {
            var wallet = new Wallet(1000);
            var round = NewRound(wallet);

            round.PlaceBet(0, 10);

            Assert.IsTrue(round.Boxes[0].IsActive);
            Assert.AreEqual(10, round.Boxes[0].InitialBet);
            Assert.AreEqual(10, round.Boxes[0].Hands[0].Wager);
            Assert.AreEqual(990, wallet.Balance);
        }

        [Test]
        public void PlaceBet_OddWager_Throws()
        {
            var round = NewRound();
            Assert.Throws<ArgumentException>(() => round.PlaceBet(0, 5));
        }

        [Test]
        public void PlaceBet_BelowMinimum_Throws()
        {
            var round = NewRound();
            Assert.Throws<ArgumentException>(() => round.PlaceBet(0, 0));
        }

        [Test]
        public void PlaceBet_BeyondBalance_Throws()
        {
            var round = NewRound(new Wallet(8));
            Assert.Throws<InvalidOperationException>(() => round.PlaceBet(0, 10));
        }

        [Test]
        public void PlaceBet_OutOfRangeBox_Throws()
        {
            var round = NewRound();
            Assert.Throws<ArgumentOutOfRangeException>(() => round.PlaceBet(3, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.PlaceBet(-1, 10));
        }

        [Test]
        public void PlaceBet_Twice_OnSameBox_Throws()
        {
            var round = NewRound();
            round.PlaceBet(0, 10);
            Assert.Throws<InvalidOperationException>(() => round.PlaceBet(0, 10));
        }

        [Test]
        public void PlaceBet_OnMultipleBoxes_IsAllowed()
        {
            var wallet = new Wallet(1000);
            var round = NewRound(wallet);

            round.PlaceBet(0, 10);
            round.PlaceBet(2, 20);

            Assert.IsTrue(round.Boxes[0].IsActive);
            Assert.IsFalse(round.Boxes[1].IsActive);
            Assert.IsTrue(round.Boxes[2].IsActive);
            Assert.AreEqual(970, wallet.Balance);
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
unity command run_tests --mode editor --filter RoundDealTests
```

Expected: FAIL — `Round`, `Box`, `RoundState` do not exist.

- [ ] **Step 3: Write the enums**

`Assets/HouseRules/Blackjack/Core/Round/RoundState.cs`:

```csharp
namespace HouseRules.Blackjack
{
    public enum RoundState
    {
        Betting,
        Dealing,
        Insurance,
        PlayerTurn,
        DealerTurn,
        Settlement,
        Complete
    }
}
```

`Assets/HouseRules/Blackjack/Core/Round/PlayerAction.cs`:

```csharp
namespace HouseRules.Blackjack
{
    public enum PlayerAction
    {
        Hit,
        Stand,
        Double,
        Split,
        TakeInsurance,
        DeclineInsurance
    }
}
```

- [ ] **Step 4: Write the box**

`Assets/HouseRules/Blackjack/Core/Hands/Box.cs`:

```csharp
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    /// <summary>
    /// One betting position. Holds the original bet and one or more hands —
    /// splitting is what turns a single hand into several.
    /// </summary>
    public sealed class Box
    {
        private readonly List<Hand> _hands = new List<Hand>();

        public Box(int index)
        {
            Index = index;
        }

        public int Index { get; }

        public long InitialBet { get; private set; }

        public IReadOnlyList<Hand> Hands => _hands;

        public bool IsActive => InitialBet > 0;

        /// <summary>Number of splits performed. Hand count is always SplitCount + 1.</summary>
        public int SplitCount => _hands.Count - 1;

        /// <summary>Insurance side bet, or 0 if none was taken.</summary>
        public long InsuranceBet { get; private set; }

        public void SetInitialBet(long wager) => InitialBet = wager;

        public void SetInsuranceBet(long amount) => InsuranceBet = amount;

        public void AddHand(Hand hand) => _hands.Add(hand);

        public void InsertHandAfter(int index, Hand hand) => _hands.Insert(index + 1, hand);
    }
}
```

- [ ] **Step 5: Write the round skeleton**

`Assets/HouseRules/Blackjack/Core/Round/Round.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        private readonly BlackjackRules _rules;
        private readonly IShoe _shoe;
        private readonly Wallet _wallet;
        private readonly List<Box> _boxes = new List<Box>();

        public Round(BlackjackRules rules, IShoe shoe, Wallet wallet)
        {
            _rules = rules;
            _shoe = shoe ?? throw new ArgumentNullException(nameof(shoe));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

            for (int i = 0; i < rules.MaxBoxes; i++)
            {
                _boxes.Add(new Box(i));
            }

            State = RoundState.Betting;
            DealerHand = new Hand(0);
        }

        public RoundState State { get; private set; }

        public IReadOnlyList<Box> Boxes => _boxes;

        public Hand DealerHand { get; }

        public BlackjackRules Rules => _rules;

        /// <summary>The dealer's face-up card. Only meaningful once dealing has completed.</summary>
        public Card DealerUpcard => DealerHand.Cards.Count > 0
            ? DealerHand.Cards[0]
            : throw new InvalidOperationException("Dealer has no cards yet.");

        public void PlaceBet(int boxIndex, long wager)
        {
            if (State != RoundState.Betting)
            {
                throw new InvalidOperationException($"Cannot bet in state {State}.");
            }

            if (boxIndex < 0 || boxIndex >= _boxes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(boxIndex));
            }

            if (wager < _rules.MinimumBet)
            {
                throw new ArgumentException(
                    $"Wager {wager} is below the minimum of {_rules.MinimumBet}.", nameof(wager));
            }

            if (wager % _rules.BetIncrement != 0)
            {
                throw new ArgumentException(
                    $"Wager {wager} must be a multiple of {_rules.BetIncrement} so a 3:2 payout is exact.",
                    nameof(wager));
            }

            Box box = _boxes[boxIndex];
            if (box.IsActive)
            {
                throw new InvalidOperationException($"Box {boxIndex} already has a bet.");
            }

            if (!_wallet.CanAfford(wager))
            {
                throw new InvalidOperationException(
                    $"Cannot afford {wager} with a balance of {_wallet.Balance}.");
            }

            _wallet.Debit(wager);
            box.SetInitialBet(wager);
            box.AddHand(new Hand(wager));
        }

        private void SetState(RoundState state) => State = state;
    }
}
```

`Round` is declared `partial` because later tasks add dealing, player actions, dealer play, and settlement as separate files. That keeps each concern in a file you can hold in your head, without giving up a single cohesive type.

- [ ] **Step 6: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter RoundDealTests
```

Expected: PASS, 9 tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add box, round state machine skeleton, and bet validation"
```

---

### Task 6: The initial deal and dealer peek

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/Round/Round.Dealing.cs`
- Modify: `Assets/HouseRules/Blackjack/Tests/EditMode/RoundDealTests.cs` (append tests)

**Interfaces:**
- Consumes: everything from Task 5.
- Produces: `void Deal()` on `Round`; `int CurrentBoxIndex`, `int CurrentHandIndex`, `Box CurrentBox`, `Hand CurrentHand`, `bool DealerHasBlackjack` on `Round`.

Deal order is casino-standard: one card to each active box left-to-right, one to the dealer face up, a second to each box, then the dealer's hole card face down.

- [ ] **Step 1: Write the failing test**

Append to `Assets/HouseRules/Blackjack/Tests/EditMode/RoundDealTests.cs`, inside the `RoundDealTests` class:

```csharp
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        [Test]
        public void Deal_GivesTwoCardsToEachActiveBoxAndDealer()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.Nine),   // box0 first, dealer upcard
                C(Rank.Seven), C(Rank.Four), // box0 second, dealer hole
                C(Rank.Two)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.AreEqual(2, round.Boxes[0].Hands[0].Cards.Count);
            Assert.AreEqual(2, round.DealerHand.Cards.Count);
        }

        [Test]
        public void Deal_UsesCasinoOrder_BoxThenDealerThenBoxThenHole()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten),    // box0 card 1
                C(Rank.Nine),   // dealer upcard
                C(Rank.Seven),  // box0 card 2
                C(Rank.Four))); // dealer hole

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.AreEqual(C(Rank.Ten), round.Boxes[0].Hands[0].Cards[0]);
            Assert.AreEqual(C(Rank.Seven), round.Boxes[0].Hands[0].Cards[1]);
            Assert.AreEqual(C(Rank.Nine), round.DealerHand.Cards[0]);
            Assert.AreEqual(C(Rank.Four), round.DealerHand.Cards[1]);
        }

        [Test]
        public void Deal_SkipsInactiveBoxes()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten),    // box0 card 1
                C(Rank.Six),    // box2 card 1
                C(Rank.Nine),   // dealer upcard
                C(Rank.Seven),  // box0 card 2
                C(Rank.Three),  // box2 card 2
                C(Rank.Four))); // dealer hole

            round.PlaceBet(0, 10);
            round.PlaceBet(2, 10);
            round.Deal();

            Assert.AreEqual(2, round.Boxes[0].Hands[0].Cards.Count);
            Assert.AreEqual(0, round.Boxes[1].Hands.Count);
            Assert.AreEqual(2, round.Boxes[2].Hands[0].Cards.Count);
        }

        [Test]
        public void Deal_WithNoBets_Throws()
        {
            var round = NewRound();
            Assert.Throws<InvalidOperationException>(() => round.Deal());
        }

        [Test]
        public void Deal_EntersPlayerTurn_OnOrdinaryUpcard()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.Nine), C(Rank.Seven), C(Rank.Four)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.AreEqual(RoundState.PlayerTurn, round.State);
            Assert.AreEqual(0, round.CurrentBoxIndex);
            Assert.AreEqual(0, round.CurrentHandIndex);
        }

        [Test]
        public void Deal_EntersInsurance_OnAceUpcard()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.Ace), C(Rank.Seven), C(Rank.Four)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.AreEqual(RoundState.Insurance, round.State);
        }

        [Test]
        public void Deal_GoesStraightToSettlement_WhenDealerPeeksBlackjackOnTen()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.King), C(Rank.Seven), C(Rank.Ace)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.IsTrue(round.DealerHasBlackjack);
            Assert.AreEqual(RoundState.Complete, round.State);
        }

        [Test]
        public void Deal_DoesNotPeek_OnLowUpcard()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.Six), C(Rank.Seven), C(Rank.Four)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.AreEqual(RoundState.PlayerTurn, round.State);
        }

        [Test]
        public void Deal_Twice_Throws()
        {
            var round = NewRound(shoe: new StackedShoe(
                C(Rank.Ten), C(Rank.Nine), C(Rank.Seven), C(Rank.Four)));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.Throws<InvalidOperationException>(() => round.Deal());
        }
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter RoundDealTests
```

Expected: FAIL — `Deal`, `CurrentBoxIndex`, `DealerHasBlackjack` do not exist.

- [ ] **Step 3: Write the dealing partial**

`Assets/HouseRules/Blackjack/Core/Round/Round.Dealing.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        public int CurrentBoxIndex { get; private set; } = -1;

        public int CurrentHandIndex { get; private set; } = -1;

        public Box CurrentBox => CurrentBoxIndex >= 0 ? _boxes[CurrentBoxIndex] : null;

        public Hand CurrentHand
        {
            get
            {
                Box box = CurrentBox;
                if (box == null || CurrentHandIndex < 0 || CurrentHandIndex >= box.Hands.Count)
                {
                    return null;
                }

                return box.Hands[CurrentHandIndex];
            }
        }

        public bool DealerHasBlackjack => DealerHand.IsBlackjack;

        private IEnumerable<Box> ActiveBoxes()
        {
            foreach (Box box in _boxes)
            {
                if (box.IsActive)
                {
                    yield return box;
                }
            }
        }

        private bool AnyBoxActive()
        {
            foreach (Box box in _boxes)
            {
                if (box.IsActive)
                {
                    return true;
                }
            }

            return false;
        }

        public void Deal()
        {
            if (State != RoundState.Betting)
            {
                throw new InvalidOperationException($"Cannot deal in state {State}.");
            }

            if (!AnyBoxActive())
            {
                throw new InvalidOperationException("At least one box must have a bet before dealing.");
            }

            SetState(RoundState.Dealing);

            Emit(new RoundStarted());

            if (_shoe.NeedsReshuffle)
            {
                _shoe.Reshuffle();
                Emit(new ShoeReshuffled());
            }

            // First card to each active box, then the dealer's upcard.
            foreach (Box box in ActiveBoxes())
            {
                DealTo(box.Index, 0, box.Hands[0], faceUp: true);
            }

            DealToDealer(faceUp: true);

            // Second card to each active box, then the dealer's hole card.
            foreach (Box box in ActiveBoxes())
            {
                DealTo(box.Index, 0, box.Hands[0], faceUp: true);
            }

            DealToDealer(faceUp: false);

            // US peek rules: the dealer checks for blackjack on an ace or ten upcard,
            // so the player never loses double or split money to a dealer natural.
            bool upcardTriggersPeek =
                DealerUpcard.Rank == Rank.Ace || DealerUpcard.BaseValue == 10;

            if (DealerUpcard.Rank == Rank.Ace && _rules.InsuranceOffered)
            {
                SetState(RoundState.Insurance);
                Emit(new InsuranceOffered());
                return;
            }

            if (upcardTriggersPeek && DealerHasBlackjack)
            {
                RevealAndSettleDealerBlackjack();
                return;
            }

            BeginPlayerTurn();
        }

        private void DealTo(int boxIndex, int handIndex, Hand hand, bool faceUp)
        {
            Card card = _shoe.Deal();
            hand.Add(card);
            Emit(new CardDealt(boxIndex, handIndex, card, faceUp));
        }

        private void DealToDealer(bool faceUp)
        {
            Card card = _shoe.Deal();
            DealerHand.Add(card);
            Emit(new CardDealt(CardDealt.DealerBoxIndex, 0, card, faceUp));
        }

        private void BeginPlayerTurn()
        {
            SetState(RoundState.PlayerTurn);
            CurrentBoxIndex = -1;
            CurrentHandIndex = -1;

            if (AdvanceToNextPlayableHand())
            {
                Emit(new PlayerTurnStarted(CurrentBoxIndex, CurrentHandIndex));
            }
            else
            {
                BeginDealerTurn();
            }
        }
    }
}
```

`RevealAndSettleDealerBlackjack`, `AdvanceToNextPlayableHand`, `BeginDealerTurn`, and `Emit` are written in later tasks. To keep this task compiling on its own, add the temporary stubs in Step 4 — they get replaced, not extended.

- [ ] **Step 4: Add temporary stubs so this task compiles standalone**

Create `Assets/HouseRules/Blackjack/Core/Round/Round.Stubs.cs`:

```csharp
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    // TEMPORARY: replaced in Tasks 7-13. Exists only so Task 6 compiles and tests run.
    public sealed partial class Round
    {
        private readonly List<GameEvent> _events = new List<GameEvent>();

        private void Emit(GameEvent gameEvent) => _events.Add(gameEvent);

        private void RevealAndSettleDealerBlackjack() => SetState(RoundState.Complete);

        private bool AdvanceToNextPlayableHand()
        {
            for (int b = 0; b < _boxes.Count; b++)
            {
                Box box = _boxes[b];
                if (!box.IsActive)
                {
                    continue;
                }

                for (int h = 0; h < box.Hands.Count; h++)
                {
                    if (!box.Hands[h].IsClosed)
                    {
                        CurrentBoxIndex = b;
                        CurrentHandIndex = h;
                        return true;
                    }
                }
            }

            CurrentBoxIndex = -1;
            CurrentHandIndex = -1;
            return false;
        }

        private void BeginDealerTurn() => SetState(RoundState.DealerTurn);
    }
}
```

Also create the event types this task emits — `Assets/HouseRules/Blackjack/Core/Events/GameEvent.cs`:

```csharp
namespace HouseRules.Blackjack
{
    public abstract class GameEvent
    {
    }

    public sealed class RoundStarted : GameEvent
    {
    }

    public sealed class ShoeReshuffled : GameEvent
    {
    }

    public sealed class InsuranceOffered : GameEvent
    {
    }

    public sealed class PlayerTurnStarted : GameEvent
    {
        public PlayerTurnStarted(int boxIndex, int handIndex)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
    }

    public sealed class CardDealt : GameEvent
    {
        /// <summary>Box index used to mean "the dealer" rather than a player box.</summary>
        public const int DealerBoxIndex = -1;

        public CardDealt(int boxIndex, int handIndex, Card card, bool faceUp)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
            Card = card;
            FaceUp = faceUp;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
        public Card Card { get; }
        public bool FaceUp { get; }
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter RoundDealTests
```

Expected: PASS, 18 tests (9 from Task 5 plus 9 new).

- [ ] **Step 6: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add initial deal with casino order and dealer peek"
```

---

### Task 7: Legal actions

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/Round/Round.Legality.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/LegalActionsTests.cs`

**Interfaces:**
- Consumes: `Round`, `Hand`, `Box`, `BlackjackRules`, `PlayerAction`.
- Produces: `IReadOnlyList<PlayerAction> LegalActions` on `Round`.

All rule knowledge about what a player may do lives here and nowhere else. The UI renders buttons from this list and holds no rules of its own.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/LegalActionsTests.cs`:

```csharp
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class LegalActionsTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round Dealt(params Card[] script)
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(script), new Wallet(1000));
            round.PlaceBet(0, 10);
            round.Deal();
            return round;
        }

        [Test]
        public void FreshTwoCardHand_AllowsHitStandDouble()
        {
            // box: 9,7 = 16   dealer: 6, 4
            var round = Dealt(C(Rank.Nine), C(Rank.Six), C(Rank.Seven), C(Rank.Four));

            CollectionAssert.Contains(round.LegalActions, PlayerAction.Hit);
            CollectionAssert.Contains(round.LegalActions, PlayerAction.Stand);
            CollectionAssert.Contains(round.LegalActions, PlayerAction.Double);
            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.Split);
        }

        [Test]
        public void PairHand_AlsoAllowsSplit()
        {
            // box: 8,8   dealer: 6, 4
            var round = Dealt(C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four));
            CollectionAssert.Contains(round.LegalActions, PlayerAction.Split);
        }

        [Test]
        public void TenAndKing_IsNotSplittable()
        {
            var round = Dealt(C(Rank.Ten), C(Rank.Six), C(Rank.King), C(Rank.Four));
            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.Split);
        }

        [Test]
        public void Split_IsIllegal_WhenWalletCannotCoverSecondWager()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four)),
                new Wallet(10));

            round.PlaceBet(0, 10);
            round.Deal();

            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.Split);
        }

        [Test]
        public void Double_IsIllegal_WhenWalletCannotCoverIt()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Six), C(Rank.Two), C(Rank.Four)),
                new Wallet(10));

            round.PlaceBet(0, 10);
            round.Deal();

            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.Double);
        }

        [Test]
        public void InsuranceState_OffersOnlyInsuranceActions()
        {
            var round = Dealt(C(Rank.Nine), C(Rank.Ace), C(Rank.Seven), C(Rank.Four));

            Assert.AreEqual(RoundState.Insurance, round.State);
            CollectionAssert.AreEquivalent(
                new[] { PlayerAction.TakeInsurance, PlayerAction.DeclineInsurance },
                round.LegalActions);
        }

        [Test]
        public void NonPlayerStates_OfferNoActions()
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), new Wallet(1000));
            Assert.AreEqual(RoundState.Betting, round.State);
            Assert.IsEmpty(round.LegalActions);
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
unity command run_tests --mode editor --filter LegalActionsTests
```

Expected: FAIL — `LegalActions` does not exist.

- [ ] **Step 3: Write the legality partial**

`Assets/HouseRules/Blackjack/Core/Round/Round.Legality.cs`:

```csharp
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        private static readonly PlayerAction[] NoActions = new PlayerAction[0];

        private static readonly PlayerAction[] InsuranceActions =
        {
            PlayerAction.TakeInsurance,
            PlayerAction.DeclineInsurance
        };

        /// <summary>
        /// Everything the player may legally do right now. All rule knowledge about
        /// permitted actions lives here — consumers render from this list and nothing else.
        /// </summary>
        public IReadOnlyList<PlayerAction> LegalActions
        {
            get
            {
                if (State == RoundState.Insurance)
                {
                    return InsuranceActions;
                }

                if (State != RoundState.PlayerTurn)
                {
                    return NoActions;
                }

                Hand hand = CurrentHand;
                Box box = CurrentBox;
                if (hand == null || box == null || hand.IsClosed)
                {
                    return NoActions;
                }

                var actions = new List<PlayerAction>(4);

                bool isSplitAce = hand.IsFromSplit
                                  && hand.Cards.Count > 0
                                  && hand.Cards[0].Rank == Rank.Ace;

                // A split ace receives exactly one card and cannot act further.
                if (isSplitAce && !_rules.HitSplitAces)
                {
                    return NoActions;
                }

                actions.Add(PlayerAction.Hit);
                actions.Add(PlayerAction.Stand);

                bool isFirstDecision = hand.Cards.Count == 2 && !hand.IsDoubled;
                bool doubleAllowedHere = !hand.IsFromSplit || _rules.DoubleAfterSplit;

                if (isFirstDecision && doubleAllowedHere && _wallet.CanAfford(hand.Wager))
                {
                    actions.Add(PlayerAction.Double);
                }

                bool underSplitLimit = box.SplitCount < _rules.MaxSplitsPerBox;
                bool acesResplitAllowed = !isSplitAce || _rules.ResplitAces;

                if (isFirstDecision
                    && hand.IsPair
                    && underSplitLimit
                    && acesResplitAllowed
                    && _wallet.CanAfford(hand.Wager))
                {
                    actions.Add(PlayerAction.Split);
                }

                return actions;
            }
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter LegalActionsTests
```

Expected: PASS, 7 tests. Every test in this class must be green — this task commits no failing tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: compute legal player actions in the engine"
```

---

### Task 8: Hit, stand, and turn advancement

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/Round/Round.Actions.cs`
- Modify: `Assets/HouseRules/Blackjack/Core/Round/Round.Stubs.cs` (remove `AdvanceToNextPlayableHand`)
- Create: `Assets/HouseRules/Blackjack/Core/Events/HandEvents.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/PlayerActionTests.cs`

**Interfaces:**
- Consumes: `Round`, `LegalActions`.
- Produces: `void Apply(PlayerAction action)` on `Round`. Events `HandBusted(int boxIndex, int handIndex)`, `HandStood(int boxIndex, int handIndex)`, `PlayerTurnStarted(int boxIndex, int handIndex)`.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/PlayerActionTests.cs`:

```csharp
using System;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class PlayerActionTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round Dealt(Wallet wallet, params Card[] script)
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(script), wallet);
            round.PlaceBet(0, 10);
            round.Deal();
            return round;
        }

        [Test]
        public void Hit_AddsACardToCurrentHand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Five), C(Rank.Six), C(Rank.Four), C(Rank.Four), C(Rank.Three));

            round.Apply(PlayerAction.Hit);

            Assert.AreEqual(3, round.Boxes[0].Hands[0].Cards.Count);
            Assert.AreEqual(C(Rank.Three), round.Boxes[0].Hands[0].Cards[2]);
        }

        [Test]
        public void Hit_ToBust_ClosesHandAndMovesOn()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Four), C(Rank.King));

            round.Apply(PlayerAction.Hit);

            Assert.IsTrue(round.Boxes[0].Hands[0].IsBust);
            Assert.IsTrue(round.Boxes[0].Hands[0].IsClosed);
            Assert.AreEqual(RoundState.DealerTurn, round.State);
        }

        [Test]
        public void Stand_ClosesHand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Four));

            round.Apply(PlayerAction.Stand);

            Assert.IsTrue(round.Boxes[0].Hands[0].IsClosed);
            Assert.AreEqual(RoundState.DealerTurn, round.State);
        }

        [Test]
        public void TurnAdvances_AcrossBoxes_LeftToRight()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(
                    C(Rank.Ten),   // box0 c1
                    C(Rank.Nine),  // box2 c1
                    C(Rank.Six),   // dealer up
                    C(Rank.Seven), // box0 c2
                    C(Rank.Eight), // box2 c2
                    C(Rank.Four)), // dealer hole
                wallet);

            round.PlaceBet(0, 10);
            round.PlaceBet(2, 10);
            round.Deal();

            Assert.AreEqual(0, round.CurrentBoxIndex);
            round.Apply(PlayerAction.Stand);
            Assert.AreEqual(2, round.CurrentBoxIndex);
            round.Apply(PlayerAction.Stand);
            Assert.AreEqual(RoundState.DealerTurn, round.State);
        }

        [Test]
        public void Apply_IllegalAction_Throws()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Four));

            Assert.Throws<InvalidOperationException>(() => round.Apply(PlayerAction.Split));
        }

        [Test]
        public void Apply_OutsidePlayerTurn_Throws()
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(), new Wallet(1000));
            Assert.Throws<InvalidOperationException>(() => round.Apply(PlayerAction.Hit));
        }

        [Test]
        public void Hit_To21_DoesNotAutoStand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ten), C(Rank.Six), C(Rank.Five), C(Rank.Four), C(Rank.Six));

            round.Apply(PlayerAction.Hit);

            Assert.AreEqual(21, round.Boxes[0].Hands[0].Value.Total);
            Assert.AreEqual(RoundState.PlayerTurn, round.State);
            Assert.IsFalse(round.Boxes[0].Hands[0].IsClosed);
        }

        [Test]
        public void AfterHitting_DoubleIsNoLongerLegal()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Five), C(Rank.Six), C(Rank.Four), C(Rank.Four), C(Rank.Two));

            round.Apply(PlayerAction.Hit);

            CollectionAssert.Contains(round.LegalActions, PlayerAction.Hit);
            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.Double);
        }

        [Test]
        public void NaturalBlackjack_AutoClosesAndIsNeverOfferedActions()
        {
            // Player is dealt A,K — a natural. It stands automatically, so with no
            // other live hand the round goes straight to the dealer.
            var round = Dealt(new Wallet(1000),
                C(Rank.Ace), C(Rank.Nine), C(Rank.King), C(Rank.Seven));

            Assert.IsTrue(round.Boxes[0].Hands[0].IsBlackjack);
            Assert.IsTrue(round.Boxes[0].Hands[0].IsClosed);
            Assert.AreNotEqual(RoundState.PlayerTurn, round.State);
        }

        [Test]
        public void NaturalOnOneBox_DoesNotSkipTheOtherBox()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(
                    C(Rank.Ace),   // box0 c1
                    C(Rank.Ten),   // box1 c1
                    C(Rank.Nine),  // dealer up
                    C(Rank.King),  // box0 c2  -> natural
                    C(Rank.Six),   // box1 c2  -> 16
                    C(Rank.Seven)),// dealer hole
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.PlaceBet(1, 10);
            round.Deal();

            Assert.AreEqual(RoundState.PlayerTurn, round.State);
            Assert.AreEqual(1, round.CurrentBoxIndex, "Play should skip the natural and land on box 1.");
        }
    }
}
```

`CollectionAssert` needs `using NUnit.Framework;`, which the file already has.

Hitting to exactly 21 deliberately does not auto-stand. Auto-standing is a UI convenience, and putting it in the engine would make the state machine's behaviour depend on a presentation preference.

- [ ] **Step 2: Run the test to verify it fails**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter PlayerActionTests
```

Expected: FAIL — `Apply` does not exist.

- [ ] **Step 3: Add the hand events**

`Assets/HouseRules/Blackjack/Core/Events/HandEvents.cs`:

```csharp
namespace HouseRules.Blackjack
{
    public sealed class HandBusted : GameEvent
    {
        public HandBusted(int boxIndex, int handIndex)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
    }

    public sealed class HandStood : GameEvent
    {
        public HandStood(int boxIndex, int handIndex)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
    }
}
```

- [ ] **Step 4: Write the actions partial**

`Assets/HouseRules/Blackjack/Core/Round/Round.Actions.cs`:

```csharp
using System;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        /// <summary>
        /// Apply a player intent. Always validated against <see cref="LegalActions"/> —
        /// a bug in a caller surfaces as a rejected intent, never a corrupted round.
        /// </summary>
        public void Apply(PlayerAction action)
        {
            if (!Contains(LegalActions, action))
            {
                throw new InvalidOperationException(
                    $"{action} is not legal in state {State}.");
            }

            switch (action)
            {
                case PlayerAction.Hit:
                    ApplyHit();
                    break;
                case PlayerAction.Stand:
                    ApplyStand();
                    break;
                case PlayerAction.Double:
                    ApplyDouble();
                    break;
                case PlayerAction.Split:
                    ApplySplit();
                    break;
                case PlayerAction.TakeInsurance:
                    ApplyInsurance(taken: true);
                    break;
                case PlayerAction.DeclineInsurance:
                    ApplyInsurance(taken: false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<PlayerAction> list, PlayerAction action)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == action)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyHit()
        {
            Hand hand = CurrentHand;
            DealTo(CurrentBoxIndex, CurrentHandIndex, hand, faceUp: true);

            if (hand.IsBust)
            {
                hand.Close();
                Emit(new HandBusted(CurrentBoxIndex, CurrentHandIndex));
                AdvanceTurn();
            }
        }

        private void ApplyStand()
        {
            CurrentHand.Close();
            Emit(new HandStood(CurrentBoxIndex, CurrentHandIndex));
            AdvanceTurn();
        }

        private void AdvanceTurn()
        {
            if (AdvanceToNextPlayableHand())
            {
                Emit(new PlayerTurnStarted(CurrentBoxIndex, CurrentHandIndex));
            }
            else
            {
                BeginDealerTurn();
            }
        }

        /// <summary>
        /// Walks boxes left-to-right and, within a box, hands in order. A split mid-turn
        /// simply extends the walk, because the new hand is inserted after the current one.
        /// </summary>
        private bool AdvanceToNextPlayableHand()
        {
            for (int b = 0; b < _boxes.Count; b++)
            {
                Box box = _boxes[b];
                if (!box.IsActive)
                {
                    continue;
                }

                for (int h = 0; h < box.Hands.Count; h++)
                {
                    Hand hand = box.Hands[h];
                    if (hand.IsClosed || hand.IsBust)
                    {
                        continue;
                    }

                    // A natural stands automatically — the player never acts on it.
                    if (hand.IsBlackjack)
                    {
                        hand.Close();
                        continue;
                    }

                    CurrentBoxIndex = b;
                    CurrentHandIndex = h;

                    // A split ace has no legal actions; close it and keep walking.
                    if (LegalActions.Count == 0)
                    {
                        hand.Close();
                        continue;
                    }

                    return true;
                }
            }

            CurrentBoxIndex = -1;
            CurrentHandIndex = -1;
            return false;
        }
    }
}
```

- [ ] **Step 5: Remove the superseded stub**

Delete `AdvanceToNextPlayableHand` from `Assets/HouseRules/Blackjack/Core/Round/Round.Stubs.cs`, leaving:

```csharp
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    // TEMPORARY: replaced in Tasks 9-13.
    public sealed partial class Round
    {
        private readonly List<GameEvent> _events = new List<GameEvent>();

        private void Emit(GameEvent gameEvent) => _events.Add(gameEvent);

        private void RevealAndSettleDealerBlackjack() => SetState(RoundState.Complete);

        private void BeginDealerTurn() => SetState(RoundState.DealerTurn);

        private void ApplyDouble() => throw new System.NotImplementedException();

        private void ApplySplit() => throw new System.NotImplementedException();

        private void ApplyInsurance(bool taken) => throw new System.NotImplementedException();
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter PlayerActionTests
```

Expected: PASS, 10 tests.

```bash
unity command run_tests --mode editor --filter LegalActionsTests
```

Expected: PASS, 7 tests — still green, unchanged by this task.

- [ ] **Step 7: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add hit, stand, and turn advancement across boxes"
```

---

### Task 9: Double down

**Files:**
- Modify: `Assets/HouseRules/Blackjack/Core/Round/Round.Actions.cs` (add `ApplyDouble`)
- Modify: `Assets/HouseRules/Blackjack/Core/Round/Round.Stubs.cs` (remove `ApplyDouble`)
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/PlayerActionTests.cs` (append)

**Interfaces:**
- Consumes: `Round.Apply`, `Hand.MarkDoubled`, `Hand.SetWager`, `Wallet.Debit`.
- Produces: `HandDoubled(int boxIndex, int handIndex, long newWager)` event.

- [ ] **Step 1: Write the failing test**

Append to `PlayerActionTests`:

```csharp
        [Test]
        public void Double_DebitsWallet_DoublesWager_DealsExactlyOneCard_AndCloses()
        {
            var wallet = new Wallet(1000);
            var round = Dealt(wallet,
                C(Rank.Six), C(Rank.Six), C(Rank.Five), C(Rank.Four), C(Rank.Nine));

            Assert.AreEqual(990, wallet.Balance);

            round.Apply(PlayerAction.Double);

            Hand hand = round.Boxes[0].Hands[0];
            Assert.AreEqual(20, hand.Wager);
            Assert.IsTrue(hand.IsDoubled);
            Assert.AreEqual(3, hand.Cards.Count);
            Assert.IsTrue(hand.IsClosed);
            Assert.AreEqual(980, wallet.Balance);
            Assert.AreEqual(RoundState.DealerTurn, round.State);
        }

        [Test]
        public void Double_ThatBusts_StillClosesTheHand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ten), C(Rank.Six), C(Rank.Six), C(Rank.Four), C(Rank.King));

            round.Apply(PlayerAction.Double);

            Hand hand = round.Boxes[0].Hands[0];
            Assert.IsTrue(hand.IsBust);
            Assert.IsTrue(hand.IsClosed);
        }
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter PlayerActionTests
```

Expected: FAIL with `NotImplementedException` from the stub.

- [ ] **Step 3: Add the event**

Append to `Assets/HouseRules/Blackjack/Core/Events/HandEvents.cs`:

```csharp
    public sealed class HandDoubled : GameEvent
    {
        public HandDoubled(int boxIndex, int handIndex, long newWager)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
            NewWager = newWager;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
        public long NewWager { get; }
    }
```

- [ ] **Step 4: Implement double**

Add to `Assets/HouseRules/Blackjack/Core/Round/Round.Actions.cs`, inside the class:

```csharp
        private void ApplyDouble()
        {
            Hand hand = CurrentHand;

            _wallet.Debit(hand.Wager);
            hand.SetWager(hand.Wager * 2);
            hand.MarkDoubled();
            Emit(new HandDoubled(CurrentBoxIndex, CurrentHandIndex, hand.Wager));

            DealTo(CurrentBoxIndex, CurrentHandIndex, hand, faceUp: true);

            if (hand.IsBust)
            {
                Emit(new HandBusted(CurrentBoxIndex, CurrentHandIndex));
            }

            // A doubled hand receives exactly one card and is finished either way.
            hand.Close();
            AdvanceTurn();
        }
```

Remove `ApplyDouble` from `Round.Stubs.cs`.

- [ ] **Step 5: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter PlayerActionTests
```

Expected: PASS, 12 tests.

- [ ] **Step 6: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add double down"
```

---

### Task 10: Split

**Files:**
- Modify: `Assets/HouseRules/Blackjack/Core/Round/Round.Actions.cs` (add `ApplySplit`)
- Modify: `Assets/HouseRules/Blackjack/Core/Round/Round.Stubs.cs` (remove `ApplySplit`)
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/SplitTests.cs`

**Interfaces:**
- Consumes: `Box.InsertHandAfter`, `Hand`, `Wallet.Debit`.
- Produces: `HandSplit(int boxIndex, int handIndex, int newHandIndex)` event.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/SplitTests.cs`:

```csharp
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class SplitTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round Dealt(Wallet wallet, params Card[] script)
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(script), wallet);
            round.PlaceBet(0, 10);
            round.Deal();
            return round;
        }

        [Test]
        public void Split_CreatesTwoHands_EachWithOneOriginalCard()
        {
            var wallet = new Wallet(1000);
            var round = Dealt(wallet,
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                C(Rank.Three), C(Rank.Two));

            round.Apply(PlayerAction.Split);

            Box box = round.Boxes[0];
            Assert.AreEqual(2, box.Hands.Count);
            Assert.AreEqual(Rank.Eight, box.Hands[0].Cards[0].Rank);
            Assert.AreEqual(Rank.Eight, box.Hands[1].Cards[0].Rank);
        }

        [Test]
        public void Split_DebitsASecondWager()
        {
            var wallet = new Wallet(1000);
            var round = Dealt(wallet,
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                C(Rank.Three), C(Rank.Two));

            Assert.AreEqual(990, wallet.Balance);
            round.Apply(PlayerAction.Split);
            Assert.AreEqual(980, wallet.Balance);
            Assert.AreEqual(10, round.Boxes[0].Hands[1].Wager);
        }

        [Test]
        public void Split_DealsOneCardToEachNewHand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                C(Rank.Three), C(Rank.Two));

            round.Apply(PlayerAction.Split);

            Assert.AreEqual(2, round.Boxes[0].Hands[0].Cards.Count);
            Assert.AreEqual(2, round.Boxes[0].Hands[1].Cards.Count);
        }

        [Test]
        public void Split_MarksBothHandsAsFromSplit()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                C(Rank.Three), C(Rank.Two));

            round.Apply(PlayerAction.Split);

            Assert.IsTrue(round.Boxes[0].Hands[0].IsFromSplit);
            Assert.IsTrue(round.Boxes[0].Hands[1].IsFromSplit);
        }

        [Test]
        public void SplitHand_ThatMakes21_IsNotBlackjack()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ace), C(Rank.Six), C(Rank.Ace), C(Rank.Four),
                C(Rank.King), C(Rank.Queen));

            round.Apply(PlayerAction.Split);

            Hand first = round.Boxes[0].Hands[0];
            Assert.AreEqual(21, first.Value.Total);
            Assert.IsFalse(first.IsBlackjack);
        }

        [Test]
        public void SplitAces_ReceiveOneCardEach_AndCannotAct()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Ace), C(Rank.Six), C(Rank.Ace), C(Rank.Four),
                C(Rank.Five), C(Rank.Seven));

            round.Apply(PlayerAction.Split);

            // Both split aces auto-close, so play passes straight to the dealer.
            Assert.AreEqual(RoundState.DealerTurn, round.State);
            Assert.AreEqual(2, round.Boxes[0].Hands[0].Cards.Count);
            Assert.AreEqual(2, round.Boxes[0].Hands[1].Cards.Count);
            Assert.IsTrue(round.Boxes[0].Hands[0].IsClosed);
            Assert.IsTrue(round.Boxes[0].Hands[1].IsClosed);
        }

        [Test]
        public void Split_RespectsMaxSplitsPerBox()
        {
            // Every card is an eight, so the player can keep splitting until the cap.
            var script = new Card[40];
            for (int i = 0; i < script.Length; i++)
            {
                script[i] = C(Rank.Eight);
            }

            var round = new Round(
                BlackjackRules.Standard,
                StackedShoe.WithFiller(C(Rank.Eight), script),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();

            int splits = 0;
            while (round.State == RoundState.PlayerTurn
                   && round.LegalActions.Contains(PlayerAction.Split))
            {
                round.Apply(PlayerAction.Split);
                splits++;
            }

            Assert.AreEqual(3, splits, "Standard rules allow at most 3 splits per box.");
            Assert.AreEqual(4, round.Boxes[0].Hands.Count);
        }

        [Test]
        public void AfterSplit_PlayContinuesOnTheFirstNewHand()
        {
            var round = Dealt(new Wallet(1000),
                C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Four),
                C(Rank.Three), C(Rank.Two));

            round.Apply(PlayerAction.Split);

            Assert.AreEqual(0, round.CurrentBoxIndex);
            Assert.AreEqual(0, round.CurrentHandIndex);
        }
    }
}
```

The `Contains` call needs `using System.Linq;` at the top of the file. Add it.

- [ ] **Step 2: Run the test to verify it fails**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter SplitTests
```

Expected: FAIL with `NotImplementedException`.

- [ ] **Step 3: Add the event**

Append to `Assets/HouseRules/Blackjack/Core/Events/HandEvents.cs`:

```csharp
    public sealed class HandSplit : GameEvent
    {
        public HandSplit(int boxIndex, int handIndex, int newHandIndex)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
            NewHandIndex = newHandIndex;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
        public int NewHandIndex { get; }
    }
```

- [ ] **Step 4: Implement split**

Add to `Round.Actions.cs`:

```csharp
        private void ApplySplit()
        {
            Box box = CurrentBox;
            Hand original = CurrentHand;
            int handIndex = CurrentHandIndex;

            _wallet.Debit(original.Wager);

            // Move the second card of the original hand into a new hand beside it.
            Card moved = original.Cards[1];
            var replacement = new Hand(original.Wager, isFromSplit: true);
            replacement.Add(original.Cards[0]);

            var created = new Hand(original.Wager, isFromSplit: true);
            created.Add(moved);

            box.ReplaceHand(handIndex, replacement);
            box.InsertHandAfter(handIndex, created);

            Emit(new HandSplit(CurrentBoxIndex, handIndex, handIndex + 1));

            // One card to each of the two resulting hands.
            DealTo(CurrentBoxIndex, handIndex, replacement, faceUp: true);
            DealTo(CurrentBoxIndex, handIndex + 1, created, faceUp: true);

            // Re-resolve from the start of the walk. AdvanceToNextPlayableHand scans
            // boxes in order and lands on the first hand that still has legal actions,
            // so a split ace is closed automatically and play moves past it.
            AdvanceTurn();
        }
```

- [ ] **Step 5: Add the missing box method**

Add to `Assets/HouseRules/Blackjack/Core/Hands/Box.cs`:

```csharp
        public void ReplaceHand(int index, Hand hand) => _hands[index] = hand;
```

Remove `ApplySplit` from `Round.Stubs.cs`.

- [ ] **Step 6: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter SplitTests
```

Expected: PASS, 8 tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add split with ace and max-split rules"
```

---

### Task 11: Insurance

**Files:**
- Modify: `Assets/HouseRules/Blackjack/Core/Round/Round.Actions.cs` (add `ApplyInsurance`)
- Modify: `Assets/HouseRules/Blackjack/Core/Round/Round.Stubs.cs` (remove `ApplyInsurance`)
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/InsuranceTests.cs`

**Interfaces:**
- Consumes: `Box.SetInsuranceBet`, `Wallet`, `RoundState.Insurance`.
- Produces: `InsuranceTaken(int boxIndex, long amount)` and `InsuranceDeclined()` events.

Insurance costs half the original bet and pays 2:1 when the dealer has blackjack. It is offered once for all active boxes together.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/InsuranceTests.cs`:

```csharp
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class InsuranceTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round DealtWithAceUpcard(Wallet wallet, Card dealerHole)
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Ace), C(Rank.Seven), dealerHole),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            return round;
        }

        [Test]
        public void AceUpcard_EntersInsuranceState()
        {
            var round = DealtWithAceUpcard(new Wallet(1000), C(Rank.Four));
            Assert.AreEqual(RoundState.Insurance, round.State);
        }

        [Test]
        public void TakeInsurance_DebitsHalfTheOriginalBet()
        {
            var wallet = new Wallet(1000);
            var round = DealtWithAceUpcard(wallet, C(Rank.Four));

            Assert.AreEqual(990, wallet.Balance);
            round.Apply(PlayerAction.TakeInsurance);
            Assert.AreEqual(985, wallet.Balance);
            Assert.AreEqual(5, round.Boxes[0].InsuranceBet);
        }

        [Test]
        public void DeclineInsurance_CostsNothing()
        {
            var wallet = new Wallet(1000);
            var round = DealtWithAceUpcard(wallet, C(Rank.Four));

            round.Apply(PlayerAction.DeclineInsurance);

            Assert.AreEqual(990, wallet.Balance);
            Assert.AreEqual(0, round.Boxes[0].InsuranceBet);
        }

        [Test]
        public void AfterInsurance_NoDealerBlackjack_ProceedsToPlayerTurn()
        {
            var round = DealtWithAceUpcard(new Wallet(1000), C(Rank.Four));
            round.Apply(PlayerAction.DeclineInsurance);
            Assert.AreEqual(RoundState.PlayerTurn, round.State);
        }

        [Test]
        public void AfterInsurance_DealerBlackjack_EndsTheRound()
        {
            var round = DealtWithAceUpcard(new Wallet(1000), C(Rank.King));
            round.Apply(PlayerAction.DeclineInsurance);

            Assert.IsTrue(round.DealerHasBlackjack);
            Assert.AreEqual(RoundState.Complete, round.State);
        }

        [Test]
        public void Insurance_IsIllegal_WhenWalletCannotCoverIt()
        {
            var wallet = new Wallet(10);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Ace), C(Rank.Seven), C(Rank.Four)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();

            CollectionAssert.DoesNotContain(round.LegalActions, PlayerAction.TakeInsurance);
            CollectionAssert.Contains(round.LegalActions, PlayerAction.DeclineInsurance);
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
unity command run_tests --mode editor --filter InsuranceTests
```

Expected: FAIL with `NotImplementedException`.

- [ ] **Step 3: Add the events**

Append to `Assets/HouseRules/Blackjack/Core/Events/HandEvents.cs`:

```csharp
    public sealed class InsuranceTaken : GameEvent
    {
        public InsuranceTaken(int boxIndex, long amount)
        {
            BoxIndex = boxIndex;
            Amount = amount;
        }

        public int BoxIndex { get; }
        public long Amount { get; }
    }

    public sealed class InsuranceDeclined : GameEvent
    {
    }
```

- [ ] **Step 4: Make insurance affordability part of legality**

Replace the `InsuranceActions` branch in `Round.Legality.cs` with:

```csharp
                if (State == RoundState.Insurance)
                {
                    long cost = TotalInsuranceCost();
                    if (cost > 0 && _wallet.CanAfford(cost))
                    {
                        return InsuranceActions;
                    }

                    return new[] { PlayerAction.DeclineInsurance };
                }
```

And add to the same file:

```csharp
        private long TotalInsuranceCost()
        {
            long total = 0;
            foreach (Box box in _boxes)
            {
                if (box.IsActive)
                {
                    total += box.InitialBet / 2;
                }
            }

            return total;
        }
```

Insurance costs half the bet, and bets are always even, so this division is exact.

- [ ] **Step 5: Implement insurance**

Add to `Round.Actions.cs`:

```csharp
        private void ApplyInsurance(bool taken)
        {
            if (taken)
            {
                foreach (Box box in _boxes)
                {
                    if (!box.IsActive)
                    {
                        continue;
                    }

                    long premium = box.InitialBet / 2;
                    _wallet.Debit(premium);
                    box.SetInsuranceBet(premium);
                    Emit(new InsuranceTaken(box.Index, premium));
                }
            }
            else
            {
                Emit(new InsuranceDeclined());
            }

            if (DealerHasBlackjack)
            {
                RevealAndSettleDealerBlackjack();
                return;
            }

            BeginPlayerTurn();
        }
```

Remove `ApplyInsurance` from `Round.Stubs.cs`.

- [ ] **Step 6: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter InsuranceTests
```

Expected: PASS, 6 tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add insurance side bet"
```

---

### Task 12: Dealer play

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/Round/Round.Dealer.cs`
- Modify: `Assets/HouseRules/Blackjack/Core/Round/Round.Stubs.cs` (remove `BeginDealerTurn`)
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/DealerTests.cs`

**Interfaces:**
- Consumes: `DealerHand`, `BlackjackRules.DealerHitsSoft17`.
- Produces: `DealerRevealed(Card holeCard)` event; `BeginDealerTurn()` implementation that plays the dealer out and enters `Settlement`.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/DealerTests.cs`:

```csharp
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class DealerTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round PlayerStands(params Card[] script)
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(script), new Wallet(1000));
            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Stand);
            return round;
        }

        [Test]
        public void Dealer_HitsUntil17()
        {
            // player 20; dealer 5 + 6 = 11, then draws 9 for 20.
            var round = PlayerStands(
                C(Rank.Ten), C(Rank.Five), C(Rank.King), C(Rank.Six), C(Rank.Nine));

            Assert.AreEqual(20, round.DealerHand.Value.Total);
        }

        [Test]
        public void Dealer_StandsOnHard17()
        {
            var round = PlayerStands(
                C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Seven));

            Assert.AreEqual(17, round.DealerHand.Value.Total);
            Assert.AreEqual(2, round.DealerHand.Cards.Count);
        }

        [Test]
        public void Dealer_StandsOnSoft17_UnderStandardRules()
        {
            // Dealer 6 up, ace in the hole = soft 17, must stand.
            // The ace must be the HOLE card: an ace upcard diverts to the Insurance state.
            var round = PlayerStands(
                C(Rank.Ten), C(Rank.Six), C(Rank.King), C(Rank.Ace));

            Assert.AreEqual(17, round.DealerHand.Value.Total);
            Assert.IsTrue(round.DealerHand.Value.IsSoft);
            Assert.AreEqual(2, round.DealerHand.Cards.Count);
        }

        [Test]
        public void Dealer_HitsSoft17_WhenRulesSaySo()
        {
            var rules = new BlackjackRules(
                deckCount: 6,
                dealerHitsSoft17: true,
                doubleAfterSplit: true,
                maxSplitsPerBox: 3,
                resplitAces: false,
                hitSplitAces: false,
                surrenderAllowed: false,
                insuranceOffered: true,
                penetration: 0.75,
                minimumBet: 2,
                betIncrement: 2,
                maxBoxes: 3);

            // Dealer 6 up, ace in the hole = soft 17, hits under H17, draws a 2 for 19.
            var round = new Round(
                rules,
                new StackedShoe(C(Rank.Ten), C(Rank.Six), C(Rank.King), C(Rank.Ace), C(Rank.Two)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Stand);

            Assert.AreEqual(3, round.DealerHand.Cards.Count);
            Assert.AreEqual(19, round.DealerHand.Value.Total);
        }

        [Test]
        public void Dealer_CanBust()
        {
            var round = PlayerStands(
                C(Rank.Ten), C(Rank.Six), C(Rank.King), C(Rank.Six), C(Rank.King));

            Assert.IsTrue(round.DealerHand.IsBust);
        }

        [Test]
        public void DealerTurn_EndsWithRoundComplete()
        {
            var round = PlayerStands(
                C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Seven));

            Assert.AreEqual(RoundState.Complete, round.State);
        }

        [Test]
        public void Dealer_DoesNotDraw_WhenEveryPlayerHandBusted()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Four), C(Rank.King)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Hit); // busts with 29

            // Dealer has 10 and must not draw, because there is nothing left to beat.
            Assert.AreEqual(2, round.DealerHand.Cards.Count);
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
unity command run_tests --mode editor --filter DealerTests
```

Expected: FAIL — the dealer never draws, because `BeginDealerTurn` is still the stub.

- [ ] **Step 3: Add the event**

Append to `Assets/HouseRules/Blackjack/Core/Events/HandEvents.cs`:

```csharp
    public sealed class DealerRevealed : GameEvent
    {
        public DealerRevealed(Card holeCard)
        {
            HoleCard = holeCard;
        }

        public Card HoleCard { get; }
    }
```

- [ ] **Step 4: Implement dealer play**

`Assets/HouseRules/Blackjack/Core/Round/Round.Dealer.cs`:

```csharp
namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        private void BeginDealerTurn()
        {
            SetState(RoundState.DealerTurn);
            Emit(new DealerRevealed(DealerHand.Cards[1]));

            if (AnyLiveHandRemains())
            {
                PlayDealerOut();
            }

            Settle();
        }

        /// <summary>
        /// The dealer only draws when some player hand can still be beaten.
        /// If every hand busted, the house already won and drawing is theatre.
        /// </summary>
        private bool AnyLiveHandRemains()
        {
            foreach (Box box in _boxes)
            {
                if (!box.IsActive)
                {
                    continue;
                }

                foreach (Hand hand in box.Hands)
                {
                    if (!hand.IsBust)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void PlayDealerOut()
        {
            while (true)
            {
                HandValue value = DealerHand.Value;

                if (value.Total > 17)
                {
                    break;
                }

                if (value.Total == 17)
                {
                    bool mustHit = value.IsSoft && _rules.DealerHitsSoft17;
                    if (!mustHit)
                    {
                        break;
                    }
                }

                DealToDealer(faceUp: true);
            }
        }
    }
}
```

Remove `BeginDealerTurn` from `Round.Stubs.cs`.

- [ ] **Step 5: Add a temporary `Settle` stub**

Add to `Round.Stubs.cs`:

```csharp
        private void Settle() => SetState(RoundState.Complete);
```

- [ ] **Step 6: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter DealerTests
```

Expected: PASS, 7 tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: add dealer play with soft-17 rule"
```

---

### Task 13: Settlement and payouts

**Files:**
- Create: `Assets/HouseRules/Blackjack/Core/Settlement/HandOutcome.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Settlement/Settlement.cs`
- Create: `Assets/HouseRules/Blackjack/Core/Round/Round.Settlement.cs`
- Delete: `Assets/HouseRules/Blackjack/Core/Round/Round.Stubs.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/SettlementTests.cs`

**Interfaces:**
- Consumes: everything prior.
- Produces: `enum HandOutcome { Win, Lose, Push, Blackjack, Bust }`; `sealed class Settlement` with `int BoxIndex`, `int HandIndex`, `HandOutcome Outcome`, `long Wager`, `long Payout`, `long Delta`; `IReadOnlyList<Settlement> Settlements` on `Round`; events `HandSettled(Settlement)` and `RoundSettled(long totalDelta)`.

**Payout convention** — stated once, precisely, because this is where ambiguity causes bugs:

- The wager is **already debited** at bet time.
- `Payout` is what gets credited back to the wallet.
- `Delta` is `Payout - Wager` — the net change across the whole round.

| Outcome | Payout | Delta |
|---|---|---|
| Lose / Bust | `0` | `-Wager` |
| Push | `Wager` | `0` |
| Win | `Wager * 2` | `+Wager` |
| Blackjack | `Wager + Wager * 3 / 2` | `+Wager * 3 / 2` |

Insurance settles separately: it pays `2 * premium` profit, so `3 * premium` is credited when the dealer has blackjack, and `0` otherwise.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/SettlementTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class SettlementTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        private static Round PlayOut(Wallet wallet, PlayerAction? action, params Card[] script)
        {
            var round = new Round(BlackjackRules.Standard, new StackedShoe(script), wallet);
            round.PlaceBet(0, 10);
            round.Deal();

            if (action.HasValue && round.State == RoundState.PlayerTurn)
            {
                round.Apply(action.Value);
            }

            return round;
        }

        [Test]
        public void PlayerWins_PaysEvenMoney()
        {
            var wallet = new Wallet(1000);
            // player 20, dealer 18
            var round = PlayOut(wallet, PlayerAction.Stand,
                C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Eight));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Win, s.Outcome);
            Assert.AreEqual(20, s.Payout);
            Assert.AreEqual(10, s.Delta);
            Assert.AreEqual(1010, wallet.Balance);
        }

        [Test]
        public void PlayerLoses_PaysNothing()
        {
            var wallet = new Wallet(1000);
            // player 18, dealer 20
            var round = PlayOut(wallet, PlayerAction.Stand,
                C(Rank.Ten), C(Rank.Ten), C(Rank.Eight), C(Rank.King));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Lose, s.Outcome);
            Assert.AreEqual(0, s.Payout);
            Assert.AreEqual(-10, s.Delta);
            Assert.AreEqual(990, wallet.Balance);
        }

        [Test]
        public void Push_ReturnsTheWager()
        {
            var wallet = new Wallet(1000);
            var round = PlayOut(wallet, PlayerAction.Stand,
                C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.King));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Push, s.Outcome);
            Assert.AreEqual(10, s.Payout);
            Assert.AreEqual(0, s.Delta);
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Bust_LosesRegardlessOfDealerHand()
        {
            var wallet = new Wallet(1000);
            var round = PlayOut(wallet, PlayerAction.Hit,
                C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Six), C(Rank.King), C(Rank.King));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Bust, s.Outcome);
            Assert.AreEqual(-10, s.Delta);
            Assert.AreEqual(990, wallet.Balance);
        }

        [Test]
        public void Blackjack_Pays3To2()
        {
            var wallet = new Wallet(1000);
            var round = PlayOut(wallet, null,
                C(Rank.Ace), C(Rank.Nine), C(Rank.King), C(Rank.Seven));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Blackjack, s.Outcome);
            Assert.AreEqual(25, s.Payout, "10 stake + 15 winnings");
            Assert.AreEqual(15, s.Delta);
            Assert.AreEqual(1015, wallet.Balance);
        }

        [Test]
        public void BlackjackVersusDealerBlackjack_Pushes()
        {
            var wallet = new Wallet(1000);
            // Dealer's ace must be the hole card — an ace upcard diverts to Insurance.
            var round = PlayOut(wallet, null,
                C(Rank.Ace), C(Rank.King), C(Rank.King), C(Rank.Ace));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Push, s.Outcome);
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void DealerBlackjack_BeatsPlayerTwenty()
        {
            var wallet = new Wallet(1000);
            // Player 20 (not a natural) against a dealer natural. Ace in the hole.
            var round = PlayOut(wallet, null,
                C(Rank.Ten), C(Rank.King), C(Rank.Ten), C(Rank.Ace));

            Settlement s = round.Settlements.Single();
            Assert.AreEqual(HandOutcome.Lose, s.Outcome);
            Assert.AreEqual(990, wallet.Balance);
        }

        [Test]
        public void Insurance_Pays2To1_WhenDealerHasBlackjack()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Nine), C(Rank.Ace), C(Rank.Seven), C(Rank.King)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.TakeInsurance);

            // Bet 10 lost, insurance premium 5 returned as 15.
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Insurance_IsLost_WhenDealerHasNoBlackjack()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Ace), C(Rank.King), C(Rank.Four), C(Rank.Five)),
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.TakeInsurance);
            round.Apply(PlayerAction.Stand);

            // Player 20 beats dealer 20? No: dealer draws to 20 -> push on main bet,
            // insurance premium of 5 is lost.
            Assert.AreEqual(995, wallet.Balance);
        }

        [Test]
        public void EveryHandOfASplitBoxSettlesIndependently()
        {
            var wallet = new Wallet(1000);
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(
                    C(Rank.Eight), C(Rank.Six), C(Rank.Eight), C(Rank.Ten),
                    C(Rank.King),  // hand 0 -> 18
                    C(Rank.Two),   // hand 1 -> 10
                    C(Rank.Nine)), // dealer draws to 25? 6+10=16, +9 = 25 bust
                wallet);

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Split);
            round.Apply(PlayerAction.Stand);
            round.Apply(PlayerAction.Stand);

            Assert.AreEqual(2, round.Settlements.Count);
        }

        [Test]
        public void RoundSettled_TotalMatchesSumOfDeltas()
        {
            var wallet = new Wallet(1000);
            var round = PlayOut(wallet, PlayerAction.Stand,
                C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Eight));

            long expected = round.Settlements.Sum(s => s.Delta);
            Assert.AreEqual(expected, round.TotalDelta);
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
unity command run_tests --mode editor --filter SettlementTests
```

Expected: FAIL — `Settlement`, `HandOutcome`, `Settlements`, `TotalDelta` do not exist.

- [ ] **Step 3: Write the settlement types**

`Assets/HouseRules/Blackjack/Core/Settlement/HandOutcome.cs`:

```csharp
namespace HouseRules.Blackjack
{
    public enum HandOutcome
    {
        Win,
        Lose,
        Push,
        Blackjack,
        Bust
    }
}
```

`Assets/HouseRules/Blackjack/Core/Settlement/Settlement.cs`:

```csharp
namespace HouseRules.Blackjack
{
    /// <summary>
    /// The result of one hand. The wager was debited when the bet was placed, so
    /// <see cref="Payout"/> is what gets credited back and <see cref="Delta"/> is the net change.
    /// </summary>
    public sealed class Settlement
    {
        public Settlement(int boxIndex, int handIndex, HandOutcome outcome, long wager, long payout)
        {
            BoxIndex = boxIndex;
            HandIndex = handIndex;
            Outcome = outcome;
            Wager = wager;
            Payout = payout;
        }

        public int BoxIndex { get; }
        public int HandIndex { get; }
        public HandOutcome Outcome { get; }
        public long Wager { get; }
        public long Payout { get; }
        public long Delta => Payout - Wager;
    }
}
```

- [ ] **Step 4: Add the settlement events**

Append to `Assets/HouseRules/Blackjack/Core/Events/HandEvents.cs`:

```csharp
    public sealed class HandSettled : GameEvent
    {
        public HandSettled(Settlement settlement)
        {
            Settlement = settlement;
        }

        public Settlement Settlement { get; }
    }

    public sealed class RoundSettled : GameEvent
    {
        public RoundSettled(long totalDelta)
        {
            TotalDelta = totalDelta;
        }

        public long TotalDelta { get; }
    }
```

- [ ] **Step 5: Implement settlement**

`Assets/HouseRules/Blackjack/Core/Round/Round.Settlement.cs`:

```csharp
using System.Collections.Generic;

namespace HouseRules.Blackjack
{
    public sealed partial class Round
    {
        private readonly List<Settlement> _settlements = new List<Settlement>();

        public IReadOnlyList<Settlement> Settlements => _settlements;

        public long TotalDelta { get; private set; }

        /// <summary>
        /// Called when the dealer peeks a natural. Player hands settle immediately;
        /// nobody gets to act, so only naturals push.
        /// </summary>
        private void RevealAndSettleDealerBlackjack()
        {
            SetState(RoundState.DealerTurn);
            Emit(new DealerRevealed(DealerHand.Cards[1]));
            Settle();
        }

        private void Settle()
        {
            SetState(RoundState.Settlement);

            bool dealerBlackjack = DealerHasBlackjack;
            int dealerTotal = DealerHand.Value.Total;
            bool dealerBust = DealerHand.IsBust;

            foreach (Box box in _boxes)
            {
                if (!box.IsActive)
                {
                    continue;
                }

                SettleInsurance(box, dealerBlackjack);

                for (int h = 0; h < box.Hands.Count; h++)
                {
                    Hand hand = box.Hands[h];
                    Settlement settlement = SettleHand(box.Index, h, hand, dealerBlackjack, dealerBust, dealerTotal);

                    _settlements.Add(settlement);
                    TotalDelta += settlement.Delta;

                    if (settlement.Payout > 0)
                    {
                        _wallet.Credit(settlement.Payout);
                    }

                    Emit(new HandSettled(settlement));
                }
            }

            Emit(new RoundSettled(TotalDelta));
            SetState(RoundState.Complete);
        }

        private void SettleInsurance(Box box, bool dealerBlackjack)
        {
            if (box.InsuranceBet <= 0)
            {
                return;
            }

            if (dealerBlackjack)
            {
                // Pays 2:1 — the premium plus twice the premium in winnings.
                _wallet.Credit(box.InsuranceBet * 3);
                TotalDelta += box.InsuranceBet * 2;
            }
            else
            {
                TotalDelta -= box.InsuranceBet;
            }
        }

        private static Settlement SettleHand(
            int boxIndex,
            int handIndex,
            Hand hand,
            bool dealerBlackjack,
            bool dealerBust,
            int dealerTotal)
        {
            long wager = hand.Wager;

            if (hand.IsBust)
            {
                return new Settlement(boxIndex, handIndex, HandOutcome.Bust, wager, 0);
            }

            if (hand.IsBlackjack)
            {
                if (dealerBlackjack)
                {
                    return new Settlement(boxIndex, handIndex, HandOutcome.Push, wager, wager);
                }

                // 3:2. Wagers are always even, so this division is exact.
                long winnings = wager * 3 / 2;
                return new Settlement(boxIndex, handIndex, HandOutcome.Blackjack, wager, wager + winnings);
            }

            if (dealerBlackjack)
            {
                return new Settlement(boxIndex, handIndex, HandOutcome.Lose, wager, 0);
            }

            if (dealerBust)
            {
                return new Settlement(boxIndex, handIndex, HandOutcome.Win, wager, wager * 2);
            }

            int playerTotal = hand.Value.Total;

            if (playerTotal > dealerTotal)
            {
                return new Settlement(boxIndex, handIndex, HandOutcome.Win, wager, wager * 2);
            }

            if (playerTotal == dealerTotal)
            {
                return new Settlement(boxIndex, handIndex, HandOutcome.Push, wager, wager);
            }

            return new Settlement(boxIndex, handIndex, HandOutcome.Lose, wager, 0);
        }
    }
}
```

- [ ] **Step 6: Delete the stub file**

```bash
rm Assets/HouseRules/Blackjack/Core/Round/Round.Stubs.cs
rm Assets/HouseRules/Blackjack/Core/Round/Round.Stubs.cs.meta
```

Move the `_events` list and `Emit` method into `Round.cs` (they were living in the stub file):

```csharp
        private readonly List<GameEvent> _events = new List<GameEvent>();

        private void Emit(GameEvent gameEvent) => _events.Add(gameEvent);
```

- [ ] **Step 7: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter SettlementTests
```

Expected: PASS, 11 tests.

- [ ] **Step 8: Run the whole suite**

```bash
unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly
```

Expected: PASS, all tests written so far.

- [ ] **Step 9: Commit**

```bash
git add -A Assets/HouseRules/Blackjack
git commit -m "feat: add settlement with 3:2 blackjack and insurance payouts"
```

---

### Task 14: The event stream

**Files:**
- Modify: `Assets/HouseRules/Blackjack/Core/Round/Round.cs` (expose `DrainEvents`)
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/EventTests.cs`

**Interfaces:**
- Consumes: all event types.
- Produces: `IReadOnlyList<GameEvent> DrainEvents()` on `Round` — returns everything emitted since the last call and clears the buffer.

The presentation layer in Plan 2 consumes exactly this. Ordering is a contract, not an accident, so it gets tested here.

- [ ] **Step 1: Write the failing test**

`Assets/HouseRules/Blackjack/Tests/EditMode/EventTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class EventTests
    {
        private static Card C(Rank rank) => new Card(rank, Suit.Spades);

        [Test]
        public void Deal_EmitsRoundStartedThenCardsInDealOrder()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Nine), C(Rank.Seven), C(Rank.Four)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();

            List<GameEvent> events = round.DrainEvents().ToList();

            Assert.IsInstanceOf<RoundStarted>(events[0]);

            var cards = events.OfType<CardDealt>().ToList();
            Assert.AreEqual(4, cards.Count);
            Assert.AreEqual(C(Rank.Ten), cards[0].Card);
            Assert.AreEqual(C(Rank.Nine), cards[1].Card);
            Assert.AreEqual(C(Rank.Seven), cards[2].Card);
            Assert.AreEqual(C(Rank.Four), cards[3].Card);
        }

        [Test]
        public void HoleCard_IsEmittedFaceDown()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Nine), C(Rank.Seven), C(Rank.Four)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();

            var cards = round.DrainEvents().OfType<CardDealt>().ToList();
            Assert.IsTrue(cards[1].FaceUp, "Upcard should be face up.");
            Assert.IsFalse(cards[3].FaceUp, "Hole card should be face down.");
            Assert.AreEqual(CardDealt.DealerBoxIndex, cards[3].BoxIndex);
        }

        [Test]
        public void DrainEvents_ClearsTheBuffer()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Nine), C(Rank.Seven), C(Rank.Four)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();

            Assert.IsNotEmpty(round.DrainEvents());
            Assert.IsEmpty(round.DrainEvents());
        }

        [Test]
        public void Bust_EmitsHandBustedBeforeDealerRevealed()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Six), C(Rank.Nine), C(Rank.Four), C(Rank.King)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();
            round.DrainEvents();

            round.Apply(PlayerAction.Hit);
            List<GameEvent> events = round.DrainEvents().ToList();

            int bustIndex = events.FindIndex(e => e is HandBusted);
            int revealIndex = events.FindIndex(e => e is DealerRevealed);

            Assert.Greater(bustIndex, -1, "Expected a HandBusted event.");
            Assert.Greater(revealIndex, bustIndex, "Dealer must be revealed after the bust.");
        }

        [Test]
        public void RoundSettled_IsTheFinalEvent()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Eight)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.Deal();
            round.Apply(PlayerAction.Stand);

            List<GameEvent> events = round.DrainEvents().ToList();
            Assert.IsInstanceOf<RoundSettled>(events[events.Count - 1]);
        }

        [Test]
        public void EverySettledHand_EmitsAHandSettledEvent()
        {
            var round = new Round(
                BlackjackRules.Standard,
                new StackedShoe(C(Rank.Ten), C(Rank.Ten), C(Rank.King), C(Rank.Eight)),
                new Wallet(1000));

            round.PlaceBet(0, 10);
            round.PlaceBet(1, 10);
            round.Deal();
            round.Apply(PlayerAction.Stand);
            round.Apply(PlayerAction.Stand);

            int settled = round.DrainEvents().OfType<HandSettled>().Count();
            Assert.AreEqual(round.Settlements.Count, settled);
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
unity command run_tests --mode editor --filter EventTests
```

Expected: FAIL — `DrainEvents` does not exist.

- [ ] **Step 3: Expose the event stream**

Add to `Assets/HouseRules/Blackjack/Core/Round/Round.cs`:

```csharp
        /// <summary>
        /// Returns everything emitted since the last call and clears the buffer.
        /// The presentation layer drains this and plays the events back as animation.
        /// </summary>
        public IReadOnlyList<GameEvent> DrainEvents()
        {
            var drained = _events.ToArray();
            _events.Clear();
            return drained;
        }
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter EventTests
```

Expected: PASS, 6 tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "feat: expose ordered event stream for the presentation layer"
```

---

### Task 15: Basic strategy and the house-edge test

**Files:**
- Create: `Assets/HouseRules/Blackjack/Tests/EditMode/BasicStrategy.cs`
- Test: `Assets/HouseRules/Blackjack/Tests/EditMode/HouseEdgeTests.cs`

**Interfaces:**
- Consumes: `Round`, `LegalActions`, `Hand`, `Card`.
- Produces: test-only `static class BasicStrategy` with `PlayerAction Decide(Hand hand, Card dealerUpcard, IReadOnlyList<PlayerAction> legal)`.

**Read this before implementing.** The strategy table below is written from standard 6-deck, dealer-stands-soft-17, double-after-split, no-surrender basic strategy. **Verify it against a published chart before trusting a house-edge failure.** If the table is wrong, the house edge comes out too high and the test fails — but you will not be able to tell whether the bug is in the engine or in the table. Validating the table first makes a failure point unambiguously at the engine.

- [ ] **Step 1: Write the strategy table**

`Assets/HouseRules/Blackjack/Tests/EditMode/BasicStrategy.cs`:

```csharp
using System.Collections.Generic;

namespace HouseRules.Blackjack.Tests
{
    /// <summary>
    /// Test-only basic strategy for 6 decks, dealer stands soft 17, double after split
    /// allowed, no surrender. Used to drive the statistical house-edge test.
    /// VERIFY AGAINST A PUBLISHED CHART before diagnosing a house-edge failure.
    /// </summary>
    public static class BasicStrategy
    {
        public static PlayerAction Decide(
            Hand hand,
            Card dealerUpcard,
            IReadOnlyList<PlayerAction> legal)
        {
            int up = UpcardIndex(dealerUpcard);

            if (hand.IsPair && Contains(legal, PlayerAction.Split) && ShouldSplit(hand, up))
            {
                return PlayerAction.Split;
            }

            HandValue value = hand.Value;

            if (value.IsSoft)
            {
                return SoftDecision(value.Total, up, legal);
            }

            return HardDecision(value.Total, up, legal);
        }

        /// <summary>Maps a dealer upcard to a column index: 0 = 2 … 8 = 10, 9 = ace.</summary>
        private static int UpcardIndex(Card card)
        {
            if (card.Rank == Rank.Ace)
            {
                return 9;
            }

            return card.BaseValue - 2;
        }

        private static bool ShouldSplit(Hand hand, int up)
        {
            Rank rank = hand.Cards[0].Rank;

            switch (rank)
            {
                case Rank.Ace:
                    return true;
                case Rank.Eight:
                    return true;
                case Rank.Ten:
                case Rank.Jack:
                case Rank.Queen:
                case Rank.King:
                    return false;
                case Rank.Nine:
                    // Split against 2-6 and 8-9; stand against 7, 10, ace.
                    return (up >= 0 && up <= 4) || up == 6 || up == 7;
                case Rank.Seven:
                    return up <= 5;
                case Rank.Six:
                    return up <= 4;
                case Rank.Five:
                    return false;
                case Rank.Four:
                    return up == 3 || up == 4;
                case Rank.Three:
                case Rank.Two:
                    return up <= 5;
                default:
                    return false;
            }
        }

        private static PlayerAction HardDecision(int total, int up, IReadOnlyList<PlayerAction> legal)
        {
            if (total >= 17)
            {
                return PlayerAction.Stand;
            }

            if (total >= 13 && total <= 16)
            {
                return up <= 4 ? PlayerAction.Stand : Hit(legal);
            }

            if (total == 12)
            {
                return (up >= 2 && up <= 4) ? PlayerAction.Stand : Hit(legal);
            }

            if (total == 11)
            {
                return up <= 8 ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
            }

            if (total == 10)
            {
                return up <= 7 ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
            }

            if (total == 9)
            {
                return (up >= 1 && up <= 4) ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
            }

            return Hit(legal);
        }

        private static PlayerAction SoftDecision(int total, int up, IReadOnlyList<PlayerAction> legal)
        {
            switch (total)
            {
                case 20:
                case 21:
                    return PlayerAction.Stand;
                case 19:
                    return PlayerAction.Stand;
                case 18:
                    if (up <= 4)
                    {
                        return DoubleOr(PlayerAction.Stand, legal);
                    }

                    if (up == 5 || up == 6)
                    {
                        return PlayerAction.Stand;
                    }

                    return Hit(legal);
                case 17:
                    return (up >= 1 && up <= 4) ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
                case 16:
                case 15:
                    return (up >= 2 && up <= 4) ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
                case 14:
                case 13:
                    return (up >= 3 && up <= 4) ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
                default:
                    return Hit(legal);
            }
        }

        /// <summary>Double if it is legal right now, otherwise fall back.</summary>
        private static PlayerAction DoubleOr(PlayerAction fallback, IReadOnlyList<PlayerAction> legal)
        {
            if (Contains(legal, PlayerAction.Double))
            {
                return PlayerAction.Double;
            }

            return fallback == PlayerAction.Hit ? Hit(legal) : PlayerAction.Stand;
        }

        private static PlayerAction Hit(IReadOnlyList<PlayerAction> legal)
        {
            return Contains(legal, PlayerAction.Hit) ? PlayerAction.Hit : PlayerAction.Stand;
        }

        private static bool Contains(IReadOnlyList<PlayerAction> legal, PlayerAction action)
        {
            for (int i = 0; i < legal.Count; i++)
            {
                if (legal[i] == action)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
```

- [ ] **Step 2: Write the statistical test**

`Assets/HouseRules/Blackjack/Tests/EditMode/HouseEdgeTests.cs`:

```csharp
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class HouseEdgeTests
    {
        private const int Rounds = 100000;
        private const long Wager = 10;

        [Test]
        [Category("Statistical")]
        public void BasicStrategy_ProducesTheExpectedHouseEdge()
        {
            var rules = BlackjackRules.Standard;
            var random = new SeededRandom(20260813);
            var shoe = new Shoe(rules.DeckCount, rules.Penetration, random);

            // A large float balance would lose precision, so track staked and returned separately.
            long totalStaked = 0;
            long netDelta = 0;
            var wallet = new Wallet(long.MaxValue / 4);
            long startingBalance = wallet.Balance;

            for (int i = 0; i < Rounds; i++)
            {
                var round = new Round(rules, shoe, wallet);
                round.PlaceBet(0, Wager);
                totalStaked += Wager;

                round.Deal();

                while (round.State == RoundState.Insurance)
                {
                    // Basic strategy never takes insurance.
                    round.Apply(PlayerAction.DeclineInsurance);
                }

                int guard = 0;
                while (round.State == RoundState.PlayerTurn)
                {
                    PlayerAction action = BasicStrategy.Decide(
                        round.CurrentHand, round.DealerUpcard, round.LegalActions);

                    round.Apply(action);

                    if (++guard > 100)
                    {
                        Assert.Fail("Player turn failed to terminate — likely a turn-advancement bug.");
                    }
                }

                Assert.AreEqual(RoundState.Complete, round.State, $"Round {i} did not complete.");

                netDelta += round.TotalDelta;
                round.DrainEvents();
            }

            // Wallet must reconcile exactly with the deltas the engine reported.
            Assert.AreEqual(startingBalance + netDelta, wallet.Balance,
                "Wallet balance diverged from the sum of settlement deltas.");

            double houseEdgePercent = -100.0 * netDelta / totalStaked;

            TestContext.WriteLine(
                $"Rounds: {Rounds}, staked: {totalStaked}, net: {netDelta}, " +
                $"house edge: {houseEdgePercent:F3}%");

            // Published basic-strategy edge for this ruleset is roughly 0.4%.
            // The band absorbs sampling variance across 100k rounds; a result outside
            // it means a systemic payout or rule error, not bad luck.
            Assert.That(houseEdgePercent, Is.InRange(-1.0, 1.5),
                $"House edge {houseEdgePercent:F3}% is outside the plausible band.");
        }

        [Test]
        public void ThousandRounds_NeverThrowAndAlwaysComplete()
        {
            var rules = BlackjackRules.Standard;
            var shoe = new Shoe(rules.DeckCount, rules.Penetration, new SeededRandom(99));
            var wallet = new Wallet(long.MaxValue / 4);

            for (int i = 0; i < 1000; i++)
            {
                var round = new Round(rules, shoe, wallet);
                round.PlaceBet(0, 10);
                round.PlaceBet(1, 10);
                round.Deal();

                while (round.State == RoundState.Insurance)
                {
                    round.Apply(PlayerAction.DeclineInsurance);
                }

                while (round.State == RoundState.PlayerTurn)
                {
                    round.Apply(BasicStrategy.Decide(
                        round.CurrentHand, round.DealerUpcard, round.LegalActions));
                }

                Assert.AreEqual(RoundState.Complete, round.State);
            }
        }
    }
}
```

The band is deliberately wide. Its job is to catch a systemic error — a payout paying 2:1 instead of 3:2, a dealer hitting when it should stand — not to certify the edge to three decimals. Tightening it later, once the number is stable across seeds, is a reasonable follow-up.

- [ ] **Step 3: Run the test**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter HouseEdgeTests --timeout 600
```

Expected: PASS, 2 tests. Read the printed house-edge line. If it sits far outside roughly 0.2%–0.8%, stop and investigate before continuing — verify the strategy table against a published chart first, then the settlement math.

- [ ] **Step 4: Run the complete suite**

```bash
unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly --timeout 600
```

Expected: PASS, every test in the plan.

- [ ] **Step 5: Commit**

```bash
git add Assets/HouseRules/Blackjack
git commit -m "test: add basic strategy table and statistical house-edge test"
```

---

## Completion Criteria

The core engine is done when:

- Every test above passes via `unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly`.
- `Round.Stubs.cs` no longer exists.
- The `HouseRules.Blackjack` asmdef still has `"noEngineReferences": true` and an empty `references` array.
- The house-edge test prints a figure consistent with published basic-strategy expectations for the ruleset.

At that point the engine plays complete, correct blackjack headlessly, and Plan 2 can be written against its real API.
