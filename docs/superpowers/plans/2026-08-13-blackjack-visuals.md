# Blackjack Visuals Implementation Plan (Plan 2b)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make blackjack playable and then make it look like a card game.

**Architecture:** Everything plugs into the `IEventPresenter` seam that Plan 2a already built. Task 1 ships a *text* presenter so the game is playable immediately; Tasks 2–5 replace it with a card presenter driving pooled 3D card views on a felt table. Because both implement the same interface, the visuals are swapped behind a game that already works.

**Tech Stack:** Unity 6.3 LTS (`6000.3.22f1`), URP 17.3.0, uGUI 2.0.0 (includes TextMeshPro), C# 9, .NET Standard 2.1.

**Source spec:** `docs/superpowers/specs/2026-08-13-blackjack-design.md` (§6 presentation, §11 definition of done)

## Ordering Rationale — Playable First

Task 1 delivers a scene you can press Play on and actually play: real betting, hitting, standing, doubling, splitting, insurance, a live balance, and a text log of every card. It is deliberately ugly.

This ordering is not just about seeing something sooner. A text presenter and a card presenter implement the *same* interface, so Task 1 proves the wiring — session, sequencer, input gating, persistence — while the visual layer is still trivial. Building the pretty version first would surface wiring bugs and layout bugs simultaneously, with no way to tell them apart.

## Global Constraints

- Unity `6000.3.22f1`, URP 17.3.0, C# 9, .NET Standard 2.1.
- Core assembly `HouseRules.Blackjack` stays untouched: `"references": []`, `"overrideReferences": true`, `"noEngineReferences": true`. **No task in this plan modifies Core.**
- All new code goes in `HouseRules.Blackjack.Presentation`.
- **No `record` types, no `init`-only setters** — Unity's .NET Standard 2.1 profile lacks `IsExternalInit`.
- Money is `long`. Naming: PascalCase public, `_camelCase` private including `[SerializeField]`.
- `.meta` files committed alongside assets. Conventional commits.
- Never edit `Library/`, `Temp/`, `obj/`, `Logs/`.

## Working Loop

The Editor must be running. `unity` is NOT on PATH; prefix your shell:

```bash
$env:PATH = "C:\Users\bensi\AppData\Local\Unity\bin;" + $env:PATH
```

After any `.cs` / `.asmdef` / `.json` change: `unity command recompile`, then poll `unity command recompile_status` until `completed`.

- EditMode: `unity command run_tests --mode editor --filter <Class>`
- PlayMode: **async only.** `unity command run_tests --mode playmode --async_tests true --timeout 600`, then poll `unity command test_status`. A synchronous PlayMode run reports `Total: 0` with `success: true` and executes nothing — treat `Total: 0` as "did not run", never "passed".

### Verifying visuals

This plan produces things you look at, so verification is visual as well as automated:

```bash
unity command editor_play                     # enter play mode
unity command capture_game_view --width 900 --height 500 --save_path Assets/_shot.png
unity command editor_stop
```

`--save_path` resolves against the **authoring root** (`Assets/`), and paths outside the project root are rejected. Delete the capture and its `.meta` afterwards — it is a check, not an asset.

Check `unity command get_console_logs --severity error` after entering play mode. A silent NullReferenceException in `Start` looks identical to a blank screen.

## The API You Are Building On

`HouseRules.Blackjack.Presentation` (from Plan 2a):

```csharp
public interface IEventPresenter { IEnumerator Present(GameEvent gameEvent); }

public sealed class EventSequencer : MonoBehaviour
{
    public bool IsIdle { get; }
    public int PendingCount { get; }
    public event Action Idle;
    public void SetPresenter(IEventPresenter presenter);
    public void Enqueue(IEnumerable<GameEvent> events);
}

public sealed class BlackjackSession : MonoBehaviour
{
    public Wallet Wallet { get; }
    public Round CurrentRound { get; }
    public RoundState State { get; }
    public bool IsBusy { get; }
    public bool CanAcceptInput { get; }
    public IReadOnlyList<PlayerAction> LegalActions { get; }   // EMPTY while busy
    public event Action RoundCompleted;
    public void Configure(BlackjackRules rules, IShoe shoe, Wallet wallet, EventSequencer sequencer);
    public void BeginRound();
    public void PlaceBet(int boxIndex, long wager);
    public void Deal();
    public void Apply(PlayerAction action);
    public void AbandonRound();
}

public sealed class WalletStore
{
    public WalletStore(string filePath);
    public static string DefaultPath { get; }
    public long StartingBalanceDefault { get; }
    public Wallet Load();
    public void Save(Wallet wallet);
    public void Delete();
}

public static class Easing { /* Linear, OutCubic, InOutCubic, OutBack, Clamp01 */ }
public static class Tween
{
    public static IEnumerator Move(Transform t, Vector3 to, float duration, Func<float,float> ease = null);
    public static IEnumerator MoveAndRotate(Transform t, Vector3 toPos, Quaternion toRot, float duration, Func<float,float> ease = null);
    public static IEnumerator Wait(float seconds);
}
```

Engine events a presenter must handle: `RoundStarted`, `ShoeReshuffled`, `CardDealt` (`BoxIndex`, `HandIndex`, `Card`, `FaceUp`; `CardDealt.DealerBoxIndex == -1` means the dealer), `PlayerTurnStarted`, `HandStood`, `HandBusted`, `HandDoubled`, `HandSplit`, `InsuranceOffered`, `InsuranceTaken`, `InsuranceDeclined`, `InsuranceSettled`, `DealerRevealed`, `HandSettled`, `RoundSettled`, `RoundAbandoned`.

`Card` exposes `Rank` (`Two`=2 … `Ten`=10, `Jack`, `Queen`, `King`, `Ace`=14) and `Suit` (`Clubs`, `Diamonds`, `Hearts`, `Spades`).

---

## File Structure

