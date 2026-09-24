using System;
using Bouncer.Core;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Bouncer.UI
{
    /// <summary>
    /// Один вкладыш на экране выбора: цвет обёртки, картинка, название, описание, сколько раз уже взят.
    /// Выбранный (мышью, стрелками, геймпадом) чуть больше, ровный и подсвечен. Анимации — по реальному времени,
    /// потому что игра на время выбора стоит.
    /// </summary>
    public sealed class UpgradeCardView : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] UnityEngine.UI.Button button;
        [Tooltip("Всё, что качается и масштабируется (внутри карточки, чтобы не спорить с раскладкой)")]
        [SerializeField] RectTransform body;
        [SerializeField] CanvasGroup group;
        [SerializeField] UnityEngine.UI.Image paper;
        [SerializeField] UnityEngine.UI.Image icon;
        [SerializeField] UnityEngine.UI.Image glow;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text description;
        [SerializeField] TMP_Text category;
        [SerializeField] TMP_Text stacks;
        [SerializeField] TMP_Text hotkey;

        [Header("Анимация")]
        [SerializeField] float appearTime = 0.35f;
        [SerializeField] float maxTilt = 4f;
        [SerializeField] float selectedScale = 1.07f;

        float _appearAt;
        float _tilt;
        float _scale = 1f;

        public event Action<UpgradeCardView> Clicked;
        public bool IsSelected { get; private set; }

        void Awake() => button.onClick.AddListener(() => Clicked?.Invoke(this));

        public void Show(UpgradeCard card, int taken, int number, float delay)
        {
            paper.color = card.wrapperColor;
            icon.sprite = card.icon;
            icon.enabled = card.icon != null;
            title.text = card.title.GetLocalizedString();
            description.text = card.description.GetLocalizedString();
            category.text = CategoryName(card.category);
            stacks.text = card.maxStacks > 1 && card.category != UpgradeCategory.Treat ? $"{taken + 1}/{card.maxStacks}" : string.Empty;
            hotkey.text = number.ToString();

            _appearAt = Time.unscaledTime + delay;
            _tilt = UnityEngine.Random.Range(-maxTilt, maxTilt);
            _scale = 1f;
            IsSelected = false;
            gameObject.SetActive(true);
            Animate();
        }

        void Update() => Animate();

        void Animate()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _appearAt) / appearTime);
            _scale = Mathf.MoveTowards(_scale, IsSelected ? selectedScale : 1f, Time.unscaledDeltaTime * 1.2f);
            body.localScale = Vector3.one * (EaseOutBack(t) * _scale);
            body.localRotation = Quaternion.Euler(0f, 0f, IsSelected ? 0f : _tilt);
            group.alpha = t;
            if (glow)
            {
                glow.enabled = IsSelected;
                var color = glow.color;
                color.a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
                glow.color = color;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button.interactable && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData eventData)
        {
            IsSelected = true;
            if (Time.unscaledTime > _appearAt)
                GameEvents.PlaySound(SoundCue.UiMove, Vector3.zero);
        }

        public void OnDeselect(BaseEventData eventData) => IsSelected = false;

        static string CategoryName(UpgradeCategory category) => Loc.Get(category switch
        {
            UpgradeCategory.Ball => "card.category.ball",
            UpgradeCategory.Modifier => "card.category.modifier",
            UpgradeCategory.Passive => "card.category.passive",
            _ => "card.category.treat",
        });

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
