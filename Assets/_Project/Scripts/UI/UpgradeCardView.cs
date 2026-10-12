using System;
using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Bouncer.UI
{
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
        [Tooltip("Рамка редкости: у обычных скрыта, у редких и золотых — своего цвета")]
        [SerializeField] UnityEngine.UI.Image rarityFrame;

        [Header("Цвет рамки редкости")]
        [SerializeField] Color rareColor = new(0.45f, 0.72f, 1f);
        [SerializeField] Color goldColor = new(1f, 0.82f, 0.25f);

        [Header("Плашка про карман (выбор и ларёк)")]
        [Tooltip("Плашка на нижнем крае вкладыша: займёт ли он карман. Пусто — плашки нет")]
        [SerializeField] GameObject pocketTag;
        [SerializeField] TMP_Text pocketTagText;
        [SerializeField] Color tagNewColor = new(0.96f, 0.95f, 0.92f);
        [SerializeField] Color tagStackColor = new(0.58f, 0.86f, 0.42f);
        [SerializeField] Color tagFreeColor = new(0.55f, 0.8f, 1f);
        [SerializeField] Color tagComboColor = new(1f, 0.82f, 0.25f);
        [SerializeField] Color tagFullColor = new(1f, 0.45f, 0.38f);

        [Header("Анимация")]
        [SerializeField] float appearTime = 0.35f;
        [SerializeField] float maxTilt = 4f;
        [SerializeField] float selectedScale = 1.07f;

        [Header("Карточка мяча: шкалы вместо части текста")]
        [SerializeField] BallStatBars ballStats;
        [SerializeField] float ballIconSize = 128f;
        [SerializeField] float ballTitleY = -146f;
        [SerializeField] float ballDescriptionY = -194f;
        [SerializeField] float ballDescriptionHeight = 76f;

        [Header("Нельзя взять (мешает мяч)")]
        [SerializeField] Color blockedColor = new(1f, 0.45f, 0.38f);
        [SerializeField, Range(0f, 1f)] float blockedAlpha = 0.55f;

        float _appearAt;
        float _tilt;
        float _scale = 1f;
        bool _blocked;
        Color _paperColor = Color.white;
        bool _layoutSaved;
        Vector2 _iconSize;
        Vector2 _iconPosition;
        Vector2 _titlePosition;
        Vector2 _descriptionPosition;
        Vector2 _descriptionSize;

        public event Action<UpgradeCardView> Clicked;
        public bool IsSelected { get; private set; }
        public UnityEngine.UI.Button Button => button;

        void Awake() => button.onClick.AddListener(() => Clicked?.Invoke(this));

        public void Show(UpgradeCard card, int taken, int number, float delay)
        {
            _paperColor = card.wrapperColor;
            paper.color = card.wrapperColor;
            icon.sprite = card.icon;
            icon.enabled = card.icon != null;
            title.text = card.title.GetLocalizedString();
            description.text = card.description.GetLocalizedString();
            category.text = card.rarity == CardRarity.Common
                ? CategoryName(card)
                : $"{CategoryName(card)} · {RarityName(card)}";
            if (rarityFrame)
            {
                rarityFrame.enabled = card.rarity != CardRarity.Common;
                rarityFrame.color = card.rarity == CardRarity.Gold ? goldColor : rareColor;
            }
            stacks.text = card.maxStacks > 1 && card.category != UpgradeCategory.Treat ? $"{taken + 1}/{card.maxStacks}" : string.Empty;
            hotkey.text = number > 0 ? number.ToString() : string.Empty;
            HidePocketTag();
            SetBlocked(false);
            ShowBallStats(card);

            _appearAt = Time.unscaledTime + delay;
            _tilt = UnityEngine.Random.Range(-maxTilt, maxTilt);
            _scale = 1f;
            IsSelected = false;
            gameObject.SetActive(true);
            Animate();
        }

        public void ShowPocketNeed(PocketNeed need, int used, int max)
        {
            if (_blocked)
                SetBlocked(false);
            if (!pocketTag)
                return;
            pocketTag.SetActive(true);
            pocketTagText.text = need switch
            {
                PocketNeed.New => Loc.Format("pockets.tag.new", used + 1, max),
                PocketNeed.Stack => Loc.Get("pockets.tag.stack"),
                PocketNeed.Free => Loc.Get("pockets.tag.free"),
                PocketNeed.Combo => Loc.Get("pockets.tag.combo"),
                PocketNeed.Swap => Loc.Get("pockets.tag.swap"),
                _ => Loc.Get("pockets.tag.full"),
            };
            pocketTagText.color = need switch
            {
                PocketNeed.New => tagNewColor,
                PocketNeed.Stack => tagStackColor,
                PocketNeed.Free => tagFreeColor,
                PocketNeed.Combo or PocketNeed.Swap => tagComboColor,
                _ => tagFullColor,
            };
        }

        void ShowBallStats(UpgradeCard card)
        {
            if (!ballStats)
                return;
            SaveLayout();
            var definition = card is BallTypeCard type && type.ballPrefab ? type.ballPrefab.Definition : null;
            bool ball = definition != null;
            var iconRect = icon.rectTransform;
            iconRect.sizeDelta = ball ? new Vector2(ballIconSize, ballIconSize) : _iconSize;
            iconRect.anchoredPosition = _iconPosition;
            title.rectTransform.anchoredPosition = ball ? new Vector2(_titlePosition.x, ballTitleY) : _titlePosition;
            description.rectTransform.anchoredPosition = ball ? new Vector2(_descriptionPosition.x, ballDescriptionY) : _descriptionPosition;
            description.rectTransform.sizeDelta = ball ? new Vector2(_descriptionSize.x, ballDescriptionHeight) : _descriptionSize;
            if (ball)
                ballStats.Show(definition.ratings);
            else
                ballStats.Hide();
        }

        void SaveLayout()
        {
            if (_layoutSaved)
                return;
            _layoutSaved = true;
            _iconSize = icon.rectTransform.sizeDelta;
            _iconPosition = icon.rectTransform.anchoredPosition;
            _titlePosition = title.rectTransform.anchoredPosition;
            _descriptionPosition = description.rectTransform.anchoredPosition;
            _descriptionSize = description.rectTransform.sizeDelta;
        }

        // Серая карточка с плашкой-причиной: например, «Ещё мяч» при набивном.
        public void ShowBlocked(string reasonKey, string ballTitle)
        {
            SetBlocked(true);
            if (!pocketTag)
                return;
            pocketTag.SetActive(true);
            string ball = string.IsNullOrEmpty(ballTitle) ? ballTitle : char.ToLowerInvariant(ballTitle[0]) + ballTitle.Substring(1);
            pocketTagText.text = Loc.Format(reasonKey, ball);
            pocketTagText.color = blockedColor;
        }

        void SetBlocked(bool blocked)
        {
            _blocked = blocked;
            icon.color = blocked ? new Color(0.55f, 0.55f, 0.55f) : Color.white;
            if (paper)
                paper.color = blocked ? Color.Lerp(_paperColor, new Color(0.6f, 0.6f, 0.6f), 0.7f) : _paperColor;
        }

        public void HidePocketTag()
        {
            if (pocketTag)
                pocketTag.SetActive(false);
        }

        void Update() => Animate();

        void Animate()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _appearAt) / appearTime);
            _scale = Mathf.MoveTowards(_scale, IsSelected ? selectedScale : 1f, Time.unscaledDeltaTime * 1.2f);
            body.localScale = Vector3.one * (EaseOutBack(t) * _scale);
            body.localRotation = Quaternion.Euler(0f, 0f, IsSelected ? 0f : _tilt);
            group.alpha = t * (_blocked ? blockedAlpha : 1f);
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

        static string CategoryName(UpgradeCard card) => Loc.Get(card.IsCombo ? "card.category.combo"
            : card is AbilityCard ? "card.category.ability" : card.category switch
        {
            UpgradeCategory.Ball => "card.category.ball",
            UpgradeCategory.Modifier => "card.category.modifier",
            UpgradeCategory.Passive => "card.category.passive",
            _ => "card.category.treat",
        });

        static string RarityName(UpgradeCard card) => Loc.Get(card.rarity != CardRarity.Gold ? "card.rarity.rare"
            : card.IsCombo ? "card.rarity.gold" : "card.rarity.gold_once");


        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
