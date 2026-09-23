using System;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Bouncer.UI
{
    /// <summary>
    /// Экраны поверх забега: заставка, пауза, настройки, «Выбит!» и «Двор наш!» — с кнопками для мыши,
    /// клавиатуры и геймпада. Какой экран показать, решает состояние <see cref="GameSession"/>; кнопки только
    /// зовут её методы. Настройки открываются с заставки и паузы, Esc / B возвращают назад.
    /// </summary>
    public sealed class RunScreens : MonoBehaviour
    {
        [Serializable]
        sealed class Screen
        {
            public CanvasGroup group;
            [Tooltip("Его выбирает клавиатура и геймпад, когда экран открывается")]
            public UnityEngine.UI.Selectable first;
            [NonSerialized] public bool Ready;
        }

        [SerializeField] Screen title;
        [SerializeField] Screen pause;
        [SerializeField] Screen settings;
        [SerializeField] Screen gameOver;
        [SerializeField] Screen victory;
        [SerializeField] SettingsScreen settingsScreen;
        [SerializeField] TMP_Text gameOverStats;
        [SerializeField] TMP_Text victoryStats;
        [SerializeField] float fadeSpeed = 6f;

        [Header("Кнопки")]
        [SerializeField] UnityEngine.UI.Button[] playButtons;
        [SerializeField] UnityEngine.UI.Button[] resumeButtons;
        [SerializeField] UnityEngine.UI.Button[] restartButtons;
        [SerializeField] UnityEngine.UI.Button[] menuButtons;
        [SerializeField] UnityEngine.UI.Button[] quitButtons;
        [SerializeField] UnityEngine.UI.Button[] settingsButtons;
        [SerializeField] UnityEngine.UI.Button[] backButtons;

        PlayerProgression _progression;
        /// <summary>Кнопка «Настройки», на которую вернуться после настроек.</summary>
        GameObject _returnTo;

        void Awake()
        {
            Bind(playButtons, () => Session(s => s.StartRun()));
            Bind(resumeButtons, () => Session(s => s.TogglePause()));
            Bind(restartButtons, () => Session(s => s.Restart()));
            Bind(menuButtons, () => Session(s => s.ToTitle()));
            Bind(quitButtons, Quit);
            Bind(settingsButtons, OpenSettings);
            Bind(backButtons, CloseSettings);
        }

        void Update()
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            if (_progression == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                    player.TryGetComponent(out _progression);
            }

            var state = session.State;
            bool titleOrPause = state == SessionState.Title || (state == SessionState.Playing && GameFeel.Paused);
            if (session.OverlayOpen && !titleOrPause)
                CloseSettings();
            bool settingsOpen = session.OverlayOpen;
            Show(title, state == SessionState.Title && !settingsOpen, true);
            Show(pause, state == SessionState.Playing && GameFeel.Paused && !settingsOpen, true);
            Show(settings, settingsOpen, true);
            // Кнопки конца забега оживают не сразу — чтобы случайное нажатие не перезапустило игру.
            Show(gameOver, state == SessionState.GameOver, session.CanRestart);
            Show(victory, state == SessionState.Victory, session.CanRestart);

            if (state == SessionState.GameOver)
                gameOverStats.text = Stats(session, "продержался");
            else if (state == SessionState.Victory)
                victoryStats.text = Stats(session, "двор взят за");
        }

        // Esc и B закрывают настройки. Здесь, а не в Update: тот же Esc — это и кнопка паузы, и к этому
        // моменту игрок его уже прочитал, а GameSession.OverlayOpen не дал снять паузу.
        void LateUpdate()
        {
            var session = GameSession.Instance;
            if (session != null && session.OverlayOpen && CancelPressed())
                CloseSettings();
        }

        void Show(Screen screen, bool visible, bool ready)
        {
            if (screen.group == null)
                return;
            screen.group.alpha = Mathf.MoveTowards(screen.group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
            bool active = visible && ready;
            screen.group.interactable = active;
            screen.group.blocksRaycasts = active;
            if (active && !screen.Ready && EventSystem.current != null)
            {
                // Из настроек возвращаемся на кнопку «Настройки», а не на первую кнопку экрана.
                var target = screen.first != null ? screen.first.gameObject : null;
                if (_returnTo != null && _returnTo.transform.IsChildOf(screen.group.transform))
                {
                    target = _returnTo;
                    _returnTo = null;
                }
                if (target != null)
                    EventSystem.current.SetSelectedGameObject(target);
            }
            screen.Ready = active;
        }

        void OpenSettings()
        {
            var session = GameSession.Instance;
            if (session == null || session.OverlayOpen || settingsScreen == null)
                return;
            _returnTo = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            settingsScreen.Refresh();
            session.OverlayOpen = true;
        }

        void CloseSettings()
        {
            var session = GameSession.Instance;
            if (session == null || !session.OverlayOpen)
                return;
            settingsScreen.Save();
            session.OverlayOpen = false;
        }

        static bool CancelPressed()
        {
            var module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
            var cancel = module != null && module.cancel != null ? module.cancel.action : null;
            return cancel != null && cancel.WasPressedThisFrame();
        }

        string Stats(GameSession session, string timeLabel)
        {
            int seconds = Mathf.FloorToInt(session.SurvivalTime);
            string level = _progression != null ? $"    уровень: {_progression.Level}" : string.Empty;
            return $"{timeLabel} {seconds / 60}:{seconds % 60:00}    выбито: {session.Kills}{level}";
        }

        static void Session(Action<GameSession> action)
        {
            if (GameSession.Instance != null)
                action(GameSession.Instance);
        }

        static void Bind(UnityEngine.UI.Button[] buttons, UnityEngine.Events.UnityAction action)
        {
            if (buttons == null)
                return;
            foreach (var button in buttons)
                if (button != null)
                    button.onClick.AddListener(action);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
