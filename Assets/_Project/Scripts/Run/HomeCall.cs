using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Visuals;
using UnityEngine;
using UnityEngine.Localization;

namespace Bouncer.Run
{
    [DefaultExecutionOrder(-70)]
    public sealed class HomeCall : MonoBehaviour
    {
        [Tooltip("Есть только в финале: накладки на окна, фонари, укрытия для «Считалочки», дорога домой")]
        [SerializeField] GameObject finaleOnly;
        [Tooltip("Прячется в финале: гараж на месте прохода к подъезду")]
        [SerializeField] GameObject[] hiddenInFinale;
        [SerializeField] WindowClock windows;

        [Header("Дорога домой")]
        [Tooltip("Зажигается, когда мама позвала: свет из подъезда (LightZone отгоняет сумеречных) и открытая дверь")]
        [SerializeField] GameObject homeLight;
        [Tooltip("Стрелка мелом у прохода: видна, когда мама позвала")]
        [SerializeField] ArenaExit goalArrow;
        [Tooltip("Надпись у стрелки — строка таблицы «UI»")]
        [SerializeField] LocalizedString goalLabel = new("UI", "exit.home");
        [Tooltip("Точка у края двора: дошёл сюда — дальше бежит домой сам")]
        [SerializeField] Transform goal;
        [SerializeField] float goalRadius = 2f;
        [Tooltip("Путь от прохода до двери подъезда")]
        [SerializeField] Transform[] pathHome;
        [SerializeField] float runSpeed = 7f;
        [Tooltip("Наше окно: оттуда зовёт мама")]
        [SerializeField] Transform momWindow;

        PlayerController _runner;
        int _waypoint;
        bool _won;
        bool _finished;

        public static event System.Action<bool> CallStarted;

        public static HomeCall Instance { get; private set; }
        public bool IsFinale { get; private set; }
        public bool Called { get; private set; }
        public bool BossDefeated { get; private set; }
        public bool GoingHome => _runner != null;

        public float Remaining
        {
            get
            {
                var director = ArenaDirector.Instance;
                return director == null || Called ? 0f : Mathf.Max(0f, director.ArenaDuration - director.ArenaTime);
            }
        }

        public float Progress01
        {
            get
            {
                var director = ArenaDirector.Instance;
                if (Called || director == null || director.ArenaDuration <= 0f)
                    return Called ? 1f : 0f;
                return Mathf.Clamp01(director.ArenaTime / director.ArenaDuration);
            }
        }

        void Awake()
        {
            Instance = this;
            var director = ArenaDirector.Instance;
            IsFinale = director != null && director.Arena != null && director.IsLastArena
                       && director.Arena.goal == ArenaGoal.SurviveUntilCall;
            if (finaleOnly)
                finaleOnly.SetActive(IsFinale);
            if (hiddenInFinale != null)
                foreach (var go in hiddenInFinale)
                    if (go)
                        go.SetActive(!IsFinale);
            if (homeLight)
                homeLight.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void BeginCall(bool bossDefeated)
        {
            if (!IsFinale || Called)
                return;
            Called = true;
            BossDefeated = bossDefeated;
            LightsOut.End();
            if (windows)
            {
                windows.Progress01 = 1f;
                windows.OurWindowLit = true;
            }
            if (homeLight)
                homeLight.SetActive(true);
            if (goalArrow)
                goalArrow.Show(goalLabel.IsEmpty ? "↑" : goalLabel.GetLocalizedString());
            GameEvents.RaiseMomCalled();
            if (!NetHooks.IsGuest)
                CallStarted?.Invoke(bossDefeated);
            GameEvents.PlaySound(SoundCue.MomCall, momWindow ? momWindow.position : transform.position);
            GameEvents.Announce(new Announcement
            {
                Title = "call.title",
                Hint = bossDefeated ? "call.hint.boss" : "call.hint",
                Seconds = 5f,
            });
        }

        void Update()
        {
            if (!IsFinale)
                return;
            if (windows && !Called)
                windows.Progress01 = Progress01;
            if (!Called)
                return;
            if (Online.Active && Online.IsHost)
                CheckEveryoneHome();
            if (_won)
                return;
            if (_runner == null)
                WaitAtGate();
            else
                RunHome(Time.deltaTime);
        }

        void CheckEveryoneHome()
        {
            if (_finished)
                return;
            bool anyHome = false;
            foreach (var player in Players.All)
            {
                if (player.IsHome)
                    anyHome = true;
                else if (!player.IsDead)
                    return;
            }
            if (!anyHome)
                return;
            _finished = true;
            if (ArenaDirector.Instance != null)
                ArenaDirector.Instance.WinFromHome(BossDefeated);
        }

        void WaitAtGate()
        {
            var session = GameSession.Instance;
            if (goal == null || session == null || session.State != SessionState.Playing || GameFeel.Paused)
                return;
            foreach (var target in Targetable.All)
            {
                if (target.Team != Team.Player || !target.IsAlive)
                    continue;
                Vector3 delta = target.Position - goal.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > goalRadius * goalRadius || !target.TryGetComponent(out PlayerController player)
                    || !player.IsLocal)
                    continue;
                _runner = player;
                _waypoint = 0;
                player.BeginScripted();
                var door = pathHome != null && pathHome.Length > 0 ? pathHome[pathHome.Length - 1] : null;
                GameEvents.PlaySound(SoundCue.DoorOpen, door ? door.position : goal.position);
                return;
            }
        }

        void RunHome(float dt)
        {
            if (GameFeel.Paused)
                return;
            if (!Online.Active)
                Targetable.FreezeEnemies(0.5f);
            var runner = _runner.transform;
            if (pathHome == null || _waypoint >= pathHome.Length || pathHome[_waypoint] == null)
            {
                Arrive();
                return;
            }
            Vector3 target = pathHome[_waypoint].position;
            target.y = runner.position.y;
            Vector3 to = target - runner.position;
            float step = runSpeed * dt;
            if (to.magnitude <= step)
            {
                runner.position = target;
                _waypoint++;
                return;
            }
            runner.position += to.normalized * step;
            runner.rotation = Quaternion.RotateTowards(runner.rotation, Quaternion.LookRotation(to), 720f * dt);
        }

        void Arrive()
        {
            _won = true;
            if (Online.Active)
            {
                _runner.IsHome = true;
                GameEvents.AnnounceLocal(new Announcement { Title = "net.home.title", Hint = "net.home.hint", Seconds = 4f });
                return;
            }
            var session = GameSession.Instance;
            if (session != null)
                session.Win();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => CallStarted = null;
    }
}