```
Assets/HouseRules/Blackjack/Presentation/
  Bootstrap/
    BlackjackBootstrap.cs        Task 1 — builds the whole rig in code
  Views/
    TextEventPresenter.cs        Task 1 — the playable text presenter
    ActionBarView.cs             Task 1 — buttons from LegalActions
    WalletView.cs                Task 1 — balance readout
    CardView.cs                  Task 3
    CardPool.cs                  Task 3
    HandView.cs                  Task 4
    BoxView.cs                   Task 4
    TableView.cs                 Task 4
    TableCardPresenter.cs        Task 5 — the real presenter
  Art/
    CardFaces.cs                 Task 2 — atlas lookup at runtime

Assets/HouseRules/Blackjack/Editor/
  HouseRules.Blackjack.EditorTools.asmdef   Task 2
  CardAtlasGenerator.cs                     Task 2 — menu item, generates the atlas

Assets/HouseRules/Blackjack/Art/Generated/   Task 2 output (committed)
Assets/Scenes/Blackjack.unity                Task 1
```

---

### Task 1: A playable game

**Files:**
- Create: `Assets/HouseRules/Blackjack/Presentation/Views/TextEventPresenter.cs`
- Create: `Assets/HouseRules/Blackjack/Presentation/Views/ActionBarView.cs`
- Create: `Assets/HouseRules/Blackjack/Presentation/Views/WalletView.cs`
- Create: `Assets/HouseRules/Blackjack/Presentation/Bootstrap/BlackjackBootstrap.cs`
- Create: `Assets/Scenes/Blackjack.unity` (via the pipeline CLI)

**Interfaces:**
- Consumes: `BlackjackSession`, `EventSequencer`, `IEventPresenter`, `WalletStore`, `Tween`, and the Core engine types.
- Produces: `TextEventPresenter` (implements `IEventPresenter`), `ActionBarView`, `WalletView`, `BlackjackBootstrap`.

**The bootstrap builds its entire UI in code.** The scene contains exactly one GameObject with one component. There is no prefab wiring to get wrong, no scene YAML to hand-edit, and the whole playable slice is reviewable as C#. This matters more than it sounds: hand-authored `.unity` YAML is the single most fragile artifact in a Unity repo.

- [ ] **Step 1: Write the text presenter**

`Assets/HouseRules/Blackjack/Presentation/Views/TextEventPresenter.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HouseRules.Blackjack;
using UnityEngine;
using UnityEngine.UI;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Renders the engine's event stream as a scrolling text log. Deliberately the
    /// simplest possible IEventPresenter: it makes the game playable before any art
    /// exists, and proves the session/sequencer wiring independently of layout.
    /// </summary>
    public sealed class TextEventPresenter : MonoBehaviour, IEventPresenter
    {
        private const int MaxLines = 18;

        private readonly Queue<string> _lines = new Queue<string>();
        private Text _target;

        /// <summary>Seconds each event lingers, so a deal reads as a sequence rather than a blur.</summary>
        public float SecondsPerEvent { get; set; } = 0.18f;

        public void SetTarget(Text target) => _target = target;

        public IEnumerator Present(GameEvent gameEvent)
        {
            string line = Describe(gameEvent);

            if (!string.IsNullOrEmpty(line))
            {
                Append(line);
                yield return Tween.Wait(SecondsPerEvent);
            }
        }

        private void Append(string line)
        {
            _lines.Enqueue(line);

            while (_lines.Count > MaxLines)
            {
                _lines.Dequeue();
            }

            if (_target == null)
            {
                return;
            }

            var builder = new StringBuilder();
            foreach (string existing in _lines)
            {
                builder.AppendLine(existing);
            }

            _target.text = builder.ToString();
        }

        public void Clear()
        {
            _lines.Clear();
            if (_target != null)
            {
                _target.text = string.Empty;
            }
        }

        private static string Describe(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case RoundStarted _:
                    return "--- new round ---";
                case ShoeReshuffled _:
                    return "shoe reshuffled";
                case CardDealt dealt:
                    return DescribeCard(dealt);
                case PlayerTurnStarted turn:
                    return $"your turn: box {turn.BoxIndex + 1}, hand {turn.HandIndex + 1}";
                case HandStood stood:
                    return $"stand (box {stood.BoxIndex + 1})";
                case HandBusted busted:
                    return $"BUST (box {busted.BoxIndex + 1})";
                case HandDoubled doubled:
                    return $"double to {doubled.NewWager} (box {doubled.BoxIndex + 1})";
                case HandSplit split:
                    return $"split (box {split.BoxIndex + 1})";
                case InsuranceOffered _:
                    return "insurance offered";
                case InsuranceTaken taken:
                    return $"insurance taken: {taken.Amount}";
                case InsuranceDeclined _:
                    return "insurance declined";
                case InsuranceSettled insurance:
                    return $"insurance {(insurance.Delta >= 0 ? "+" : string.Empty)}{insurance.Delta}";
                case DealerRevealed revealed:
                    return $"dealer reveals {Short(revealed.HoleCard)}";
                case HandSettled settled:
                    return DescribeSettlement(settled.Settlement);
                case RoundSettled round:
                    return $"=== round net {(round.TotalDelta >= 0 ? "+" : string.Empty)}{round.TotalDelta} ===";
                case RoundAbandoned abandoned:
                    return $"round abandoned, {abandoned.Refunded} refunded";
                default:
                    return null;
            }
        }

        private static string DescribeCard(CardDealt dealt)
        {
            string who = dealt.BoxIndex == CardDealt.DealerBoxIndex
                ? "dealer"
                : $"box {dealt.BoxIndex + 1}";

            return dealt.FaceUp
                ? $"{who}: {Short(dealt.Card)}"
                : $"{who}: [face down]";
        }

        private static string DescribeSettlement(Settlement settlement)
        {
            string sign = settlement.Delta >= 0 ? "+" : string.Empty;
            return $"box {settlement.BoxIndex + 1}: {settlement.Outcome} {sign}{settlement.Delta}";
        }

        private static string Short(Card card)
        {
            string rank;
            switch (card.Rank)
            {
                case Rank.Ace: rank = "A"; break;
                case Rank.King: rank = "K"; break;
                case Rank.Queen: rank = "Q"; break;
                case Rank.Jack: rank = "J"; break;
                case Rank.Ten: rank = "10"; break;
                default: rank = ((int)card.Rank).ToString(); break;
            }

            string suit;
            switch (card.Suit)
            {
                case Suit.Clubs: suit = "\u2663"; break;
                case Suit.Diamonds: suit = "\u2666"; break;
                case Suit.Hearts: suit = "\u2665"; break;
                default: suit = "\u2660"; break;
            }

            return rank + suit;
        }
    }
}
```

