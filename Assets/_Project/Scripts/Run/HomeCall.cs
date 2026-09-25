using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Visuals;
using UnityEngine;
using UnityEngine.Localization;

namespace Bouncer.Run
{
    /// <summary>
    /// Финал прогулки «Мама зовёт домой» — наш двор ночью. Пока идёт бой, в панельках одно за другим загораются
    /// окна (часы до зова, <see cref="WindowClock"/>). Время вышло или «Тот, кто в сумерках» выбит раньше —
    /// мама зовёт из нашего окна: свет больше не гаснет, мелочь босса разбегается, в проходе между гаражами
    /// светится дорога к подъезду (в этот свет сумеречные не заходят). Добежал до прохода — дальше персонаж
    /// бежит к двери сам, и прогулка пройдена; выбили по дороге — нет. На других аренах сцены двора всё
    /// ночное спрятано, а гараж стоит на месте прохода.
    /// </summary>
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

        public static HomeCall Instance { get; private set; }
        /// <summary>Эта арена — финал прогулки: продержаться до зова мамы.</summary>
        public bool IsFinale { get; private set; }
        /// <summary>Мама позвала.</summary>
        public bool Called { get; private set; }
        /// <summary>Мама позвала раньше времени: босс выбит.</summary>
        public bool BossDefeated { get; private set; }
        /// <summary>Игрок дошёл до прохода и бежит к подъезду.</summary>
        public bool GoingHome => _runner != null;

        /// <summary>Сколько секунд осталось до зова.</summary>
        public float Remaining
        {
            get
            {
                var director = ArenaDirector.Instance;
                return director == null || Called ? 0f : Mathf.Max(0f, director.ArenaDuration - director.ArenaTime);
            }
        }

        /// <summary>0 — начало финала, 1 — мама зовёт.</summary>
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

        /// <summary>Мама позвала: время вышло или босс выбит (<see cref="ArenaDirector"/>).</summary>
        public void BeginCall(bool bossDefeated)
        {
            if (!IsFinale || Called)
                return;
            Called = true;
            BossDefeated = bossDefeated;
            // Во дворе зажигается всё: босс больше не погасит свет.
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
            if (!Called || _won)
                return;
            if (_runner == null)
                WaitAtGate();
            else
                RunHome(Time.deltaTime);
        }

        /// <summary>Кто из живых игроков дошёл до прохода — тот бежит домой.</summary>
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
                if (delta.sqrMagnitude > goalRadius * goalRadius || !target.TryGetComponent(out PlayerController player))
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
            // Пока бежит домой, двор замер: сумеречным до подъезда не добраться.
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

        /// <summary>У двери подъезда: прогулка пройдена.</summary>
        void Arrive()
        {
            _won = true;
            var session = GameSession.Instance;
            if (session != null)
                session.Win();
        }
    }
}
