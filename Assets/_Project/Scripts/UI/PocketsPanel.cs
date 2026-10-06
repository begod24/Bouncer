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
    public sealed class PocketsPanel : MonoBehaviour
    {
        public enum Mode
        {
            View,
            Replace,
            Sell,
        }

        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text hint;

        [Header("Крупные вкладыши")]
        [Tooltip("Что отдаёшь (выкинуть, продать); в режиме «Посмотреть» — выбранный вкладыш по центру")]
        [SerializeField] RectTransform giveSide;
        [SerializeField] TMP_Text giveLabel;
        [SerializeField] UpgradeCardView giveCard;
        [Tooltip("Штамп поверх отмеченной карточки: «ВЫКИНУТЬ» / «ПРОДАТЬ»")]
        [SerializeField] TMP_Text giveStamp;
        [Tooltip("Надпись, пока ничего не отмечено или карманы пусты")]
        [SerializeField] TMP_Text givePlaceholder;
        [Tooltip("Что получаешь: новая карточка или покупка")]
        [SerializeField] RectTransform takeSide;
        [SerializeField] TMP_Text takeLabel;
        [SerializeField] UpgradeCardView takeCard;
        [SerializeField] GameObject arrow;
        [Tooltip("Насколько раздвинуты крупные вкладыши в замене и обмене")]
        [SerializeField] float sideOffset = 330f;

        [Header("Ряд карманов")]
        [SerializeField] PocketCardMini miniPrefab;
        [SerializeField] RectTransform row;
        [SerializeField] float miniSize = 110f;
        [SerializeField] float miniSpacing = 165f;
        [Tooltip("Размер названия под вкладышем на экране")]
        [SerializeField] float nameSize = 26f;
        [Tooltip("Дополнительный отступ перед вкладышами без кармана")]
        [SerializeField] float freeGap = 70f;
        [Tooltip("«без кармана» над вкладышами, которые карман не занимают")]
        [SerializeField] TMP_Text freeCaption;

        [Header("Кнопки")]
        [SerializeField] UnityEngine.UI.Button confirmButton;
        [SerializeField] TMP_Text confirmLabel;
        [SerializeField] UnityEngine.UI.Button backButton;
        [SerializeField] TMP_Text backLabel;
        [SerializeField] UnityEngine.UI.Button skipButton;
        [SerializeField] TMP_Text skipLabel;
        [SerializeField] float buttonSpacing = 540f;

        [Header("Вид")]
        [SerializeField] Color takeColor = new(1f, 0.82f, 0.25f);
        [SerializeField] Color giveColor = new(1f, 0.45f, 0.38f);
        [SerializeField] Color sellColor = new(0.58f, 0.86f, 0.42f);
        [SerializeField] Color labelColor = new(0.96f, 0.95f, 0.92f);
        [SerializeField] Color mutedColor = new(0.96f, 0.95f, 0.92f, 0.5f);
        [Tooltip("Сколько секунд после открытия нажатия не принимаются")]
        [SerializeField] float inputDelay = 0.3f;

        static PocketsPanel s_instance;

        readonly List<PocketCardMini> _minis = new();
        readonly List<UpgradeCard> _pockets = new();
        readonly List<UpgradeCard> _free = new();
        PlayerCards _cards;
        ShopStock _stock;
        int _buySlot = -1;
        Mode _mode;
        bool _open;
        float _openedAt;
        int _openedFrame = -1;
        UpgradeCard _chosen;
        UpgradeCard _previewed;
        bool _previewShown;
        GameObject _returnTo;

        public static PocketsPanel Instance => s_instance;
        public bool IsOpen => _open;
        public Mode CurrentMode => _mode;
        public int ClosedFrame { get; private set; } = -1;

        public event Action Closed;

        bool AcceptsInput => Time.unscaledTime - _openedAt >= inputDelay;

        void Awake()
        {
            s_instance = this;
            Bind(confirmButton, Confirm);
            Bind(backButton, Back);
            Bind(skipButton, Skip);
            foreach (var card in new[] { giveCard, takeCard })
                if (card)
                    card.Button.interactable = false;
            SetVisible(false);
        }

        void Start()
        {
            Players.LocalChanged += Bind;
            Bind(Players.Local);
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
            Players.LocalChanged -= Bind;
            if (_cards != null)
                _cards.DiscardChanged -= OnDiscardChanged;
        }

        void Bind(PlayerController player)
        {
            if (_cards != null)
                _cards.DiscardChanged -= OnDiscardChanged;
            _cards = null;
            if (player != null && player.TryGetComponent(out _cards))
                _cards.DiscardChanged += OnDiscardChanged;
        }

        public void OpenView() => Open(Mode.View);

        public void OpenSell(ShopStock stock, int buySlot = -1)
        {
            _stock = stock;
            _buySlot = stock != null ? buySlot : -1;
            if (_cards == null && stock != null)
                _cards = stock.Customer;
            Open(Mode.Sell);
        }

        void OnDiscardChanged()
        {
            if (_cards != null && _cards.IsDiscarding)
                Open(Mode.Replace);
            else if (_open && _mode == Mode.Replace)
                Close();
        }

        void Open(Mode mode)
        {
            if (_cards == null)
                return;
            if (!_open)
                _returnTo = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _mode = mode;
            _open = true;
            _openedAt = Time.unscaledTime;
            _openedFrame = Time.frameCount;
            _chosen = null;
            _previewShown = false;
            Refresh();
            SetVisible(true);
            SelectFirst();
        }

        public void Close()
        {
            if (!_open)
                return;
            _open = false;
            ClosedFrame = Time.frameCount;
            SetVisible(false);
            var events = EventSystem.current;
            if (events != null)
                events.SetSelectedGameObject(_mode != Mode.Replace && _returnTo != null && _returnTo.activeInHierarchy ? _returnTo : null);
            _returnTo = null;
            _buySlot = -1;
            Closed?.Invoke();
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        void Refresh()
        {
            _pockets.Clear();
            _pockets.AddRange(_cards.PocketCards);
            if (_mode == Mode.View)
                _cards.GetFreeCards(_free);
            else
                _free.Clear();
            if (_chosen != null && !_pockets.Contains(_chosen))
                _chosen = null;

            BuildRow();
            RefreshTexts();
            _previewShown = false;
            RefreshPreview();
        }

        void BuildRow()
        {
            int slots = _mode == Mode.Replace ? _pockets.Count : Mathf.Max(_cards.MaxPockets, _pockets.Count);
            int count = slots + _free.Count;
            while (_minis.Count < count)
                _minis.Add(CreateMini());

            float span = Mathf.Max(0, slots - 1) * miniSpacing + (_free.Count > 0 ? freeGap + _free.Count * miniSpacing : 0f);
            float start = -span * 0.5f;
            float scale = miniSize / Mathf.Max(1f, ((RectTransform)miniPrefab.transform).rect.width);
            float freeFrom = 0f, freeTo = 0f;
            for (int i = 0; i < _minis.Count; i++)
            {
                var mini = _minis[i];
                bool visible = i < count;
                mini.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                bool free = i >= slots;
                float x = free ? start + (slots - 1) * miniSpacing + freeGap + (i - slots + 1) * miniSpacing : start + i * miniSpacing;
                var rect = (RectTransform)mini.transform;
                rect.anchoredPosition = new Vector2(x, 0f);
                rect.localScale = Vector3.one * scale;

                if (free)
                {
                    var card = _free[i - slots];
                    mini.Show(card, _cards.StacksOf(card));
                    mini.SetLabel(card.title.GetLocalizedString(), mutedColor);
                    mini.SetInteractable(true);
                    if (i == slots)
                        freeFrom = x;
                    freeTo = x;
                }
                else if (i < _pockets.Count)
                {
                    var card = _pockets[i];
                    mini.Show(card, _cards.StacksOf(card));
                    mini.SetLabel(card.title.GetLocalizedString());
                    mini.SetHotkey(_mode == Mode.View ? 0 : i + 1);
                    mini.SetMarked(card == _chosen);
                    mini.SetInteractable(true);
                }
                else
                {
                    mini.ShowEmpty();
                    mini.SetLabel(Loc.Get("pockets.slot.empty"), mutedColor);
                    mini.SetInteractable(false);
                }
            }

            if (freeCaption)
            {
                freeCaption.gameObject.SetActive(_free.Count > 0);
                freeCaption.text = Loc.Get("pockets.free");
                var rect = freeCaption.rectTransform;
                rect.anchoredPosition = new Vector2((freeFrom + freeTo) * 0.5f, rect.anchoredPosition.y);
            }
        }

        PocketCardMini CreateMini()
        {
            var mini = Instantiate(miniPrefab, row);
            mini.name = $"Pocket_{_minis.Count + 1}";
            var rect = (RectTransform)mini.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            float scale = miniSize / Mathf.Max(1f, rect.rect.width);
            mini.SetLabelStyle(nameSize / scale, (miniSpacing - 12f) / scale, wrap: false);
            mini.Clicked += OnMiniClicked;
            return mini;
        }

        void RefreshTexts()
        {
            bool swap = _mode == Mode.Sell && _buySlot >= 0 && _stock != null && _stock.CanBuy(_buySlot);
            bool pair = _mode == Mode.Replace || swap;
            var buyCard = swap ? _stock.Slots[_buySlot].Card : null;

            switch (_mode)
            {
                case Mode.View:
                    title.text = Loc.Format("pockets.view.title", _pockets.Count, _cards.MaxPockets);
                    subtitle.text = Loc.Get("pockets.view.subtitle");
                    hint.text = Loc.Get("pockets.hint.view");
                    break;
                case Mode.Replace:
                    title.text = Loc.Get("pockets.full");
                    subtitle.text = Loc.Format("pockets.replace.subtitle", _cards.PendingCard.title.GetLocalizedString());
                    hint.text = Loc.Get("pockets.hint.replace");
                    break;
                default:
                    title.text = swap ? Loc.Get("pockets.full") : Loc.Get("pockets.sell.title");
                    subtitle.text = swap ? Loc.Format("pockets.sell.swap_subtitle", buyCard.title.GetLocalizedString())
                        : Loc.Get("pockets.sell.subtitle");
                    hint.text = Loc.Get("pockets.hint.sell");
                    break;
            }

            giveSide.anchoredPosition = new Vector2(pair ? -sideOffset : 0f, giveSide.anchoredPosition.y);
            takeSide.gameObject.SetActive(pair);
            if (arrow)
                arrow.SetActive(pair);
            if (pair)
            {
                takeSide.anchoredPosition = new Vector2(sideOffset, takeSide.anchoredPosition.y);
                var card = _mode == Mode.Replace ? _cards.PendingCard : buyCard;
                takeCard.Show(card, _cards.StacksOf(card), 0, -1f);
                takeLabel.text = _mode == Mode.Replace ? Loc.Get("pockets.replace.new")
                    : Loc.Format("pockets.sell.buy", _stock.Slots[_buySlot].Price);
                takeLabel.color = takeColor;
            }

            confirmButton.gameObject.SetActive(_mode != Mode.View);
            skipButton.gameObject.SetActive(_mode == Mode.Replace);
            backLabel.text = Loc.Get(_mode == Mode.Replace ? "pockets.back" : "pockets.close");
            skipLabel.text = Loc.Get("pockets.skip");
            RefreshConfirm();
            LayoutButtons();
        }

        void RefreshConfirm()
        {
            if (_mode == Mode.View)
                return;
            bool swap = _mode == Mode.Sell && _buySlot >= 0;
            bool ready = _chosen != null && (!swap || _stock.SwapAffordable(_buySlot, _chosen));
            confirmButton.interactable = ready;
            confirmLabel.color = ready ? labelColor : mutedColor;
            confirmLabel.text = _chosen == null ? Loc.Get("pockets.pick")
                : _mode == Mode.Replace ? Loc.Get("pockets.replace.confirm")
                : !swap ? Loc.Format("pockets.sell.confirm", _stock.SellPriceOf(_chosen))
                : ready ? Loc.Get("pockets.sell.swap")
                : Loc.Get("pockets.sell.short");
        }

        void LayoutButtons()
        {
            var visible = new List<UnityEngine.UI.Button>(3);
            foreach (var button in new[] { backButton, confirmButton, skipButton })
                if (button && button.gameObject.activeSelf)
                    visible.Add(button);
            for (int i = 0; i < visible.Count; i++)
            {
                var rect = (RectTransform)visible[i].transform;
                rect.anchoredPosition = new Vector2((i - (visible.Count - 1) * 0.5f) * buttonSpacing, rect.anchoredPosition.y);
            }
        }

        UpgradeCard PreviewTarget()
        {
            var focused = FocusedMini();
            if (focused != null && focused.Card != null)
                return focused.Card;
            if (_mode == Mode.View)
                return _previewShown && _previewed != null && (_pockets.Contains(_previewed) || _free.Contains(_previewed))
                    ? _previewed
                    : _pockets.Count > 0 ? _pockets[0] : _free.Count > 0 ? _free[0] : null;
            return _chosen;
        }

        void RefreshPreview()
        {
            var card = PreviewTarget();
            if (_previewShown && card == _previewed)
                return;
            _previewed = card;
            _previewShown = true;

            bool has = card != null;
            giveCard.gameObject.SetActive(has);
            if (has)
                giveCard.Show(card, Mathf.Max(0, _cards.StacksOf(card) - 1), 0, -0.22f);
            if (givePlaceholder)
            {
                givePlaceholder.gameObject.SetActive(!has);
                givePlaceholder.text = Loc.Get(_mode switch
                {
                    Mode.View => "pockets.view.empty",
                    Mode.Replace => "pockets.replace.pick",
                    _ => "pockets.sell.pick",
                });
            }

            bool marked = has && card == _chosen && _mode != Mode.View;
            if (giveStamp)
            {
                giveStamp.gameObject.SetActive(marked);
                giveStamp.text = Loc.Get(_mode == Mode.Replace ? "pockets.replace.stamp" : "pockets.sell.stamp");
                giveStamp.color = _mode == Mode.Replace ? giveColor : sellColor;
            }
            giveLabel.gameObject.SetActive(has && _mode != Mode.View);
            if (_mode == Mode.Replace)
            {
                giveLabel.text = Loc.Get("pockets.replace.drop");
                giveLabel.color = giveColor;
            }
            else if (_mode == Mode.Sell && has)
            {
                giveLabel.text = Loc.Format("pockets.sell.give", _stock.SellPriceOf(card));
                giveLabel.color = sellColor;
            }
        }

        PocketCardMini FocusedMini()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null)
                return null;
            foreach (var mini in _minis)
                if (mini.gameObject == selected && mini.gameObject.activeSelf)
                    return mini;
            return null;
        }

        void OnMiniClicked(PocketCardMini mini)
        {
            if (!_open || !AcceptsInput || mini.Card == null || _mode == Mode.View)
                return;
            Choose(mini.Card);
        }

        void Choose(UpgradeCard card)
        {
            if (card == null || !_pockets.Contains(card))
                return;
            if (card == _chosen)
            {
                Confirm();
                return;
            }
            _chosen = card;
            foreach (var mini in _minis)
                mini.SetMarked(mini.gameObject.activeSelf && mini.Card == card && _pockets.Contains(card));
            GameEvents.PlaySound(SoundCue.UiMove, Vector3.zero);
            RefreshConfirm();
            _previewShown = false;
            if (EventSystem.current != null && confirmButton.interactable)
                EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
            RefreshPreview();
        }

        void Confirm()
        {
            if (!_open || _chosen == null || !AcceptsInput)
                return;
            switch (_mode)
            {
                case Mode.Replace:
                    _cards.ResolveDiscard(_chosen);
                    break;
                case Mode.Sell when _buySlot >= 0:
                    if (_stock.SellAndBuy(_buySlot, _chosen) == PurchaseResult.Ok)
                        Close();
                    else
                        GameEvents.PlaySound(SoundCue.NotEnoughCoins, Vector3.zero);
                    break;
                case Mode.Sell:
                    if (_stock.Sell(_chosen) != PurchaseResult.Ok)
                        return;
                    _chosen = null;
                    if (_cards.PocketsUsed == 0)
                    {
                        Close();
                        return;
                    }
                    Refresh();
                    SelectFirst();
                    break;
            }
        }

        void Back()
        {
            if (!_open)
                return;
            if (_mode == Mode.Replace)
                _cards.CancelDiscard();
            else
                Close();
        }

        void Skip()
        {
            if (_open && _mode == Mode.Replace && AcceptsInput)
                _cards.ResolveDiscard(null);
        }

        void Update()
        {
            if (!_open)
            {
                if (_cards != null && _cards.IsChoosing && ClosedFrame != Time.frameCount && TogglePressed())
                    OpenView();
                return;
            }

            RefreshPreview();
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null && NavigationPressed())
                SelectFirst();
            if (!AcceptsInput || Time.frameCount == _openedFrame)
                return;

            var keyboard = Keyboard.current;
            if (_mode != Mode.View && keyboard != null)
            {
                for (int i = 0; i < 6; i++)
                    if ((keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame) && i < _pockets.Count)
                        Choose(_pockets[i]);
            }
            if (_mode != Mode.Replace && TogglePressed())
                Close();
        }

        void LateUpdate()
        {
            if (_open && Time.frameCount != _openedFrame && CancelPressed())
                Back();
        }

        void SelectFirst()
        {
            var events = EventSystem.current;
            if (events == null)
                return;
            foreach (var mini in _minis)
            {
                if (mini.gameObject.activeInHierarchy && mini.Card != null && mini.Button.IsInteractable())
                {
                    events.SetSelectedGameObject(mini.gameObject);
                    return;
                }
            }
            events.SetSelectedGameObject(backButton ? backButton.gameObject : null);
        }

        public static bool TogglePressed()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                   || (gamepad != null && gamepad.selectButton.wasPressedThisFrame);
        }

        static void Bind(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button)
                button.onClick.AddListener(action);
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