`switch` on type patterns is C# 7+ and safe here. Note `Describe` returns `null` for unknown events and `Present` then yields nothing — a new engine event will simply not appear in the log rather than crashing the game.

- [ ] **Step 2: Write the wallet readout**

`Assets/HouseRules/Blackjack/Presentation/Views/WalletView.cs`:

```csharp
using HouseRules.Blackjack;
using UnityEngine;
using UnityEngine.UI;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>Displays the chip balance. Polls, because the wallet raises no events.</summary>
    public sealed class WalletView : MonoBehaviour
    {
        private Wallet _wallet;
        private Text _target;
        private long _lastShown = long.MinValue;

        public void Bind(Wallet wallet, Text target)
        {
            _wallet = wallet;
            _target = target;
            _lastShown = long.MinValue;
        }

        private void Update()
        {
            if (_wallet == null || _target == null)
            {
                return;
            }

            if (_wallet.Balance == _lastShown)
            {
                return;
            }

            _lastShown = _wallet.Balance;
            _target.text = $"Chips: {_wallet.Balance}";
        }
    }
}
```

- [ ] **Step 3: Write the action bar**

`Assets/HouseRules/Blackjack/Presentation/Views/ActionBarView.cs`:

```csharp
using System;
using System.Collections.Generic;
using HouseRules.Blackjack;
using UnityEngine;
using UnityEngine.UI;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Renders one button per player action and enables them strictly from
    /// session.LegalActions. It holds ZERO rules of its own — if an action is not
    /// in that list the button is not interactable, which is what makes a
    /// double-tap during a deal animation impossible.
    /// </summary>
    public sealed class ActionBarView : MonoBehaviour
    {
        private readonly Dictionary<PlayerAction, Button> _buttons = new Dictionary<PlayerAction, Button>();
        private BlackjackSession _session;
        private Button _dealButton;
        private Button _betButton;

        public void Bind(BlackjackSession session) => _session = session;

        public void Register(PlayerAction action, Button button)
        {
            _buttons[action] = button;
            PlayerAction captured = action;
            button.onClick.AddListener(() => _session?.Apply(captured));
        }

        public void RegisterDeal(Button button, Action onDeal)
        {
            _dealButton = button;
            button.onClick.AddListener(() => onDeal?.Invoke());
        }

        public void RegisterBet(Button button, Action onBet)
        {
            _betButton = button;
            button.onClick.AddListener(() => onBet?.Invoke());
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            IReadOnlyList<PlayerAction> legal = _session.LegalActions;

            foreach (KeyValuePair<PlayerAction, Button> pair in _buttons)
            {
                pair.Value.interactable = Contains(legal, pair.Key);
            }

            bool betting = _session.CanAcceptInput && _session.State == RoundState.Betting;
            bool anyBet = betting && AnyBoxActive();

            if (_betButton != null)
            {
                _betButton.interactable = betting;
            }

            if (_dealButton != null)
            {
                _dealButton.interactable = anyBet;
            }
        }

        private bool AnyBoxActive()
        {
            Round round = _session.CurrentRound;
            if (round == null)
            {
                return false;
            }

            foreach (Box box in round.Boxes)
            {
                if (box.IsActive)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(IReadOnlyList<PlayerAction> list, PlayerAction action)
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
    }
}
```

- [ ] **Step 4: Write the bootstrap**

`Assets/HouseRules/Blackjack/Presentation/Bootstrap/BlackjackBootstrap.cs`:

