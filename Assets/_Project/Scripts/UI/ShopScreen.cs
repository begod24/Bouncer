using System;
using Bouncer.Core;
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
    /// Витрина ларька «Союзпечать»: 4 жвачки с видимыми вкладышами и ценой, замок «отложить», перебор витрины,
    /// лимонад и бутерброд, монетки игрока. Покупка — клик или Enter по карточке, клавиши 1–4; R / Y — перебрать,
    /// L / X — отложить выбранную; Esc / B или «Дальше» — закрыть. Вся логика — в <see cref="ShopStock"/>,
    /// экран только показывает её и передаёт нажатия.
    /// </summary>
    public sealed class ShopScreen : MonoBehaviour
    {
        [Serializable]
        sealed class SlotView
        {
            public UpgradeCardView card;
            public CanvasGroup cardGroup;
            public TMP_Text price;
            public UnityEngine.UI.Button lockButton;
            public TMP_Text lockLabel;
            [Tooltip("Штамп «Продано» поверх карточки")]
            public GameObject sold;
            [NonSerialized] public UpgradeCard Shown;
        }

        [SerializeField] CanvasGroup group;
        [SerializeField] SlotView[] slots = Array.Empty<SlotView>();
        [SerializeField] TMP_Text coins;
        [Tooltip("«Копилка: +N» — видна, если копилка что-то принесла")]
        [SerializeField] TMP_Text interest;
        [Tooltip("«Не хватает монеток» и т.п.")]
        [SerializeField] TMP_Text message;

        [Header("Кнопки")]
        [SerializeField] UnityEngine.UI.Button rerollButton;
        [SerializeField] TMP_Text rerollPrice;
        [SerializeField] UnityEngine.UI.Button lemonadeButton;
        [SerializeField] TMP_Text lemonadePrice;
        [SerializeField] UnityEngine.UI.Button sandwichButton;
        [SerializeField] TMP_Text sandwichPrice;
        [SerializeField] UnityEngine.UI.Button leaveButton;

        [Header("Вид")]
        [SerializeField] Color affordableColor = new(0.96f, 0.95f, 0.92f);
        [SerializeField] Color expensiveColor = new(0.93f, 0.4f, 0.35f);
        [Tooltip("Прозрачность проданной или пустой карточки")]
        [SerializeField, Range(0f, 1f)] float soldAlpha = 0.35f;
        [Tooltip("Сколько секунд после открытия покупки не принимаются")]
        [SerializeField] float inputDelay = 0.35f;
        [SerializeField] float messageTime = 1.4f;
        [SerializeField] float cardStagger = 0.06f;

        Kiosk _kiosk;
        ShopStock _stock;
        bool _open;
        bool _closeRequested;
        float _openedAt;
        float _messageUntil;

        void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].card.Clicked += _ => Buy(index);
                if (slots[i].lockButton)
                    slots[i].lockButton.onClick.AddListener(() => ToggleLock(index));
            }
            Bind(rerollButton, Reroll);
            Bind(lemonadeButton, () => Feedback(_stock?.BuyLemonade()));
            Bind(sandwichButton, () => Feedback(_stock?.BuySandwich()));
            Bind(leaveButton, () => _closeRequested = true);
            SetVisible(false);
        }

        void OnDestroy() => Unbind();

        void Update()
        {
            if (_kiosk != Kiosk.Instance)
            {
                Unbind();
                _kiosk = Kiosk.Instance;
                if (_kiosk != null)
                {
                    _kiosk.ShopOpened += OnShopOpened;
                    _kiosk.ShopClosed += OnShopClosed;
                }
            }
            if (!_open)
                return;

            RefreshCoins();
            if (message)
                message.alpha = Mathf.Clamp01((_messageUntil - Time.unscaledTime) * 3f);

            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null && NavigationPressed())
                SelectFirst();

            if (!AcceptsInput)
                return;
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if (keyboard != null)
            {
                for (int i = 0; i < slots.Length && i < 4; i++)
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                        Buy(i);
                if (keyboard.rKey.wasPressedThisFrame)
                    Reroll();
                if (keyboard.lKey.wasPressedThisFrame)
                    ToggleLock(SelectedSlot());
            }
            if (gamepad != null)
            {
                if (gamepad.buttonNorth.wasPressedThisFrame)
                    Reroll();
                if (gamepad.buttonWest.wasPressedThisFrame)
                    ToggleLock(SelectedSlot());
            }
        }

        // Esc / B закрывают витрину здесь, а не в Update: тот же Esc — кнопка паузы, и игрок его уже прочитал,
        // пока ларёк был открыт (пауза в ларьке не включается).
        void LateUpdate()
        {
            if (!_open)
                return;
            if (CancelPressed())
                _closeRequested = true;
            if (_closeRequested && _kiosk != null)
                _kiosk.CloseShop();
            _closeRequested = false;
        }

        bool AcceptsInput => Time.unscaledTime - _openedAt >= inputDelay;

        void OnShopOpened()
        {
            if (_stock != null)
                _stock.Changed -= Refresh;
            _stock = _kiosk.Stock;
            if (_stock == null)
                return;
            _stock.Changed += Refresh;
            _open = true;
            _openedAt = Time.unscaledTime;
            _messageUntil = 0f;
            foreach (var slot in slots)
                slot.Shown = null;
            if (interest)
            {
                interest.gameObject.SetActive(_kiosk.Interest > 0);
                interest.text = Loc.Format("shop.interest", _kiosk.Interest);
            }
            SetVisible(true);
            Refresh();
            SelectFirst();
        }

        void OnShopClosed()
        {
            _open = false;
            SetVisible(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        void Refresh()
        {
            if (_stock == null)
                return;
            int money = RunState.Coins;
            var stock = _stock.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                var view = slots[i];
                var slot = i < stock.Count ? stock[i] : null;
                bool has = slot != null && slot.Card != null;
                view.card.gameObject.SetActive(has);
                if (has && view.Shown != slot.Card)
                {
                    view.Shown = slot.Card;
                    view.card.Show(slot.Card, _kiosk.Customer.StacksOf(slot.Card), i + 1, i * cardStagger);
                }
                bool available = has && !slot.Sold;
                if (view.cardGroup)
                    view.cardGroup.alpha = available ? 1f : soldAlpha;
                view.card.Button.interactable = available;
                if (view.sold)
                    view.sold.SetActive(has && slot.Sold);
                if (view.price)
                {
                    view.price.gameObject.SetActive(available);
                    view.price.text = has ? slot.Price.ToString() : string.Empty;
                    view.price.color = available && slot.Price <= money ? affordableColor : expensiveColor;
                }
                if (view.lockButton)
                {
                    view.lockButton.gameObject.SetActive(available);
                    view.lockLabel.text = Loc.Get(has && slot.Locked ? "shop.locked" : "shop.lock");
                }
            }
            SetPrice(rerollButton, rerollPrice, _stock.RerollPrice, true);
            SetPrice(lemonadeButton, lemonadePrice, _stock.LemonadePrice, !_stock.HeartsFull);
            SetPrice(sandwichButton, sandwichPrice, _stock.SandwichPrice, !_stock.HeartsFull);
            RefreshCoins();
        }

        void SetPrice(UnityEngine.UI.Button button, TMP_Text label, int price, bool available)
        {
            if (button)
                button.interactable = available;
            if (label)
            {
                label.text = price.ToString();
                label.color = available && price <= RunState.Coins ? affordableColor : expensiveColor;
            }
        }

        void RefreshCoins()
        {
            if (coins)
                coins.text = RunState.Coins.ToString();
        }

        void Buy(int index)
        {
            if (!_open || !AcceptsInput || _stock == null)
                return;
            Feedback(_stock.Buy(index));
        }

        void Reroll()
        {
            if (!_open || !AcceptsInput || _stock == null)
                return;
            Feedback(_stock.Reroll());
        }

        void ToggleLock(int index)
        {
            if (!_open || !AcceptsInput || _stock == null || index < 0)
                return;
            _stock.ToggleLock(index);
        }

        void Feedback(PurchaseResult? result)
        {
            if (result != PurchaseResult.NotEnoughCoins)
                return;
            GameEvents.PlaySound(SoundCue.NotEnoughCoins, Vector3.zero);
            if (message)
            {
                message.text = Loc.Get("shop.not_enough");
                _messageUntil = Time.unscaledTime + messageTime;
            }
        }

        /// <summary>Какая карточка витрины сейчас выбрана (или её замок). -1 — ни одна.</summary>
        int SelectedSlot()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null)
                return -1;
            for (int i = 0; i < slots.Length; i++)
                if (selected == slots[i].card.gameObject || (slots[i].lockButton && selected == slots[i].lockButton.gameObject))
                    return i;
            return -1;
        }

        void SelectFirst()
        {
            if (EventSystem.current == null)
                return;
            foreach (var slot in slots)
            {
                if (slot.card.gameObject.activeInHierarchy && slot.card.Button.interactable)
                {
                    EventSystem.current.SetSelectedGameObject(slot.card.gameObject);
                    return;
                }
            }
            if (leaveButton)
                EventSystem.current.SetSelectedGameObject(leaveButton.gameObject);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        void Unbind()
        {
            if (_kiosk != null)
            {
                _kiosk.ShopOpened -= OnShopOpened;
                _kiosk.ShopClosed -= OnShopClosed;
            }
            if (_stock != null)
                _stock.Changed -= Refresh;
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
                                         || keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame))
                   || (gamepad != null && (gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame
                                           || gamepad.dpad.up.wasPressedThisFrame || gamepad.dpad.down.wasPressedThisFrame));
        }
    }
}
