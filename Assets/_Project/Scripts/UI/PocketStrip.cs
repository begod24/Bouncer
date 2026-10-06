using System;
using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Bouncer.UI
{
    /// <summary>
    /// Ряд карманов игрока: вкладыши по порядку взятия, пустые карманы пунктиром и подпись «карманы 4/6».
    /// Только показывает; новая карточка в кармане подпрыгивает. Таких рядов три: на HUD (в бою, прячется
    /// под экранами карточек и паузой), на экране выбора и в ларьке.
    /// Ряд на HUD в коопе показывает слева от карманов ещё и рюкзак: неоткрытые портфели и кнопку, которая их
    /// открывает («R — открыть»).
    /// </summary>
    public sealed class PocketStrip : MonoBehaviour
    {
        [SerializeField] PocketCardMini miniPrefab;
        [Tooltip("Куда ставить вкладыши (слева направо от своей точки)")]
        [SerializeField] RectTransform row;
        [SerializeField] TMP_Text caption;
        [Tooltip("Строка таблицы UI для подписи: {0} — занято карманов, {1} — сколько всего")]
        [SerializeField] string captionKey = "pockets.strip";
        [Tooltip("Размер вкладыша и шаг между ними")]
        [SerializeField] float size = 60f;
        [SerializeField] float spacing = 68f;
        [Tooltip("Ряд растёт влево от точки row (HUD в правом углу), иначе — от центра")]
        [SerializeField] bool alignRight;
        [Tooltip("Подписывать вкладыши названием")]
        [SerializeField] bool showNames;
        [Tooltip("Подпись под вкладышем: размер шрифта и ширина на экране")]
        [SerializeField] float nameSize = 20f;
        [SerializeField] float nameWidth = 110f;
        [Tooltip("HUD: прятать под экранами карточек, ларьком и паузой")]
        [SerializeField] CanvasGroup hideGroup;
        [SerializeField] Color captionColor = new(0.96f, 0.95f, 0.92f, 0.85f);
        [SerializeField] Color fullColor = new(1f, 0.45f, 0.38f);

        [Header("Рюкзак (кооп)")]
        [Tooltip("Показывать слева от карманов неоткрытые портфели из рюкзака")]
        [SerializeField] bool showBackpack;
        [SerializeField] Sprite backpackIcon;
        [SerializeField] Color backpackColor = new(0.62f, 0.4f, 0.24f);
        [SerializeField] string backpackKeyboardKey = "R";
        [SerializeField] string backpackGamepadKey = "Y";

        readonly List<PocketCardMini> _minis = new();
        readonly List<UpgradeCard> _shown = new();
        readonly List<int> _shownStacks = new();
        readonly List<UpgradeCard> _previous = new();
        PlayerCards _cards;
        int _shownMax = -1;
        bool _dirty = true;
        bool _filled;
        PocketCardMini _backpack;
        int _backpackShown;
        int _backpackSlots = -1;
        int _backpackMode = -1;

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        void OnLocaleChanged(Locale locale)
        {
            _dirty = true;
            _backpackMode = -1;
        }

        void Update()
        {
            if (hideGroup)
                hideGroup.alpha = Mathf.MoveTowards(hideGroup.alpha, Hidden ? 0f : 1f, Time.unscaledDeltaTime * 6f);
            if (_cards == null)
            {
                var player = Players.Local;
                if (player == null || !player.TryGetComponent(out _cards))
                    return;
            }
            var pockets = _cards.PocketCards;
            if (_dirty || Changed(pockets))
                Show(pockets, _cards.StacksOf, _cards.MaxPockets);
            if (showBackpack)
                UpdateBackpack();
        }

        /// <summary>Рюкзак слева от карманов: портфель, сколько их и какой кнопкой открыть. Пусто — не виден.</summary>
        void UpdateBackpack()
        {
            int count = _cards.Backpack;
            int slots = Mathf.Max(_shownMax, _shown.Count);
            int mode = _cards.Player.LastIntent.UsingGamepad ? 1 : 0;
            if (count == _backpackShown && slots == _backpackSlots && mode == _backpackMode)
                return;
            if (count <= 0)
            {
                _backpackShown = 0;
                if (_backpack)
                    _backpack.gameObject.SetActive(false);
                return;
            }
            if (_backpack == null)
            {
                _backpack = CreateMini();
                _backpack.name = "Backpack";
            }
            float scale = size / Mathf.Max(1f, ((RectTransform)miniPrefab.transform).rect.width);
            var rect = (RectTransform)_backpack.transform;
            rect.localScale = Vector3.one * scale;
            // Через промежуток слева от первого кармана.
            float first = alignRight ? -(slots - 1) * spacing - size * 0.5f : -(slots - 1) * 0.5f * spacing;
            rect.anchoredPosition = new Vector2(first - spacing * 1.35f, 0f);
            _backpack.gameObject.SetActive(true);
            _backpack.ShowItem(backpackIcon, backpackColor, count > 1 ? "×" + count : string.Empty);
            _backpack.SetLabelStyle(nameSize / scale, nameWidth * 1.2f / scale, wrap: false);
            _backpack.SetLabel(Loc.Format("hud.backpack", mode == 1 ? backpackGamepadKey : backpackKeyboardKey));
            if (count > _backpackShown)
                _backpack.Pop();
            _backpackShown = count;
            _backpackSlots = slots;
            _backpackMode = mode;
        }

        static bool Hidden
        {
            get
            {
                var session = GameSession.Instance;
                if (session == null)
                    return false;
                return GameFeel.Paused || session.State is not (SessionState.Playing or SessionState.Cleared)
                                       || session.Menu != LocalMenu.None
                                       || (PocketsPanel.Instance != null && PocketsPanel.Instance.IsOpen);
            }
        }

        bool Changed(IReadOnlyList<UpgradeCard> pockets)
        {
            if (pockets.Count != _shown.Count || _cards.MaxPockets != _shownMax)
                return true;
            for (int i = 0; i < pockets.Count; i++)
                if (pockets[i] != _shown[i] || _cards.StacksOf(pockets[i]) != _shownStacks[i])
                    return true;
            return false;
        }

        /// <summary>Показать карманы: вкладыши по порядку, остальное до max — пустые. Новые подпрыгивают.</summary>
        public void Show(IReadOnlyList<UpgradeCard> pockets, Func<UpgradeCard, int> stacksOf, int max)
        {
            _dirty = false;
            _previous.Clear();
            _previous.AddRange(_shown);
            _shown.Clear();
            _shownStacks.Clear();
            for (int i = 0; i < pockets.Count; i++)
            {
                _shown.Add(pockets[i]);
                _shownStacks.Add(stacksOf(pockets[i]));
            }
            _shownMax = max;

            int count = Mathf.Max(max, _shown.Count);
            while (_minis.Count < count)
                _minis.Add(CreateMini());
            float scale = size / Mathf.Max(1f, ((RectTransform)miniPrefab.transform).rect.width);
            for (int i = 0; i < _minis.Count; i++)
            {
                var mini = _minis[i];
                bool visible = i < count;
                mini.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var rect = (RectTransform)mini.transform;
                rect.localScale = Vector3.one * scale;
                // Справа: последний вкладыш правым краем к точке ряда; иначе ряд по центру.
                float x = alignRight ? -(count - 1 - i) * spacing - size * 0.5f : (i - (count - 1) * 0.5f) * spacing;
                rect.anchoredPosition = new Vector2(x, 0f);
                if (i < _shown.Count)
                {
                    var card = _shown[i];
                    mini.Show(card, _shownStacks[i]);
                    if (showNames)
                    {
                        mini.SetLabelStyle(nameSize / scale, nameWidth / scale, wrap: false);
                        mini.SetLabel(card.title.GetLocalizedString());
                    }
                    // Новая карточка в кармане (не при первом показе и не после выброса соседней) подпрыгивает.
                    if (_filled && !_previous.Contains(card))
                        mini.Pop();
                }
                else
                {
                    mini.ShowEmpty();
                }
            }
            _filled = true;

            if (caption)
            {
                bool full = _shown.Count >= max;
                caption.text = Loc.Format(captionKey, _shown.Count, max);
                caption.color = full ? fullColor : captionColor;
            }
        }

        PocketCardMini CreateMini()
        {
            var mini = Instantiate(miniPrefab, row);
            mini.name = $"Pocket_{_minis.Count + 1}";
            var rect = (RectTransform)mini.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(alignRight ? 1f : 0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            mini.SetInteractable(false);
            // Ряд только показывает: вкладыши не перехватывают мышь.
            foreach (var graphic in mini.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.raycastTarget = false;
            return mini;
        }
    }
}