```csharp
using HouseRules.Blackjack;
using UnityEngine;
using UnityEngine.UI;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Builds the entire playable rig in code — session, sequencer, presenter, and a
    /// uGUI layer — so the scene holds exactly one GameObject with one component.
    /// Hand-authored .unity YAML is the most fragile artifact in a Unity repo; this
    /// keeps the whole slice reviewable as C#.
    /// </summary>
    public sealed class BlackjackBootstrap : MonoBehaviour
    {
        [SerializeField] private long _betSize = 10;
        [SerializeField] private int _shoeSeed = 20260813;

        private BlackjackSession _session;
        private EventSequencer _sequencer;
        private TextEventPresenter _presenter;
        private ActionBarView _actionBar;
        private WalletView _walletView;
        private WalletStore _store;
        private Text _log;
        private Text _status;

        private void Start()
        {
            _store = new WalletStore(WalletStore.DefaultPath);
            Wallet wallet = _store.Load();

            _sequencer = gameObject.AddComponent<EventSequencer>();
            _session = gameObject.AddComponent<BlackjackSession>();
            _presenter = gameObject.AddComponent<TextEventPresenter>();
            _actionBar = gameObject.AddComponent<ActionBarView>();
            _walletView = gameObject.AddComponent<WalletView>();

            _sequencer.SetPresenter(_presenter);

            var shoe = new Shoe(
                BlackjackRules.Standard.DeckCount,
                BlackjackRules.Standard.Penetration,
                new SeededRandom(_shoeSeed));

            _session.Configure(BlackjackRules.Standard, shoe, wallet, _sequencer);
            _session.RoundCompleted += OnRoundCompleted;

            BuildUi(wallet);

            _actionBar.Bind(_session);
            _walletView.Bind(wallet, _status);
            _presenter.SetTarget(_log);

            _session.BeginRound();
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.RoundCompleted -= OnRoundCompleted;

                if (_store != null && _session.Wallet != null)
                {
                    _store.Save(_session.Wallet);
                }
            }
        }

        private void OnRoundCompleted()
        {
            // Persist only between rounds: a round is atomic and is never saved mid-play.
            _store.Save(_session.Wallet);
            _session.BeginRound();
            _presenter.Clear();
        }

        private void BuildUi(Wallet wallet)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            EnsureEventSystem();

            _status = CreateText(canvasGo.transform, "Status", new Vector2(0f, 1f), new Vector2(20f, -20f),
                new Vector2(420f, 44f), TextAnchor.UpperLeft, 30);

            _log = CreateText(canvasGo.transform, "Log", new Vector2(0f, 1f), new Vector2(20f, -80f),
                new Vector2(560f, 460f), TextAnchor.UpperLeft, 22);

            _status.text = $"Chips: {wallet.Balance}";

            float x = 20f;
            const float ButtonWidth = 132f;
            const float Gap = 8f;

            _actionBar.RegisterBet(
                CreateButton(canvasGo.transform, $"Bet {_betSize}", new Vector2(x, 24f), ButtonWidth),
                () => _session.PlaceBet(FirstFreeBox(), _betSize));
            x += ButtonWidth + Gap;

            _actionBar.RegisterDeal(
                CreateButton(canvasGo.transform, "Deal", new Vector2(x, 24f), ButtonWidth),
                () => _session.Deal());
            x += ButtonWidth + Gap;

            RegisterAction(canvasGo.transform, PlayerAction.Hit, "Hit", ref x, ButtonWidth, Gap);
            RegisterAction(canvasGo.transform, PlayerAction.Stand, "Stand", ref x, ButtonWidth, Gap);
            RegisterAction(canvasGo.transform, PlayerAction.Double, "Double", ref x, ButtonWidth, Gap);
            RegisterAction(canvasGo.transform, PlayerAction.Split, "Split", ref x, ButtonWidth, Gap);
            RegisterAction(canvasGo.transform, PlayerAction.TakeInsurance, "Insure", ref x, ButtonWidth, Gap);
            RegisterAction(canvasGo.transform, PlayerAction.DeclineInsurance, "No Ins.", ref x, ButtonWidth, Gap);
        }

        private void RegisterAction(Transform parent, PlayerAction action, string label, ref float x, float width, float gap)
        {
            _actionBar.Register(action, CreateButton(parent, label, new Vector2(x, 24f), width));
            x += width + gap;
        }

        private int FirstFreeBox()
        {
            Round round = _session.CurrentRound;
            if (round == null)
            {
                return 0;
            }

            for (int i = 0; i < round.Boxes.Count; i++)
            {
                if (!round.Boxes[i].IsActive)
                {
                    return i;
                }
            }

            return 0;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            new GameObject(
                "EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        private static Text CreateText(
            Transform parent, string name, Vector2 anchor, Vector2 offset,
            Vector2 size, TextAnchor alignment, int fontSize)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, float width)
        {
            var go = new GameObject($"Button_{label}", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(width, 56f);

            go.GetComponent<Image>().color = new Color(0.16f, 0.22f, 0.18f, 1f);

            Text text = CreateText(go.transform, "Label", new Vector2(0f, 1f), Vector2.zero,
                new Vector2(width, 56f), TextAnchor.MiddleCenter, 22);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }
    }
}
```

`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` is the Unity 2022+ name for the built-in font — `Arial.ttf` was removed and returns null, which renders invisible text and looks exactly like a broken scene.

- [ ] **Step 5: Compile**

```bash
unity command recompile
```

Poll until `completed`. Fix any errors before continuing. Then confirm no regression:

```bash
unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly --timeout 600
```

Expected: PASS, 139 tests.

- [ ] **Step 6: Create the scene**

```bash
unity command create_scene --path Assets/Scenes/Blackjack.unity
unity command open_scene --path Assets/Scenes/Blackjack.unity
unity command create_gameobject --name Blackjack
unity command attach_script --target Blackjack --type BlackjackBootstrap
unity command save_scene
unity command add_scene_to_build --path Assets/Scenes/Blackjack.unity --enabled true
```

Run `unity command` with no arguments if any flag name differs from the above — the CLI prints each command's exact parameters. Do not guess a flag; look it up.

- [ ] **Step 7: Verify it actually plays**

```bash
unity command editor_play
unity command get_console_logs --severity error --limit 20
```

Expected: **zero errors.** A NullReferenceException in `Start` produces a blank screen that looks identical to a working-but-empty scene, so check the console before believing the picture.

```bash
unity command capture_game_view --width 900 --height 500 --save_path Assets/_shot.png
```

Confirm the capture shows the chip balance, the button row, and the log. Then drive a round through `eval` to prove the buttons are wired to a live session:

```bash
unity command eval --code 'var b = UnityEngine.Object.FindAnyObjectByType<HouseRules.Blackjack.Presentation.BlackjackSession>(); return b == null ? "NO SESSION" : b.State.ToString();'
```

Expected: `Betting`.

```bash
unity command editor_stop
```

Delete the capture and its meta — it is a check, not an asset:

```bash
unity command delete_asset --asset Assets/_shot.png --confirm true
```

- [ ] **Step 8: Commit**

```bash
git add Assets docs
git commit -m "feat: add playable text-mode blackjack scene"
```

---

### Task 2: Card face atlas

**Files:**
- Create: `Assets/HouseRules/Blackjack/Editor/HouseRules.Blackjack.EditorTools.asmdef`
- Create: `Assets/HouseRules/Blackjack/Editor/CardAtlasGenerator.cs`
- Create: `Assets/HouseRules/Blackjack/Presentation/Art/CardFaces.cs`

**Interfaces:**
- Produces: menu item `Tools/HouseRules/Generate Card Atlas`, which writes `Assets/HouseRules/Blackjack/Art/Generated/CardAtlas.png` (a 13×5 grid: 13 ranks × 4 suits plus a back row); and `static class CardFaces` with `Rect UvFor(Card card)`, `Rect UvForBack()`, `const int Columns`, `const int Rows`.

Placeholder art, generated rather than drawn: each cell is a white rounded rectangle with the rank string and suit glyph in red or black. It reads correctly at a glance, costs nothing to regenerate, and is replaced by Blender-authored art later behind the same UV lookup.

- [ ] **Step 1: Create the Editor assembly**

