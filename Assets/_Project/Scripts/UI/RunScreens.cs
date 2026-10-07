using System;
using Bouncer.Core;
using Bouncer.Net;
using Bouncer.Player;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Bouncer.UI
{
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

        enum Overlay
        {
            None,
            Settings,
            Kids,
            Pockets,
            Notebook,
            TutorialAsk,
            Mode,
            Online,
            Lobby,
        }

        [SerializeField] Screen title;
        [SerializeField] Screen pause;
        [SerializeField] Screen settings;
        [Tooltip("«Кто выходит гулять?» — после «Играть»")]
        [SerializeField] Screen kidSelect;
        [SerializeField] Screen gameOver;
        [SerializeField] Screen victory;
        [Tooltip("Тетрадка: бестиарий, рекорды, «Как играть»")]
        [SerializeField] Screen notebook;
        [SerializeField] NotebookScreen notebookScreen;
        [Tooltip("«новое!» на кнопке «Тетрадка»")]
        [SerializeField] GameObject notebookNew;
        [Tooltip("«Пройти обучение?» — перед самой первой прогулкой")]
        [SerializeField] Screen tutorialAsk;
        [Tooltip("«Как гуляем?» — после «Играть»")]
        [SerializeField] Screen mode;
        [Tooltip("«Гуляем вместе»: создать комнату или войти по коду")]
        [SerializeField] Screen online;
        [SerializeField] OnlineScreen onlineScreen;
        [Tooltip("Комната")]
        [SerializeField] Screen lobby;
        [SerializeField] LobbyScreen lobbyScreen;
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
        [Tooltip("«Назад» на экранах поверх заставки")]
        [SerializeField] UnityEngine.UI.Button[] backButtons;
        [Tooltip("«Карманы» в паузе")]
        [SerializeField] UnityEngine.UI.Button[] pocketsButtons;
        [Tooltip("«Пройти тренировку» в тетрадке и «Да» в вопросе про обучение")]
        [SerializeField] UnityEngine.UI.Button[] tutorialButtons;
        [Tooltip("«Нет, сразу гулять» в вопросе про обучение")]
        [SerializeField] UnityEngine.UI.Button[] skipTutorialButtons;
        [SerializeField] UnityEngine.UI.Button[] notebookButtons;
        [Tooltip("«Одному» в «Как гуляем?»")]
        [SerializeField] UnityEngine.UI.Button[] soloButtons;
        [Tooltip("«Вместе» в «Как гуляем?»")]
        [SerializeField] UnityEngine.UI.Button[] coopButtons;

        PlayerCards _cards;
        bool _pocketsResume;
        bool _newRecord;
        float _bestTime;
        int _unlockedDanger;
        Overlay _overlay;
        GameObject _returnTo;
        NetMode _onlineMode;
        string _onlineNotice;

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
            Bind(backButtons, CloseOverlay);
            Bind(pocketsButtons, () => Open(Overlay.Pockets));
            Bind(tutorialButtons, StartTutorial);
            Bind(skipTutorialButtons, SkipTutorial);
            Bind(notebookButtons, () => Open(Overlay.Notebook));
            Bind(soloButtons, PlaySolo);
            Bind(coopButtons, () => OpenOnline(NetMode.Coop, null));
            if (onlineScreen != null)
                onlineScreen.RoomEntered += OnRoomEntered;
            if (lobbyScreen != null)
                lobbyScreen.Left += CloseOverlay;
        }

        void OnEnable() => GameEvents.RunFinished += OnRunFinished;

        void OnDisable() => GameEvents.RunFinished -= OnRunFinished;

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
                players = RunState.PlayerCount,
            };
            _newRecord = RunRecords.Add(record);
            var best = RunRecords.Best(level, record.IsCoop);
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

        void Update()
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            if (_cards == null)
            {
                var player = Players.Local;
                if (player != null)
                    player.TryGetComponent(out _cards);
            }
            UpdatePockets(session);

            var state = session.State;
            if (Tutorial.OpenKidsOnTitle && state == SessionState.Title && _overlay == Overlay.None && Time.timeSinceLevelLoad > 0.1f)
            {
                Tutorial.OpenKidsOnTitle = false;
                Open(Overlay.Kids);
            }
            UpdateOnline(state);
            if (restartButtons != null)
                foreach (var button in restartButtons)
                    if (button != null && button.gameObject.activeSelf == Online.Active)
                        button.gameObject.SetActive(!Online.Active);
            bool paused = state is (SessionState.Playing or SessionState.Cleared) && GameFeel.Paused;
            bool titleOrPause = state == SessionState.Title || paused;
            if (_overlay != Overlay.None && !titleOrPause)
                CloseOverlay();
            bool noOverlay = _overlay == Overlay.None;
            Show(title, state == SessionState.Title && noOverlay && !Online.Joining, true);
            Show(pause, paused && noOverlay, true);
            Show(settings, _overlay == Overlay.Settings, true);
            Show(kidSelect, _overlay == Overlay.Kids, true);
            Show(notebook, _overlay == Overlay.Notebook, true);
            Show(tutorialAsk, _overlay == Overlay.TutorialAsk, true);
            Show(mode, _overlay == Overlay.Mode, true);
            Show(online, _overlay == Overlay.Online, true);
            Show(lobby, _overlay == Overlay.Lobby, true);
            Show(gameOver, state == SessionState.GameOver, session.CanRestart);
            Show(victory, state == SessionState.Victory, session.CanRestart);

            if (notebookNew && state == SessionState.Title && noOverlay)
            {
                bool hasNew = notebookScreen != null && notebookScreen.HasNew;
                if (notebookNew.activeSelf != hasNew)
                    notebookNew.SetActive(hasNew);
            }

            if (state == SessionState.GameOver)
                gameOverStats.text = Stats(session, "gameover.stats");
            else if (state == SessionState.Victory)
                victoryStats.text = Stats(session, "victory.stats");
        }

        void UpdateOnline(SessionState state)
        {
            if (state != SessionState.Title || Online.Joining)
                return;
            bool inRoom = NetSession.Instance != null && NetSession.Instance.Status == NetStatus.InRoom;
            if (_overlay == Overlay.Lobby && !inRoom)
                CloseOverlay();
            if (!string.IsNullOrEmpty(NetSession.PendingNotice) && (_overlay == Overlay.None || _overlay == Overlay.Lobby)
                && Time.timeSinceLevelLoad > 0.1f)
            {
                string notice = NetSession.PendingNotice;
                NetSession.PendingNotice = null;
                OpenOnline(NetMode.Coop, notice);
                return;
            }
            if (inRoom && _overlay == Overlay.None && Time.timeSinceLevelLoad > 0.1f)
                Open(Overlay.Lobby);
        }

        void UpdatePockets(GameSession session)
        {
            var pockets = PocketsPanel.Instance;
            if (pockets == null)
                return;
            if (_overlay == Overlay.Pockets)
            {
                if (!pockets.IsOpen)
                    CloseOverlay();
                return;
            }
            if (_overlay != Overlay.None || pockets.IsOpen || pockets.ClosedFrame == Time.frameCount || !PocketsPanel.TogglePressed())
                return;
            if (session.State is (SessionState.Playing or SessionState.Cleared) && GameFeel.Paused)
            {
                Open(Overlay.Pockets);
                return;
            }
            if (!session.PlayerCanAct || _cards == null || _cards.Player.IsDead)
                return;
            session.TogglePause();
            if (!GameFeel.Paused)
                return;
            Open(Overlay.Pockets);
            _pocketsResume = _overlay == Overlay.Pockets;
            if (!_pocketsResume)
                session.TogglePause();
        }

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
            else if (overlay == Overlay.Notebook)
            {
                if (notebook.group == null || notebookScreen == null)
                    return;
                notebook.first = notebookScreen.Open();
            }
            else if (overlay == Overlay.TutorialAsk)
            {
                if (tutorialAsk.group == null)
                    return;
            }
            else if (overlay == Overlay.Mode)
            {
                if (mode.group == null)
                    return;
            }
            else if (overlay == Overlay.Online)
            {
                if (online.group == null || onlineScreen == null)
                    return;
                online.first = onlineScreen.Open(_onlineMode, _onlineNotice);
                _onlineNotice = null;
            }
            else if (overlay == Overlay.Lobby)
            {
                if (lobby.group == null || lobbyScreen == null)
                    return;
                lobby.first = lobbyScreen.Open();
            }
            else if (overlay == Overlay.Pockets)
            {
                var pockets = PocketsPanel.Instance;
                if (pockets == null)
                    return;
                pockets.OpenView();
                if (!pockets.IsOpen)
                    return;
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
            else if (_overlay == Overlay.Notebook && notebookScreen != null)
                notebookScreen.Close();
            else if (_overlay == Overlay.Pockets && PocketsPanel.Instance != null)
                PocketsPanel.Instance.Close();
            else if (_overlay == Overlay.Lobby)
            {
                lobbyScreen.Close();
                if (NetSession.Instance != null)
                    NetSession.Instance.Leave();
            }
            _overlay = Overlay.None;
            var session = GameSession.Instance;
            if (session != null)
            {
                session.OverlayOpen = false;
                if (_pocketsResume && GameFeel.Paused)
                {
                    _returnTo = null;
                    session.TogglePause();
                }
            }
            _pocketsResume = false;
        }

        void Play()
        {
            if (mode.group != null)
                Open(Overlay.Mode);
            else
                PlaySolo();
        }

        void PlaySolo()
        {
            CloseOverlay();
            if (Tutorial.ShouldAsk && tutorialAsk.group != null)
                Open(Overlay.TutorialAsk);
            else if (kidSelectScreen != null)
                Open(Overlay.Kids);
            else
                Session(s => s.StartRun());
        }

        void StartTutorial()
        {
            Tutorial.MarkOffered();
            CloseOverlay();
            Session(s => s.StartTutorial());
        }

        void SkipTutorial()
        {
            Tutorial.MarkOffered();
            CloseOverlay();
            PlaySolo();
        }

        void OpenOnline(NetMode netMode, string notice)
        {
            CloseOverlay();
            _onlineMode = netMode;
            _onlineNotice = notice;
            Open(Overlay.Online);
        }

        void OnRoomEntered()
        {
            CloseOverlay();
            Open(Overlay.Lobby);
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

        string Stats(GameSession session, string key)
        {
            string time = RunRecords.FormatTime(RunState.RunClock > 0f ? RunState.RunClock : session.RunTime);
            int cards = _cards != null ? _cards.Count : 0;
            string stats = Loc.Format(key, time, session.RunKills, cards, RunState.CoinsEarned);
            string backToRoom = Online.Active ? "\n" + Loc.Get("net.back.lobby") : string.Empty;
            if (session.State != SessionState.Victory)
                return stats + backToRoom;
            stats += "\n" + (_newRecord ? Loc.Format("victory.record", Danger.Level)
                : Loc.Format("victory.best", Danger.Level, RunRecords.FormatTime(_bestTime)));
            if (_unlockedDanger > 0)
                stats += "\n" + Loc.Format("victory.unlocked", _unlockedDanger);
            return stats + backToRoom;
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
