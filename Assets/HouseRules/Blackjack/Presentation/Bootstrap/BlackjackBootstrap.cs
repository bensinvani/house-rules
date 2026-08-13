using HouseRules.Blackjack;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Builds the entire playable rig in code — session, sequencer, table, presenter, and a
    /// uGUI HUD — so the scene holds exactly one GameObject with one component. Hand-authored
    /// .unity YAML is the most fragile artifact in a Unity repo; this keeps the whole slice
    /// reviewable as C#.
    /// </summary>
    public sealed class BlackjackBootstrap : MonoBehaviour
    {
        // Visual Quality Bar type scale (docs/superpowers/plans/2026-08-13-blackjack-visuals.md):
        // 34 / 24 / 18 / 14 only. 24 is reserved for a size this HUD doesn't currently need.
        private const int BalanceFontSize = 34;
        private const int ButtonFontSize = 18;
        private const int LogFontSize = 14;

        private const float ButtonMinWidth = 110f;
        private const float ButtonHeight = 56f;

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

            var tableGo = new GameObject("Table");
            tableGo.transform.SetParent(transform, false);
            var table = tableGo.AddComponent<TableView>();
            table.Build(BlackjackRules.Standard.MaxBoxes);

            var poolGo = new GameObject("CardPool");
            poolGo.transform.SetParent(transform, false);
            var pool = poolGo.AddComponent<CardPool>();

            var cardPresenter = gameObject.AddComponent<TableCardPresenter>();
            cardPresenter.Bind(table, pool, _presenter);
            _sequencer.SetPresenter(cardPresenter);

            SetupCameraAndLighting();

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

        /// <summary>Required fix: a directional light so the felt and cards aren't unlit, and a
        /// SolidColor camera in the Surround token so the void behind the table is never pure black.</summary>
        private void SetupCameraAndLighting()
        {
            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light", typeof(Light));
                var light = lightGo.GetComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.968f, 0.918f); // soft warm white
                light.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                camera = camGo.GetComponent<Camera>();
            }

            // Raised and pulled back from Task 5's original framing so all three boxes
            // (now spaced 4.2 apart, spanning -4.2..+4.2) stay comfortably in frame.
            camera.transform.position = new Vector3(0f, 10.5f, -6.6f);
            camera.transform.rotation = Quaternion.Euler(56f, 0f, 0f);
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Surround;
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

            RectTransform safeArea = CreateSafeArea(canvasGo.transform);
            Sprite roundedSprite = CreateRoundedSprite();

            // --- Balance, top-left, on its own PanelDark backing so it never sits on bare felt. ---
            Image balancePanel = CreatePanel(
                safeArea, roundedSprite, "BalancePanel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -20f), new Vector2(300f, 64f));

            _status = CreateText(
                balancePanel.transform, "Balance", TextAnchor.MiddleLeft, BalanceFontSize, Palette.TextPrimary);
            StretchWithPadding(_status.GetComponent<RectTransform>(), 16f);

            // --- Log, directly below the balance, on its own PanelDark backing. ---
            Image logPanel = CreatePanel(
                safeArea, roundedSprite, "LogPanel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -96f), new Vector2(460f, 200f));

            _log = CreateText(logPanel.transform, "Log", TextAnchor.UpperLeft, LogFontSize, Palette.TextMuted);
            StretchWithPadding(_log.GetComponent<RectTransform>(), 12f);

            // --- Bottom action bar: one PanelDark backing bar, laid out by HorizontalLayoutGroup. ---
            var barGo = new GameObject("ActionBar", typeof(Image), typeof(HorizontalLayoutGroup));
            barGo.transform.SetParent(safeArea, false);

            var barRect = barGo.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 16f);
            barRect.sizeDelta = new Vector2(0f, ButtonHeight + 32f);

            var barImage = barGo.GetComponent<Image>();
            barImage.sprite = roundedSprite;
            barImage.type = Image.Type.Sliced;
            barImage.color = Palette.PanelDark;

            var layout = barGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _actionBar.RegisterBet(
                CreateButton(barGo.transform, roundedSprite, $"Bet {_betSize}"),
                () => _session.PlaceBet(FirstFreeBox(), _betSize));

            _actionBar.RegisterDeal(
                CreateButton(barGo.transform, roundedSprite, "Deal"),
                () => _session.Deal());

            RegisterAction(barGo.transform, roundedSprite, PlayerAction.Hit, "Hit");
            RegisterAction(barGo.transform, roundedSprite, PlayerAction.Stand, "Stand");
            RegisterAction(barGo.transform, roundedSprite, PlayerAction.Double, "Double");
            RegisterAction(barGo.transform, roundedSprite, PlayerAction.Split, "Split");
            RegisterAction(barGo.transform, roundedSprite, PlayerAction.TakeInsurance, "Insure");
            RegisterAction(barGo.transform, roundedSprite, PlayerAction.DeclineInsurance, "No Ins.");
        }

        private void RegisterAction(Transform parent, Sprite roundedSprite, PlayerAction action, string label)
        {
            _actionBar.Register(action, CreateButton(parent, roundedSprite, label));
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

        /// <summary>A container whose rect maps to Screen.safeArea; every HUD element parents under this.</summary>
        private static RectTransform CreateSafeArea(Transform canvasTransform)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform));
            go.transform.SetParent(canvasTransform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Rect safeArea = Screen.safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            if (Screen.width > 0 && Screen.height > 0)
            {
                anchorMin.x /= Screen.width;
                anchorMin.y /= Screen.height;
                anchorMax.x /= Screen.width;
                anchorMax.y /= Screen.height;
            }
            else
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            return rect;
        }

        /// <summary>
        /// Generates one rounded-rect sprite at startup and 9-slices it, so every panel and
        /// button gets real rounded corners without a single imported texture asset.
        /// </summary>
        private static Sprite CreateRoundedSprite()
        {
            const int size = 32;
            const float radius = 10f;
            const float border = 12f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RoundedPanel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte alpha = (byte)Mathf.RoundToInt(RoundedRectCoverage(x, y, size, radius) * 255f);
                    pixels[(y * size) + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        /// <summary>1 inside the rounded rect, 0 outside, anti-aliased across ~1px at the corner arcs.</summary>
        private static float RoundedRectCoverage(int x, int y, int size, float radius)
        {
            float px = x + 0.5f;
            float py = y + 0.5f;

            bool inCornerBandX = px < radius || px > size - radius;
            bool inCornerBandY = py < radius || py > size - radius;

            if (!inCornerBandX || !inCornerBandY)
            {
                return 1f;
            }

            float cx = px < radius ? radius : size - radius;
            float cy = py < radius ? radius : size - radius;
            float dist = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));

            return Mathf.Clamp01(radius - dist + 0.5f);
        }

        private static Image CreatePanel(
            Transform parent, Sprite roundedSprite, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var image = go.GetComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = Palette.PanelDark;
            return image;
        }

        private static void StretchWithPadding(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static Text CreateText(
            Transform parent, string name, TextAnchor alignment, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(Transform parent, Sprite roundedSprite, string label)
        {
            var go = new GameObject($"Button_{label}", typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = Palette.PanelDark;

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.minWidth = ButtonMinWidth;
            layoutElement.preferredWidth = ButtonMinWidth;
            layoutElement.minHeight = ButtonHeight;
            layoutElement.preferredHeight = ButtonHeight;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.normalColor = Palette.PanelDark;
            colors.highlightedColor = Palette.Lift(Palette.PanelDark, 0.15f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Palette.Darken(Palette.PanelDark, 0.10f);
            // A visibly, unmistakably disabled state — not just "slightly dimmer" — plus
            // ActionBarView mutes the label colour from the same interactable flag.
            colors.disabledColor = new Color(Palette.PanelDark.r, Palette.PanelDark.g, Palette.PanelDark.b, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text text = CreateText(go.transform, "Label", TextAnchor.MiddleCenter, ButtonFontSize, Palette.TextPrimary);
            text.text = label;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            StretchWithPadding(text.GetComponent<RectTransform>(), 4f);

            return button;
        }
    }
}