`Assets/HouseRules/Blackjack/Editor/HouseRules.Blackjack.EditorTools.asmdef`:

```json
{
    "name": "HouseRules.Blackjack.EditorTools",
    "rootNamespace": "HouseRules.Blackjack.EditorTools",
    "references": [
        "HouseRules.Blackjack",
        "HouseRules.Blackjack.Presentation"
    ],
    "includePlatforms": [
        "Editor"
    ],
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

- [ ] **Step 2: Write the UV lookup**

`Assets/HouseRules/Blackjack/Presentation/Art/CardFaces.cs`:

```csharp
using HouseRules.Blackjack;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Maps a card to its cell in the generated atlas. The atlas is a 13-column grid:
    /// one column per rank, one row per suit, plus a final row whose first cell is the
    /// card back. Real art can replace the texture without touching this lookup.
    /// </summary>
    public static class CardFaces
    {
        public const int Columns = 13;
        public const int Rows = 5;

        public static Rect UvFor(Card card)
        {
            int column = RankColumn(card.Rank);
            int row = (int)card.Suit;
            return CellUv(column, row);
        }

        public static Rect UvForBack() => CellUv(0, 4);

        private static int RankColumn(Rank rank)
        {
            // Two=2 maps to column 0 … Ace=14 maps to column 12.
            return (int)rank - 2;
        }

        private static Rect CellUv(int column, int row)
        {
            float width = 1f / Columns;
            float height = 1f / Rows;

            // Row 0 sits at the TOP of the texture, so invert for UV space.
            float y = 1f - ((row + 1) * height);
            return new Rect(column * width, y, width, height);
        }
    }
}
```

- [ ] **Step 3: Write the generator**

`Assets/HouseRules/Blackjack/Editor/CardAtlasGenerator.cs`:

```csharp
using System.IO;
using HouseRules.Blackjack;
using HouseRules.Blackjack.Presentation;
using UnityEditor;
using UnityEngine;

namespace HouseRules.Blackjack.EditorTools
{
    /// <summary>
    /// Generates the placeholder card atlas. Procedural rather than hand-drawn so it
    /// costs nothing to regenerate and carries no licensing baggage; Blender-authored
    /// art replaces the texture later behind the same CardFaces UV lookup.
    /// </summary>
    public static class CardAtlasGenerator
    {
        private const int CellWidth = 128;
        private const int CellHeight = 178;
        private const string OutputDirectory = "Assets/HouseRules/Blackjack/Art/Generated";
        private const string OutputPath = OutputDirectory + "/CardAtlas.png";

        [MenuItem("Tools/HouseRules/Generate Card Atlas")]
        public static void Generate()
        {
            int width = CardFaces.Columns * CellWidth;
            int height = CardFaces.Rows * CellHeight;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(0, 0, 0, 0);
            }

