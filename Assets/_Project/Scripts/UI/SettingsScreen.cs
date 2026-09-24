using System;
using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Bouncer.UI
{
    /// <summary>
    /// Экран «Настройки» с вкладками: игра (тряска, прицел), видео (окно, разрешение, синхронизация),
    /// звук (громкости) и язык. Всё меняется сразу и пишется в <see cref="GameSettings"/>; на диск — при
    /// закрытии (<see cref="Save"/>). Язык выбирает и запоминает пакет Localization. Вкладки листаются
    /// стрелками на ряду вкладок, Q / E или LB / RB откуда угодно и кликом мыши. Открывает и закрывает экран
    /// <see cref="RunScreens"/>. Разрешение и режим окна в редакторе не меняются — только в билде.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SettingsScreen : MonoBehaviour
    {
        [Serializable]
        sealed class Page
        {
            public GameObject panel;
            [Tooltip("Пункты вкладки сверху вниз — по ним ходят стрелки и геймпад")]
            public UnityEngine.UI.Selectable[] items;
        }

        [Header("Вкладки: игра, видео, звук, язык")]
        [SerializeField] ChalkTabs tabs;
        [SerializeField] Page[] pages;
        [SerializeField] UnityEngine.UI.Selectable backButton;

        [Header("Игра")]
        [SerializeField] UnityEngine.UI.Slider screenShake;
        [SerializeField] ChalkStepper autoAimMouse;
        [SerializeField] ChalkStepper autoAimGamepad;
        [SerializeField] ChalkStepper aimLine;

        [Header("Видео")]
        [SerializeField] ChalkStepper displayMode;
        [SerializeField] ChalkStepper resolution;
        [SerializeField] ChalkStepper vSync;

        [Header("Звук")]
        [SerializeField] UnityEngine.UI.Slider masterVolume;
        [SerializeField] UnityEngine.UI.Slider musicVolume;
        [SerializeField] UnityEngine.UI.Slider sfxVolume;

        [Header("Язык")]
        [SerializeField] ChalkStepper language;

        [Tooltip("Разрешение применяется через столько секунд после выбора: пока листаешь список, окно не прыгает")]
        [SerializeField, Min(0f)] float resolutionDelay = 0.7f;

        readonly List<Vector2Int> _resolutions = new();
        readonly List<Locale> _locales = new();
        readonly List<UnityEngine.UI.Selectable> _navigation = new();
        CanvasGroup _group;
        float _applyScreenAt = -1f;

        static string[] OnOff => new[] { Loc.Get("settings.off"), Loc.Get("settings.on") };
        static string[] DisplayModes => new[] { Loc.Get("settings.windowed"), Loc.Get("settings.fullscreen") };

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();

            screenShake.onValueChanged.AddListener(value => GameSettings.ScreenShake = value);
            autoAimMouse.Changed += index => GameSettings.AutoAimMouse = index == 1;
            autoAimGamepad.Changed += index => GameSettings.AutoAimGamepad = index == 1;
            aimLine.Changed += index => GameSettings.ShowAimPreview = index == 1;

            displayMode.Changed += _ => OnDisplayModeChanged();
            resolution.Changed += _ => _applyScreenAt = Time.unscaledTime + resolutionDelay;
            vSync.Changed += index =>
            {
                GameSettings.VSync = index == 1;
                GameSettings.Apply();
            };

            masterVolume.onValueChanged.AddListener(value => GameSettings.MasterVolume = value);
            musicVolume.onValueChanged.AddListener(value => GameSettings.MusicVolume = value);
            sfxVolume.onValueChanged.AddListener(value => GameSettings.SfxVolume = value);

            language.Changed += index => LocalizationSettings.SelectedLocale = _locales[index];
            tabs.Changed += ShowPage;
        }

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        /// <summary>Показать текущие значения — перед открытием экрана.</summary>
        public void Refresh()
        {
            screenShake.SetValueWithoutNotify(GameSettings.ScreenShake);
            var onOff = OnOff;
            autoAimMouse.SetOptions(onOff, GameSettings.AutoAimMouse ? 1 : 0);
            autoAimGamepad.SetOptions(onOff, GameSettings.AutoAimGamepad ? 1 : 0);
            aimLine.SetOptions(onOff, GameSettings.ShowAimPreview ? 1 : 0);

            displayMode.SetOptions(DisplayModes, Screen.fullScreenMode == FullScreenMode.Windowed ? 0 : 1);
            RefreshResolutions();
            vSync.SetOptions(onOff, GameSettings.VSync ? 1 : 0);

            masterVolume.SetValueWithoutNotify(GameSettings.MasterVolume);
            musicVolume.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfxVolume.SetValueWithoutNotify(GameSettings.SfxVolume);

            RefreshLanguages();
            ShowPage(tabs.Index);
            _applyScreenAt = -1f;
        }

        /// <summary>Экран закрывается: применить отложенное разрешение и записать настройки на диск.</summary>
        public void Save()
        {
            if (_applyScreenAt >= 0f)
                ApplyScreen();
            GameSettings.Save();
        }

        void Update()
        {
            if (_applyScreenAt >= 0f && Time.unscaledTime >= _applyScreenAt)
                ApplyScreen();
            if (!_group.interactable)
                return;
            int step = TabStepPressed();
            if (step != 0)
                tabs.Step(step);
        }

        // Надписи в префабе переводит LocalizeStringEvent, а подписи значений собираются здесь — заново,
        // с теми же выбранными значениями.
        void OnLocaleChanged(Locale locale)
        {
            var onOff = OnOff;
            autoAimMouse.SetOptions(onOff);
            autoAimGamepad.SetOptions(onOff);
            aimLine.SetOptions(onOff);
            vSync.SetOptions(onOff);
            displayMode.SetOptions(DisplayModes);
            RefreshLanguages();
        }

        void ShowPage(int index)
        {
            for (int i = 0; i < pages.Length; i++)
                pages[i].panel.SetActive(i == index);

            // Вверх-вниз по кругу: ряд вкладок → пункты вкладки → «Назад».
            var tabsItem = tabs.GetComponent<UnityEngine.UI.Selectable>();
            _navigation.Clear();
            _navigation.Add(tabsItem);
            _navigation.AddRange(pages[index].items);
            _navigation.Add(backButton);
            for (int i = 0; i < _navigation.Count; i++)
            {
                _navigation[i].navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnUp = _navigation[(i - 1 + _navigation.Count) % _navigation.Count],
                    selectOnDown = _navigation[(i + 1) % _navigation.Count],
                };
            }

            // Выбранный пункт остался на спрятанной вкладке — переходим на первый пункт новой.
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            if (selected != null && !selected.activeInHierarchy && pages[index].items.Length > 0)
                events.SetSelectedGameObject(pages[index].items[0].gameObject);
        }

        static int TabStepPressed()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if ((keyboard != null && keyboard.qKey.wasPressedThisFrame) || (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame))
                return -1;
            if ((keyboard != null && keyboard.eKey.wasPressedThisFrame) || (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame))
                return 1;
            return 0;
        }

        void RefreshLanguages()
        {
            _locales.Clear();
            _locales.AddRange(LocalizationSettings.AvailableLocales.Locales);
            var names = new List<string>(_locales.Count);
            foreach (var locale in _locales)
                names.Add(NativeName(locale));
            language.SetOptions(names, Mathf.Max(0, _locales.IndexOf(LocalizationSettings.SelectedLocale)));
        }

        /// <summary>Язык называется на самом себе: English, Русский.</summary>
        static string NativeName(Locale locale)
        {
            var culture = locale.Identifier.CultureInfo;
            string name = culture != null ? culture.NativeName : locale.LocaleName;
            return name.Length > 0 ? char.ToUpperInvariant(name[0]) + name.Substring(1) : name;
        }

        void RefreshResolutions()
        {
            _resolutions.Clear();
            foreach (var mode in Screen.resolutions)
            {
                var size = new Vector2Int(mode.width, mode.height);
                if (size.x >= 800 && size.y >= 600 && !_resolutions.Contains(size))
                    _resolutions.Add(size);
            }
            // Окно могли растянуть вручную — такого размера нет в списке монитора.
            var current = new Vector2Int(Screen.width, Screen.height);
            if (!_resolutions.Contains(current))
                _resolutions.Add(current);
            _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            var labels = new List<string>(_resolutions.Count);
            foreach (var size in _resolutions)
                labels.Add($"{size.x}×{size.y}");
            resolution.SetOptions(labels, _resolutions.IndexOf(current));
        }

        void OnDisplayModeChanged()
        {
            // Окно размером с монитор не влезет вместе с рамкой — берём самое большое разрешение, которое влезает.
            if (displayMode.Index == 0)
            {
                var desktop = Screen.currentResolution;
                var size = _resolutions[resolution.Index];
                if (size.x > desktop.width * 0.9f || size.y > desktop.height * 0.9f)
                {
                    int fit = _resolutions.FindLastIndex(s => s.x <= desktop.width * 0.9f && s.y <= desktop.height * 0.9f);
                    if (fit >= 0)
                        resolution.SetIndex(fit);
                }
            }
            ApplyScreen();
        }

        void ApplyScreen()
        {
            _applyScreenAt = -1f;
            var size = _resolutions[resolution.Index];
            var mode = displayMode.Index == 0 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            if (size.x != Screen.width || size.y != Screen.height || mode != Screen.fullScreenMode)
                Screen.SetResolution(size.x, size.y, mode);
        }
    }
}
