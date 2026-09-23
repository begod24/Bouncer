using System;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Bouncer.UI
{
    /// <summary>
    /// Экраны поверх забега: заставка, пауза, «Выбит!» и «Двор наш!» — с кнопками для мыши, клавиатуры и геймпада.
    /// Какой экран показать, решает состояние <see cref="GameSession"/>; кнопки только зовут её методы.
    /// </summary>
    public sealed class RunScreens : MonoBehaviour
    {
        [Serializable]
        sealed class Screen
        {
            public CanvasGroup group;
            [Tooltip("Её выбирает клавиатура и геймпад, когда экран открывается")]
            public UnityEngine.UI.Button first;
            [NonSerialized] public bool Ready;
        }

        [SerializeField] Screen title;
        [SerializeField] Screen pause;
        [SerializeField] Screen gameOver;
        [SerializeField] Screen victory;
        [SerializeField] TMP_Text gameOverStats;
        [SerializeField] TMP_Text victoryStats;
        [SerializeField] float fadeSpeed = 6f;

        [Header("Кнопки")]
        [SerializeField] UnityEngine.UI.Button[] playButtons;
        [SerializeField] UnityEngine.UI.Button[] resumeButtons;
        [SerializeField] UnityEngine.UI.Button[] restartButtons;
        [SerializeField] UnityEngine.UI.Button[] menuButtons;
        [SerializeField] UnityEngine.UI.Button[] quitButtons;

        PlayerProgression _progression;

        void Awake()
        {
            Bind(playButtons, () => Session(s => s.StartRun()));
            Bind(resumeButtons, () => Session(s => s.TogglePause()));
            Bind(restartButtons, () => Session(s => s.Restart()));
            Bind(menuButtons, () => Session(s => s.ToTitle()));
            Bind(quitButtons, Quit);
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
            Show(title, state == SessionState.Title, true);
            Show(pause, state == SessionState.Playing && GameFeel.Paused, true);
            // Кнопки конца забега оживают не сразу — чтобы случайное нажатие не перезапустило игру.
            Show(gameOver, state == SessionState.GameOver, session.CanRestart);
            Show(victory, state == SessionState.Victory, session.CanRestart);

            if (state == SessionState.GameOver)
                gameOverStats.text = Stats(session, "продержался");
            else if (state == SessionState.Victory)
                victoryStats.text = Stats(session, "двор взят за");
        }

        void Show(Screen screen, bool visible, bool ready)
        {
            if (screen.group == null)
                return;
            screen.group.alpha = Mathf.MoveTowards(screen.group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
            bool active = visible && ready;
            screen.group.interactable = active;
            screen.group.blocksRaycasts = active;
            if (active && !screen.Ready && screen.first != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(screen.first.gameObject);
            screen.Ready = active;
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