            texture.SetPixels32(pixels);

            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < CardFaces.Columns; column++)
                {
                    DrawCell(texture, column, row, new Color32(250, 250, 248, 255));
                }
            }

            DrawCell(texture, 0, 4, new Color32(38, 62, 104, 255));

            texture.Apply();

            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter();

            Debug.Log($"Card atlas written to {OutputPath}");
        }

        private static void DrawCell(Texture2D texture, int column, int row, Color32 fill)
        {
            int originX = column * CellWidth;
            // Row 0 is the top row, matching CardFaces.CellUv's inversion.
            int originY = texture.height - ((row + 1) * CellHeight);

            for (int y = 0; y < CellHeight; y++)
            {
                for (int x = 0; x < CellWidth; x++)
                {
                    bool border = x < 3 || y < 3 || x >= CellWidth - 3 || y >= CellHeight - 3;
                    Color32 color = border ? new Color32(24, 24, 24, 255) : fill;
                    texture.SetPixel(originX + x, originY + y, color);
                }
            }
        }

        private static void ConfigureImporter()
        {
            var importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
```

The rank glyphs are deliberately not drawn here — `Texture2D` has no text rasterizer, and pulling in a font renderer for placeholder art is not earned. Cells are distinguishable by position; Task 3 overlays the rank/suit as a world-space `Text` on the card view, which is both simpler and easier to read.

- [ ] **Step 4: Generate and verify**

```bash
unity command recompile
```

Poll until `completed`, then run the menu item:

```bash
unity command menu --path "Tools/HouseRules/Generate Card Atlas"
```

Run `unity command` with no arguments to confirm the `menu` command's exact flag name before using it.

Verify the asset exists and imported:

```bash
unity command find_assets --query "CardAtlas"
```

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add generated placeholder card atlas"
```

---

### Task 3: Card view and pool

**Files:**
- Create: `Assets/HouseRules/Blackjack/Presentation/Views/CardView.cs`
- Create: `Assets/HouseRules/Blackjack/Presentation/Views/CardPool.cs`

**Interfaces:**
- Consumes: `CardFaces`, `Tween`, `Card`.
- Produces: `sealed class CardView : MonoBehaviour` with `void Show(Card card, bool faceUp)`, `IEnumerator Flip(float duration)`, `Card Card { get; }`, `bool IsFaceUp { get; }`, and `static CardView Create(Material faceMaterial, Material backMaterial)`; `sealed class CardPool : MonoBehaviour` with `CardView Rent()`, `void Return(CardView view)`, `void ReturnAll()`.

A card is a thin box primitive with a face `Text` on one side. Pool size stays small — a 312-card shoe never puts more than a handful on the felt.

- [ ] **Step 1: Write the card view**

`Assets/HouseRules/Blackjack/Presentation/Views/CardView.cs`:

```csharp
using System.Collections;
using HouseRules.Blackjack;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// One card on the felt: a thin box with its rank and suit rendered on the face.
    /// Pooled — a 312-card shoe never puts more than a handful in play at once.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        public static readonly Vector3 CardSize = new Vector3(0.63f, 0.02f, 0.88f);

        private TextMesh _faceText;
        private Renderer _renderer;

        public Card Card { get; private set; }

        public bool IsFaceUp { get; private set; }

        public static CardView Create()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Card";
            root.transform.localScale = CardSize;

            Object.Destroy(root.GetComponent<BoxCollider>());

            var view = root.AddComponent<CardView>();
            view._renderer = root.GetComponent<Renderer>();

            var textGo = new GameObject("Face");
            textGo.transform.SetParent(root.transform, false);
            // Lift slightly above the top face and lie flat, readable from a top-down camera.
            textGo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            textGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            textGo.transform.localScale = new Vector3(0.12f, 3.8f, 0.09f);

            var text = textGo.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.5f;
            view._faceText = text;

            return view;
        }

        public void Show(Card card, bool faceUp)
        {
            Card = card;
            SetFaceUp(faceUp);
        }

        public void SetFaceUp(bool faceUp)
        {
            IsFaceUp = faceUp;

            if (_faceText != null)
            {
                _faceText.gameObject.SetActive(faceUp);
                _faceText.text = Label(Card);
                _faceText.color = IsRed(Card.Suit) ? new Color(0.75f, 0.12f, 0.12f) : Color.black;
            }

            if (_renderer != null)
            {
                _renderer.material.color = faceUp
                    ? new Color(0.97f, 0.97f, 0.95f)
                    : new Color(0.15f, 0.24f, 0.41f);
            }
        }

        /// <summary>Rotates 180 degrees about the long axis, swapping the face at the midpoint.</summary>
        public IEnumerator Flip(float duration)
        {
            Quaternion from = transform.rotation;
            Quaternion to = from * Quaternion.Euler(0f, 0f, 180f);

            float elapsed = 0f;
            bool swapped = false;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Easing.Clamp01(elapsed / duration);
                transform.rotation = Quaternion.SlerpUnclamped(from, to, Easing.InOutCubic(t));

                if (!swapped && t >= 0.5f)
                {
                    swapped = true;
                    SetFaceUp(!IsFaceUp);
                }

                yield return null;
            }

            transform.rotation = to;
        }

        private static bool IsRed(Suit suit) => suit == Suit.Diamonds || suit == Suit.Hearts;

        private static string Label(Card card)
        {
            string rank;
            switch (card.Rank)
            {
                case Rank.Ace: rank = "A"; break;
                case Rank.King: rank = "K"; break;
                case Rank.Queen: rank = "Q"; break;
                case Rank.Jack: rank = "J"; break;
                case Rank.Ten: rank = "10"; break;
                default: rank = ((int)card.Rank).ToString(); break;
            }

            string suit;
            switch (card.Suit)
            {
                case Suit.Clubs: suit = "\u2663"; break;
                case Suit.Diamonds: suit = "\u2666"; break;
                case Suit.Hearts: suit = "\u2665"; break;
                default: suit = "\u2660"; break;
            }

            return rank + "\n" + suit;
        }
    }
}
```

- [ ] **Step 2: Write the pool**

`Assets/HouseRules/Blackjack/Presentation/Views/CardPool.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Recycles card views. Instantiating and destroying primitives every deal
    /// produces avoidable GC churn on a phone; a handful of reused objects does not.
    /// </summary>
    public sealed class CardPool : MonoBehaviour
    {
        private readonly Stack<CardView> _idle = new Stack<CardView>();
        private readonly List<CardView> _live = new List<CardView>();

        public int LiveCount => _live.Count;

        public int IdleCount => _idle.Count;

        public CardView Rent()
        {
            CardView view = _idle.Count > 0 ? _idle.Pop() : CardView.Create();
            view.transform.SetParent(transform, false);
            view.gameObject.SetActive(true);
            _live.Add(view);
            return view;
        }

        public void Return(CardView view)
        {
            if (view == null || !_live.Remove(view))
            {
                return;
            }

            view.gameObject.SetActive(false);
            _idle.Push(view);
        }

        public void ReturnAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                CardView view = _live[i];
                view.gameObject.SetActive(false);
                _idle.Push(view);
            }

            _live.Clear();
        }
    }
}
```

- [ ] **Step 3: Compile and commit**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly --timeout 600
```

Expected: PASS, 139 tests.

```bash
git add Assets
git commit -m "feat: add pooled card view"
```

---

### Task 4: Table, box, and hand layout

**Files:**
- Create: `Assets/HouseRules/Blackjack/Presentation/Views/HandView.cs`
- Create: `Assets/HouseRules/Blackjack/Presentation/Views/BoxView.cs`
- Create: `Assets/HouseRules/Blackjack/Presentation/Views/TableView.cs`

**Interfaces:**
- Produces: `HandView` with `Vector3 SlotPosition(int cardIndex)`, `void Add(CardView view)`, `void Clear()`, `int Count { get; }`;
  `BoxView` with `HandView HandAt(int handIndex)`, `void Clear()`;
  `TableView` with `void Build(int boxCount)`, `BoxView BoxAt(int index)`, `HandView DealerHand { get; }`, `Vector3 ShoePosition { get; }`, `void ClearAll()`.

Layout only — no animation, no event handling. Positions are pure functions of index, so the presenter in Task 5 asks where a card goes rather than computing it.

- [ ] **Step 1: Write the hand view**

`Assets/HouseRules/Blackjack/Presentation/Views/HandView.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>Fans cards within one hand. Slot positions are a pure function of index.</summary>
    public sealed class HandView : MonoBehaviour
    {
        private const float FanStepX = 0.22f;
        private const float FanStepZ = -0.06f;
        private const float StackLift = 0.025f;

        private readonly List<CardView> _cards = new List<CardView>();

        public int Count => _cards.Count;

        public IReadOnlyList<CardView> Cards => _cards;

        public Vector3 SlotPosition(int cardIndex)
        {
            return transform.position + new Vector3(
                cardIndex * FanStepX,
                cardIndex * StackLift,
                cardIndex * FanStepZ);
        }

        public void Add(CardView view) => _cards.Add(view);

        public void Remove(CardView view) => _cards.Remove(view);

        public void Clear() => _cards.Clear();
    }
}
```

- [ ] **Step 2: Write the box view**

`Assets/HouseRules/Blackjack/Presentation/Views/BoxView.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// One betting position. Owns up to four hand views, because a box can split
    /// three times — they are created lazily as splits occur.
    /// </summary>
    public sealed class BoxView : MonoBehaviour
    {
        private const float SplitOffsetX = 1.15f;

        private readonly List<HandView> _hands = new List<HandView>();

        public int HandCount => _hands.Count;

        public HandView HandAt(int handIndex)
        {
            while (_hands.Count <= handIndex)
            {
                var go = new GameObject($"Hand{_hands.Count}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(_hands.Count * SplitOffsetX, 0f, 0f);
                _hands.Add(go.AddComponent<HandView>());
            }

            return _hands[handIndex];
        }

        public void Clear()
        {
            foreach (HandView hand in _hands)
            {
                hand.Clear();
            }
        }
    }
}
```

- [ ] **Step 3: Write the table view**

`Assets/HouseRules/Blackjack/Presentation/Views/TableView.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>Scene root: felt, box anchors, the dealer's hand, and the shoe position.</summary>
    public sealed class TableView : MonoBehaviour
    {
        private const float BoxSpacingX = 2.6f;
        private const float PlayerRowZ = -1.9f;
        private const float DealerRowZ = 1.7f;

        private readonly List<BoxView> _boxes = new List<BoxView>();

        public HandView DealerHand { get; private set; }

        public Vector3 ShoePosition { get; private set; }

        public int BoxCount => _boxes.Count;

        public void Build(int boxCount)
        {
            CreateFelt();

            float firstX = -((boxCount - 1) * BoxSpacingX) / 2f;

            for (int i = 0; i < boxCount; i++)
            {
                var go = new GameObject($"Box{i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(firstX + (i * BoxSpacingX), 0f, PlayerRowZ);
                _boxes.Add(go.AddComponent<BoxView>());
            }

            var dealerGo = new GameObject("DealerHand");
            dealerGo.transform.SetParent(transform, false);
            dealerGo.transform.localPosition = new Vector3(-0.6f, 0f, DealerRowZ);
            DealerHand = dealerGo.AddComponent<HandView>();

            ShoePosition = transform.position + new Vector3(4.2f, 0.3f, DealerRowZ + 0.6f);
        }

        public BoxView BoxAt(int index) => _boxes[index];

        public void ClearAll()
        {
            foreach (BoxView box in _boxes)
            {
                box.Clear();
            }

            DealerHand.Clear();
        }

        private void CreateFelt()
        {
            var felt = GameObject.CreatePrimitive(PrimitiveType.Plane);
            felt.name = "Felt";
            felt.transform.SetParent(transform, false);
            felt.transform.localScale = new Vector3(1.6f, 1f, 1.1f);
            Object.Destroy(felt.GetComponent<MeshCollider>());
            felt.GetComponent<Renderer>().material.color = new Color(0.09f, 0.32f, 0.19f);
        }
    }
}
```

- [ ] **Step 4: Compile and commit**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly --timeout 600
```

