using HouseRules.Blackjack;
using UnityEngine;
using UnityEngine.InputSystem.UI;
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
                typeof(InputSystemUIInputModule));
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
            text.text = label;
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
