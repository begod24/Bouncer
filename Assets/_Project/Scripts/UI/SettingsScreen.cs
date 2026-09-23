using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.UI
{
    /// <summary>
    /// Экран «Настройки»: громкость, окно и разрешение, тряска и прицел. Всё меняется сразу и пишется
    /// в <see cref="GameSettings"/>; на диск — при закрытии (<see cref="Save"/>). Открывает и закрывает экран
    /// <see cref="RunScreens"/>. Разрешение и режим окна в редакторе не меняются — только в билде.
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour
    {
        static readonly string[] OnOff = { "выкл", "вкл" };
        static readonly string[] DisplayModes = { "в окне", "полный экран" };

        [Header("Звук")]
        [SerializeField] UnityEngine.UI.Slider masterVolume;
        [SerializeField] UnityEngine.UI.Slider musicVolume;
        [SerializeField] UnityEngine.UI.Slider sfxVolume;

        [Header("Экран")]
        [SerializeField] ChalkStepper displayMode;
        [SerializeField] ChalkStepper resolution;
        [SerializeField] ChalkStepper vSync;

        [Header("Игра")]
        [SerializeField] UnityEngine.UI.Slider screenShake;
        [SerializeField] ChalkStepper autoAimMouse;
        [SerializeField] ChalkStepper autoAimGamepad;
        [SerializeField] ChalkStepper aimLine;

        [Tooltip("Разрешение применяется через столько секунд после выбора: пока листаешь список, окно не прыгает")]
        [SerializeField, Min(0f)] float resolutionDelay = 0.7f;

        readonly List<Vector2Int> _resolutions = new();
        float _applyScreenAt = -1f;

        void Awake()
        {
            masterVolume.onValueChanged.AddListener(value => GameSettings.MasterVolume = value);
            musicVolume.onValueChanged.AddListener(value => GameSettings.MusicVolume = value);
            sfxVolume.onValueChanged.AddListener(value => GameSettings.SfxVolume = value);
            screenShake.onValueChanged.AddListener(value => GameSettings.ScreenShake = value);

            displayMode.Changed += _ => OnDisplayModeChanged();
            resolution.Changed += _ => _applyScreenAt = Time.unscaledTime + resolutionDelay;
            vSync.Changed += index =>
            {
                GameSettings.VSync = index == 1;
                GameSettings.Apply();
            };
            autoAimMouse.Changed += index => GameSettings.AutoAimMouse = index == 1;
            autoAimGamepad.Changed += index => GameSettings.AutoAimGamepad = index == 1;
            aimLine.Changed += index => GameSettings.ShowAimPreview = index == 1;
        }

        /// <summary>Показать текущие значения — перед открытием экрана.</summary>
        public void Refresh()
        {
            masterVolume.SetValueWithoutNotify(GameSettings.MasterVolume);
            musicVolume.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfxVolume.SetValueWithoutNotify(GameSettings.SfxVolume);
            screenShake.SetValueWithoutNotify(GameSettings.ScreenShake);

            displayMode.SetOptions(DisplayModes, Screen.fullScreenMode == FullScreenMode.Windowed ? 0 : 1);
            RefreshResolutions();
            vSync.SetOptions(OnOff, GameSettings.VSync ? 1 : 0);
            autoAimMouse.SetOptions(OnOff, GameSettings.AutoAimMouse ? 1 : 0);
            autoAimGamepad.SetOptions(OnOff, GameSettings.AutoAimGamepad ? 1 : 0);
            aimLine.SetOptions(OnOff, GameSettings.ShowAimPreview ? 1 : 0);
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
