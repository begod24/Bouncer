using Bouncer.Player;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Bouncer.UI
{
    /// <summary>
    /// Экран «Новый уровень»: показывает предложение <see cref="PlayerProgression"/> и передаёт выбор.
    /// Выбор — мышью, стрелками/WASD + Enter, геймпадом или клавишами 1–3. Первые доли секунды ввод
    /// не принимается, чтобы случайный клик или бросок не выбрал карточку вслепую.
    /// </summary>
    public sealed class UpgradeScreen : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] UpgradeCardView[] cards;
        [SerializeField] TMP_Text title;
        [Tooltip("Сколько секунд после открытия выбор не принимается")]
        [SerializeField] float inputDelay = 0.45f;
        [Tooltip("Пауза между появлением соседних карточек, с")]
        [SerializeField] float cardStagger = 0.07f;

        PlayerProgression _progression;
        float _openedAt;
        bool _open;

        void Awake()
        {
            foreach (var card in cards)
                card.Clicked += OnCardClicked;
            SetVisible(false);
        }

        void Start() => Bind(FindFirstObjectByType<PlayerController>());

        void OnDestroy()
        {
            if (_progression != null)
                _progression.OfferChanged -= OnOfferChanged;
        }

        void Bind(PlayerController player)
        {
            if (player == null || !player.TryGetComponent(out _progression))
                return;
            _progression.OfferChanged += OnOfferChanged;
            OnOfferChanged();
        }

        void OnOfferChanged()
        {
            if (_progression.IsChoosing)
                Open();
            else
                Close();
        }

        void Open()
        {
            _open = true;
            _openedAt = Time.unscaledTime;
            SetVisible(true);
            title.text = Loc.Format("upgrade.title", _progression.Level);

            var offer = _progression.Offer;
            for (int i = 0; i < cards.Length; i++)
            {
                if (i < offer.Count)
                    cards[i].Show(offer[i], _progression.StacksOf(offer[i]), i + 1, i * cardStagger);
                else
                    cards[i].gameObject.SetActive(false);
            }
            Select(0);
        }

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
            if (!_open)
                return;

            // Мышь кликнула мимо карточек — стрелки и геймпад снова должны что-то выбирать.
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null && NavigationPressed())
                Select(0);

            if (!AcceptsInput)
                return;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                Pick(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                Pick(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                Pick(2);
        }

        bool AcceptsInput => Time.unscaledTime - _openedAt >= inputDelay;

        void OnCardClicked(UpgradeCardView card)
        {
            if (_open && AcceptsInput)
                Pick(System.Array.IndexOf(cards, card));
        }

        void Pick(int index)
        {
            if (index >= 0 && index < _progression.Offer.Count)
                _progression.Choose(index);
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
