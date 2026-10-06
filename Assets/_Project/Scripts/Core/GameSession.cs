using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Bouncer.Core
{
    public enum SessionState
    {
        Title,
        Playing,
        Upgrade,
        Cleared,
        Shop,
        GameOver,
        Victory,
    }

    public enum LocalMenu
    {
        None,
        Card,
        Shop,
    }

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
        public float SurvivalTime { get; private set; }
        public int Kills { get; private set; }
        public float RunTime => RunState.PastTime + SurvivalTime;
        public int RunKills => RunState.PastKills + Kills;
        public bool IsPlaying => State == SessionState.Playing && (Online.Active || !GameFeel.Paused);
        public bool PlayerCanAct => State is (SessionState.Playing or SessionState.Cleared) && Menu == LocalMenu.None
                                    && !GameFeel.Paused && !ScreenFade.IsBusy;
        public LocalMenu Menu { get; private set; }
        public bool IsChoosingCard => State == SessionState.Upgrade || Menu == LocalMenu.Card;
        public bool IsShopOpen => State == SessionState.Shop || Menu == LocalMenu.Shop;
        public bool IsFinished => State is SessionState.GameOver or SessionState.Victory;
        public bool CanRestart => IsFinished && Time.unscaledTime - _gameOverTime > restartDelay;

        public bool OverlayOpen { get; set; }

        float _gameOverTime;
        SessionState _resumeState = SessionState.Playing;

        void Awake()
        {
            Instance = this;
            GameFeel.Paused = false;
            GameFeel.Frozen = false;
            LightsOut.Clear();
            Targetable.ClearEnemyFreeze();
            if (Tutorial.Begin())
            {
                RunState.BeginTutorial();
                State = SessionState.Playing;
            }
            else if (RunState.ContinuesRun)
            {
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
            if (RunState.Active && State is (SessionState.Playing or SessionState.Cleared or SessionState.Shop or SessionState.Upgrade)
                && !GameFeel.Paused && !ScreenFade.IsBusy)
                RunState.RunClock += Time.unscaledDeltaTime;
            if (CanRestart && RestartPressed())
                Restart();
        }

        public static bool IsGameplayActive => Instance == null || Instance.IsPlaying;

        public static bool IsPlayerActive => Instance == null || Instance.PlayerCanAct;

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

        public void Restart()
        {
            if (Online.Active)
                return;
            if (Tutorial.Active)
                Tutorial.Request();
            Reload(skipTitle: true);
        }

        public void StartTutorial()
        {
            Tutorial.Request();
            Reload(skipTitle: true);
        }

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

        public void ClearArena()
        {
            if (State == SessionState.Playing)
                State = SessionState.Cleared;
            else if (State == SessionState.Upgrade && _resumeState == SessionState.Playing)
                _resumeState = SessionState.Cleared;
        }

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

        public void LeaveArena(string nextScene)
        {
            if (State != SessionState.Cleared || ScreenFade.IsBusy)
                return;
            RunState.AdvanceArena(SurvivalTime, Kills);
            GameFeel.Frozen = true;
            ScreenFade.LoadScene(nextScene);
        }

        public void AdvanceOnline()
        {
            if (State != SessionState.Cleared)
                return;
            Menu = LocalMenu.None;
            RunState.AdvanceArena(SurvivalTime, Kills);
            ScreenFade.Cover();
        }

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
            if (Online.Active || State is not (SessionState.Playing or SessionState.Cleared))
                return;
            if (Targetable.CountAlive(Team.Player) > 0)
                return;
            State = SessionState.GameOver;
            _gameOverTime = Time.unscaledTime;
            GameFeel.SlowMotion(0.25f, 1.2f);
            GameEvents.PlaySound(SoundCue.GameOver, Vector3.zero);
            GameEvents.RaiseRunFinished(false);
        }

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
