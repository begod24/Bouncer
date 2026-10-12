using Bouncer.Player;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Bouncer.UI
{
    public sealed class UpgradeScreen : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] UpgradeCardView[] cards;
        [SerializeField] TMP_Text title;
        [Tooltip("«Отложить»: закрыть выбор, портфель останется в рюкзаке")]
        [SerializeField] UnityEngine.UI.Button postponeButton;
        [Tooltip("Сколько секунд после открытия выбор не принимается")]
        [SerializeField] float inputDelay = 0.45f;
        [Tooltip("Пауза между появлением соседних карточек, с")]
        [SerializeField] float cardStagger = 0.07f;
        [Tooltip("Масштаб ряда из трёх карточек: снизу нужно место для карманов")]
        [SerializeField] float threeCardsScale = 0.9f;
        [Tooltip("Масштаб ряда, когда карточек четыре («Счастливый фантик»): чтобы влезли и на узком экране")]
        [SerializeField] float fourCardsScale = 0.82f;

        PlayerCards _cards;
        float _openedAt;
        bool _open;

        void Awake()
        {
            foreach (var card in cards)
                card.Clicked += OnCardClicked;
            if (postponeButton)
                postponeButton.onClick.AddListener(Postpone);
            SetVisible(false);
        }

        void Start()
        {
            Players.LocalChanged += Bind;
            Bind(Players.Local);
        }

        void OnDestroy()
        {
            Players.LocalChanged -= Bind;
            if (_cards != null)
                _cards.OfferChanged -= OnOfferChanged;
        }

        void Bind(PlayerController player)
        {
            if (_cards != null)
                _cards.OfferChanged -= OnOfferChanged;
            _cards = null;
            if (player == null || !player.TryGetComponent(out _cards))
                return;
            _cards.OfferChanged += OnOfferChanged;
            OnOfferChanged();
        }

        void OnOfferChanged()
        {
            if (_cards.IsChoosing)
                Open();
            else
                Close();
        }

        void Open()
        {
            _open = true;
            _openedAt = Time.unscaledTime;
            SetVisible(true);
            title.text = Loc.Get(_cards.OfferKind switch
            {
                OfferKind.Start => "choice.title.start",
                OfferKind.Portfolio => "choice.title.portfolio",
                _ => "choice.title.boss",
            });

            if (postponeButton)
                postponeButton.gameObject.SetActive(_cards.CanPostpone);
            var offer = _cards.Offer;
            if (cards.Length > 0 && cards[0].transform.parent)
                cards[0].transform.parent.localScale = Vector3.one * (offer.Count > 3 ? fourCardsScale : threeCardsScale);
            for (int i = 0; i < cards.Length; i++)
            {
                if (i < offer.Count)
                {
                    cards[i].Show(offer[i], _cards.StacksOf(offer[i]), i + 1, i * cardStagger);
                    string blocked = _cards.BlockReason(offer[i]);
                    string sleeping = _cards.SleepingWith(offer[i]);
                    if (blocked != null)
                        cards[i].ShowBlocked(blocked, _cards.BallTitle());
                    else if (sleeping != null)
                        cards[i].ShowWarning(Loc.Format("card.sleep.warning", sleeping));
                    else
                        cards[i].ShowPocketNeed(_cards.NeedFor(offer[i]), _cards.PocketsUsed, _cards.MaxPockets);
                    cards[i].ShowComboHint(_cards);
                }
                else
                {
                    cards[i].gameObject.SetActive(false);
                }
            }
            Select(0);
        }

        static bool PocketsOpen => PocketsPanel.Instance != null && PocketsPanel.Instance.IsOpen;

        void Close()
        {
            _open = false;
            SetVisible(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        void Update()
        {
            if (!_open || PocketsOpen)
                return;

            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null && NavigationPressed())
                Select(0);

            if (!AcceptsInput)
                return;
            if (CancelPressed())
            {
                Postpone();
                return;
            }
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                Pick(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                Pick(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                Pick(2);
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
                Pick(3);
        }

        bool AcceptsInput => Time.unscaledTime - _openedAt >= inputDelay;

        void OnCardClicked(UpgradeCardView card)
        {
            if (_open && AcceptsInput && !PocketsOpen)
                Pick(System.Array.IndexOf(cards, card));
        }

        void Postpone()
        {
            if (_open && AcceptsInput && !PocketsOpen && _cards != null && _cards.CanPostpone)
                _cards.Postpone();
        }

        static bool CancelPressed()
        {
            var module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
            var cancel = module != null && module.cancel != null ? module.cancel.action : null;
            return cancel != null && cancel.WasPressedThisFrame();
        }

        void Pick(int index)
        {
            if (index >= 0 && index < _cards.Offer.Count)
                _cards.Choose(index);
        }

        void Select(int index)
        {
            if (EventSystem.current != null && index < cards.Length && cards[index].gameObject.activeSelf)
                EventSystem.current.SetSelectedGameObject(cards[index].gameObject);
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
