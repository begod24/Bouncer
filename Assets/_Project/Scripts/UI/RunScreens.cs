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
    /// Экраны поверх прогулки: заставка, выбор ребёнка, пауза, настройки, авторы, «Выбит!» и победа — с кнопками
    /// для мыши, клавиатуры и геймпада. Какой экран показать, решает состояние <see cref="GameSession"/>; кнопки только
    /// зовут её методы. «Играть» открывает выбор ребёнка, выбор начинает прогулку. Настройки открываются с заставки
    /// и паузы, авторы и выбор — с заставки; Esc / B возвращают назад.
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

        /// <summary>Экран поверх заставки или паузы.</summary>
        enum Overlay
        {
            None,
            Settings,
            Credits,
            Kids,
            Records,
        }

        [SerializeField] Screen title;
        [SerializeField] Screen pause;
        [SerializeField] Screen settings;
        [SerializeField] Screen credits;
        [Tooltip("«Кто выходит гулять?» — после «Играть»")]
        [SerializeField] Screen kidSelect;
        [SerializeField] Screen gameOver;
        [SerializeField] Screen victory;
        [Tooltip("Рекорды: лучшие победы на каждом уровне опасности")]
        [SerializeField] Screen records;
        [SerializeField] TMP_Text recordsText;
        [SerializeField] SettingsScreen settingsScreen;
        [SerializeField] KidSelectScreen kidSelectScreen;
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
        [SerializeField] UnityEngine.UI.Button[] creditsButtons;
        [Tooltip("«Назад» на экранах настроек и авторов")]
        [SerializeField] UnityEngine.UI.Button[] backButtons;
        [SerializeField] UnityEngine.UI.Button[] recordsButtons;

        PlayerCards _cards;
        /// <summary>Итог последней победы: новый рекорд, лучшее время уровня, открыт ли следующий.</summary>
        bool _newRecord;
        float _bestTime;
        int _unlockedDanger;
        Overlay _overlay;
        /// <summary>Кнопка, открывшая экран поверх, — на неё вернуться.</summary>
        GameObject _returnTo;

        void Awake()
        {
            Bind(playButtons, Play);
            if (kidSelectScreen != null)
                kidSelectScreen.Chosen += OnKidChosen;
            Bind(resumeButtons, () => Session(s => s.TogglePause()));
            Bind(restartButtons, () => Session(s => s.Restart()));
            Bind(menuButtons, () => Session(s => s.ToTitle()));
            Bind(quitButtons, Quit);
            Bind(settingsButtons, () => Open(Overlay.Settings));
            Bind(creditsButtons, () => Open(Overlay.Credits));
            Bind(backButtons, CloseOverlay);
            Bind(recordsButtons, () => Open(Overlay.Records));
        }

        void OnEnable() => GameEvents.RunFinished += OnRunFinished;

        void OnDisable() => GameEvents.RunFinished -= OnRunFinished;

        /// <summary>Победа: запомнить рекорд (время, опасность, ребёнок, карманы) и открыть следующую опасность.</summary>
        void OnRunFinished(bool victory)
        {
            _newRecord = false;
            _unlockedDanger = 0;
            if (!victory)
                return;
            int level = Danger.Level;
            var record = new RunRecord
            {
                time = RunState.RunClock,
                danger = level,
                kid = KidName(GameSettings.Kid),
                cards = CardNames(),
            };
            _newRecord = RunRecords.Add(record);
            var best = RunRecords.Best(level);
            _bestTime = best != null ? best.time : record.time;
            if (Danger.UnlockAfterWin(level))
                _unlockedDanger = level + 1;
        }

        string KidName(int index)
        {
            var roster = kidSelectScreen != null ? kidSelectScreen.Roster : null;
            var kid = roster != null ? roster[index] : null;
            return kid != null ? kid.name : string.Empty;
        }

        string[] CardNames()
        {
            if (_cards == null)
                return Array.Empty<string>();
            var pockets = _cards.PocketCards;
            var names = new string[pockets.Count];
            for (int i = 0; i < pockets.Count; i++)
                names[i] = pockets[i] ? pockets[i].name : string.Empty;
            return names;
        }

        /// <summary>Рекорды: лучшая победа на каждом уровне опасности, от высшего.</summary>
        void RefreshRecords()
        {
            if (recordsText == null)
                return;
            var lines = new System.Text.StringBuilder();
            var roster = kidSelectScreen != null ? kidSelectScreen.Roster : null;
            for (int level = Danger.Max; level >= 1; level--)
            {
                var best = RunRecords.Best(level);
                if (best == null)
                    continue;
                string kid = best.kid;
                if (roster != null)
                    for (int i = 0; i < roster.Count; i++)
                        if (roster[i] != null && roster[i].name == best.kid && !roster[i].displayName.IsEmpty)
                            kid = roster[i].displayName.GetLocalizedString();
                lines.AppendLine(Loc.Format("records.line", level, RunRecords.FormatTime(best.time), kid,
                    best.cards != null ? best.cards.Length : 0));
            }
            recordsText.text = lines.Length > 0 ? lines.ToString() : Loc.Get("records.empty");
        }

        void Update()
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            if (_cards == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                    player.TryGetComponent(out _cards);
            }

            var state = session.State;
            bool paused = state is (SessionState.Playing or SessionState.Cleared) && GameFeel.Paused;
            bool titleOrPause = state == SessionState.Title || paused;
            if (_overlay != Overlay.None && !titleOrPause)
                CloseOverlay();
            bool noOverlay = _overlay == Overlay.None;
            Show(title, state == SessionState.Title && noOverlay, true);
            Show(pause, paused && noOverlay, true);
            Show(settings, _overlay == Overlay.Settings, true);
            Show(credits, _overlay == Overlay.Credits, true);
            Show(kidSelect, _overlay == Overlay.Kids, true);
            Show(records, _overlay == Overlay.Records, true);
            // Кнопки конца забега оживают не сразу — чтобы случайное нажатие не перезапустило игру.
            Show(gameOver, state == SessionState.GameOver, session.CanRestart);
            Show(victory, state == SessionState.Victory, session.CanRestart);

            if (state == SessionState.GameOver)
                gameOverStats.text = Stats(session, "gameover.stats");
            else if (state == SessionState.Victory)
                victoryStats.text = Stats(session, "victory.stats");
        }

        // Esc и B закрывают экран поверх. Здесь, а не в Update: тот же Esc — это и кнопка паузы, и к этому
        // моменту игрок его уже прочитал, а GameSession.OverlayOpen не дал снять паузу.
        void LateUpdate()
        {
            if (_overlay != Overlay.None && CancelPressed())
                CloseOverlay();
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
                // С экрана поверх возвращаемся на открывшую его кнопку, а не на первую кнопку экрана.
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

        void Open(Overlay overlay)
        {
            var session = GameSession.Instance;
            if (session == null || _overlay != Overlay.None)
                return;
            if (overlay == Overlay.Settings)
            {
                if (settingsScreen == null)
                    return;
                settingsScreen.Refresh();
            }
            else if (overlay == Overlay.Kids)
            {
                kidSelectScreen.Open();
                kidSelect.first = kidSelectScreen.FirstButton;
            }
            else if (overlay == Overlay.Records)
            {
                if (records.group == null)
                    return;
                RefreshRecords();
            }
            _returnTo = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _overlay = overlay;
            session.OverlayOpen = true;
        }

        void CloseOverlay()
        {
            if (_overlay == Overlay.None)
                return;
            if (_overlay == Overlay.Settings)
                settingsScreen.Save();
            else if (_overlay == Overlay.Kids)
                kidSelectScreen.Close();
            _overlay = Overlay.None;
            if (GameSession.Instance != null)
                GameSession.Instance.OverlayOpen = false;
        }

        /// <summary>«Играть»: сначала выбрать, с кем гулять (если экрана выбора нет — сразу в прогулку).</summary>
        void Play()
        {
            if (kidSelectScreen != null)
                Open(Overlay.Kids);
            else
                Session(s => s.StartRun());
        }

        void OnKidChosen(int kid)
        {
            CloseOverlay();
            Session(s => s.StartRun());
        }

        static bool CancelPressed()
        {
            var module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
            var cancel = module != null && module.cancel != null ? module.cancel.action : null;
            return cancel != null && cancel.WasPressedThisFrame();
        }

        /// <summary>
        /// Итоги всей прогулки: время (часы прогулки), выбитые, карточки, заработанные монетки; у победы — рекорд
        /// и открытая опасность.
        /// </summary>
        string Stats(GameSession session, string key)
        {
            string time = RunRecords.FormatTime(RunState.RunClock > 0f ? RunState.RunClock : session.RunTime);
            int cards = _cards != null ? _cards.Count : 0;
            string stats = Loc.Format(key, time, session.RunKills, cards, RunState.CoinsEarned);
            if (session.State != SessionState.Victory)
                return stats;
            stats += "\n" + (_newRecord ? Loc.Format("victory.record", Danger.Level)
                : Loc.Format("victory.best", Danger.Level, RunRecords.FormatTime(_bestTime)));
            if (_unlockedDanger > 0)
                stats += "\n" + Loc.Format("victory.unlocked", _unlockedDanger);
            return stats;
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
