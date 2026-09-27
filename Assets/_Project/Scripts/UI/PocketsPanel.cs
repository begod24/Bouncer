using System;
using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Run;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Bouncer.UI
{
    /// <summary>
    /// Карманы игрока: вкладыши, которые сейчас действуют (<see cref="PlayerCards.PocketCards"/>). Два случая:
    /// карманы полны, а игрок выбрал новую карточку — «что выкинуть?» (новую можно не брать); в ларьке — продажа
    /// вкладыша за полцены. Карточки — копии вкладыша с экрана выбора, уменьшенные; экран собирается сам при запуске
    /// из шаблонов. Выбор — мышью, стрелками, геймпадом или клавишами 1–7; Esc / B закрывает продажу.
    /// </summary>
    public sealed class PocketsPanel : MonoBehaviour
    {
        public enum Mode
        {
            Discard,
            Sell,
        }

        [SerializeField] CanvasGroup group;
        [Tooltip("Вкладыш с экрана выбора — по нему делаются карточки карманов")]
        [SerializeField] UpgradeCardView cardTemplate;
        [Tooltip("Заголовок экрана выбора — шрифт и размер заголовка")]
        [SerializeField] TMP_Text titleTemplate;
        [Tooltip("Мелкая подпись (цена, «выкинуть»)")]
        [SerializeField] TMP_Text labelTemplate;
        [Tooltip("Затемнение фона")]
        [SerializeField] UnityEngine.UI.Image dimTemplate;
        [SerializeField] float cardScale = 0.56f;
        [SerializeField] float spacing = 236f;
        [SerializeField] float inputDelay = 0.35f;
        [SerializeField] Color newCardColor = new(1f, 0.85f, 0.35f);
        [SerializeField] Color labelColor = new(0.96f, 0.95f, 0.92f);

        static PocketsPanel s_instance;

        readonly List<UpgradeCardView> _views = new();
        readonly List<TMP_Text> _labels = new();
        readonly List<UpgradeCard> _shown = new();
        RectTransform _row;
        TMP_Text _title;
        TMP_Text _hint;
        PlayerCards _cards;
        ShopStock _stock;
        Mode _mode;
        bool _open;
        float _openedAt;

        public static PocketsPanel Instance => s_instance;
        public bool IsOpen => _open;
        public Mode CurrentMode => _mode;

        /// <summary>Продажа закрыта — ларёк снова принимает ввод.</summary>
        public event Action Closed;

        void Awake()
        {
            s_instance = this;
            Build();
            SetVisible(false);
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
            if (_cards != null)
                _cards.DiscardChanged -= OnDiscardChanged;
        }

        void Start()
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.TryGetComponent(out _cards))
                _cards.DiscardChanged += OnDiscardChanged;
        }

        void Build()
        {
            var root = (RectTransform)transform;
            if (dimTemplate)
            {
                var dim = Instantiate(dimTemplate, root);
                dim.name = "Dim";
                Stretch(dim.rectTransform);
            }
            _row = new GameObject("Cards", typeof(RectTransform)).GetComponent<RectTransform>();
            _row.SetParent(root, false);
            _row.anchoredPosition = new Vector2(0f, -20f);
            if (titleTemplate)
            {
                _title = Instantiate(titleTemplate, root);
                _title.name = "Title";
                StripLocalize(_title);
                var rect = _title.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -150f);
            }
            if (labelTemplate)
            {
                _hint = Instantiate(labelTemplate, root);
                _hint.name = "Hint";
                StripLocalize(_hint);
                var rect = _hint.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(1700f, 60f);
                rect.anchoredPosition = new Vector2(0f, 110f);
                _hint.alignment = TextAlignmentOptions.Center;
            }
        }

        void OnDiscardChanged()
        {
            if (_cards != null && _cards.IsDiscarding)
                Open(Mode.Discard);
            else if (_open && _mode == Mode.Discard)
                Close();
        }

        /// <summary>Ларёк: продать вкладыш из кармана.</summary>
        public void OpenSell(ShopStock stock)
        {
            _stock = stock;
            if (_cards == null && stock != null)
                _cards = stock.Customer;
            Open(Mode.Sell);
        }

        void Open(Mode mode)
        {
            if (_cards == null)
                return;
            _mode = mode;
            _open = true;
            _openedAt = Time.unscaledTime;
            Refresh();
            SetVisible(true);
            SelectFirst();
        }

        public void Close()
        {
            if (!_open)
                return;
            _open = false;
            SetVisible(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            Closed?.Invoke();
        }

        void Refresh()
        {
            _shown.Clear();
            _shown.AddRange(_cards.PocketCards);
            bool discard = _mode == Mode.Discard;
            var pending = _cards.PendingCard;
            if (discard && pending)
                _shown.Add(pending);

            while (_views.Count < _shown.Count)
                AddView();
            float start = -(_shown.Count - 1) * 0.5f * spacing;
            for (int i = 0; i < _views.Count; i++)
            {
                bool visible = i < _shown.Count;
                _views[i].gameObject.SetActive(visible);
                _labels[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var card = _shown[i];
                var rect = (RectTransform)_views[i].transform;
                rect.anchoredPosition = new Vector2(start + i * spacing, 20f);
                _views[i].Show(card, Mathf.Max(0, _cards.StacksOf(card) - 1), i + 1, i * 0.04f);
                var label = _labels[i];
                ((RectTransform)label.transform).anchoredPosition = new Vector2(start + i * spacing, -190f);
                bool isNew = discard && card == pending;
                label.color = isNew ? newCardColor : labelColor;
                label.text = discard
                    ? Loc.Get(isNew ? "pockets.skip" : "pockets.discard")
                    : Loc.Format("pockets.sell.price", _stock != null ? _stock.SellPriceOf(card) : 0);
            }

            if (_title)
                _title.text = discard
                    ? Loc.Get("pockets.full")
                    : Loc.Format("pockets.title", _cards.PocketsUsed, _cards.MaxPockets);
            if (_hint)
                _hint.text = Loc.Get(discard ? "pockets.hint.discard" : "pockets.hint.sell");
        }

        void AddView()
        {
            var view = Instantiate(cardTemplate, _row);
            view.name = $"Pocket_{_views.Count + 1}";
            var rect = (RectTransform)view.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one * cardScale;
            view.Clicked += OnCardClicked;
            _views.Add(view);

            var label = Instantiate(labelTemplate, _row);
            StripLocalize(label);
            label.name = $"PocketLabel_{_labels.Count + 1}";
            var labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(spacing - 8f, 50f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            _labels.Add(label);
        }

        void OnCardClicked(UpgradeCardView view)
        {
            if (!_open || Time.unscaledTime - _openedAt < inputDelay)
                return;
            Pick(_views.IndexOf(view));
        }

        void Pick(int index)
        {
            if (index < 0 || index >= _shown.Count)
                return;
            var card = _shown[index];
            if (_mode == Mode.Discard)
            {
                // Выбрали новую — не брать её; выбрали из кармана — выкинуть и взять новую.
                _cards.ResolveDiscard(card == _cards.PendingCard ? null : card);
                return;
            }
            if (_stock == null)
                return;
            _stock.Sell(card);
            if (_cards.PocketsUsed == 0)
            {
                Close();
                return;
            }
            Refresh();
            SelectFirst();
        }

        void Update()
        {
            if (!_open)
                return;
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null && NavigationPressed())
                SelectFirst();
            if (Time.unscaledTime - _openedAt < inputDelay)
                return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
                for (int i = 0; i < 7; i++)
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                        Pick(i);
        }

        void LateUpdate()
        {
            if (_open && _mode == Mode.Sell && CancelPressed())
                Close();
        }

        void SelectFirst()
        {
            if (EventSystem.current == null)
                return;
            foreach (var view in _views)
            {
                if (view.gameObject.activeInHierarchy)
                {
                    EventSystem.current.SetSelectedGameObject(view.gameObject);
                    return;
                }
            }
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Копия подписи из префаба не должна сама переписывать свой текст (LocalizeStringEvent).</summary>
        static void StripLocalize(TMP_Text text)
        {
            foreach (var behaviour in text.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour != text && behaviour.GetType().Name is "LocalizeStringEvent" or "HintText")
                    Destroy(behaviour);
        }

        static bool CancelPressed()
        {
            var module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
            var cancel = module != null && module.cancel != null ? module.cancel.action : null;
            return cancel != null && cancel.WasPressedThisFrame();
        }

        static bool NavigationPressed()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
                                         || keyboard.aKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame))
                   || (gamepad != null && (gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame
                                           || gamepad.leftStick.left.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame));
        }
    }
}