Expected: PASS, 139 tests.

```bash
git add Assets
git commit -m "feat: add table, box, and hand layout views"
```

---

### Task 5: The card presenter

**Files:**
- Create: `Assets/HouseRules/Blackjack/Presentation/Views/TableCardPresenter.cs`
- Modify: `Assets/HouseRules/Blackjack/Presentation/Bootstrap/BlackjackBootstrap.cs`

**Interfaces:**
- Produces: `sealed class TableCardPresenter : MonoBehaviour, IEventPresenter` with `void Bind(TableView table, CardPool pool)`.

This replaces `TextEventPresenter` as the sequencer's presenter. Keep the text log on screen — having both is genuinely useful while the visuals settle.

- [ ] **Step 1: Write the presenter**

`Assets/HouseRules/Blackjack/Presentation/Views/TableCardPresenter.cs`:

```csharp
using System.Collections;
using HouseRules.Blackjack;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Turns the engine's event stream into card motion. Every method here performs
    /// something the engine already decided — no rule is evaluated in this file.
    /// </summary>
    public sealed class TableCardPresenter : MonoBehaviour, IEventPresenter
    {
        private const float DealDuration = 0.26f;
        private const float FlipDuration = 0.22f;
        private const float BeatDuration = 0.35f;

        private TableView _table;
        private CardPool _pool;

        public void Bind(TableView table, CardPool pool)
        {
            _table = table;
            _pool = pool;
        }

        public IEnumerator Present(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case RoundStarted _:
                    _table.ClearAll();
                    _pool.ReturnAll();
                    yield break;

                case CardDealt dealt:
                    yield return DealCard(dealt);
                    yield break;

                case DealerRevealed _:
                    yield return RevealHoleCard();
                    yield break;

                case HandSplit split:
                    yield return MoveSplitCard(split);
                    yield break;

                case RoundSettled _:
                    yield return Tween.Wait(BeatDuration * 2f);
                    yield break;

                case HandBusted _:
                case HandSettled _:
                    yield return Tween.Wait(BeatDuration);
                    yield break;

                default:
                    yield break;
            }
        }

        private HandView HandFor(int boxIndex, int handIndex)
        {
            return boxIndex == CardDealt.DealerBoxIndex
                ? _table.DealerHand
                : _table.BoxAt(boxIndex).HandAt(handIndex);
        }

        private IEnumerator DealCard(CardDealt dealt)
        {
            HandView hand = HandFor(dealt.BoxIndex, dealt.HandIndex);

            CardView view = _pool.Rent();
            view.transform.position = _table.ShoePosition;
            view.transform.rotation = Quaternion.identity;
            view.Show(dealt.Card, faceUp: false);

            Vector3 destination = hand.SlotPosition(hand.Count);
            hand.Add(view);

            yield return Tween.Move(view.transform, destination, DealDuration, Easing.OutCubic);

            if (dealt.FaceUp)
            {
                yield return view.Flip(FlipDuration);
            }
        }

        private IEnumerator RevealHoleCard()
        {
            HandView dealer = _table.DealerHand;

            foreach (CardView view in dealer.Cards)
            {
                if (!view.IsFaceUp)
                {
                    yield return view.Flip(FlipDuration);
                    yield break;
                }
            }
        }

        private IEnumerator MoveSplitCard(HandSplit split)
        {
            BoxView box = _table.BoxAt(split.BoxIndex);
            HandView source = box.HandAt(split.HandIndex);
            HandView target = box.HandAt(split.NewHandIndex);

            if (source.Count < 2)
            {
                yield break;
            }

            CardView moved = source.Cards[source.Count - 1];
            source.Remove(moved);
            target.Add(moved);

            yield return Tween.Move(
                moved.transform, target.SlotPosition(0), DealDuration, Easing.OutCubic);
        }
    }
}
```

