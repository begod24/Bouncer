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

    /// <summary>
    /// Арена в прогулке: заставка, бой, выбор карточки, ларёк после боя, пауза, конец игры и рестарт.
    /// Что переходит между аренами (монетки, сердца, итоги), лежит в <see cref="RunState"/>.
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
        /// <summary>Идёт бой: враги ходят, волны идут, часы арены тикают.</summary>
        public bool IsPlaying => State == SessionState.Playing && !GameFeel.Paused;
        /// <summary>Игрок может бегать и бросать: в бою и на пройденной арене.</summary>
        public bool PlayerCanAct => State is (SessionState.Playing or SessionState.Cleared) && !GameFeel.Paused && !ScreenFade.IsBusy;
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
            if (RunState.ContinuesRun)
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
            if (State is (SessionState.Playing or SessionState.Cleared) && !OverlayOpen)
                GameFeel.Paused = !GameFeel.Paused;
        }

        /// <summary>Заново всю прогулку с первой арены, без заставки.</summary>
        public void Restart() => Reload(skipTitle: true);

        /// <summary>К заставке: первая арена грузится заново и ждёт кнопку «Играть».</summary>
        public void ToTitle() => Reload(skipTitle: false);

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

        /// <summary>Остановить игру на выбор карточки (в бою или на пройденной арене). Пауза не включается.</summary>
        public bool BeginUpgradeChoice()
        {
            if (State is not (SessionState.Playing or SessionState.Cleared))
                return false;
            _resumeState = State;
            State = SessionState.Upgrade;
            GameFeel.Frozen = true;
            return true;
        }

        public void EndUpgradeChoice()
        {
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

        /// <summary>Открыть витрину ларька: время стоит.</summary>
        public bool BeginShop()
        {
            if (State != SessionState.Cleared || GameFeel.Paused)
                return false;
            State = SessionState.Shop;
            GameFeel.Frozen = true;
            return true;
        }

        public void EndShop()
        {
            if (State != SessionState.Shop)
                return;
            State = SessionState.Cleared;
            GameFeel.Frozen = false;
        }

        /// <summary>Уйти на следующую арену прогулки: экран гаснет, грузится её сцена.</summary>
        public void LeaveArena(string nextScene, int lives)
        {
            if (State != SessionState.Cleared || ScreenFade.IsBusy)
                return;
            RunState.AdvanceArena(SurvivalTime, Kills, lives);
            GameFeel.Frozen = true;
            ScreenFade.LoadScene(nextScene);
        }

        /// <summary>Прогулка пройдена: враги замирают, показывается победа.</summary>
        public void Win()
        {
            if (State is not (SessionState.Playing or SessionState.Cleared))
                return;
            State = SessionState.Victory;
            _gameOverTime = Time.unscaledTime;
            GameFeel.SlowMotion(0.3f, 1.5f);
            GameEvents.PlaySound(SoundCue.Victory, Vector3.zero);
        }

        void OnEnemyKilled(GameObject enemy, HitInfo hit)
        {
            if (State == SessionState.Playing && !hit.Has(HitFlags.Despawn))
                Kills++;
        }

        void OnPlayerDied(GameObject player)
        {
            if (State is not (SessionState.Playing or SessionState.Cleared))
                return;
            // В коопе забег кончается, когда выбиты все.
            if (Targetable.CountAlive(Team.Player) > 0)
                return;
            State = SessionState.GameOver;
            _gameOverTime = Time.unscaledTime;
            GameFeel.SlowMotion(0.25f, 1.2f);
            GameEvents.PlaySound(SoundCue.GameOver, Vector3.zero);
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
