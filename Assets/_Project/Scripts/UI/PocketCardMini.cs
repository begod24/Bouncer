using System;
using Bouncer.Core;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Bouncer.UI
{
    /// <summary>
    /// Маленький вкладыш в кармане: обёртка своего цвета, окошко с картинкой, рамка у редких и золотых, повторы
    /// («2/3»), подпись под ним. Пустой карман — меловой пунктир. Так показаны карманы на HUD, на экране выбора,
    /// в ларьке и на экране карманов; на экране карманов вкладыш — кнопка: выбранный (мышью, стрелками, геймпадом)
    /// больше и подсвечен, отмеченный на выброс или продажу — перечёркнут. Размер задаёт масштаб объекта
    /// (префаб — 120×120). Анимации — по реальному времени: на экранах карточек игра стоит.
    /// </summary>
    public sealed class PocketCardMini : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] UnityEngine.UI.Button button;
        [Tooltip("Всё, что масштабируется при выборе и подпрыгивает (подпись снаружи не прыгает)")]
        [SerializeField] RectTransform body;
        [SerializeField] UnityEngine.UI.Image glow;
        [Tooltip("Пустой карман — пунктир")]
        [SerializeField] UnityEngine.UI.Image slot;
        [Tooltip("Рамка редкости под обёрткой: у обычных скрыта")]
        [SerializeField] UnityEngine.UI.Image border;
        [SerializeField] UnityEngine.UI.Image wrapper;
        [SerializeField] UnityEngine.UI.Image window;
        [SerializeField] UnityEngine.UI.Image icon;
        [SerializeField] TMP_Text stacks;
        [SerializeField] TMP_Text hotkey;
        [Tooltip("Красный крест: отмечен на выброс или продажу")]
        [SerializeField] GameObject mark;
        [SerializeField] TMP_Text label;

        [Header("Вид")]
        [SerializeField] Color rareColor = new(0.45f, 0.72f, 1f);
        [SerializeField] Color goldColor = new(1f, 0.82f, 0.25f);
        [SerializeField] Color labelColor = new(0.96f, 0.95f, 0.92f);
        [SerializeField] float selectedScale = 1.12f;
        [Tooltip("Новая карточка в кармане подпрыгивает до такого размера")]
        [SerializeField] float popScale = 1.5f;
        [SerializeField] float popTime = 0.45f;

        float _scale = 1f;
        float _popAt = -10f;

        public UpgradeCard Card { get; private set; }
        public bool IsEmpty => Card == null;
        public bool IsSelected { get; private set; }
        public UnityEngine.UI.Button Button => button;

        public event Action<PocketCardMini> Clicked;
        public event Action<PocketCardMini> Selected;

        void Awake()
        {
            if (button)
                button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        void OnEnable()
        {
            IsSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            Animate();
        }

        /// <summary>Вкладыш в кармане. taken — сколько раз он взят (для «2/3»).</summary>
        public void Show(UpgradeCard card, int taken)
        {
            Card = card;
            slot.enabled = false;
            wrapper.gameObject.SetActive(true);
            wrapper.color = card.wrapperColor;
            icon.sprite = card.icon;
            icon.enabled = card.icon != null;
            border.enabled = card.rarity != CardRarity.Common;
            border.color = card.rarity == CardRarity.Gold ? goldColor : rareColor;
            stacks.text = card.maxStacks > 1 && card.category != UpgradeCategory.Treat ? $"{Mathf.Max(1, taken)}/{card.maxStacks}" : string.Empty;
            SetMarked(false);
            SetHotkey(0);
            SetLabel(null);
        }

        /// <summary>Пустой карман: пунктир мелом.</summary>
        public void ShowEmpty()
        {
            Card = null;
            slot.enabled = true;
            wrapper.gameObject.SetActive(false);
            border.enabled = false;
            stacks.text = string.Empty;
            SetMarked(false);
            SetHotkey(0);
            SetLabel(null);
        }

        /// <summary>Подпись под вкладышем (название, цена). Пусто — без подписи.</summary>
        public void SetLabel(string text, Color? color = null)
        {
            if (!label)
                return;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
            label.text = text ?? string.Empty;
            label.color = color ?? labelColor;
        }

        /// <summary>
        /// Размер подписи в единицах вкладыша (ряд с маленькими вкладышами делит экранный размер на свой масштаб).
        /// wrap = false — в одну строку: длинное название мельчает, а не переносится.
        /// </summary>
        public void SetLabelStyle(float fontSize, float width, bool wrap)
        {
            if (!label)
                return;
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = fontSize * (wrap ? 0.7f : 0.55f);
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(width, fontSize * (wrap ? 2.6f : 1.6f));
        }

        /// <summary>Номер для быстрого выбора (1–6); 0 — без номера.</summary>
        public void SetHotkey(int number)
        {
            if (hotkey)
                hotkey.text = number > 0 ? number.ToString() : string.Empty;
        }

        public void SetMarked(bool marked)
        {
            if (mark)
                mark.SetActive(marked);
        }

        /// <summary>Можно ли выбрать и нажать (на HUD и у пустых карманов — нет).</summary>
        public void SetInteractable(bool interactable)
        {
            if (button)
                button.interactable = interactable;
        }

        /// <summary>Новая карточка легла в карман — подпрыгнуть.</summary>
        public void Pop() => _popAt = Time.unscaledTime;

        void Update() => Animate();

        void Animate()
        {
            _scale = Mathf.MoveTowards(_scale, IsSelected ? selectedScale : 1f, Time.unscaledDeltaTime * 1.5f);
            float pop = Mathf.Clamp01((Time.unscaledTime - _popAt) / popTime);
            float bump = pop < 1f ? Mathf.Sin(pop * Mathf.PI) * (1f - pop) * (popScale - 1f) * 2f : 0f;
            if (body)
                body.localScale = Vector3.one * (_scale + bump);
            if (glow)
            {
                bool lit = IsSelected || pop < 1f;
                glow.enabled = lit;
                if (lit)
                {
                    var color = glow.color;
                    color.a = pop < 1f ? 1f - pop : 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
                    glow.color = color;
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button && button.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData eventData)
        {
            IsSelected = true;
            GameEvents.PlaySound(SoundCue.UiMove, Vector3.zero);
            Selected?.Invoke(this);
        }

        public void OnDeselect(BaseEventData eventData) => IsSelected = false;
    }
}
