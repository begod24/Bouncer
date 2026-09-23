using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Bouncer.Core
{
    public enum SessionState
    {
        Playing,
        GameOver,
    }

    /// <summary>Забег: время, счёт, пауза, конец игры и рестарт.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class GameSession : MonoBehaviour
    {
        [Tooltip("Через сколько секунд после поражения можно перезапустить")]
        [SerializeField] float restartDelay = 0.8f;

        public static GameSession Instance { get; private set; }

        public SessionState State { get; private set; }
        public float SurvivalTime { get; private set; }
        public int Kills { get; private set; }
        public bool IsPlaying => State == SessionState.Playing && !GameFeel.Paused;
        public bool CanRestart => State == SessionState.GameOver && Time.unscaledTime - _gameOverTime > restartDelay;

        float _gameOverTime;

        void Awake()
        {
            Instance = this;
            GameFeel.Paused = false;
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

        public void TogglePause()
        {
            if (State == SessionState.Playing)
                GameFeel.Paused = !GameFeel.Paused;
        }

        public void Restart()
        {
            GameFeel.Paused = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
        }

        static bool RestartPressed()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                   || (gamepad != null && (gamepad.startButton.wasPressedThisFrame || gamepad.buttonSouth.wasPressedThisFrame));
        }
    }
}
