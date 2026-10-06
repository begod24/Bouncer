using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Bouncer.Core
{
    public enum SessionState
    {
        /// <summary>Заставка: игрок стоит, враги не идут, прогулка начнётся по кнопке «Играть».</summary>
        Title,
        /// <summary>Бой на арене.</summary>
        Playing,
        /// <summary>Выбор карточки (старт, портфель, босс): время стоит.</summary>
        Upgrade,
        /// <summary>Арена пройдена: врагов нет, открыт ларёк, можно идти к стрелке на следующую арену.</summary>
        Cleared,
        /// <summary>Открыта витрина ларька: время стоит.</summary>
        Shop,
        GameOver,
        /// <summary>Прогулка пройдена целиком.</summary>
        Victory,
    }

    /// <summary>Экран поверх игры у своего игрока по сети: игра при этом идёт дальше (<see cref="GameSession.Menu"/>).</summary>
    public enum LocalMenu
    {
        None,
        /// <summary>Выбор карточки «1 из 3».</summary>
        Card,
        /// <summary>Витрина ларька.</summary>
        Shop,
    }

    /// <summary>
    /// Арена в прогулке: заставка, бой, выбор карточки, ларёк после боя, пауза, конец игры и рестарт.
    /// Что переходит между аренами (монетки, сердца, итоги), лежит в <see cref="RunState"/>.
    /// Первая арена может загрузиться и тренировкой (<see cref="Tutorial"/>): тогда сразу бой, но без волн.
    /// По сети время общее: выбор карточки и ларёк не останавливают игру и не меняют состояние арены — это
    /// только экран своего игрока (<see cref="Menu"/>), а сам он в это время стоит. Переход на следующую арену
    /// и конец прогулки решает хозяин комнаты (<see cref="AdvanceOnline"/>, <see cref="Win"/>, <see cref="LoseOnline"/>).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class GameSession : MonoBehaviour
    {
        [Tooltip("Начинать с заставки: прогулка стартует по кнопке «Играть». «Заново» заставку пропускает")]
        [SerializeField] bool startWithTitle;
        [Tooltip("Через сколько секунд после поражения можно перезапустить")]
        [SerializeField] float restartDelay = 0.8f;

        static bool s_skipTitle;

        public static GameSession Instance { get; private set; }

        public SessionState State { get; private set; }
        /// <summary>Сколько длится бой на этой арене (после победы часы стоят).</summary>
        public float SurvivalTime { get; private set; }
        /// <summary>Выбито на этой арене.</summary>
        public int Kills { get; private set; }
        /// <summary>Вся прогулка: прошлые арены и эта.</summary>
        public float RunTime => RunState.PastTime + SurvivalTime;
        public int RunKills => RunState.PastKills + Kills;
        /// <summary>Идёт бой: враги ходят, волны идут, часы арены тикают. По сети пауза у одного игрока бой не останавливает.</summary>
        public bool IsPlaying => State == SessionState.Playing && (Online.Active || !GameFeel.Paused);
        /// <summary>Игрок может бегать и бросать: в бою и на пройденной арене, не на экране карточки или ларька.</summary>
        public bool PlayerCanAct => State is (SessionState.Playing or SessionState.Cleared) && Menu == LocalMenu.None
                                    && !GameFeel.Paused && !ScreenFade.IsBusy;
        /// <summary>По сети: какой экран открыт у своего игрока (игра при этом идёт). В соло всегда None — там состояния.</summary>
        public LocalMenu Menu { get; private set; }
        /// <summary>Открыт выбор карточки (в соло — время стоит, по сети — только у своего игрока).</summary>
        public bool IsChoosingCard => State == SessionState.Upgrade || Menu == LocalMenu.Card;
        /// <summary>Открыта витрина ларька.</summary>
        public bool IsShopOpen => State == SessionState.Shop || Menu == LocalMenu.Shop;
        public bool IsFinished => State is SessionState.GameOver or SessionState.Victory;
        public bool CanRestart => IsFinished && Time.unscaledTime - _gameOverTime > restartDelay;

        /// <summary>
        /// Поверх заставки или паузы открыт ещё один экран (настройки, авторы). Esc и Start паузу тогда
        /// не снимают: экран закрывается сам и возвращает к паузе.
        /// </summary>
        public bool OverlayOpen { get; set; }

        float _gameOverTime;
        SessionState _resumeState = SessionState.Playing;

        void Awake()
        {
            Instance = this;
            GameFeel.Paused = false;
            GameFeel.Frozen = false;
            // Темнота «Гасит свет» и заморозка врагов прошлой сцены (финал, «Замри!») в новую не переходят.
            LightsOut.Clear();
            Targetable.ClearEnemyFreeze();
            if (Tutorial.Begin())
            {
                // Тренировка во дворе: сразу на арену, шаги ведёт TutorialDirector.
                RunState.BeginTutorial();
                State = SessionState.Playing;
            }
            else if (RunState.ContinuesRun)
            {
                // Следующая арена той же прогулки: сразу бой.
                RunState.ConsumeContinuation();
                State = SessionState.Playing;
            }
            else if (startWithTitle && !s_skipTitle)
            {
                RunState.Clear();
                State = SessionState.Title;
            }
            else
            {
                RunState.BeginNew();
                State = SessionState.Playing;
            }
            s_skipTitle = false;
        }

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.PlayerDied += OnPlayerDied;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.PlayerDied -= OnPlayerDied;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (IsPlaying)
                SurvivalTime += Time.deltaTime;
            // Часы прогулки идут всё время, кроме паузы и затемнений: бой, ларёк, выбор карточки.
            if (RunState.Active && State is (SessionState.Playing or SessionState.Cleared or SessionState.Shop or SessionState.Upgrade)
                && !GameFeel.Paused && !ScreenFade.IsBusy)
                RunState.RunClock += Time.unscaledDeltaTime;
            if (CanRestart && RestartPressed())
                Restart();
        }

        public static bool IsGameplayActive => Instance == null || Instance.IsPlaying;

        /// <summary>Игрок может действовать: бой или пройденная арена, не пауза и не выбор.</summary>
        public static bool IsPlayerActive => Instance == null || Instance.PlayerCanAct;

        /// <summary>Заставка закрыта — прогулка начинается.</summary>
        public void StartRun()
        {
            if (State != SessionState.Title)
                return;
            RunState.BeginNew();
            State = SessionState.Playing;
        }

        public void TogglePause()
        {
            if (State is (SessionState.Playing or SessionState.Cleared) && Menu == LocalMenu.None && !OverlayOpen)
                GameFeel.Paused = !GameFeel.Paused;
        }

        /// <summary>Заново всю прогулку с первой арены, без заставки. В обучении — обучение с начала. По сети — нельзя.</summary>
        public void Restart()
        {
            if (Online.Active)
                return;
            if (Tutorial.Active)
                Tutorial.Request();
            Reload(skipTitle: true);
        }

        /// <summary>Тренировка во дворе: первая арена грузится заново, без волн, с заданиями.</summary>
        public void StartTutorial()
        {
            Tutorial.Request();
            Reload(skipTitle: true);
        }

        /// <summary>К заставке: первая арена грузится заново и ждёт кнопку «Играть». По сети — сперва уйти из комнаты.</summary>
        public void ToTitle()
        {
            if (Online.Active)
                Online.Session?.Leave();
            Reload(skipTitle: false);
        }

        void Reload(bool skipTitle)
        {
            RunState.Clear();
            s_skipTitle = skipTitle;
            GameFeel.Paused = false;
            GameFeel.Frozen = false;
            string first = RunState.FirstScene;
            if (!string.IsNullOrEmpty(first) && Application.CanStreamedLevelBeLoaded(first))
                SceneManager.LoadScene(first);
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Остановить игру на выбор карточки (в бою или на пройденной арене). Пауза не включается.
        /// По сети игра не встаёт: выбор — экран своего игрока.
        /// </summary>
        public bool BeginUpgradeChoice()
        {
            if (State is not (SessionState.Playing or SessionState.Cleared))
                return false;
            if (Online.Active)
            {
                if (Menu != LocalMenu.None)
                    return false;
                Menu = LocalMenu.Card;
                return true;
            }
            _resumeState = State;
            State = SessionState.Upgrade;
            GameFeel.Frozen = true;
            return true;
        }

        public void EndUpgradeChoice()
        {
            if (Menu == LocalMenu.Card)
            {
                Menu = LocalMenu.None;
                return;
            }
            if (State != SessionState.Upgrade)
                return;
            State = _resumeState;
            GameFeel.Frozen = false;
        }

        /// <summary>Арена пройдена: часы и волны встают, игрок свободно ходит по арене.</summary>
        public void ClearArena()
        {
            if (State == SessionState.Playing)
                State = SessionState.Cleared;
            else if (State == SessionState.Upgrade && _resumeState == SessionState.Playing)
                _resumeState = SessionState.Cleared;
        }

        /// <summary>Открыть витрину ларька: время стоит (по сети — не стоит, витрина только у своего игрока).</summary>
        public bool BeginShop()
        {
            if (State != SessionState.Cleared || GameFeel.Paused)
                return false;
            if (Online.Active)
            {
                if (Menu != LocalMenu.None)
                    return false;
                Menu = LocalMenu.Shop;
                return true;
            }
            State = SessionState.Shop;
            GameFeel.Frozen = true;
            return true;
        }

        public void EndShop()
        {
            if (Menu == LocalMenu.Shop)
            {
                Menu = LocalMenu.None;
                return;
            }
            if (State != SessionState.Shop)
                return;
            State = SessionState.Cleared;
            GameFeel.Frozen = false;
        }

        /// <summary>
        /// Уйти на следующую арену прогулки: экран гаснет, грузится её сцена.
        /// Сердца игроков к этому времени записаны в <see cref="RunState.SetLives"/>.
        /// </summary>
        public void LeaveArena(string nextScene)
        {
            if (State != SessionState.Cleared || ScreenFade.IsBusy)
                return;
            RunState.AdvanceArena(SurvivalTime, Kills);
            GameFeel.Frozen = true;
            ScreenFade.LoadScene(nextScene);
        }

        /// <summary>
        /// По сети: хозяин комнаты ведёт всех на следующую арену — экран гаснет, а сцену грузит сеть.
        /// Сердца своего игрока к этому времени записаны в <see cref="RunState.SetLives"/>.
        /// </summary>
        public void AdvanceOnline()
        {
            if (State != SessionState.Cleared)
                return;
            Menu = LocalMenu.None;
            RunState.AdvanceArena(SurvivalTime, Kills);
            ScreenFade.Cover();
        }

        /// <summary>Прогулка пройдена: враги замирают, показывается победа.</summary>
        public void Win()
        {
            if (State is not (SessionState.Playing or SessionState.Cleared))
                return;
            Menu = LocalMenu.None;
            State = SessionState.Victory;
            _gameOverTime = Time.unscaledTime;
            GameFeel.SlowMotion(0.3f, 1.5f);
            GameEvents.PlaySound(SoundCue.Victory, Vector3.zero);
            GameEvents.RaiseRunFinished(true);
        }

        void OnEnemyKilled(GameObject enemy, HitInfo hit)
        {
            if (State == SessionState.Playing && !hit.Has(HitFlags.Despawn))
                Kills++;
        }

        void OnPlayerDied(GameObject player)
        {
            // По сети выбиты ли все, решает хозяин комнаты (<see cref="LoseOnline"/>): здесь сердца других — копии.
            if (Online.Active || State is not (SessionState.Playing or SessionState.Cleared))
                return;
            // В коопе забег кончается, когда выбиты все.
            if (Targetable.CountAlive(Team.Player) > 0)
                return;
            State = SessionState.GameOver;
            _gameOverTime = Time.unscaledTime;
            GameFeel.SlowMotion(0.25f, 1.2f);
            GameEvents.PlaySound(SoundCue.GameOver, Vector3.zero);
            GameEvents.RaiseRunFinished(false);
        }

        /// <summary>По сети: хозяин комнаты сказал, что выбиты все, — прогулка проиграна у всех.</summary>
        public void LoseOnline()
        {
            if (State is not (SessionState.Playing or SessionState.Cleared or SessionState.Upgrade or SessionState.Shop))
                return;
            Menu = LocalMenu.None;
            State = SessionState.GameOver;
            _gameOverTime = Time.unscaledTime;
            GameFeel.Frozen = false;
            GameEvents.PlaySound(SoundCue.GameOver, Vector3.zero);
            GameEvents.RaiseRunFinished(false);
        }

        // Enter и A нажимают кнопки на экране — здесь только быстрые клавиши.
        static bool RestartPressed()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                   || (gamepad != null && gamepad.startButton.wasPressedThisFrame);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_skipTitle = false;
    }
}