- [ ] **Step 2: Wire it into the bootstrap**

In `BlackjackBootstrap.Start()`, after creating the sequencer, build the table and use the card presenter instead of the text one as the sequencer's presenter — keeping `TextEventPresenter` alive and updating the log:

```csharp
            var tableGo = new GameObject("Table");
            tableGo.transform.SetParent(transform, false);
            var table = tableGo.AddComponent<TableView>();
            table.Build(BlackjackRules.Standard.MaxBoxes);

            var poolGo = new GameObject("CardPool");
            poolGo.transform.SetParent(transform, false);
            var pool = poolGo.AddComponent<CardPool>();

            var cardPresenter = gameObject.AddComponent<TableCardPresenter>();
            cardPresenter.Bind(table, pool);
            _sequencer.SetPresenter(cardPresenter);
```

Keep the text log alive alongside the cards — having both is genuinely useful while the visuals settle. `TextEventPresenter` is no longer the sequencer's presenter, so give it a non-yielding entry point and call it from the card presenter.

In `TextEventPresenter`, add:

```csharp
        /// <summary>Log an event without consuming sequencer time. Used when another presenter drives playback.</summary>
        public void Log(GameEvent gameEvent)
        {
            string line = Describe(gameEvent);
            if (!string.IsNullOrEmpty(line))
            {
                Append(line);
            }
        }
```

In `TableCardPresenter`, add a `TextEventPresenter _log` field, extend `Bind` to `Bind(TableView table, CardPool pool, TextEventPresenter log)`, and make `Present` call `_log?.Log(gameEvent);` as its first statement before the switch. Do not drain the text presenter's `Present` enumerator — that would double the pacing delay on every event.

Also add a camera positioned to see the felt:

```csharp
            Camera camera = Camera.main;
            if (camera == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                camera = camGo.GetComponent<Camera>();
            }

            camera.transform.position = new Vector3(0f, 7.5f, -4.6f);
            camera.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.06f);
```

- [ ] **Step 3: Verify visually**

```bash
unity command recompile
```

Poll until `completed`, then:

```bash
unity command editor_play
unity command get_console_logs --severity error --limit 20
```

Expected: zero errors.

Place a bet and deal through `eval`, then capture:

```bash
unity command eval --code 'var s = UnityEngine.Object.FindAnyObjectByType<HouseRules.Blackjack.Presentation.BlackjackSession>(); s.PlaceBet(0, 10); s.Deal(); return s.State.ToString();'
```

Wait a moment for playback, then:

```bash
unity command capture_game_view --width 1000 --height 560 --save_path Assets/_shot.png
```

Confirm cards are visible on the felt, the dealer's hole card is face down, and the player's cards are face up. Then:

```bash
unity command editor_stop
unity command delete_asset --asset Assets/_shot.png --confirm true
```

- [ ] **Step 4: Commit**

```bash
git add Assets
git commit -m "feat: add card presenter driving table views"
```

---

### Task 6: Android build target and device verification

**Files:** none created — this is configuration and verification.

Spec §11 requires the game playable on an Android device at 60fps. The project's active target is currently `StandaloneWindows64`; the Android module is installed.

- [ ] **Step 1: Inspect current build settings**

```bash
unity command get_build_settings
unity command list_build_targets
```

Confirm `Android` shows `isInstalled: true`.

- [ ] **Step 2: Dry-run the target switch**

```bash
unity command switch_build_target --target Android --dry_run true
```

Show the output before confirming. Switching triggers a full reimport of every texture for the new platform and can take several minutes.

- [ ] **Step 3: Switch**

```bash
unity command switch_build_target --target Android --confirm true
```

Poll until complete:

```bash
unity command switch_build_target_status
```

- [ ] **Step 4: Verify the suites still pass on the new target**

```bash
unity command run_tests --mode editor --filter HouseRules.Blackjack --filter_type assembly --timeout 600
unity command run_tests --mode playmode --async_tests true --timeout 600
```

Then poll `unity command test_status`. Expected: 139 EditMode, 17 PlayMode.

- [ ] **Step 5: Build**

```bash
unity command build --dry_run true
```

Show the output. Then, only with explicit approval from the human — a device build is slow and writes outside the project:

```bash
unity command build --confirm true
```

- [ ] **Step 6: Report device verification honestly**

Installing and running the APK on a physical phone cannot be automated from here. Report the build artifact's path and state plainly that on-device 60fps verification requires the human to install and run it. **Do not claim the definition of done is met on the basis of a successful build.**

- [ ] **Step 7: Commit**

```bash
git add ProjectSettings
git commit -m "chore: switch build target to Android"
```

---

## Completion Criteria

- `Assets/Scenes/Blackjack.unity` opens, enters play mode with zero console errors, and is playable: bet, deal, hit, stand, double, split, insurance all work through the UI.
- Buttons are enabled strictly from `session.LegalActions`, so no input is possible during playback.
- Cards deal from the shoe, land in fanned positions, and flip face-up; the dealer's hole card stays down until `DealerRevealed`.
- The chip balance persists across play sessions.
- 139 EditMode and 17 PlayMode tests still pass.
- Build target is Android and a build completes.

## Explicitly Out Of Scope

- Blender-authored card, chip, and table models (the atlas and primitives are placeholders behind a stable interface)
- Sound
- Chip-stack visuals and bet-placement animation
- Hebrew/RTL, localisation beyond English strings
- The other four games
- Leaderboards, IAP, ads, online multiplayer
