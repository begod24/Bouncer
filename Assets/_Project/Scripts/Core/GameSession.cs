using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Bouncer.Core
{
    public enum SessionState
    {
        /// <summary>Заставка: игрок стоит, враги не идут, забег начнётся по кнопке «Играть».</summary>
        Title,
        Playing,
        /// <summary>Новый уровень: время стоит, игрок выбирает карточку.</summary>
        Upgrade,
        GameOver,
        /// <summary>Босс арены выбит — забег пройден.</summary>
        Victory,
    }

    /// <summary>Забег: заставка, время, счёт, пауза, конец игры и рестарт.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class GameSession : MonoBehaviour
    {
        [Tooltip("Начинать с заставки: забег стартует по кнопке «Играть». «Заново» заставку пропускает")]
        [SerializeField] bool startWithTitle;
        [Tooltip("Через сколько секунд после поражения можно перезапустить")]
        [SerializeField] float restartDelay = 0.8f;

        static bool s_skipTitle;

        public static GameSession Instance { get; private set; }

        public SessionState State { get; private set; }
        public float SurvivalTime { get; private set; }
        public int Kills { get; private set; }
        public bool IsPlaying => State == SessionState.Playing && !GameFeel.Paused;
        public bool IsFinished => State is SessionState.GameOver or SessionState.Victory;
        public bool CanRestart => IsFinished && Time.unscaledTime - _gameOverTime > restartDelay;

        /// <summary>
        /// Поверх заставки или паузы открыт ещё один экран (настройки). Esc и Start паузу тогда не снимают:
        /// экран закрывается сам и возвращает к паузе.
        /// </summary>
        public bool OverlayOpen { get; set; }

        float _gameOverTime;

        void Awake()
        {
            Instance = this;
            GameFeel.Paused = false;
            GameFeel.Frozen = false;
            State = startWithTitle && !s_skipTitle ? SessionState.Title : SessionState.Playing;
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

        /// <summary>Заставка закрыта — забег начинается.</summary>
        public void StartRun()
        {
            if (State == SessionState.Title)
                State = SessionState.Playing;
        }

        public void TogglePause()
        {
            if (State == SessionState.Playing && !OverlayOpen)
                GameFeel.Paused = !GameFeel.Paused;
        }

        /// <summary>Заново тот же забег, без заставки.</summary>
        public void Restart() => Reload(skipTitle: true);

        /// <summary>К заставке: сцена перезагружается и ждёт кнопку «Играть».</summary>
        public void ToTitle() => Reload(skipTitle: false);

        void Reload(bool skipTitle)
        {
            s_skipTitle = skipTitle;
            GameFeel.Paused = false;
            GameFeel.Frozen = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Остановить забег на выбор карточки. Пауза в это время не включается.</summary>
        public void BeginUpgradeChoice()
        {
            if (State != SessionState.Playing)
                return;
            State = SessionState.Upgrade;
            GameFeel.Frozen = true;
        }

        public void EndUpgradeChoice()
        {
            if (State != SessionState.Upgrade)
                return;
            State = SessionState.Playing;
            GameFeel.Frozen = false;
        }

        /// <summary>Забег пройден (босс выбит): враги замирают, показывается победа.</summary>
        public void Win()
        {
            if (State != SessionState.Playing)
                return;
            State = SessionState.Victory;
            _gameOverTime = Time.unscaledTime;
            GameFeel.SlowMotion(0.3f, 1.5f);
            GameEvents.PlaySound(SoundCue.Victory, Vector3.zero);
        }

        void OnEnemyKilled(GameObject enemy, HitInfo hit)
        {
            if (State == SessionState.Playing)
                Kills++;
        }

        void OnPlayerDied(GameObject player)
        {
            if (State != SessionState.Playing)
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
