using System;
using System.Collections.Generic;
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
        [UnityEngine.Serialization.FormerlySerializedAs("ballIconSize")]
        [SerializeField] float ballCardIconSize = 128f;
        [SerializeField] float ballTitleY = -146f;
        [SerializeField] float ballDescriptionY = -194f;
        [SerializeField] float ballDescriptionHeight = 76f;

        [Header("Подсказки без текста: рецепт комбо, напарник, мячи")]
        [Tooltip("Комбо: картинки двух частей сверху, сама картинка комбо меньше и ниже")]
        [SerializeField] float recipeIconSize = 64f;
        [SerializeField] float comboIconSize = 128f;
        [SerializeField] float comboIconDrop = 66f;
        [Tooltip("Строка «+ напарник = комбо» внизу окна")]
        [SerializeField] float hintIconSize = 42f;
        [SerializeField] float hintY = -354f;
        [Tooltip("Мячи в верхнем левом углу: с какими лучше, с какими карточка спит")]
        [SerializeField] float cornerBallSize = 40f;
        [SerializeField] Color hintColor = new(0.35f, 0.32f, 0.3f);
        [SerializeField] Color readyColor = new(0.85f, 0.55f, 0.05f);
        [SerializeField] Color sleepColor = new(0.85f, 0.2f, 0.15f);

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
            ShowRecipe(card);
            ShowBalls(card);
            HideComboHint();

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
                PocketNeed.ComboSame => Loc.Get("pockets.tag.combo_same"),
                PocketNeed.Swap => Loc.Get("pockets.tag.swap"),
                _ => Loc.Get("pockets.tag.full"),
            };
            pocketTagText.color = need switch
            {
                PocketNeed.New => tagNewColor,
                PocketNeed.Stack => tagStackColor,
                PocketNeed.Free => tagFreeColor,
                PocketNeed.Combo or PocketNeed.ComboSame or PocketNeed.Swap => tagComboColor,
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
            iconRect.sizeDelta = ball ? new Vector2(ballCardIconSize, ballCardIconSize) : _iconSize;
            iconRect.anchoredPosition = _iconPosition;
            title.rectTransform.anchoredPosition = ball ? new Vector2(_titlePosition.x, ballTitleY) : _titlePosition;
            description.rectTransform.anchoredPosition = ball ? new Vector2(_descriptionPosition.x, ballDescriptionY) : _descriptionPosition;
            description.rectTransform.sizeDelta = ball ? new Vector2(_descriptionSize.x, ballDescriptionHeight) : _descriptionSize;
            if (ball)
                ballStats.Show(definition.ratings);
            else
                ballStats.Hide();
        }

        // ------------------------------------------------------------------ подсказки
        static readonly List<UpgradeCard> s_list = new();
        static readonly List<BallTypeCard> s_balls = new();

        RectTransform _recipe;
        UnityEngine.UI.Image _recipeA;
        UnityEngine.UI.Image _recipeB;
        RectTransform _hint;
        TMP_Text _hintPlus;
        UnityEngine.UI.Image _hintPartner;
        TMP_Text _hintEquals;
        UnityEngine.UI.Image _hintCombo;
        TMP_Text _hintName;
        readonly List<UnityEngine.UI.Image> _ballIcons = new();
        readonly List<UnityEngine.UI.Image> _ballSlashes = new();
        UpgradeCard _card;

        RectTransform Window => (RectTransform)icon.rectTransform.parent;

        UnityEngine.UI.Image NewImage(string name, Transform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        TMP_Text NewText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
        {
            var text = Instantiate(stacks, parent, false);
            text.name = name;
            text.raycastTarget = false;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return text;
        }

        static RectTransform Row(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        void BuildHintUi()
        {
            if (_recipe != null)
                return;
            var window = Window;
            _recipe = Row("Recipe", window, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(240f, recipeIconSize));
            _recipeA = NewImage("PartA", _recipe, recipeIconSize);
            ((RectTransform)_recipeA.transform).anchoredPosition = new Vector2(-62f, -recipeIconSize * 0.5f);
            var plus = NewText("Plus", _recipe, 40f, TextAlignmentOptions.Center);
            plus.text = "+";
            plus.color = hintColor;
            plus.rectTransform.sizeDelta = new Vector2(40f, recipeIconSize);
            plus.rectTransform.anchoredPosition = new Vector2(0f, -recipeIconSize * 0.5f);
            _recipeB = NewImage("PartB", _recipe, recipeIconSize);
            ((RectTransform)_recipeB.transform).anchoredPosition = new Vector2(62f, -recipeIconSize * 0.5f);
            foreach (var image in new[] { _recipeA, _recipeB })
            {
                var rect = (RectTransform)image.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -recipeIconSize * 0.5f);
            }
            var plusRect = plus.rectTransform;
            plusRect.anchorMin = plusRect.anchorMax = new Vector2(0.5f, 1f);

            _hint = Row("ComboHint", window, new Vector2(0.5f, 1f), new Vector2(0f, hintY), new Vector2(300f, hintIconSize + 4f));
            float y = -(hintIconSize + 4f) * 0.5f;
            _hintPlus = NewText("Plus", _hint, 30f, TextAlignmentOptions.Center);
            _hintPlus.text = "+";
            _hintPartner = NewImage("Partner", _hint, hintIconSize);
            _hintEquals = NewText("Equals", _hint, 30f, TextAlignmentOptions.Center);
            _hintEquals.text = "=";
            _hintCombo = NewImage("Combo", _hint, hintIconSize);
            _hintName = NewText("Name", _hint, 26f, TextAlignmentOptions.Left);
            var items = new (RectTransform rect, float x, float width)[]
            {
                (_hintPlus.rectTransform, -142f, 22f), ((RectTransform)_hintPartner.transform, -108f, hintIconSize),
                (_hintEquals.rectTransform, -74f, 22f), ((RectTransform)_hintCombo.transform, -40f, hintIconSize),
                (_hintName.rectTransform, 68f, 150f),
            };
            foreach (var (rect, x, width) in items)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(width, hintIconSize);
                rect.anchoredPosition = new Vector2(x, y);
            }
            _recipe.gameObject.SetActive(false);
            _hint.gameObject.SetActive(false);
        }

        /// <summary>Комбо — «слияние»: картинки двух частей со знаком «+», под ними картинка самого комбо.</summary>
        void ShowRecipe(UpgradeCard card)
        {
            _card = card;
            BuildHintUi();
            SaveLayout();
            bool combo = card.IsCombo && card.requires.Length >= 2;
            _recipe.gameObject.SetActive(combo);
            if (!combo)
                return;
            _recipeA.sprite = card.requires[0] ? card.requires[0].icon : null;
            _recipeB.sprite = card.requires[1] ? card.requires[1].icon : null;
            _recipeA.color = _recipeB.color = Color.white;
            var iconRect = icon.rectTransform;
            iconRect.sizeDelta = new Vector2(comboIconSize, comboIconSize);
            iconRect.anchoredPosition = _iconPosition + Vector2.down * comboIconDrop;
        }

        /// <summary>
        /// Мячи в углу: с какими мячами карточка складывается в комбо, а перечёркнутые — с какими спит.
        /// У карточки мяча — карточки, с которыми он даёт комбо.
        /// </summary>
        void ShowBalls(UpgradeCard card)
        {
            var window = Window;
            int shown = 0;
            if (card is BallTypeCard)
            {
                CardHints.CombosWith(card, s_list);
                foreach (var combo in s_list)
                    if (CardHints.Partner(combo, card) is { } partner)
                        SetBallIcon(window, shown++, partner.icon, false);
            }
            else if (!card.IsCombo)
            {
                CardHints.GoodBalls(card, s_balls);
                foreach (var ball in s_balls)
                    SetBallIcon(window, shown++, ball.icon, false);
                CardHints.SleepyBalls(card, s_balls);
                foreach (var ball in s_balls)
                    SetBallIcon(window, shown++, ball.icon, true);
            }
            for (int i = shown; i < _ballIcons.Count; i++)
                _ballIcons[i].gameObject.SetActive(false);
        }

        void SetBallIcon(RectTransform window, int index, Sprite sprite, bool asleep)
        {
            if (index >= 4)
                return;
            while (_ballIcons.Count <= index)
            {
                var created = NewImage("Ball_" + _ballIcons.Count, window, cornerBallSize);
                var rect = (RectTransform)created.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(8f + _ballIcons.Count * (cornerBallSize + 2f), -8f);
                var slash = NewImage("Slash", rect, cornerBallSize * 1.2f);
                var slashRect = (RectTransform)slash.transform;
                slashRect.sizeDelta = new Vector2(cornerBallSize * 1.25f, 5f);
                slashRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                slash.preserveAspect = false;
                _ballIcons.Add(created);
                _ballSlashes.Add(slash);
            }
            var image = _ballIcons[index];
            image.gameObject.SetActive(sprite != null);
            image.sprite = sprite;
            image.color = asleep ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
            _ballSlashes[index].color = sleepColor;
            _ballSlashes[index].gameObject.SetActive(asleep);
        }

        /// <summary>
        /// Строка внизу: «+ [напарник] = [комбо] Название». Если напарник уже есть — подсвечена: возьмёшь — соберёшь.
        /// </summary>
        public void ShowComboHint(PlayerCards owner)
        {
            HideComboHint();
            if (_card == null || _card.IsCombo || _card is BallTypeCard || _card.category == UpgradeCategory.Treat)
                return;
            var combo = CardHints.BestCombo(_card, owner, out bool ready);
            var partner = CardHints.Partner(combo, _card);
            if (combo == null || partner == null)
                return;
            _hint.gameObject.SetActive(true);
            _hintPartner.sprite = partner.icon;
            _hintCombo.sprite = combo.icon;
            _hintName.text = combo.title.GetLocalizedString();
            var color = ready ? readyColor : hintColor;
            _hintPlus.color = _hintEquals.color = _hintName.color = color;
            description.rectTransform.sizeDelta = new Vector2(_descriptionSize.x, _descriptionSize.y - (hintIconSize + 8f));
        }

        void HideComboHint()
        {
            if (_hint == null)
                return;
            _hint.gameObject.SetActive(false);
            if (_layoutSaved && !(_card is BallTypeCard))
                description.rectTransform.sizeDelta = _descriptionSize;
        }

        /// <summary>Тетрадка: комбо ещё не собрано — видно, из чего оно, но не название и не что делает.</summary>
        public void ShowLocked()
        {
            SetBlocked(true);
            title.text = Loc.Get("bestiary.locked.name");
            description.text = Loc.Get("notebook.combo.locked");
            icon.color = new Color(0f, 0f, 0f, 0.35f);
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

        /// <summary>Жёлтая плашка-предупреждение без серого: «уснут: Попрыгунчик» на карточке мяча.</summary>
        public void ShowWarning(string text)
        {
            SetBlocked(false);
            if (!pocketTag)
                return;
            pocketTag.SetActive(true);
            pocketTagText.text = text;
            pocketTagText.color = tagComboColor;
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
